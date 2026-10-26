using Resend;
using Spine.Worker;
using Spine.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configure Resend
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o => {
    o.ApiToken = builder.Configuration["Resend:ApiKey"]!;
});
builder.Services.AddTransient<IResend, ResendClient>();

// 2. Register Services
builder.Services.AddScoped<EmailService>(); 

// 3. Register the new Worker class
builder.Services.AddHostedService<KafkaConsumerWorker>();

var host = builder.Build();
host.Run();