using IINACT;
using Xunit;

public class DiagLogTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 20, 0, 0);

    [Fact]
    public void File_names_carry_the_start_time_and_pid()
    {
        Assert.Equal("iinact-20260905-195211-364.log", DiagLog.FileName(new DateTime(2026, 9, 5, 19, 52, 11), 364));
    }

    [Fact]
    public void Only_files_past_the_retention_window_are_pruned()
    {
        var files = new[]
        {
            "/d/iinact-20260905-195211-364.log",
            "/d/iinact-20260829-090000-12.log",
            "/d/iinact-20260828-090000-12.log",
            "/d/notes.log",
        };
        Assert.Equal(new[] { "/d/iinact-20260828-090000-12.log" }, DiagLog.FilesToPrune(files, Now));
    }

    [Fact]
    public void Writes_land_in_the_file_and_the_directory_is_created()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iinact-diag-test-" + Guid.NewGuid());
        using (var log = new DiagLog(dir, Now, 42))
        {
            log.Write("hello");
            Assert.Equal(Path.Combine(dir, "iinact-20260905-200000-42.log"), log.Path);
        }
        Assert.Contains("] hello", File.ReadAllText(Path.Combine(dir, "iinact-20260905-200000-42.log")));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Memory_types_are_the_scan_thread_lines()
    {
        Assert.True(LogLineTypes.IsMemoryType(1));
        Assert.True(LogLineTypes.IsMemoryType(4));
        Assert.True(LogLineTypes.IsMemoryType(40));
        Assert.False(LogLineTypes.IsMemoryType(0));
        Assert.False(LogLineTypes.IsMemoryType(21));
    }
}

public class RepeatThrottleTests
{
    private static readonly DateTime T0 = new(2026, 9, 5, 20, 0, 0);

    [Fact]
    public void The_first_occurrence_passes_and_repeats_are_held()
    {
        var throttle = new RepeatThrottle(TimeSpan.FromSeconds(60));
        Assert.Equal("read failed", throttle.Admit("read failed", T0));
        Assert.Null(throttle.Admit("read failed", T0.AddSeconds(1)));
        Assert.Null(throttle.Admit("read failed", T0.AddSeconds(59)));
    }

    [Fact]
    public void After_the_window_the_message_returns_with_its_count()
    {
        var throttle = new RepeatThrottle(TimeSpan.FromSeconds(60));
        throttle.Admit("read failed", T0);
        throttle.Admit("read failed", T0.AddSeconds(1));
        throttle.Admit("read failed", T0.AddSeconds(2));
        Assert.Equal("read failed (repeated 2x since the last line)", throttle.Admit("read failed", T0.AddSeconds(61)));
        Assert.Null(throttle.Admit("read failed", T0.AddSeconds(62)));
    }

    [Fact]
    public void Different_messages_are_independent()
    {
        var throttle = new RepeatThrottle(TimeSpan.FromSeconds(60));
        Assert.NotNull(throttle.Admit("a", T0));
        Assert.NotNull(throttle.Admit("b", T0));
    }
}
