namespace IINACT;

public static class VersionFile
{
    public const string Unknown = "0000.00.00.0000.0000";

    // Wine answered a read of ffxivgame.ver with "Invalid handle" during a reload on 2026-11-03 and read it
    // fine a moment later, so a few retries stand between a reload and a failed constructor.
    public static string Read(Func<string> read, int attempts = 5, Action<int>? wait = null)
    {
        wait ??= Thread.Sleep;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return read();
            }
            catch (IOException) when (attempt < attempts)
            {
                wait(100 * attempt);
            }
            catch (IOException)
            {
                return Unknown;
            }
        }
    }
}
