using Microsoft.Extensions.Configuration;
using Spine.Api.Events;
using Spine.Api.Services;

namespace Spine.Tests.Integration;

public class MockKafkaProducerService : KafkaProducerService
{
    // We pass a null config because the mock doesn't actually connect to Kafka
    public MockKafkaProducerService() : base(new ConfigurationBuilder().Build()) { }

    public override Task PublishUserRegisteredEventAsync(UserRegisteredEvent registerEvent)
    {
        // Do nothing (swallow the message)
        return Task.CompletedTask;
    }

    public override Task PublishPasswordResetEventAsync(PasswordResetRequestedEvent resetEvent)
    {
        // Do nothing
        return Task.CompletedTask;
    }
}