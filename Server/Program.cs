using Scrutor; // Added for assembly scanning registration
using AutoMapper; // Added for IMapper
using ShadowverseBeyond.Server.Dtos; // Added for DTO types like CardDefinitionDto
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
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions; // Added for AddDslFunctionSystem extension
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation; // For IDslCompiler
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
using ShadowverseBeyond.Server.Domain.Query;               // Added for ICardQueryExecutor
using ShadowverseBeyond.Server.Services.Abstractions;       // Added for IGameDataProvider
using ShadowverseBeyond.Server.Domain.Events.Services; // Added for IEventHistoryService
using ShadowverseBeyond.Server.Infrastructure.Services; // Added for InMemoryEventHistoryService
using ShadowverseBeyond.Server.Options;


using ShadowverseBeyond.Server.Domain.Abilities.Common; // For IAbility
using ShadowverseBeyond.Server.Domain.Abilities.System; // For System Ability classes
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;

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
                                                       // 【修复日志重复】删除重复的Console配置，只保留appsettings.Development.json中的配置
                                                       // .WriteTo.Console()                          // 删除：避免与配置文件中的Console配置重复
        .WriteTo.File(                                 // Explicitly configure file sink
            logFilePath,                               // Use the dynamically generated path
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}" // Keep the desired output template
                                                                                                                                      // rollingInterval, fileSizeLimitBytes, retainedFileCountLimit removed as each run gets a new file
        )
    ); // End UseSerilog lambda
    // Log.Warning("Serilog configuration (UseSerilog) is temporarily commented out for debugging."); // Removed Warning Log
    // --- End Serilog Integration ---


    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddEndpointsApiExplorer(); // Use standard .NET 8 methods
    builder.Services.AddSwaggerGen();       // Use standard .NET 8 methods

    // Add support for API Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
    Log.Information("Registered API Controllers.");

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("replay-read", limiterOptions =>
        {
            limiterOptions.PermitLimit = 120;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
    });
    Log.Information("Registered RateLimiter policies.");

    // Register AutoMapper - Scans the assembly containing Program for Profile classes
    builder.Services.AddAutoMapper(typeof(Program).Assembly);
    Log.Information("Registered AutoMapper."); // Added Log

    // Register custom AutoMapper type converters


    // 【修复】暂时移除自定义解析器注册，改用更简单的方法
    // builder.Services.AddSingleton<ShadowverseBeyond.Server.Mappings.HandCollectionResolver>();
    // Log.Information("Registered custom AutoMapper resolvers for CardInstance collections as Singleton."); // Added Log

    // Explicitly specify the implementation namespace to resolve ambiguity
    // CRITICAL FIX: GameService holds shared game state (_gameStates), must be Singleton.
    // builder.Services.AddSingleton<IGameService, ShadowverseBeyond.Server.Services.GameService>(); // Moved inside conditional block
    // Log.Information("Registered IGameService."); // Moved inside conditional block

    // Register Repository and Service for Card Data
    builder.Services.AddScoped<ICardRepository, EFCardRepository>(); // Keep as Scoped due to DbContext dependency
    Log.Information("Registered ICardRepository."); // Added Log
                                                    // Removed ICardService registration (Controller will use ICardRepository directly)

    builder.Services.AddSingleton<IGuestDeckStore, GuestDeckStore>();
    builder.Services.AddSingleton<IDeckCodeService, DeckCodeService>();
    builder.Services.AddScoped<IDeckValidationService, DeckValidationService>();
    builder.Services.AddScoped<IDeckService, DeckService>();
    Log.Information("Registered deck services.");

    // Register GameDataProvider for data caching and preloading
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IGameDataProvider, ShadowverseBeyond.Server.Services.GameDataProvider>();
    Log.Information("Registered IGameDataProvider as Singleton.");

    // Register GameDataPreloadService for startup data initialization
    builder.Services.AddHostedService<ShadowverseBeyond.Server.Services.GameDataPreloadService>();
    Log.Information("Registered GameDataPreloadService as HostedService.");

    builder.Services.AddSingleton<IGameStateManager, InMemoryGameStateManager>(); // Register InMemoryGameStateManager as singleton
    Log.Information("Registered IGameStateManager."); // Added Log
    builder.Services.AddScoped<IAiService, AiService>(); // Register AiService as Scoped
    Log.Information("Registered IAiService."); // Added Log
                                               // Register Effect Handling Services (moved to avoid duplication)
                                               // ITargetProcessingService registration moved to line ~233
                                               // Effect Handler registration moved to Scrutor section below
                                               // builder.Services.AddScoped<AiTurnExecutor>(); // Moved inside conditional block
                                               // Log.Information("Registered AiTurnExecutor."); // Moved inside conditional block
                                               // Register the custom Hub filter for global exception logging
                                               // builder.Services.AddScoped<HubExceptionLoggerFilter>(); // Moved inside conditional block
                                               // Log.Information("Registered HubExceptionLoggerFilter."); // Moved inside conditional block
    builder.Services.AddMemoryCache(); // Add Memory Cache service for idempotency checks
    Log.Information("Registered MemoryCache."); // Added Log
    // builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IClientMethodMapper, SignalRClientMethodMapper>(); // Moved inside conditional block
    // Log.Information("Registered IClientMethodMapper."); // Moved inside conditional block
    // Register ApplicationDbContext with SQLite
    // Reads connection string from configuration ("DefaultConnection") or defaults to Data/shadowverse.db
    Log.Information("Attempting to register ApplicationDbContext..."); // Added Log
    // Temporarily force using the hardcoded connection string to isolate configuration issues during design time.
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/shadowverse.db"));
    // options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/shadowverse.db")); // Original line
    Log.Information("Successfully registered ApplicationDbContext (using hardcoded path for debugging)."); // Added Log
    // Configure System.Text.Json serialization options globally to handle enums as strings
    // The configuration is now handled by AddJsonOptions on AddControllers, so this is redundant.

    // --- SignalR and Dependent Services Registration ---
    // With DesignTimeDbContextFactory in place, Program.cs should only contain runtime configuration.
    // EF Core tools will use the factory and bypass this startup logic.
    Log.Information("Registering SignalR and dependent services for runtime...");

    // Add SignalR services and explicitly configure its JSON protocol to use the string enum converter
    // (Applying both global Http.Json.JsonOptions and specific SignalR protocol config)
    Log.Information("Attempting to configure SignalR..."); // Added Log
    builder.Services.AddSignalR(hubOptions => // Start AddSignalR
    { // Start lambda body
        hubOptions.EnableDetailedErrors = true; // Added detailed errors option
        hubOptions.AddFilter<GameContextFilter>(); // Register the GameContextFilter for all hubs
    }) // End lambda body, now chain AddJsonProtocol
    .AddJsonProtocol(options => // Chain AddJsonProtocol
    { // Start AddJsonProtocol lambda body
        // Ensure SignalR's specific protocol options also include the converter
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Explicitly set CamelCase naming policy for SignalR JSON protocol
        options.PayloadSerializerOptions.PropertyNamingPolicy = null; // FIX: Preserve PascalCase from C# DTOs // Restore CamelCase naming policy
        // Note: hubOptions configuration is separate from AddJsonProtocol options
    }); // End AddJsonProtocol lambda body and add semicolon at the end of the statement
    Log.Information("Successfully configured SignalR."); // Added Log

    // Register services dependent on SignalR (IHubContext)
    // GameService registration is now dependent on other singletons, so we'll register it after them.
    Log.Information("Registered IGameService."); // Added Log
    builder.Services.AddScoped<AiTurnExecutor>(); // Register the new AI turn executor
    Log.Information("Registered AiTurnExecutor."); // Added Log
    builder.Services.AddScoped<HubExceptionLoggerFilter>(); // Register the custom Hub filter for global exception logging
    Log.Information("Registered HubExceptionLoggerFilter."); // Added Log
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IClientMethodMapper, SignalRClientMethodMapper>(); // Register the client method mapper
    Log.Information("Registered IClientMethodMapper."); // Added Log
    builder.Services.AddScoped<ShadowverseBeyond.Server.Services.Abstractions.IDebugCommandParser, ShadowverseBeyond.Server.Services.DebugCommandParser>();
    Log.Information("Registered IDebugCommandParser."); // Added Log
    builder.Services.AddSingleton<IMatchService, MatchService>(); // Register in-memory match service for lobby
    Log.Information("Registered IMatchService (in-memory lobby)."); // Added Log

    // Register Transaction Service
    builder.Services.AddScoped<ITransactionService, TransactionService>(); // Register the transaction service
    Log.Information("Registered ITransactionService."); // Added Log

    // Register Economy Service
    builder.Services.AddScoped<IEconomyService, EconomyService>(); // Register the economy service
    Log.Information("Registered IEconomyService."); // Added Log
    builder.Services.AddScoped<IMetaProgressionService, MetaProgressionService>();
    Log.Information("Registered IMetaProgressionService.");

    // Register DSL Function System (includes DslEvaluator, DslExpressionParser, IFunctionRegistry)
    builder.Services.AddDslFunctionSystem();
    // Override DslExpressionParser and DslEvaluator to be Singleton to match ValueSourceResolver lifetime
    Log.Information("Registered DSL Function System with Singleton DslEvaluator and DslExpressionParser.");

    // Register Card Query Executor
    builder.Services.AddScoped<IQueryDslParser, QueryDslParser>(); // Register the new DSL parser
    builder.Services.AddScoped<IQueryExecutor, QueryExecutor>(); // Register the new query executor

    // Register the DSL expression cache as a Singleton to share compiled expressions across the application.
    builder.Services.AddSingleton<IDslExpressionCache, DslExpressionCache>();
    builder.Services.AddScoped<IDslCompiler, DslCompiler>(); // Register the new DSL compiler
    Log.Information("Registered ICardQueryDslParser, ICardQueryExecutor, IDslExpressionCache, and IDslCompiler."); // Added Log

    // Register AbilityLoader as a scoped service with a direct dependency on the database context.
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.AbilityLoader>();
    Log.Information("Registered AbilityLoader with direct database access.");

    // Register Core Ability System Services (New Registrations)
    // TODO: Create and register concrete implementations for IValueSourceResolver if it doesn't exist.
    // Commenting out old non-generic IEffectHandler registration, replaced by specific handler registrations below or a factory pattern later.
    // builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Effects.IEffectHandler, ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectHandler>();
    // Log.Information("Registered old IEffectHandler (commented out).");
    // Changed to Singleton to match EffectExecutor lifetime
    // Use factory pattern to resolve ICardRepository from scope when needed
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Effects.IValueSourceResolver>(serviceProvider =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ShadowverseBeyond.Server.Domain.Abilities.Effects.ValueSourceResolver>>();
        var dslCompiler = serviceProvider.GetRequiredService<ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation.IDslCompiler>();

        // DslExpressionParser is no longer needed as the new Parser is instantiated on the fly.
        return new ShadowverseBeyond.Server.Domain.Abilities.Effects.ValueSourceResolver(
            logger, dslCompiler);
    });
    Log.Information("Registered IValueSourceResolver as Singleton with scoped ICardRepository.");
    // Register GameStateActionDispatcher - REMOVED as it's part of the old system
    // builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Effects.IGameStateActionDispatcher, ShadowverseBeyond.Server.Domain.Abilities.Effects.GameStateActionDispatcher>();
    // Log.Information("Registered IGameStateActionDispatcher.");
    // Changed to Singleton to match AbilityManager lifetime
    // Use factory pattern to resolve ICardRepository from scope when needed
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Conditions.IConditionFactory>(serviceProvider =>
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var mapper = serviceProvider.GetRequiredService<IMapper>();
        // The factory now depends on IConditionEvaluator, which is resolved from the DI container.
        var conditionEvaluator = serviceProvider.GetRequiredService<ShadowverseBeyond.Server.Domain.Abilities.Conditions.IConditionEvaluator>();
        return new ShadowverseBeyond.Server.Domain.Abilities.Conditions.ConditionFactory(
            loggerFactory, mapper, conditionEvaluator);
    });
    Log.Information("Registered IConditionFactory as Singleton with scoped ICardRepository.");
    // Register ConditionEvaluator (Assuming implementation exists)
    // Register IConditionEvaluator, ensuring the concrete implementation is DslConditionEvaluator
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Conditions.IConditionEvaluator, ShadowverseBeyond.Server.Domain.Abilities.Conditions.DslConditionEvaluator>();
    Log.Information("Registered IConditionEvaluator with DslConditionEvaluator implementation.");
    // Changed to Scoped to allow access to Scoped EffectHandlers
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Effects.IEffectExecutor, ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectExecutor>();
    Log.Information("Registered IEffectExecutor as Scoped.");

    // --- Target Selection System ---
    builder.Services.AddScoped<TargetSelectionService>();
    builder.Services.AddScoped<ITargetFactory, TargetFactory>();
    Log.Information("Registered Target Selection System with Scoped lifetime.");

    // --- Pending Action Service for interactive choices ---
    builder.Services.AddSingleton<IPendingActionService, PendingActionService>();
    Log.Information("Registered IPendingActionService as Singleton.");

    // Register Game Action Transaction Service
    builder.Services.AddScoped<IGameActionTransactionService, GameActionTransactionService>();
    Log.Information("Registered IGameActionTransactionService as Scoped.");

    // Register Effect UI Metadata Service
    builder.Services.AddSingleton<IEffectUIMetadataService, EffectUIMetadataService>();
    Log.Information("Registered IEffectUIMetadataService as Singleton.");

    // Register DSL Metadata Service
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IDslMetadataService, ShadowverseBeyond.Server.Services.DslMetadataService>();
    Log.Information("Registered IDslMetadataService as Singleton.");

    // Register Event Metadata Service
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IEventMetadataService, ShadowverseBeyond.Server.Services.EventMetadataService>();
    Log.Information("Registered IEventMetadataService as Singleton.");

    // Register Query Operation Metadata Service
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IQueryOperationMetadataService, ShadowverseBeyond.Server.Services.QueryOperationMetadataService>();
    Log.Information("Registered IQueryOperationMetadataService as Singleton.");

    // Register Ability Editor Metadata Service (Top-Level)
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IAbilityEditorMetadataService, ShadowverseBeyond.Server.Services.AbilityEditorMetadataService>();
    Log.Information("Registered IAbilityEditorMetadataService as Singleton.");

    // Register Target Processing Service
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services.ITargetProcessingService, ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services.TargetProcessingService>();
    Log.Information("Registered ITargetProcessingService as Scoped.");

    // Register a factory for ILogger (non-generic) to resolve logger issues for EffectHandlers
    builder.Services.AddScoped<Microsoft.Extensions.Logging.ILogger>(serviceProvider =>
        serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EffectHandler"));

    // Register the new Effect Handler Registry as a Singleton
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services.IEffectHandlerRegistry, ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services.EffectHandlerRegistry>();
    Log.Information("Registered IEffectHandlerRegistry as Singleton.");

    // Register all Effect Handlers using Scrutor (Updated after refactoring)
    // All handlers now inherit from EffectHandlerBase and use ITargetProcessingService
    builder.Services.Scan(scan => scan
        .FromAssemblyOf<Program>()
        .AddClasses(classes => classes.AssignableTo(typeof(ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases.EffectHandlerBase<>)))
        .AsSelf()
        .WithScopedLifetime());

    Log.Information("Automatically registered all Effect Handlers using Scrutor (all inherit from EffectHandlerBase).");
    Log.Information("New handlers added: VanishEffectHandler, ReturnEffectHandler, RemoveBuffEffectHandler, AddBuffEffectHandler, DiscardEffectHandler");

    // --- Ability and Buff Management ---
    // AbilityFactory and its dependencies must be Scoped to align with the GameState/request lifetime.
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.IAbilityFactory, ShadowverseBeyond.Server.Domain.Abilities.AbilityFactory>();

    // AbilityManager must be Scoped as it manages abilities for a specific game instance.
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Abilities.AbilityManager>();
    builder.Services.AddScoped<IAbilityManager>(provider =>
    {
        var contextAccessor = provider.GetRequiredService<IGameContextAccessor>();
        if (contextAccessor.GameContext?.AbilityManager == null)
        {
            // This can happen outside of a game context, which is valid.
            // Return a transient, non-game-state-bound instance for such cases (e.g., API calls).
            // The real, stateful manager is provided by the GameContext.
            return provider.GetRequiredService<AbilityManager>();
        }
        return contextAccessor.GameContext.AbilityManager;
    });
    Log.Information("Registered AbilityFactory and AbilityManager as Singleton.");

    // Register System Abilities as IAbility
    // builder.Services.AddSingleton<IAbility, EvolveAbility>(); // Temporarily disabled after refactoring the evolve flow
    // Register MulliganAbility's dependency to be injected into its constructor
    Log.Information("Registered System Abilities (EndTurn, PlayCard, Attack, Evolve, Mulligan) and their dependencies as Singleton.");

    // Register IBuffManager as Scoped, as it manages buffs for a specific game instance.
    builder.Services.AddScoped<BuffManager>();
    builder.Services.AddScoped<IBuffManager>(provider =>
    {
        var contextAccessor = provider.GetRequiredService<IGameContextAccessor>();
        if (contextAccessor.GameContext?.BuffManager == null)
        {
            // Similar to AbilityManager, provide a transient instance if outside a game context.
            return provider.GetRequiredService<BuffManager>();
        }
        return contextAccessor.GameContext.BuffManager;
    });
    Log.Information("Registered IBuffManager as Scoped.");
    builder.Services.AddScoped<IBuffEvaluator, BuffEvaluator>();
    Log.Information("Registered IBuffEvaluator as Scoped.");
    // Register BuffFactory for creating Buff instances with GameDataProvider support
    // Changed to Singleton to match GameService lifetime

    // Register CardFactory
    // Register BuffFactory from the correct namespace
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Buffs.IBuffFactory, ShadowverseBeyond.Server.Domain.Buffs.BuffFactory>();
    Log.Information("Registered IBuffFactory (from Domain.Buffs) as Scoped.");

    // Register CardFactory
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Factories.ICardFactory, ShadowverseBeyond.Server.Domain.Factories.CardFactory>();
    Log.Information("Registered ICardFactory as Scoped.");

    // Register BuffStackingEngine for buff conflict resolution
    // Changed to Singleton to match GameService lifetime
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Domain.Buffs.BuffStackingEngine>();
    Log.Information("Registered BuffStackingEngine as Singleton.");
    // Register CardStatsService for Buff-aware property calculations
    // Register Buff Service for stat calculations
    builder.Services.AddScoped<IBuffService, BuffService>();
    Log.Information("Registered IBuffService.");

    // Register GameContextAccessor as Singleton. It uses AsyncLocal internally, so a single instance
    // can safely manage contexts for multiple concurrent requests.
    builder.Services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
    Log.Information("Registered IGameContextAccessor as Singleton.");

    // Register Event Processor as Scoped to allow access to Scoped services like IEffectExecutor
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.Events.IEventProcessor, ShadowverseBeyond.Server.Domain.Events.EventProcessor>();
    Log.Information("Registered IEventProcessor as Scoped.");

    // Register Effect Queue System
    builder.Services.AddScoped<ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.IEffectQueue, ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.EffectQueue>();
    builder.Services.AddSingleton<ShadowverseBeyond.Server.Services.Abstractions.IEffectQueueService, ShadowverseBeyond.Server.Services.EffectQueueService>();

    Log.Information("Registered Effect Queue System.");

    // Register Event History Service
    builder.Services.AddSingleton<InMemoryEventHistoryService>();
    builder.Services.AddSingleton<IEventHistoryService, JournalBackedEventHistoryService>();
    Log.Information("Registered IEventHistoryService as Singleton (JournalBackedEventHistoryService).");

    builder.Services.Configure<ReplayOptions>(builder.Configuration.GetSection("Replay"));
    Log.Information("Configured ReplayOptions (Replay section).");

    builder.Services.AddScoped<ICommittedEventJournal, SqliteReplayJournal>();
    Log.Information("Registered ICommittedEventJournal as Scoped (SqliteReplayJournal).");

    builder.Services.AddScoped<ISnapshotStore, SqliteSnapshotStore>();
    Log.Information("Registered ISnapshotStore as Scoped (SqliteSnapshotStore).");

    builder.Services.AddSingleton<IRecordedEventMapper, JsonRecordedEventMapper>();
    Log.Information("Registered IRecordedEventMapper as Singleton (JsonRecordedEventMapper).");

    builder.Services.AddSingleton<IGameEventRawEventMapper, JsonGameEventRawEventMapper>();
    Log.Information("Registered IGameEventRawEventMapper as Singleton (JsonGameEventRawEventMapper).");

    builder.Services.AddSingleton<IRawEventValidator, RawEventValidator>();
    Log.Information("Registered IRawEventValidator as Singleton (RawEventValidator).");

    builder.Services.AddSingleton<IRawEventGameEventRegistry, RawEventGameEventRegistry>();
    Log.Information("Registered IRawEventGameEventRegistry as Singleton (RawEventGameEventRegistry).");

    builder.Services.AddSingleton<IGameEventRehydrator, BestEffortGameEventRehydrator>();
    Log.Information("Registered IGameEventRehydrator as Singleton (BestEffortGameEventRehydrator).");

    builder.Services.AddHostedService<ReplayCleanupHostedService>();
    Log.Information("Registered ReplayCleanupHostedService.");

    // Register Database Seeding Services

    Log.Information("SignalR and dependent services registered.");

    // Register GameService as Singleton now that all its dependencies are registered as Singletons
    // IGameService MUST be a Singleton to manage all game states.
    // We will resolve its scoped dependencies manually using IServiceProvider.
    builder.Services.AddSingleton<IGameService>(provider =>
    {
        var hubContext = provider.GetRequiredService<IHubContext<GameHub>>();
        var gameServiceLogger = provider.GetRequiredService<ILogger<GameService>>();
        var mapper = provider.GetRequiredService<IMapper>();
        var clientMethodMapper = provider.GetRequiredService<IClientMethodMapper>();
        var gameDataProvider = provider.GetRequiredService<IGameDataProvider>();
        var matchService = provider.GetRequiredService<IMatchService>();
        var serviceScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var pendingActionService = provider.GetRequiredService<IPendingActionService>();

        return new GameService(
            hubContext,
            gameServiceLogger,
            mapper,
            clientMethodMapper,
            gameDataProvider,
            matchService,
            serviceScopeFactory,
            pendingActionService
        );
    });
    Log.Information("Registered IGameService as Singleton.");
    // --- End SignalR Registration ---


    Log.Information("All services registered (or skipped). Attempting builder.Build()..."); // Modified Log
    var app = builder.Build();
    Log.Information("builder.Build() completed.");

    // --- Initialize Static Service Providers ---
    // This must be done after app is built but before it runs.
    var gameContextAccessor = app.Services.GetRequiredService<IGameContextAccessor>();
    GameServiceProvider.ContextAccessor = gameContextAccessor;
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    GameServiceProvider.Logger = loggerFactory.CreateLogger("GameServiceProvider");
    Log.Information("Initialized static GameServiceProvider with IGameContextAccessor and ILogger.");
    // --- End Initialization ---

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

    app.UseRateLimiter();

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
    // REMOVED: Duplicate minimal API endpoint for /api/cards
    // This was causing AmbiguousMatchException with CardsController.GetAllCards()
    // The CardsController properly handles GET /api/cards with proper error handling,
    // logging, and DTO mapping. No need for duplicate minimal API endpoint.
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

    // Map API Controllers
    app.MapControllers();


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

            // 在开发环境中强制重新加载数据（临时调试）
            // The logic to check if seeding is needed is now entirely within DataSeeder.
            // Program.cs is only responsible for invoking the seeder.
            logger.LogInformation("Invoking DataSeeder to ensure database is seeded...");
            await DataSeeder.SeedDatabaseAsync(app);
            logger.LogInformation("DataSeeder invocation complete.");

            // Seed CardAbility data
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

// Add this partial class definition to make the Program class public and accessible for integration tests.
public partial class Program { }
