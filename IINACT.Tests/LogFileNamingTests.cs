using Advanced_Combat_Tracker;
using Xunit;

public class LogFileNamingTests
{
    [Fact]
    public void The_window_id_goes_before_the_extension()
    {
        var path = Path.Combine("logs", "Network_30304_20261030.log");
        Assert.Equal(Path.Combine("logs", "Network_30304_20261030_3304.log"), LogFileNaming.PerWindow(path, 3304));
    }

    [Fact]
    public void A_bare_file_name_stays_where_it_was()
    {
        Assert.Equal("Network_30304_20261030_652.log", LogFileNaming.PerWindow("Network_30304_20261030.log", 652));
    }
}
