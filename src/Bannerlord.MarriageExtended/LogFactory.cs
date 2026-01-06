using Bannerlord.ButterLib.Common.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarriageExtended
{
    /// <summary>
    /// Factory for creating loggers using ButterLib's logging infrastructure.
    /// </summary>
    internal static class LogFactory
    {
        /// <summary>
        /// Gets a logger for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to create a logger for.</typeparam>
        /// <returns>An ILogger instance, or NullLogger if logging is not available.</returns>
        internal static ILogger Get<T>()
        {
            var serviceProvider = SubModule.Instance?.GetServiceProvider()
                ?? SubModule.Instance?.GetTempServiceProvider();

            return serviceProvider?.GetRequiredService<ILogger<T>>()
                ?? NullLogger<T>.Instance;
        }
    }
}
