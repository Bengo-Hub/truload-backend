using System.Text;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.ThermalDocuments;

/// <summary>
/// Raw ESC/POS command bytes for 80mm thermal receipt printers. These are the standard
/// Epson ESC/POS command values implemented by the large majority of thermal receipt
/// printers on the market (Epson TM series and the many compatible/clone controllers found
/// in generic 80mm USB/serial/network receipt printers). Nothing in this file is bespoke to
/// TruLoad - it is the same command set any ESC/POS integration would use, kept here as a
/// small, direct implementation rather than a third-party dependency.
/// </summary>
public static class EscPosCommands
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    /// <summary>Line feed.</summary>
    public const byte Lf = 0x0A;

    /// <summary>ESC @ - Initialize printer: clears the print buffer and resets modes to defaults.</summary>
    public static readonly byte[] Initialize = { Esc, 0x40 };

    /// <summary>ESC a 0 - Left alignment.</summary>
    public static readonly byte[] AlignLeft = { Esc, 0x61, 0x00 };

    /// <summary>ESC a 1 - Center alignment.</summary>
    public static readonly byte[] AlignCenter = { Esc, 0x61, 0x01 };

    /// <summary>ESC a 2 - Right alignment.</summary>
    public static readonly byte[] AlignRight = { Esc, 0x61, 0x02 };

    /// <summary>ESC E 1 - Emphasized (bold) text on.</summary>
    public static readonly byte[] BoldOn = { Esc, 0x45, 0x01 };

    /// <summary>ESC E 0 - Emphasized (bold) text off.</summary>
    public static readonly byte[] BoldOff = { Esc, 0x45, 0x00 };

    /// <summary>GS ! 0x00 - Normal character size (no width/height magnification).</summary>
    public static readonly byte[] SizeNormal = { Gs, 0x21, 0x00 };

    /// <summary>GS ! 0x11 - Double width + double height character size.</summary>
    public static readonly byte[] SizeDoubleWidthHeight = { Gs, 0x21, 0x11 };

    /// <summary>GS V 1 - Partial cut (leaves a small connecting tab so the strip does not fully separate).</summary>
    public static readonly byte[] PartialCut = { Gs, 0x56, 0x01 };

    /// <summary>ESC d n - Print and feed n lines. Used to advance paper clear of the cutter before cutting.</summary>
    public static byte[] FeedLines(byte n) => new[] { Esc, (byte)0x64, n };

    /// <summary>
    /// Encodes text to raw bytes for the printer. ESC/POS printers apply their configured
    /// codepage to bytes 0x80-0xFF, which varies by printer/region (CP437, PC850, etc.) and
    /// cannot be assumed here. Bytes 0x20-0x7E (printable ASCII) render identically across
    /// essentially every ESC/POS codepage, so text is sanitized down to that range - common
    /// "smart" punctuation is transliterated to a plain-ASCII equivalent first, and anything
    /// else outside the range becomes '?' rather than risking mojibake on the physical printer.
    /// </summary>
    public static byte[] EncodeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '–' or '—' => '-',           // en dash / em dash
                '‘' or '’' => '\'',           // curly single quotes
                '“' or '”' => '"',            // curly double quotes
                '…' => '.',                        // ellipsis (approximated)
                '\r' or '\n' or '\t' => ' ',             // line-layout is managed by the caller, not embedded text
                >= (char)0x20 and <= (char)0x7E => ch,   // printable ASCII passes through unchanged
                _ => '?'
            });
        }

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
