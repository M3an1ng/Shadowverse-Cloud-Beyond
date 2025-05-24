using Microsoft.Extensions.FileProviders; // Added for PhysicalFileProvider
using System.Text.Json; // Needed for JsonSerializer
using System.Text.Json.Serialization; // Needed for JsonPropertyName attribute

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serve default files (like index.html) from the configured web root
// Configure options for UseDefaultFiles to use the same physical file provider
var defaultFilesOptions = new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(
        System.IO.Path.Combine(builder.Environment.ContentRootPath, "..")),
    RequestPath = ""
};
app.UseDefaultFiles(defaultFilesOptions);

// Serve static files (CSS, JS, images, data) from the workspace root directory (one level up from the project's ContentRootPath)
app.UseStaticFiles(new StaticFileOptions
{
    // Point the FileProvider to the workspace root (d:\workspace\Shadowverse-beyond)
    FileProvider = new PhysicalFileProvider(
        System.IO.Path.Combine(builder.Environment.ContentRootPath, "..")),
    RequestPath = "" // Serve files directly from the root URL path (e.g., /style.css, /src/gameManager.js)
});

// app.UseHttpsRedirection(); // Temporarily disabled for testing static files

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// --- Card Data API Endpoint ---
var cardDataPath = Path.Combine(builder.Environment.ContentRootPath, "..", "data", "card_data.json");
List<CardDefinition>? cardDatabase = null; // Cache the data after first read

app.MapGet("/api/cards", async (HttpContext context) =>
{
    // Read and cache data on first request (simple caching)
    if (cardDatabase == null)
    {
        try
        {
            using FileStream jsonStream = File.OpenRead(cardDataPath);
            // Use options if needed, e.g., for case sensitivity, but JsonPropertyName handles snake_case/camelCase diffs
            cardDatabase = await JsonSerializer.DeserializeAsync<List<CardDefinition>>(jsonStream) ?? new List<CardDefinition>();
            Console.WriteLine($"Successfully loaded {cardDatabase.Count} card definitions.");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: Card data file not found at {cardDataPath}");
            context.Response.StatusCode = 500; // Internal Server Error
            await context.Response.WriteAsync("Card data file not found.");
            return Results.StatusCode(500); // Return error using Results helper
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error: Failed to deserialize card data. {ex.Message}");
            context.Response.StatusCode = 500; // Internal Server Error
            await context.Response.WriteAsync("Failed to parse card data.");
            return Results.StatusCode(500);
        }
        
         catch (Exception ex)
        {
            Console.WriteLine($"Error loading card data: {ex.Message}");
             context.Response.StatusCode = 500; // Internal Server Error
            await context.Response.WriteAsync("An unexpected error occurred while loading card data.");
            return Results.StatusCode(500);
        }
    }

    // Return the cached data (or an empty list if loading failed initially but wasn't caught by return above)
    return Results.Ok(cardDatabase ?? new List<CardDefinition>());

})
.WithName("GetCardData");
// --- End Card Data API Endpoint ---

// --- M3 API Endpoints ---
app.MapPost("/api/game/playCard", (PlayCardRequest request) => {
    // TODO: Implement actual game logic here (Validation, State Update)
    Console.WriteLine($"[API SERVER] Received playCard request: Card={request.CardUid}, Target={request.TargetUid}");
    // Placeholder: Always return success for now
    return Results.Ok(new { success = true });
});
// --- End M3 API Endpoints ---




app.Run();

// --- C# Records ---

// Represents a single effect definition within a card's effects array
public record CardEffect(
    [property: JsonPropertyName("trigger")] string Trigger,
    [property: JsonPropertyName("action")] string Action,
    // Value might be int or other types depending on action, using object and checking later might be safer
    // For now, assuming int based on current data. If other types appear, change to object or handle specific actions.
    [property: JsonPropertyName("value")] int Value
);

// Represents a card definition from card_data.json
public record CardDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("attack")] int? Attack, // Nullable for non-followers
    [property: JsonPropertyName("health")] int? Health, // Nullable for non-followers
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("keywords")] List<string> Keywords,
    // Effects might be null or empty, handle gracefully
    [property: JsonPropertyName("effects")] List<CardEffect>? Effects,
    [property: JsonPropertyName("art")] string? Art,
    [property: JsonPropertyName("isEvolved")] bool IsEvolved,
    [property: JsonPropertyName("can_evolve")] bool CanEvolve,
    [property: JsonPropertyName("evolves_into")] List<string>? EvolvesInto,
    [property: JsonPropertyName("is_evolved_form_of")] string? IsEvolvedFormOf,
    [property: JsonPropertyName("requiresTarget")] bool? RequiresTarget,
    [property: JsonPropertyName("validTargets")] List<string>? ValidTargets
);

// Represents the weather forecast data
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// --- M3 API Models ---
record PlayCardRequest(string CardUid, string? TargetUid);

// --- End C# Records ---

