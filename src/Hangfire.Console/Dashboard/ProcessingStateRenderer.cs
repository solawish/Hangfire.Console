﻿using System;
using System.Collections.Generic;
using System.Text;
using Hangfire.Common;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;
using Hangfire.Console.Storage;
using Hangfire.Dashboard;
using Hangfire.Dashboard.Extensions;

namespace Hangfire.Console.Dashboard;

/// <summary>
///     Replacement renderer for Processing state.
/// </summary>
internal class ProcessingStateRenderer
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ConsoleOptions _options;

    public ProcessingStateRenderer(ConsoleOptions? options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public NonEscapedString Render(HtmlHelper helper, IDictionary<string, string> stateData)
    {
        var builder = new StringBuilder();

        builder.Append("<dl class=\"dl-horizontal\">");

        if (stateData.TryGetValue("ServerId", out var serverId) || stateData.TryGetValue("ServerName", out serverId))
        {
            builder.Append("<dt>Server:</dt>");
            builder.Append($"<dd>{helper.ServerId(serverId)}</dd>");
        }

        if (stateData.TryGetValue("WorkerId", out var workerId))
        {
            builder.Append("<dt>Worker:</dt>");
            builder.Append($"<dd>{workerId.Substring(0, 8)}</dd>");
        }
        else if (stateData.TryGetValue("WorkerNumber", out var workerNumber))
        {
            builder.Append("<dt>Worker:</dt>");
            builder.Append($"<dd>#{workerNumber}</dd>");
        }

        builder.Append("</dl>");

        var page = helper.GetPage();
        if (!page.RequestPath.StartsWith("/jobs/details/"))
        {
            return new NonEscapedString(builder.ToString());
        }

        // We cannot cast page to an internal type JobDetailsPage to get jobId :(
        var jobId = page.RequestPath.Substring("/jobs/details/".Length);

        var startedAt = JobHelper.DeserializeDateTime(stateData["StartedAt"]);
        var consoleId = new ConsoleId(jobId, startedAt);

        builder.Append("<div class=\"console-area\">");
        builder.AppendFormat("<div class=\"console\" data-id=\"{0}\">", consoleId);

        using (IConsoleStorage storage = _options.UseConsoleHub 
            ? new ConsoleHubStorage(page.Storage.GetConnection(), new ConsoleHub()) 
            : new ConsoleStorage(page.Storage.GetConnection()))
        {
            ConsoleRenderer.RenderLineBuffer(builder, storage, consoleId, 0);
        }

        builder.Append("</div>");
        builder.Append("</div>");

        return new NonEscapedString(builder.ToString());
    }
}
