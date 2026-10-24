using Confluent.Kafka;

namespace Spine.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly string _topic = "password-reset-events";

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "spine-email-worker-group",
            AutoOffsetReset = AutoOffsetReset.Earliest // Read from the beginning if no offset
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_topic);

        _logger.LogInformation("Kafka Consumer started. Listening on topic: {Topic}", _topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Consume blocks until a message arrives
                var consumeResult = consumer.Consume(stoppingToken);
                
                _logger.LogInformation("Received message: {Message}", consumeResult.Message.Value);

                // In Sprint 3, we will parse the JSON and send an email here using SendGrid/Resend.
                // For now, we just log it to prove it works.
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
            consumer.Close();
        }
    }
}