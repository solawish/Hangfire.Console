using Hangfire.Common;
using Hangfire.Console.Constants;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hangfire.Console.Storage;

internal class ConsoleHubStorage : IConsoleStorage
{
    private const int ValueFieldLimit = 256;

    // maybe this one be released every time?
    private readonly JobStorageConnection _connection;

    private readonly IConsoleHub _consoleHub;

    public ConsoleHubStorage(IStorageConnection connection, IConsoleHub consoleHub)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (consoleHub is null)
        {
            throw new ArgumentNullException(nameof(consoleHub));
        }

        if (connection is not JobStorageConnection jobStorageConnection)
        {
            throw new NotSupportedException("Storage connections must implement JobStorageConnection");
        }

        _connection = jobStorageConnection;
        _consoleHub = consoleHub;
    }

    public void AddLine(ConsoleId consoleId, ConsoleLine line)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        if (line.IsReference)
        {
            throw new ArgumentException("Cannot add reference directly", nameof(line));
        }
        _consoleHub.AddLine(consoleId, line);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public void Expire(ConsoleId consoleId, TimeSpan expireIn)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        using var tran = (JobStorageTransaction)_connection.CreateWriteTransaction();
        using var expiration = new ConsoleExpirationTransaction(tran);

        expiration.Expire(consoleId, expireIn);

        tran.Commit();
    }

    public void Flush(ConsoleId consoleId)
    {
        var lines = _consoleHub.GetLines(consoleId);
        using var transaction = _connection.CreateWriteTransaction();

        if (transaction is not JobStorageTransaction)
        {
            throw new NotSupportedException("Storage tranactions must implement JobStorageTransaction");
        }

        transaction.SetRangeInHash(consoleId.GetHashKey(), new[] { new KeyValuePair<string, string>("jobId", consoleId.JobId) });

        foreach (var line in lines)
        {
            string? value;

            if (line.Message.Length > ValueFieldLimit - 36)
            {
                // pretty sure it won't fit
                // (36 is an upper bound for JSON formatting, TimeOffset and TextColor)
                value = null;
            }
            else
            {
                // try to encode and see if it fits
                value = SerializationHelper.Serialize(line);

                if (value.Length > ValueFieldLimit)
                {
                    value = null;
                }
            }

            if (value is null)
            {
                var referenceKey = Guid.NewGuid().ToString("N");

                transaction.SetRangeInHash(consoleId.GetHashKey(), new[] { new KeyValuePair<string, string>(referenceKey, line.Message) });

                line.Message = referenceKey;
                line.IsReference = true;

                value = SerializationHelper.Serialize(line);
            }

            transaction.AddToSet(consoleId.GetSetKey(), value, line.TimeOffset);

            if (line.ProgressValue.HasValue && line.Message == "1")
            {
                var progress = line.ProgressValue.Value.ToString(CultureInfo.InvariantCulture);

                transaction.SetRangeInHash(consoleId.GetHashKey(), new[] { new KeyValuePair<string, string>("progress", progress) });
            }
        }

        transaction.Commit();
        _consoleHub.Flush(consoleId);
    }

    public TimeSpan GetConsoleTtl(ConsoleId consoleId)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        return _connection.GetHashTtl(consoleId.GetHashKey());
    }

    public int GetLineCount(ConsoleId consoleId)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        var result = _consoleHub.GetLines(consoleId).Count();

        if (result == 0)
        {
            // maybe already flush           
            result = (int)_connection.GetSetCount(consoleId.GetSetKey());
        }

        if (result == 0)
        {
            // Read operations should be backwards compatible and use
            // old keys, if new one don't contain any data.
            result = (int)_connection.GetSetCount(consoleId.GetOldConsoleKey());
        }

        return result;
    }

    public IEnumerable<ConsoleLine> GetLines(ConsoleId consoleId, int start, int end)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        var useOldKeys = false;

        var items = _consoleHub.GetLines(consoleId).Skip(start).Take(end - start + 1).ToList();

        if (items.Count == 0)
        {
            items = _connection.GetRangeFromSet(consoleId.GetSetKey(), start, end)
                ?.Select(x => SerializationHelper.Deserialize<ConsoleLine>(x))
                .ToList();            
        }

        if (items is null || items.Count == 0)
        {
            // Read operations should be backwards compatible and use
            // old keys, if new one don't contain any data.
            items = _connection.GetRangeFromSet(consoleId.GetOldConsoleKey(), start, end)
                .Select(x => SerializationHelper.Deserialize<ConsoleLine>(x))
                .ToList();
            useOldKeys = true;
        }

        foreach (var line in items)
        {
            if (line.IsReference)
            {
                if (useOldKeys)
                {
                    try
                    {
                        line.Message = _connection.GetValueFromHash(consoleId.GetOldConsoleKey(), line.Message);
                    }
                    catch
                    {
                        // This may happen, when using Hangfire.Redis storage and having
                        // background job, whose console session was stored using old key
                        // format.
                    }
                }
                else
                {
                    line.Message = _connection.GetValueFromHash(consoleId.GetHashKey(), line.Message);
                }

                line.IsReference = false;
            }

            yield return line;
        }
    }

    public double? GetProgress(ConsoleId consoleId)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        var progress = _consoleHub.GetLines(consoleId)
            .FirstOrDefault(x => x.ProgressValue.HasValue && x.Message == "1")
            ?.ProgressValue.Value.ToString(CultureInfo.InvariantCulture);

        progress ??= _connection.GetValueFromHash(consoleId.GetHashKey(), "progress");

        if (string.IsNullOrEmpty(progress))
        {
            // progress value is not set
            return null;
        }

        try
        {
            return double.Parse(progress, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            // corrupted data?
            return null;
        }
    }

    public StateData? GetState(ConsoleId consoleId)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }

        return _connection.GetStateData(consoleId.JobId);
    }

    public void InitConsole(ConsoleId consoleId)
    {
        if (consoleId is null)
        {
            throw new ArgumentNullException(nameof(consoleId));
        }
        
        _consoleHub.Init(consoleId);

        // Add ip 
        using var transaction = (JobStorageTransaction)_connection.CreateWriteTransaction();
        transaction.SetRangeInHash(consoleId.GetHashKey(), new[] { new KeyValuePair<string, string>(HashKey.JobServerIp, IpExtensions.GetIp()) });
        transaction.Commit();
    }
}
