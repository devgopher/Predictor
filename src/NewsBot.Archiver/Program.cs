using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewsBot.Archiver;
using NewsBot.Logic;
using NewsBot.Logic.Data;
using NLog.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddNewsArchiveInfrastructure(builder.Configuration)
    .AddLogging(cfg => cfg.AddNLog())
    .AddHostedService<NewsArchiveCollectorWorker>();

var host = builder.Build();

await host.Services.MigrateNewsBotDatabaseAsync();
await host.RunAsync();
