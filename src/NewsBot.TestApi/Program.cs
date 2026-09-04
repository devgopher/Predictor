using Botticelli.Framework.Extensions;
using NewsBot.Logic;
using NewsBot.Logic.Data;
using NewsBot.Logic.Services;
using NewsBot.TestApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true));
});

builder.Services
    .AddBotticelliFramework()
    .AddNewsBotDatabase(builder.Configuration)
    .AddNewsBotServices(builder.Configuration)
    .AddNewsBotCommands();

builder.Services.AddSingleton<FakeBot>();
builder.Services.AddSingleton<Botticelli.Interfaces.IBot>(sp => sp.GetRequiredService<FakeBot>());
builder.Services.AddScoped<BotCommandTestService>();

var app = builder.Build();

await app.Services.MigrateNewsBotDatabaseAsync();

app.UseCors();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
