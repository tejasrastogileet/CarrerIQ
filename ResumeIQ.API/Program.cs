using ResumeIQ.API.Services;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["Groq:ApiKey"]))
{
    var localEnvPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
    if (File.Exists(localEnvPath))
    {
        var apiKeyLine = File.ReadLines(localEnvPath)
            .FirstOrDefault(line => line.TrimStart().StartsWith(
                "GROQ_API_KEY=",
                StringComparison.Ordinal));

        if (apiKeyLine is not null)
        {
            var apiKey = apiKeyLine[(apiKeyLine.IndexOf('=') + 1)..].Trim();
            if (apiKey.Length >= 2
                && ((apiKey[0] == '"' && apiKey[^1] == '"')
                    || (apiKey[0] == '\'' && apiKey[^1] == '\'')))
            {
                apiKey = apiKey[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                builder.Configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Groq:ApiKey"] = apiKey
                    });
            }
        }
    }
}

builder.Services.AddControllers();
builder.Services.AddSingleton<IResumeDocumentExtractor, ResumeDocumentExtractor>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"]
                    ?? "http://localhost:11434";

builder.Services.AddHttpClient<IRagContextService, OllamaRagContextService>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var aiProvider = builder.Configuration["Ai:Provider"] ?? "Ollama";
switch (aiProvider.Trim().ToLowerInvariant())
{
    case "groq":
        builder.Services.AddHttpClient<IAiService, GroqAiService>(client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration["Groq:BaseUrl"] ?? "https://api.groq.com");
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        break;

    case "ollama":
        builder.Services.AddHttpClient<IAiService, OllamaAiService>(client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported AI provider '{aiProvider}'. Configure Ai:Provider as 'Groq' or 'Ollama'.");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AngularClient");

app.MapControllers();

app.Run();