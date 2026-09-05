namespace IINACT;

public static class LogLineTypes
{
    // FFXIV_ACT_Plugin numbers its packet-derived lines 20 and up; 40 (map) comes from memory.
    public static bool IsNetworkType(int type) => type is >= 20 and <= 42 && type != 40;

    /// <summary>The numeric line type, tolerating ACT's optional "[time] " prefix; null if malformed.</summary>
    public static int? TypeOf(string line)
    {
        var start = line.StartsWith('[') ? line.IndexOf("] ", StringComparison.Ordinal) + 2 : 0;
        if (start < 0 || start >= line.Length)
            return null;
        var bar = line.IndexOf('|', start);
        return bar - start is 2 or 3 && int.TryParse(line.AsSpan(start, bar - start), out var type) ? type : null;
    }
}
