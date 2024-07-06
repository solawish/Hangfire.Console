﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire.Common;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;
using Hangfire.Console.Storage;
using Hangfire.States;
using Hangfire.Storage;
using Moq;
using Xunit;
using KVP = System.Collections.Generic.KeyValuePair<string, string>;

namespace Hangfire.Console.Tests.Storage;

public class ConsoleHubStorageFacts
{
    private readonly Mock<JobStorageConnection> _connection;

    private readonly ConsoleId _consoleId;

    private readonly Mock<JobStorageTransaction> _transaction;

    private readonly Mock<IConsoleHub> _consoleHub;

    public ConsoleHubStorageFacts()
    {
        _consoleId = new ConsoleId("1", DateTime.UtcNow);

        _connection = new Mock<JobStorageConnection>();
        _transaction = new Mock<JobStorageTransaction>();
        _consoleHub = new Mock<IConsoleHub>();

        _connection.Setup(x => x.CreateWriteTransaction())
            .Returns(_transaction.Object);
    }

    [Fact]
    public void Ctor_ThrowsException_IfConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>("connection", () => new ConsoleHubStorage(null!, _consoleHub.Object));
    }

    [Fact]
    public void Ctor_ThrowsException_IfNotImplementsJobStorageConnection()
    {
        var dummyConnection = new Mock<IStorageConnection>();

        Assert.Throws<NotSupportedException>(() => new ConsoleHubStorage(dummyConnection.Object, _consoleHub.Object));
    }

    [Fact]
    public void Ctor_ThrowsException_IfConsoleHubIsNull()
    {
        Assert.Throws<ArgumentNullException>("consoleHub", () => new ConsoleHubStorage(_connection.Object, null!));
    }

    [Fact]
    public void Dispose_ReallyDisposesConnection()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        storage.Dispose();

        _connection.Verify(x => x.Dispose());
    }

    [Fact]
    public void InitConsole_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.InitConsole(null!));
    }

    [Fact]
    public void InitConsole_AddedToConsoleHub()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        storage.InitConsole(_consoleId);

        _consoleHub.Verify(x => x.Init(_consoleId), Times.Once);
    }

    [Fact]
    public void AddLine_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.AddLine(null!, new ConsoleLine{ Message = "" }));
    }

    [Fact]
    public void AddLine_ThrowsException_IfLineIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("line", () => storage.AddLine(_consoleId, null!));
    }

    [Fact]
    public void AddLine_ThrowsException_IfLineIsReference()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentException>("line", () => storage.AddLine(_consoleId, new ConsoleLine { Message = "", IsReference = true }));
    }

    [Fact]
    public void AddLine_AddedToConsoleHub()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);
        var line = new ConsoleLine { Message = "test" };

        storage.AddLine(_consoleId, line);

        _consoleHub.Verify(x => x.AddLine(_consoleId, line), Times.Once);
    }

    [Fact]
    public void Flush_ThrowsException_IfNotImplementsJobStorageTransaction()
    {
        var dummyTransaction = new Mock<IWriteOnlyTransaction>();
        _connection.Setup(x => x.CreateWriteTransaction())
            .Returns(dummyTransaction.Object);

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<NotSupportedException>(() => storage.Flush(_consoleId));
    }

    [Fact]
    public void Flush_JobIdIsAddedToHash()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(Array.Empty<ConsoleLine>());

        storage.Flush(_consoleId);

        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == "jobId")));
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void Flush_ShortLineIsAddedToSet()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);
        var line = new ConsoleLine { Message = "test" };
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(new[] { line });

        storage.Flush(_consoleId);

        Assert.False(line.IsReference);
        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == "jobId")));
        _transaction.Verify(x => x.AddToSet(_consoleId.GetSetKey(), It.IsAny<string>(), It.IsAny<double>()));
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == line.Message)), Times.Never);
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void Flush_LongLineIsAddedToHash_AndReferenceIsAddedToSet()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);
        var line = new ConsoleLine
        {
            Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor " +
                      "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
                      "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure " +
                      "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. " +
                      "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum."
        };
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(new[] { line });

        storage.Flush(_consoleId);

        Assert.True(line.IsReference);
        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == "jobId")));
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == line.Message)));
        _transaction.Verify(x => x.AddToSet(_consoleId.GetSetKey(), It.IsAny<string>(), It.IsAny<double>()));
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void Flush_ProgressBarIsAddedToSet_AndProgressIsUpdated()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);
        var line = new ConsoleLine
        {
            Message = "1",
            ProgressValue = 10
        };
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(new[] { line });

        storage.Flush(_consoleId);

        Assert.False(line.IsReference);
        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == "jobId")));
        _transaction.Verify(x => x.AddToSet(_consoleId.GetSetKey(), It.IsAny<string>(), It.IsAny<double>()));
        _transaction.Verify(x => x.SetRangeInHash(_consoleId.GetHashKey(), It2.AnyIs<KVP>(p => p.Key == "progress")));
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void Expire_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.Expire(null!, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Expire_ExpiresSetAndHash()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        storage.Expire(_consoleId, TimeSpan.FromHours(1));

        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.ExpireSet(_consoleId.GetSetKey(), It.IsAny<TimeSpan>()));
        _transaction.Verify(x => x.ExpireHash(_consoleId.GetHashKey(), It.IsAny<TimeSpan>()));
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void Expire_ExpiresOldSetAndHashKeysEither_ForBackwardsCompatibility()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        storage.Expire(_consoleId, TimeSpan.FromHours(1));

        _connection.Verify(x => x.CreateWriteTransaction(), Times.Once);
        _transaction.Verify(x => x.ExpireSet(_consoleId.GetOldConsoleKey(), It.IsAny<TimeSpan>()));
        _transaction.Verify(x => x.ExpireHash(_consoleId.GetOldConsoleKey(), It.IsAny<TimeSpan>()));
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void GetConsoleTtl_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.GetConsoleTtl(null!));
    }

    [Fact]
    public void GetConsoleTtl_ReturnsTtlOfHash()
    {
        _connection.Setup(x => x.GetHashTtl(_consoleId.GetHashKey()))
            .Returns(TimeSpan.FromSeconds(123));

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var ttl = storage.GetConsoleTtl(_consoleId);

        Assert.Equal(TimeSpan.FromSeconds(123), ttl);
    }

    [Fact]
    public void GetLineCount_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.GetLineCount(null!));
    }

    [Fact]
    public void GetLineCount_ReturnsCountOfSet()
    {
        _connection.Setup(x => x.GetSetCount(_consoleId.GetSetKey()))
            .Returns(123);

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var count = storage.GetLineCount(_consoleId);

        Assert.Equal(123, count);
    }

    [Fact]
    public void GetLineCount_ReturnsCountOfSetFromConsoleHub()
    {
        _consoleHub.Setup(x => x.GetLines(_consoleId))
            .Returns(new[] { new ConsoleLine { Message = "test" } });

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var count = storage.GetLineCount(_consoleId);

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetLineCount_ReturnsCountOfOldSet_WhenNewOneReturnsZero_ForBackwardsCompatibility()
    {
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(Enumerable.Empty<ConsoleLine>());
        _connection.Setup(x => x.GetSetCount(_consoleId.GetSetKey())).Returns(0);
        _connection.Setup(x => x.GetSetCount(_consoleId.GetOldConsoleKey())).Returns(123);

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var count = storage.GetLineCount(_consoleId);

        Assert.Equal(123, count);
    }

    [Fact]
    public void GetLines_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.GetLines(null!, 0, 1).ToArray());
    }

    [Fact]
    public void GetLines_ReturnsRangeFromConnectionSet()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1" },
            new ConsoleLine { TimeOffset = 1, Message = "line2" },
            new ConsoleLine { TimeOffset = 2, Message = "line3" },
            new ConsoleLine { TimeOffset = 3, Message = "line4" }
        };

        _connection.Setup(x => x.GetRangeFromSet(_consoleId.GetSetKey(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string _, int start, int end) => lines.Where((_, i) => i >= start && i <= end).Select(SerializationHelper.Serialize).ToList());

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 1, 2).ToArray();

        Assert.Equal(lines.Skip(1).Take(2).Select(x => x.Message), result.Select(x => x.Message));
    }

    [Fact]
    public void GetLines_ReturnsRangeFromConcolseHub()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1" },
            new ConsoleLine { TimeOffset = 1, Message = "line2" },
            new ConsoleLine { TimeOffset = 2, Message = "line3" },
            new ConsoleLine { TimeOffset = 3, Message = "line4" }
        };

        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(lines);

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 1, 2).ToArray();

        Assert.Equal(lines.Skip(1).Take(2).Select(x => x.Message), result.Select(x => x.Message));
    }

    [Fact]
    public void GetLines_ReturnsRangeFromOldSet_ForBackwardsCompatibility()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1" },
            new ConsoleLine { TimeOffset = 1, Message = "line2" },
            new ConsoleLine { TimeOffset = 2, Message = "line3" },
            new ConsoleLine { TimeOffset = 3, Message = "line4" }
        };

        _connection.Setup(x => x.GetRangeFromSet(_consoleId.GetOldConsoleKey(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string _, int start, int end) => lines.Where((_, i) => i >= start && i <= end).Select(SerializationHelper.Serialize).ToList());

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 1, 2).ToArray();

        Assert.Equal(lines.Skip(1).Take(2).Select(x => x.Message), result.Select(x => x.Message));
    }

    [Fact]
    public void GetLines_ExpandsReferences()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1", IsReference = true }
        };

        _connection.Setup(x => x.GetRangeFromSet(_consoleId.GetSetKey(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string _, int start, int end) => lines.Where((_, i) => i >= start && i <= end).Select(SerializationHelper.Serialize).ToList());
        _connection.Setup(x => x.GetValueFromHash(_consoleId.GetHashKey(), It.IsAny<string>()))
            .Returns("Dereferenced Line");

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 0, 1).Single();

        Assert.False(result.IsReference);
        Assert.Equal("Dereferenced Line", result.Message);
    }

    [Fact]
    public void GetLines_ExpandsReferencesFromOldHash_ForBackwardsCompatibility()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1", IsReference = true }
        };

        _connection.Setup(x => x.GetRangeFromSet(_consoleId.GetOldConsoleKey(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string _, int start, int end) => lines.Where((_, i) => i >= start && i <= end).Select(SerializationHelper.Serialize).ToList());
        _connection.Setup(x => x.GetValueFromHash(_consoleId.GetOldConsoleKey(), It.IsAny<string>()))
            .Returns("Dereferenced Line");

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 0, 1).Single();

        Assert.False(result.IsReference);
        Assert.Equal("Dereferenced Line", result.Message);
    }

    [Fact]
    public void GetLines_HandlesHashException_WhenTryingToExpandReferences()
    {
        var lines = new[]
        {
            new ConsoleLine { TimeOffset = 0, Message = "line1", IsReference = true }
        };

        _connection.Setup(x => x.GetRangeFromSet(_consoleId.GetOldConsoleKey(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string _, int start, int end) => lines.Where((_, i) => i >= start && i <= end).Select(SerializationHelper.Serialize).ToList());

        _connection.Setup(x => x.GetValueFromHash(_consoleId.GetOldConsoleKey(), It.IsAny<string>()))
            .Throws(new NotSupportedException());

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetLines(_consoleId, 0, 1).Single();

        Assert.False(result.IsReference);
        Assert.Equal("line1", result.Message);
    }

    [Fact]
    public void GetState_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.GetState(null!));
    }

    [Fact]
    public void GetState_ReturnsStateData()
    {
        var state = new StateData
        {
            Name = ProcessingState.StateName,
            Data = new Dictionary<string, string>()
        };

        _connection.Setup(x => x.GetStateData(It.IsAny<string>()))
            .Returns(state);

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetState(_consoleId);

        Assert.Same(state, result);
    }

    [Fact]
    public void GetProgress_ThrowsException_IfConsoleIdIsNull()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        Assert.Throws<ArgumentNullException>("consoleId", () => storage.GetProgress(null!));
    }

    [Fact]
    public void GetProgress_ReturnsNull_IfProgressNotPresent()
    {
        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Null(result);
    }

    [Fact]
    public void GetProgress_ReturnsNull_IfValueFromConnectionIsInvalid()
    {
        _connection.Setup(x => x.GetValueFromHash(It.IsAny<string>(), It.IsIn("progress")))
            .Returns("invalidValue");

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Null(result);
    }

    [Fact]
    public void GetProgress_ReturnsNull_IfValueFromConsoleHubIsInvalid()
    {
        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(new[] { new ConsoleLine { Message = "invalidValue" } });

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Null(result);
    }

    [Fact]
    public void GetProgress_ReturnsNull_IfValueFromHubAndConnectionIsEmpty()
    {
        _connection.Setup(x => x.GetValueFromHash(It.IsAny<string>(), It.IsIn("progress")))
            .Returns(string.Empty);
        _consoleHub.Setup(x => x.GetLines(_consoleId))
            .Returns(Enumerable.Empty<ConsoleLine>());

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Null(result);
    }

    [Fact]
    public void GetProgress_ReturnsProgressValueFromConsoleHub()
    {
        const double progress = 12.5;

        _consoleHub.Setup(x => x.GetLines(_consoleId)).Returns(
            new[]
            {
                new ConsoleLine { Message = "1", ProgressValue = progress }
            });

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Equal(progress, result);
    }

    [Fact]
    public void GetProgress_ReturnsProgressValueFromConnection()
    {
        const double progress = 12.5;

        _connection.Setup(x => x.GetValueFromHash(It.IsAny<string>(), It.IsIn("progress")))
            .Returns(progress.ToString(CultureInfo.InvariantCulture));

        var storage = new ConsoleHubStorage(_connection.Object, _consoleHub.Object);

        var result = storage.GetProgress(_consoleId);

        Assert.Equal(progress, result);
    }
}
