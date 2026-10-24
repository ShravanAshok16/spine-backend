using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spine.Infrastructure.Data;

namespace Spine.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // 1. Generate the name ONCE per factory
        var dbName = "SpineTestDb_" + Guid.NewGuid();

        builder.ConfigureServices(services =>
        {
            // 2. Use the captured name
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });
    }
}