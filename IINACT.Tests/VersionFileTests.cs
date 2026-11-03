using Xunit;

namespace IINACT.Tests;

public class VersionFileTests
{
    [Fact]
    public void A_transient_read_failure_is_retried()
    {
        var calls = 0;
        var waited = new List<int>();
        var result = VersionFile.Read(() => ++calls < 3 ? throw new IOException("Invalid handle") : "2026.10.16.0000.0000", wait: waited.Add);
        Assert.Equal("2026.10.16.0000.0000", result);
        Assert.Equal(new[] { 100, 200 }, waited);
    }

    [Fact]
    public void A_file_that_never_reads_gives_the_unknown_version()
    {
        Assert.Equal(VersionFile.Unknown, VersionFile.Read(() => throw new IOException("Invalid handle"), attempts: 3, wait: _ => { }));
    }
}
