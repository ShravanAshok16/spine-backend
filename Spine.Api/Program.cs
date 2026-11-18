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

// 🔥 3. External Book APIs (FORCING IPV4 & TIMEOUTS)
builder.Services.AddHttpClient<OpenLibraryService>()
    .ConfigurePrimaryHttpMessageHandler(() => Ipv4HttpHandler.Create())
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10)); // 🔥 Added Timeout

builder.Services.AddHttpClient<GoogleBooksService>()
    .ConfigurePrimaryHttpMessageHandler(() => Ipv4HttpHandler.Create())
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10)); // 🔥 Added Timeout

builder.Services.AddMemoryCache();
builder.Services.AddScoped<BookLookupService>();

// 4. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 5. JWT Auth
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

// 6. Logging
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// 7. Controllers & Validation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// 8. Swagger
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
// MIDDLEWARE PIPELINE (Order matters!)
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

// Apply CORS before auth
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }