using Hangfire.Console.Serialization;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Xunit;

namespace Hangfire.Console.Server.Tests;

public class ConsoleHubTests
{
    [Fact]
    public void Init_ShouldAddConsoleIdToHub()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId1", DateTime.UtcNow);

        // Act
        var consoleHub = new ConsoleHub();
        consoleHub.Init(consoleId);

        // Assert
        Assert.True(ConsoleHub.Hub.ContainsKey(consoleId));
    }

    [Fact]
    public void Init_ShouldThrowArgumentException_WhenConsoleIdExist()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId2", DateTime.UtcNow);

        // Act
        var consoleHub = new ConsoleHub();
        consoleHub.Init(consoleId);

        // Assert
        Assert.Throws<ArgumentException>(() => consoleHub.Init(consoleId));
    }

    [Fact]
    public void AddLine_ShouldAddConsoleLineToHub()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId3", DateTime.UtcNow);
        var consoleLine = new ConsoleLine { Message = "test" };

        // Act
        var consoleHub = new ConsoleHub();
        consoleHub.Init(consoleId);
        consoleHub.AddLine(consoleId, consoleLine);

        // Assert
        Assert.Contains(consoleLine, ConsoleHub.Hub[consoleId]);
    }

    [Fact]
    public void AddLine_ShouldThrowArgumentException_WhenConsoleIdMismatch()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId4", DateTime.UtcNow);
        var consoleLine = new ConsoleLine { Message = "test" };

        // Act
        var consoleHub = new ConsoleHub();

        // Assert
        Assert.Throws<ArgumentException>(() => consoleHub.AddLine(consoleId, consoleLine));
    }

    [Fact]
    public void GetLines_ShouldReturnConsoleLinesFromHub()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId5", DateTime.UtcNow);
        var consoleLine1 = new ConsoleLine { Message = "line1" };
        var consoleLine2 = new ConsoleLine { Message = "line2" };

        // Act
        var consoleHub = new ConsoleHub();
        consoleHub.Init(consoleId);
        consoleHub.AddLine(consoleId, consoleLine1);
        consoleHub.AddLine(consoleId, consoleLine2);
        var lines = consoleHub.GetLines(consoleId);

        // Assert
        Assert.Equal(2, lines.Count());
        Assert.Contains(consoleLine1, lines);
        Assert.Contains(consoleLine2, lines);
    }

    [Fact]
    public void GetLines_ShouldReturnEmpty_WhenConsoleIdMismatch()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId6", DateTime.UtcNow);

        // Act
        var consoleHub = new ConsoleHub();
        var lines = consoleHub.GetLines(consoleId);

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public void Flush_ShouldRemoveConsoleIdFromHub()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId7", DateTime.UtcNow);

        // Act
        var consoleHub = new ConsoleHub();
        consoleHub.Init(consoleId);
        consoleHub.Flush(consoleId);

        // Assert
        Assert.False(ConsoleHub.Hub.ContainsKey(consoleId));
    }

    [Fact]
    public void Flush_ShouldThrowArgumentException_WhenConsoleIdMismatch()
    {
        // Arrange
        var consoleId = new ConsoleId("jobId8", DateTime.UtcNow);

        // Act
        var consoleHub = new ConsoleHub();

        // Assert
        Assert.Throws<ArgumentException>(() => consoleHub.Flush(consoleId));
    }
}
