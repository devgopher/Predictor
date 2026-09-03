using Predictor.Ollama;
using Predictor.Predictions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddPredictorOllama(builder.Configuration);
builder.Services.AddPredictions(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();