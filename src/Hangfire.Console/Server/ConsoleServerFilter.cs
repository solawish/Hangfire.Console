﻿using System;
using Hangfire.Common;
using Hangfire.Console.Serialization;
using Hangfire.Console.Storage;
using Hangfire.Server;
using Hangfire.States;

namespace Hangfire.Console.Server;

/// <summary>
///     Server filter to initialize and cleanup console environment.
/// </summary>
internal class ConsoleServerFilter : IServerFilter
{
    private readonly ConsoleOptions _options;

    public ConsoleServerFilter(ConsoleOptions? options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        var state = filterContext.Connection.GetStateData(filterContext.BackgroundJob.Id);

        if (state == null)
        {
            // State for job not found?
            return;
        }

        if (!string.Equals(state.Name, ProcessingState.StateName, StringComparison.OrdinalIgnoreCase))
        {
            // Not in Processing state? Something is really off...
            return;
        }

        var startedAt = JobHelper.DeserializeDateTime(state.Data["StartedAt"]);

        filterContext.Items["ConsoleContext"] = new ConsoleContext(
            new ConsoleId(filterContext.BackgroundJob.Id, startedAt),
            _options.UseConsoleHub 
                ? new ConsoleHubStorage(filterContext.Connection, new ConsoleHub())
                : new ConsoleStorage(filterContext.Connection));
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        var consoleContext = ConsoleContext.FromPerformContext(filterContext);
        if (consoleContext is null)
        {
            return;
        }

        if (_options.FollowJobRetentionPolicy)
        {
            // Console sessions follow parent job expiration.
            // Normally, ConsoleApplyStateFilter will update expiration for all consoles, no extra work needed.

            // If the running job is deleted from the Dashboard, ConsoleApplyStateFilter will be called immediately,
            // but the job will continue running, unless IJobCancellationToken.ThrowIfCancellationRequested() is met. 
            // If anything is written to console after the job was deleted, it won't get a correct expiration assigned.

            // Need to re-apply expiration to prevent those records from becoming eternal garbage.
            consoleContext.FixExpiration();
        }
        else
        {
            consoleContext.Expire(_options.ExpireIn);
        }

        if (_options.UseConsoleHub)
        {
            // Flush ConsoleLog
            consoleContext.Flush();
        }

        consoleContext.Dispose();

        // remove console context to prevent further writes from filters
        filterContext.Items.Remove("ConsoleContext");
    }
}
