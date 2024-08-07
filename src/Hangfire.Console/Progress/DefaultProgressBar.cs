﻿using System;
using System.Threading;
using Hangfire.Console.Serialization;
using Hangfire.Console.Server;

namespace Hangfire.Console.Progress;

/// <summary>
///     Default progress bar.
/// </summary>
internal class DefaultProgressBar : IProgressBar
{
    private readonly ConsoleContext _context;

    private readonly string _progressBarId;

    private readonly int _digits;

    private string? _color;

    private string? _name;

    private double _value;

    internal DefaultProgressBar(ConsoleContext context, string progressBarId, string? name, string? color, int digits)
    {
        if (string.IsNullOrEmpty(progressBarId))
        {
            throw new ArgumentNullException(nameof(progressBarId));
        }

        _context = context ?? throw new ArgumentNullException(nameof(context));
        _progressBarId = progressBarId;
        _digits = digits;
        _name = string.IsNullOrEmpty(name) ? null : name;
        _color = string.IsNullOrEmpty(color) ? null : color;
        _value = -1;
    }

    public void SetValue(int value)
    {
        SetValue((double)value);
    }

    public void SetValue(double value)
    {
        value = Math.Round(value, _digits);

        if (value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Value should be in range 0..100");
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Interlocked.Exchange(ref _value, value) == value)
        {
            return;
        }

        _context.AddLine(new ConsoleLine { Message = _progressBarId, ProgressName = _name, ProgressValue = value, TextColor = _color });

        _name = null; // write name only once
        _color = null; // write color only once
    }
}
