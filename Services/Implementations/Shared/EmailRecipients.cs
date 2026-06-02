namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// Normalizes email recipient inputs into clean, individual, validated addresses.
/// Inputs may arrive as a single element containing several addresses joined by commas,
/// semicolons or newlines (workflow pools use commas, scheduled-report recipients are
/// entered line-by-line). Passing such a joined element to the notifications-api as one
/// recipient causes an SMTP "501 invalid address" and the whole message is dropped.
/// This splits on comma/semicolon/newline, trims, drops blanks, validates each address,
/// and dedupes case-insensitively while preserving order.
/// </summary>
public static class EmailRecipients
{
    private static readonly char[] Separators = { ',', ';', '\n', '\r' };

    public static List<string> Normalize(IEnumerable<string>? input)
    {
        if (input is null) return new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in input)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var part in raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IsValid(part)) continue;
                if (seen.Add(part)) result.Add(part);
            }
        }
        return result;
    }

    private static bool IsValid(string addr)
    {
        try
        {
            var parsed = new global::System.Net.Mail.MailAddress(addr);
            return string.Equals(parsed.Address, addr, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
