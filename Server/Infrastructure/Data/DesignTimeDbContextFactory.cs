// Server/Infrastructure/Data/DesignTimeDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Serilog; // Keep Serilog using for consistency if needed, but ensure minimal setup
using System;
using System.IO;

namespace ShadowverseBeyond.Server.Infrastructure.Data;

/// <summary>
/// Factory for creating ApplicationDbContext instances during design time (e.g., for EF Core Migrations).
/// This factory provides a way to configure the DbContext independently of the application's main startup logic,
/// avoiding issues with services like SignalR that are not needed or cause conflicts during design-time operations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Set up configuration sources to read appsettings.json
        // EF Core tools typically run with the project directory as the current directory.
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"; // Default to Development for safety
        var basePath = Directory.GetCurrentDirectory();

        // Ensure the base path correctly points to the Server project directory where appsettings reside
        // This might need adjustment depending on where 'dotnet ef' is executed from.
        // If executed from solution root, basePath might be 'd:\workspace\Shadowverse-beyond'
        // If executed from Server project, basePath might be 'd:\workspace\Shadowverse-beyond\Server'
        // Let's assume it runs from solution root for now, adjust if needed.
        var serverProjectPath = Path.Combine(basePath, "Server");
        if (!Directory.Exists(serverProjectPath))
        {
            // Fallback if running from Server directory itself
            serverProjectPath = basePath;
        }


        Console.WriteLine($"[DEBUG] DesignTimeDbContextFactory: BasePath='{basePath}', ServerProjectPath='{serverProjectPath}', Environment='{environmentName}'");


        var configuration = new ConfigurationBuilder()
            .SetBasePath(serverProjectPath) // Point to the Server project directory
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args) // Include command line args if needed
            .Build();

        // Minimal Serilog setup for design-time logging if necessary
        // Log.Logger = new LoggerConfiguration()
        //     .MinimumLevel.Information() // Keep logging minimal
        //     .Enrich.FromLogContext()
        //     .WriteTo.Console()
        //     .CreateBootstrapLogger(); // Use bootstrap logger

        Log.Information("DesignTimeDbContextFactory: Creating DbContext for EF Core tools (Environment: {EnvironmentName}).", environmentName);
        Console.WriteLine($"[DEBUG] DesignTimeDbContextFactory: Creating DbContext for EF Core tools (Environment: {environmentName}).");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            var errorMsg = $"Connection string 'DefaultConnection' not found in configuration for design-time factory. Searched in '{serverProjectPath}'.";
            Log.Error(errorMsg);
            Console.WriteLine($"[ERROR] {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        Log.Information("DesignTimeDbContextFactory: Using connection string from configuration."); // Don't log the actual connection string
        Console.WriteLine("[DEBUG] DesignTimeDbContextFactory: Using connection string from configuration.");

        optionsBuilder.UseSqlite(connectionString, sqlOptions =>
        {
            // Explicitly specify the migrations assembly if it's different from the current assembly.
            // In this case, ApplicationDbContext is in the same assembly as the factory (ShadowverseBeyond.Server),
            // so this might not be strictly necessary, but it's good practice for clarity.
            sqlOptions.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName);
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}