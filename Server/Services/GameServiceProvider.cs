using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Provides global, static access to game-specific, scoped services for the current operation.
    /// This is a Service Locator pattern implementation, refactored to be safe for concurrent async operations.
    /// It relies on a centrally managed IGameContextAccessor to dynamically resolve the correct service scope.
    /// </summary>
    public static class GameServiceProvider
    {
        private static IGameContextAccessor? _contextAccessor;
        public static ILogger? Logger { get; set; }

        /// <summary>
        /// The single, static accessor for the current game context.
        /// This MUST be initialized at application startup.
        /// </summary>
        public static IGameContextAccessor? ContextAccessor
        {
            get => _contextAccessor;
            set
            {
                Logger?.LogTrace("GameServiceProvider.ContextAccessor is being set. New value is null: {is_null}", value == null);
                _contextAccessor = value;
            }
        }


        /// <summary>
        /// Retrieves the current IServiceProvider from the ambient GameContext.
        /// </summary>
        private static IServiceProvider ServiceProvider
        {
            get
            {
                var accessor = ContextAccessor ?? throw new InvalidOperationException(
                    "GameServiceProvider.ContextAccessor has not been initialized.");
                var context = accessor.GameContext ?? throw new InvalidOperationException(
                    "Cannot access services because the GameContext has not been set for the current scope. " +
                    "Ensure that the operation is running within a GameContextFilter (for Hub requests) " +
                    "or a manually created scope (for background tasks).");
                var serviceProvider = context.ServiceProvider;
                return serviceProvider;
            }
        }

        /// <summary>
        /// Gets a required service from the current game's service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the GameContext has not been set for the current scope,
        /// or if the requested service is not registered in the current scope's service provider.
        /// </exception>
        public static T GetService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Convenience property to get the current GameContext from the accessor.
        /// Returns null if the context has not been set for the current scope.
        /// </summary>
        public static GameContext? CurrentGameContext => ContextAccessor?.GameContext;

        /// <summary>
        /// Convenience property to get the IBuffService from the current scope.
        /// </summary>
        public static IBuffService BuffService => GetService<IBuffService>();

    }
}