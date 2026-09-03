using System.Text.Encodings.Web;
using Predictor.Ollama;
using Predictor.PredictionJobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UtcIso8601DateTimeOffsetConverter());
    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new UtcIso8601DateTimeOffsetConverter());
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.AddPredictorOllama(builder.Configuration);
builder.Services.AddPredictions(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();
