using Hangfire.Console.Serialization;
using Hangfire.Console.Storage;
using Hangfire.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hangfire.Console.Server;

internal class ConsoleHub : IConsoleHub
{
    public static readonly ConcurrentDictionary<ConsoleId, BlockingCollection<ConsoleLine>> Hub = new();

    public void Init(ConsoleId consoleId)
    {
        if (Hub.ContainsKey(consoleId))
        {
            throw new ArgumentException("ConsoleId Exist", consoleId.JobId);
        }
        Hub.TryAdd(consoleId, new BlockingCollection<ConsoleLine>());
    }

    public void AddLine(ConsoleId consoleId, ConsoleLine consoleLine)
    {
        if (!Hub.ContainsKey(consoleId))
        {
            throw new ArgumentException("ConsoleId mismatch", consoleId.JobId);
        }
        Hub[consoleId].Add(consoleLine);
    }

    public IEnumerable<ConsoleLine> GetLines(ConsoleId consoleId)
    {
        if (!Hub.ContainsKey(consoleId))
        {
            //throw new ArgumentException("ConsoleId mismatch", consoleId.JobId);
            return Enumerable.Empty<ConsoleLine>();
        }
        return Hub[consoleId];
    }

    public void Flush(ConsoleId consoleId)
    {
        if (!Hub.ContainsKey(consoleId))
        {
            throw new ArgumentException("ConsoleId mismatch", consoleId.JobId);
        }

        Hub.TryRemove(consoleId, out _);
    }
}
