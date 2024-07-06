using Hangfire.Console.Serialization;
using System.Collections.Generic;

namespace Hangfire.Console.Server;

internal interface IConsoleHub
{
    void AddLine(ConsoleId consoleId, ConsoleLine consoleLine);
    void Flush(ConsoleId consoleId);
    IEnumerable<ConsoleLine> GetLines(ConsoleId consoleId);
    void Init(ConsoleId consoleId);
}