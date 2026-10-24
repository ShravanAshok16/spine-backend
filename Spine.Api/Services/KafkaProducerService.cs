using Confluent.Kafka;
using System.Text.Json;
using Spine.Api.Events;

namespace Spine.Api.Services;

public class KafkaProducerService
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic = "password-reset-events";

    public KafkaProducerService(IConfiguration config)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092"
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    public async Task PublishPasswordResetEventAsync(PasswordResetRequestedEvent resetEvent)
    {
        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(resetEvent)
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(_topic, message);
            Console.WriteLine($"Delivered message to {deliveryResult.TopicPartitionOffset}");
        }
        catch (ProduceException<Null, string> e)
        {
            Console.WriteLine($"Delivery failed: {e.Error.Reason}");
            throw;
        }
    }
}