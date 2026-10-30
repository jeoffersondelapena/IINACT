using System.IO;

namespace Advanced_Combat_Tracker;

// FileMode.Append keeps a position per handle, so two windows on one file overwrite each other's lines.
public static class LogFileNaming
{
    public static string PerWindow(string path, int processId)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        return Path.Combine(directory, $"{name}_{processId}{Path.GetExtension(path)}");
    }
}
