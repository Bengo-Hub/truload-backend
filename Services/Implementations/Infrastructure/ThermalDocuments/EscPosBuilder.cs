namespace TruLoad.Backend.Services.Implementations.Infrastructure.ThermalDocuments;

/// <summary>
/// Small fluent builder for composing an ESC/POS byte stream line-by-line. This is a layout
/// helper only - the actual command bytes it emits come from <see cref="EscPosCommands"/>.
/// </summary>
/// <remarks>
/// Line width defaults to 48 characters, the standard column count for 80mm thermal paper
/// printing at the normal (non-condensed) Font A size on essentially all ESC/POS-compatible
/// receipt printers. Some printers/fonts fit 42, but 48 is the more common default and any
/// content built here degrades gracefully (wraps) rather than corrupting on a narrower printer.
/// </remarks>
public sealed class EscPosBuilder
{
    private readonly List<byte> _buffer = new();
    private readonly int _width;

    public EscPosBuilder(int width = 48)
    {
        _width = width;
        Raw(EscPosCommands.Initialize);
    }

    public EscPosBuilder Raw(byte[] bytes)
    {
        _buffer.AddRange(bytes);
        return this;
    }

    public EscPosBuilder AlignLeft() => Raw(EscPosCommands.AlignLeft);
    public EscPosBuilder AlignCenter() => Raw(EscPosCommands.AlignCenter);

    public EscPosBuilder Bold(bool on) => Raw(on ? EscPosCommands.BoldOn : EscPosCommands.BoldOff);
    public EscPosBuilder DoubleSize(bool on) => Raw(on ? EscPosCommands.SizeDoubleWidthHeight : EscPosCommands.SizeNormal);

    /// <summary>Writes raw text with no trailing line feed.</summary>
    public EscPosBuilder Text(string? text)
    {
        _buffer.AddRange(EscPosCommands.EncodeText(text));
        return this;
    }

    /// <summary>Writes text followed by a line feed. Empty/omitted text yields a blank line.</summary>
    public EscPosBuilder Line(string? text = null)
    {
        Text(text);
        _buffer.Add(EscPosCommands.Lf);
        return this;
    }

    /// <summary>Full-width divider line, e.g. "------------------------------------------------".</summary>
    public EscPosBuilder Divider(char fill = '-') => Line(new string(fill, _width));

    /// <summary>
    /// Label/value pair on one line when it fits the configured width (label left, value
    /// right-aligned); otherwise the label prints on its own line with the value right-aligned
    /// on the next, so long transporter/driver names etc. never truncate silently.
    /// </summary>
    public EscPosBuilder KeyValue(string label, string? value)
    {
        label ??= string.Empty;
        value ??= string.Empty;

        if (label.Length + 1 + value.Length <= _width)
        {
            var padding = Math.Max(1, _width - label.Length - value.Length);
            Line(label + new string(' ', padding) + value);
        }
        else
        {
            Line(label);
            Line(value.Length <= _width ? value.PadLeft(_width) : value[.._width]);
        }

        return this;
    }

    /// <summary>Word-wraps free text (e.g. remarks) to the configured width across multiple lines.</summary>
    public EscPosBuilder Wrap(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return this;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (candidate.Length > _width)
            {
                if (current.Length > 0) Line(current);
                current = word.Length > _width ? word[.._width] : word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) Line(current);

        return this;
    }

    public EscPosBuilder FeedAndCut(byte feedLines = 4)
    {
        Raw(EscPosCommands.FeedLines(feedLines));
        Raw(EscPosCommands.PartialCut);
        return this;
    }

    public byte[] ToArray() => _buffer.ToArray();
}
