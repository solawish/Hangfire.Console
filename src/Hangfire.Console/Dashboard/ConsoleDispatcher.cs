using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hangfire.Common;
using Hangfire.Console.Constants;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;
using Hangfire.Console.Storage;
using Hangfire.Dashboard;
using Hangfire.Storage;

namespace Hangfire.Console.Dashboard;

/// <summary>
///     Provides incremental updates for a console.
/// </summary>
internal class ConsoleDispatcher : IDashboardDispatcher
{
    private readonly ConsoleOptions _options;
    private readonly HttpClient _httpClient;

    public ConsoleDispatcher(ConsoleOptions options, HttpClient? httpClient = default)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task Dispatch(DashboardContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var consoleId = ConsoleId.Parse(context.UriMatch.Groups[1].Value);

        var startArg = context.Request.GetQuery("start");

        // try to parse offset at which we should start returning requests
        if (string.IsNullOrEmpty(startArg) || !int.TryParse(startArg, out var start))
        {
            // if not provided or invalid, fetch records from the very start
            start = 0;
        }

        var buffer = new StringBuilder();

        if(_options.UseConsoleHub)
        {
            var ip = IpExtensions.GetIp();
            var jobServerIp = ((JobStorageConnection)context.Storage.GetConnection()).GetValueFromHash(consoleId.GetHashKey(), HashKey.JobServerIp);

            if (!jobServerIp.Equals(ip))
            {
                await GetConsoleRendererFromAnotherServer(buffer, jobServerIp, consoleId, start);
            }
        }

        // if buffer is empty, render the console locally
        if (buffer.Length.Equals(0))
        {
            using IConsoleStorage storage = _options.UseConsoleHub
                ? new ConsoleHubStorage(context.Storage.GetConnection(), new ConsoleHub())
                : new ConsoleStorage(context.Storage.GetConnection());
            ConsoleRenderer.RenderLineBuffer(buffer, storage, consoleId, start);
        }

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(buffer.ToString());
    }

    private async Task GetConsoleRendererFromAnotherServer(StringBuilder buffer, string jobServerIp, ConsoleId consoleId, int start)
    {
        var response = await _httpClient.GetAsync($"http://{jobServerIp}/hangfire/console/{consoleId}?start={start}");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            buffer.Append(content);
        }
    }
}
