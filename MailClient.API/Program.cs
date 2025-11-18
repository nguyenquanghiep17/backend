using MailClient.API.Services;
using MailClient.API.Models;
using MailClient.API.Hubs;
using MailClient.API.Implementations.MailKit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register MySQL connections (for reference, actual connections are created in repository)
// Note: Connection strings are hardcoded in MailAccountRepository for now
// In production, these should come from configuration or environment variables

// Register mail services
builder.Services.AddScoped<IMailAccountRepository, MailAccountRepository>();
builder.Services.AddSingleton<IDistributedAccountAllocator, DistributedAccountAllocator>();

// Register mail client factory (abstraction layer)
// To swap mail library, just change this registration
builder.Services.AddSingleton<IMailClientFactory, MailKitMailClientFactory>();

builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddHostedService<MailMonitorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowVueApp");
app.UseAuthorization();
app.MapControllers();
app.MapHub<MailHub>("/hub/mail");

app.Run();


