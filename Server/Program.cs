using Microsoft.Extensions.DependencyInjection; // Added for CreateScope, GetRequiredService
using Microsoft.Extensions.Logging;         // Added for ILogger
using System.IO;                             // Added for Path, File
using System.Linq;                           // Added for AnyAsync, Select
using System.Collections.Generic;            // Added for List<> in DTOs
using Server.Domain.Entities; // Added to resolve CardDefinition class
using Microsoft.Extensions.FileProviders; // Added for PhysicalFileProvider
using System.Text.Json; // Needed for JsonSerializer
using System.Text.Json.Serialization; // Needed for JsonPropertyName attribute
using ShadowverseBeyond.Server.Services; // Corrected namespace for GameService
using Microsoft.EntityFrameworkCore; // Added for DbContext
using Server.Infrastructure.Data;   // Added for ApplicationDbContext
// Using statements for the new layers based on recent file creation
using Server.Application.Interfaces;
using Server.Application.Services;
using Server.Domain.Interfaces;
using Server.Infrastructure.Persistence;
// using Server.Domain.Entities; // Line 6 already includes this, no need to duplicate
using ShadowverseBeyond.Server.Hubs; // Corrected namespace for GameHub

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer(); // Use standard .NET 8 methods
builder.Services.AddSwaggerGen();       // Use standard .NET 8 methods

builder.Services.AddScoped<IGameService, GameService>(); // Added DI registration

// Register Repository and Service for Card Data
builder.Services.AddScoped<ICardRepository, EFCardRepository>();
builder.Services.AddScoped<ICardService, CardService>();

// Register ApplicationDbContext with SQLite
// Reads connection string from configuration ("DefaultConnection") or defaults to Data/shadowverse.db
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/shadowverse.db"));
builder.Services.AddSignalR(); // Added for SignalR support
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();   // Use standard .NET 8 middleware
    app.UseSwaggerUI(); // Use standard .NET 8 middleware
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
// List<CardDefinition>? cardDatabase = null; // Removed unused variable (Warning CS0219)

app.MapGet("/api/cards", async (ICardService cardService, ILogger<Program> logger) => // Inject ICardService
{
    try
    {
        logger.LogInformation("API endpoint /api/cards called.");
        var cards = await cardService.GetAllCardsAsync();
        logger.LogInformation("Returning {CardCount} cards from /api/cards.", cards?.Count() ?? 0);
        return Results.Ok(cards);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while handling /api/cards request.");
        // Return a generic server error response
        return Results.Problem("An unexpected error occurred while retrieving card data.");
    }
})
.WithName("GetCardData");
// --- End Card Data API Endpoint ---

// --- M3 API Endpoints ---
// // --- M3 API Endpoints (Replaced by SignalR) ---
// app.MapPost("/api/game/playCard", async (PlayCardRequest request, IGameService gameService) => {
//     // Delegate the work to the service
//     var response = await gameService.HandlePlayCardAsync(request);
//
//     if (response.Success)
//     {
//         // In M4+, response would contain the updated game state to return
//         // For M3, just return the success status (or the simple response object)
//         return Results.Ok(response);
//     }
//     else
//     {
//         // Return a Bad Request or other appropriate error code
//         return Results.BadRequest(response);
//     }
// })
// .WithName("PlayCard") // Optional: OpenAPI metadata
// .WithTags("GameActions"); // Optional: OpenAPI metadata
// // --- End M3 API Endpoints ---
// --- End M3 API Endpoints ---

// Map SignalR Hub
app.MapHub<GameHub>("/gamehub");



// ============================================================
// Apply migrations and Seed the database on startup
// ============================================================
try
{
    // Create a scope to resolve services
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>(); // Use a generic logger for Program
        var context = services.GetRequiredService<ApplicationDbContext>();
        var cardDataPathForSeeding = Path.Combine(builder.Environment.ContentRootPath, "..", "data", "card_data.json"); // Get path for seeding

        // Apply pending migrations automatically
        logger.LogInformation("Applying database migrations (if any)...");
        context.Database.Migrate(); // Applies pending migrations or does nothing if up-to-date
        logger.LogInformation("Database migrations applied successfully.");

        // Seed the database using the static DataSeeder class
        await DataSeeder.SeedDatabaseAsync(app); // Call the new static method, pass the app instance
    }
}
catch (Exception ex)
{
    // Use the application's logger instance directly if scope creation fails or during seeding
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during database migration or seeding.");
    // Depending on the severity, you might want to stop the application:
    // Environment.Exit(1);
}
app.Run();

// --- C# Records ---

// Removed CardEffect and CardDefinition records from here as they conflict with Entities/Definitions
// and should reside in separate files.

// Represents the weather forecast data
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// --- M3 API Models ---
// Removed duplicate PlayCardRequest record definition from here.
// It is now defined in Server/Services/IGameService.cs

// --- End C# Records ---


