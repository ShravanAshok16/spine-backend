using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Events;
using Spine.Core.Entities;
using Spine.Infrastructure.Data;
using Spine.Worker.Services;

namespace Spine.Worker;

public class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public KafkaConsumerWorker(
        ILogger<KafkaConsumerWorker> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _config = config;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "spine-email-worker-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

        // 🔥 ADD "contact-form-events" HERE
        var topics = new[] { "password-reset-events", "user-registered-events", "book-finished-events", "contact-form-events" };
        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka Consumer started. Listening on topics: {Topics}", string.Join(", ", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(stoppingToken);
                var topic = consumeResult.Topic;
                var message = consumeResult.Message.Value;

                _logger.LogInformation("Received message from {Topic}: {Message}", topic, message);

                using var scope = _scopeFactory.CreateScope();
                
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (topic == "password-reset-events")
                {
                    var resetEvent = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(message);
                    if (resetEvent != null)
                    {
                        await emailService.SendPasswordResetEmailAsync(resetEvent.Email, resetEvent.Token);
                    }
                }
                else if (topic == "user-registered-events")
                {
                    var registerEvent = JsonSerializer.Deserialize<UserRegisteredEvent>(message);
                    if (registerEvent != null)
                    {
                        await emailService.SendVerificationEmailAsync(registerEvent.Email, registerEvent.Token);
                    }
                }
                else if (topic == "book-finished-events")
                {
                    var finishEvent = JsonSerializer.Deserialize<BookFinishedEvent>(message);
                    if (finishEvent != null)
                    {
                        var friendIds = await db.Friendships
                            .Where(f => (f.RequesterId == finishEvent.UserId || f.ReceiverId == finishEvent.UserId) && f.Status == "Accepted")
                            .Select(f => f.RequesterId == finishEvent.UserId ? f.ReceiverId : f.RequesterId)
                            .ToListAsync();

                        var feedUsers = friendIds.Append(finishEvent.UserId).ToList();

                        foreach (var feedUserId in feedUsers)
                        {
                            var activity = new Activity
                            {
                                UserId = feedUserId,
                                ActionType = "FinishedBook",
                                BookId = finishEvent.BookId,
                                CreatedAt = DateTime.UtcNow
                            };
                            db.Activities.Add(activity);
                        }
                        await db.SaveChangesAsync();
                    }
                }
                // 🔥 ADD THIS BLOCK FOR CONTACT FORM
                else if (topic == "contact-form-events")
                {
                    var contactEvent = JsonSerializer.Deserialize<ContactFormSubmittedEvent>(message);
                    if (contactEvent != null)
                    {
                        // In a real app, you'd send this to your own email address.
                        _logger.LogInformation("NEW CONTACT FORM from {Name} ({Email}): {Message}", 
                            contactEvent.Name, contactEvent.Email, contactEvent.Message);
                    }
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogWarning("Topic not found yet. Waiting for Kafka to create it...");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message");
            }
        }

        consumer.Close();
    }
}