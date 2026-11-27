using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Spine.Api.Services;
using Spine.Api.Validators;
using Spine.Infrastructure.Data;
using Spine.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
// ✅ This using directive provides the AddStackExchangeRedisCache extension method
using Microsoft.Extensions.DependencyInjection; 

var builder = WebApplication.CreateBuilder(args);

// 1. Database
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
}

// 2. Auth & Token Service
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<KafkaProducerService>();

// 3. External APIs
builder.Services.AddHttpClient<OpenLibraryService>()
    .ConfigurePrimaryHttpMessageHandler(() => Ipv4HttpHandler.Create())
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<GoogleBooksService>()
    .ConfigurePrimaryHttpMessageHandler(() => Ipv4HttpHandler.Create())
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<QuoteService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("User-Agent", "Spine/1.0");
});

// 4. Caching (Redis with In-Memory fallback)
var redisConn = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConn) && !redisConn.Contains("localhost"))
{
    // ✅ This method is now available because the NuGet package is installed
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConn;
        options.InstanceName = "Spine:";
    });
    Console.WriteLine("✅ Redis cache enabled");
}
else
{
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("⚠️ Using in-memory distributed cache (dev mode)");
}

builder.Services.AddMemoryCache();
builder.Services.AddScoped<BookLookupService>();

// 5. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://spine-api-7j17.onrender.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 6. JWT Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// 7. Logging
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// 8. Controllers & Validation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// 9. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
var memoryCache = app.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
CoverHelper.Configure(memoryCache);

// ==========================================
// MIDDLEWARE PIPELINE
// ==========================================

app.UseSwagger();
app.UseSwaggerUI();


app.UseSerilogRequestLogging();

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }