using System;
using Hangfire.Console.Monitoring;
using JetBrains.Annotations;

namespace Hangfire.Console;

/// <summary>
///     Provides extension methods for <see cref="JobStorage" />.
/// </summary>
[PublicAPI]
public static class JobStorageExtensions
{
    /// <summary>
    ///     Returns an instance of <see cref="IConsoleApi" />.
    /// </summary>
    /// <param name="storage">Job storage instance</param>
    /// <param name="consoleOptions"></param>
    /// <returns>Console API instance</returns>
    public static IConsoleApi GetConsoleApi(this JobStorage storage, ConsoleOptions consoleOptions)
    {
        if (storage == null)
        {
            throw new ArgumentNullException(nameof(storage));
        }

        if (consoleOptions == null)
        {
            throw new ArgumentNullException(nameof(consoleOptions));
        }

        return new ConsoleApi(storage.GetConnection(), consoleOptions);
    }
}
