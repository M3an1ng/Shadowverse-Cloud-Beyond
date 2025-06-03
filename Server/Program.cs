using Serilog; // Added for Serilog configuration
using Microsoft.Extensions.DependencyInjection; // Added for CreateScope, GetRequiredService
using Microsoft.Extensions.Logging;         // Added for ILogger
using System.IO;                             // Added for Path, File
using System.Linq;                           // Added for AnyAsync, Select
using ShadowverseBeyond.Server.Hubs.Filters; // Add reference to the new filter namespace
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

// --- ShadowverseBeyond Namespaces ---
using ShadowverseBeyond.Server.Domain.Entities;         // Added Correct
using ShadowverseBeyond.Server.Domain.Interfaces;         // Existing Correct (was line 19)
// Removed using for Application layer
// Removed using for Application layer
using ShadowverseBeyond.Server.Infrastructure.Data;       // Added Correct
using ShadowverseBeyond.Server.Infrastructure.Persistence; // Existing Correct (was line 20)
using ShadowverseBeyond.Server.Hubs;                    // Existing Correct (was line 22)
using ShadowverseBeyond.Server.Services;
using Microsoft.AspNetCore.SignalR;               // Added for IGameService (located in Services namespace for now)
// Removed all incorrect 'using Server.*' statements

// REMOVED: Static Serilog configuration block (Lines 23-39)
try // Wrap application startup in try-finally to ensure logs are flushed
{
    Log.Information("Starting web application"); // Serilog startup message

    var builder = WebApplication.CreateBuilder(args);

    // --- Serilog Integration ---
    // Configure Serilog using the standard ASP.NET Core configuration pipeline
    // --- Dynamic Log File Path Generation ---
    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
    var logFilePath = Path.Combine("Logs", $"server-{timestamp}.log"); // Generate log path with startup timestamp
    // --- End Dynamic Log File Path Generation ---

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration) // Read levels and other settings from appsettings.*.json
        .ReadFrom.Services(services)                   // Allow DI for Serilog extensions
        .Enrich.FromLogContext()                       // Standard context enrichment
        .WriteTo.Console()                             // Keep console output
        .WriteTo.File(                                 // Explicitly configure file sink
            logFilePath,                               // Use the dynamically generated path
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}" // Keep the desired output template
                                                                                                                                      // rollingInterval, fileSizeLimitBytes, retainedFileCountLimit removed as each run gets a new file
        )
    ); // End UseSerilog lambda
    // --- End Serilog Integration ---


    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddEndpointsApiExplorer(); // Use standard .NET 8 methods
    builder.Services.AddSwaggerGen();       // Use standard .NET 8 methods

    // Explicitly specify the implementation namespace to resolve ambiguity
    // CRITICAL FIX: GameService holds shared game state (_gameStates), must be Singleton.
    builder.Services.AddSingleton<IGameService, ShadowverseBeyond.Server.Services.GameService>(); // Changed from AddScoped to AddSingleton

    // Register Repository and Service for Card Data
    builder.Services.AddScoped<ICardRepository, EFCardRepository>(); // Keep as Scoped due to DbContext dependency
                                                                     // Removed ICardService registration (Controller will use ICardRepository directly)
    builder.Services.AddSingleton<IGameStateManager, InMemoryGameStateManager>(); // Register InMemoryGameStateManager as singleton
    builder.Services.AddSingleton<IAiService, AiService>(); // Register AiService as Singleton
    builder.Services.AddScoped<AiTurnExecutor>(); // Register the new AI turn executor
                                                  // Register the custom Hub filter for global exception logging
    builder.Services.AddScoped<HubExceptionLoggerFilter>(); // Register the custom Hub filter for global exception logging
    builder.Services.AddMemoryCache(); // Add Memory Cache service for idempotency checks
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IClientMethodMapper, SignalRClientMethodMapper>(); // Register the client method mapper
    // Register ApplicationDbContext with SQLite
    // Reads connection string from configuration ("DefaultConnection") or defaults to Data/shadowverse.db
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/shadowverse.db"));
    // Configure System.Text.Json serialization options globally to handle enums as strings
    builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    {
        // Removed WriteIndented setting as 'app' is not available here. Set elsewhere if needed.
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Add other global options if needed, e.g.:
        // options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    // Add SignalR services and explicitly configure its JSON protocol to use the string enum converter
    // (Applying both global Http.Json.JsonOptions and specific SignalR protocol config)
    builder.Services.AddSignalR(hubOptions => // Start AddSignalR
    { // Start lambda body
        hubOptions.EnableDetailedErrors = true; // Added detailed errors option
        // hubOptions.AddFilter<HubExceptionLoggerFilter>(); // Keep filter commented out
    }) // End lambda body, now chain AddJsonProtocol
    .AddJsonProtocol(options => // Chain AddJsonProtocol
    { // Start AddJsonProtocol lambda body
        // Ensure SignalR's specific protocol options also include the converter
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Explicitly set CamelCase naming policy for SignalR JSON protocol
        options.PayloadSerializerOptions.PropertyNamingPolicy = null; // FIX: Preserve PascalCase from C# DTOs // Restore CamelCase naming policy
        // Note: hubOptions configuration is separate from AddJsonProtocol options
    }); // End AddJsonProtocol lambda body and add semicolon at the end of the statement
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
        var forecast = Enumerable.Range(1, 5).Select(index =>
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

    // Inject ICardRepository directly instead of ICardService
    app.MapGet("/api/cards", async (ICardRepository cardRepository, ILogger<Program> logger) =>
    {
        try
        {
            logger.LogInformation("API endpoint /api/cards called.");
            // Use the repository directly
            var cards = await cardRepository.GetAllAsync(); // Assuming GetAllAsync is the correct method on ICardRepository
            logger.LogInformation("Returning {CardCount} cards from /api/cards.", cards?.Count() ?? 0);
            return Results.Ok(cards ?? System.Linq.Enumerable.Empty<CardDefinition>()); // Handle potential null from repo
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

    // --- Run the Application ---
    app.Run();
    // --- End Run the Application ---
} // End of outer try block for Serilog
catch (Exception ex)
{
    // Print the full exception details directly to the console
    Console.WriteLine($"[FATAL] Application terminated unexpectedly: {ex.ToString()}");
    Log.Fatal(ex, "Application terminated unexpectedly"); // Also log via Serilog
}
finally
{
    Log.CloseAndFlush(); // Ensure all logs are written before application exits
}


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


