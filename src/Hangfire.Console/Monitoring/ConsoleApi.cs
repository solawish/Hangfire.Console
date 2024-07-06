﻿using System;
using System.Collections.Generic;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;
using Hangfire.Console.Storage;
using Hangfire.Storage;

namespace Hangfire.Console.Monitoring;

internal class ConsoleApi : IConsoleApi
{
    private readonly IConsoleStorage _storage;
    private readonly ConsoleOptions _consoleOptions;

    public ConsoleApi(IStorageConnection connection, ConsoleOptions consoleOptions)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        _consoleOptions = consoleOptions;
        _storage = _consoleOptions.UseConsoleHub 
            ? new ConsoleHubStorage(connection, new ConsoleHub()) 
            : new ConsoleStorage(connection);
    }

    public void Dispose()
    {
        _storage.Dispose();
    }

    public IList<LineDto> GetLines(string jobId, DateTime timestamp, LineType type = LineType.Any)
    {
        var consoleId = new ConsoleId(jobId, timestamp);

        var count = _storage.GetLineCount(consoleId);
        var result = new List<LineDto>(count);

        if (count > 0)
        {
            Dictionary<string, ProgressBarDto>? progressBars = null;

            foreach (var entry in _storage.GetLines(consoleId, 0, count))
            {
                if (entry.ProgressValue.HasValue)
                {
                    if (type == LineType.Text)
                    {
                        continue;
                    }

                    // aggregate progress value updates into single record

                    if (progressBars != null)
                    {
                        if (progressBars.TryGetValue(entry.Message, out var prev))
                        {
                            prev.Progress = entry.ProgressValue.Value;
                            prev.Color = entry.TextColor;
                            continue;
                        }
                    }
                    else
                    {
                        progressBars = new Dictionary<string, ProgressBarDto>();
                    }

                    var line = new ProgressBarDto(entry, timestamp);

                    progressBars.Add(entry.Message, line);
                    result.Add(line);
                }
                else
                {
                    if (type == LineType.ProgressBar)
                    {
                        continue;
                    }

                    result.Add(new TextLineDto(entry, timestamp));
                }
            }
        }

        return result;
    }
}
