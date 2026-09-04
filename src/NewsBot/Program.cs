using Botticelli.Framework.Telegram.Extensions;
using NewsBot;
using NewsBot.Logic;
using NewsBot.Logic.Data;
using NewsBot.Logic.Services;
using NLog.Extensions.Logging;
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddStandaloneTelegramBot(builder.Configuration)
    .Prepare();

builder.Services
    .AddTelegramLayoutsSupport()
    .AddLogging(cfg => cfg.AddNLog())
    .AddNewsBotDatabase(builder.Configuration)
    .AddNewsBotServices(builder.Configuration);

RegisterCommands(builder.Services);
builder.Services.AddHostedService<ForecastQueueWorker>();

var app = builder.Build();
await app.Services.MigrateNewsBotDatabaseAsync();
await app.RunAsync();

static void RegisterCommands(IServiceCollection services) =>
    services.AddNewsBotCommands();
