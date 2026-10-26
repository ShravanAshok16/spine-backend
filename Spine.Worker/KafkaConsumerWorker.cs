using System.Text.Json;
using Confluent.Kafka;
using Spine.Api.Events;
using Spine.Worker.Services;

namespace Spine.Worker;

public class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;

    public KafkaConsumerWorker(
        ILogger<KafkaConsumerWorker> logger,
        IConfiguration config,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _config = config;
        _serviceProvider = serviceProvider;
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

        var topics = new[] { "password-reset-events", "user-registered-events" };
        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka Consumer started. Listening on topics: {Topics}", string.Join(", ", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Consume the message (this is what throws if the topic is missing)
                var consumeResult = consumer.Consume(stoppingToken);
                var topic = consumeResult.Topic;
                var message = consumeResult.Message.Value;

                _logger.LogInformation("Received message from {Topic}: {Message}", topic, message);

                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                // 2. Process the message
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
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // The topic doesn't exist yet. Wait for someone to publish to it.
                _logger.LogWarning("Topic not found yet. Waiting for Kafka to create it...");
                await Task.Delay(2000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                // Catch any other processing errors so the worker doesn't crash
                _logger.LogError(ex, "Failed to process message");
            }
        }

        consumer.Close();
    }
}