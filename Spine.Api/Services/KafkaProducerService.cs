using Confluent.Kafka;
using System.Text.Json;
using Spine.Api.Events;


namespace Spine.Api.Services;

public class KafkaProducerService
{
    private readonly IProducer<Null, string> _producer;

    public KafkaProducerService(IConfiguration config)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config?["Kafka:BootstrapServers"] ?? "localhost:9092",
            AllowAutoCreateTopics = true
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    // Method 1: For Password Reset
    public virtual async Task PublishPasswordResetEventAsync(PasswordResetRequestedEvent resetEvent)
    {
        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(resetEvent)
        };

        var deliveryResult = await _producer.ProduceAsync("password-reset-events", message);
        Console.WriteLine($"Delivered 'password-reset' message to {deliveryResult.TopicPartitionOffset}");
    }

    // Method 2: For User Registration
    public virtual async Task PublishUserRegisteredEventAsync(UserRegisteredEvent registerEvent)
    {
        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(registerEvent)
        };

        var deliveryResult = await _producer.ProduceAsync("user-registered-events", message);
        Console.WriteLine($"Delivered 'user-registered' message to {deliveryResult.TopicPartitionOffset}");
    }

    public async Task PublishBookFinishedEventAsync(BookFinishedEvent finishedEvent)
    {
        var message = new Message<Null, string> { Value = JsonSerializer.Serialize(finishedEvent) };
        await _producer.ProduceAsync("book-finished-events", message);
    }

    public virtual async Task PublishContactFormEventAsync(ContactFormSubmittedEvent contactEvent)
    {
        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(contactEvent)
        };

        var deliveryResult = await _producer.ProduceAsync("contact-form-events", message);
        Console.WriteLine($"Delivered 'contact-form' message to {deliveryResult.TopicPartitionOffset}");
    }
}