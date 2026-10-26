using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spine.Api.Services;
using Spine.Infrastructure.Data;

namespace Spine.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var dbName = "SpineTestDb_" + Guid.NewGuid();

        builder.ConfigureServices(services =>
        {
            // 1. Remove the real Kafka Producer
            var producerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(KafkaProducerService));
            if (producerDescriptor != null)
            {
                services.Remove(producerDescriptor);
            }

            // 2. Add the Mock Kafka Producer
            services.AddSingleton<KafkaProducerService, MockKafkaProducerService>();

            // 3. Add InMemory DB
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });
    }
}