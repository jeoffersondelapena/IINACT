using IINACT;
using Xunit;

public class ParserWatchdogTests
{
    private static WatchdogSample Healthy() => new(
        PendingRefreshes: 1, InCombat: false, SecondsInCombat: 0,
        SecondsSinceNetworkLine: 5, SecondsSinceStart: 600);

    [Fact]
    public void A_healthy_parser_is_left_alone()
    {
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(Healthy()));
    }

    [Fact]
    public void Nothing_is_judged_during_startup()
    {
        var s = Healthy() with { PendingRefreshes = 10_000, SecondsSinceStart = 30 };
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void A_refresh_backlog_means_the_scan_thread_died()
    {
        var s = Healthy() with { PendingRefreshes = ParserWatchdog.StuckRefreshThreshold };
        Assert.Equal(StallKind.ScanThreadStuck, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void A_small_backlog_is_normal()
    {
        var s = Healthy() with { PendingRefreshes = ParserWatchdog.StuckRefreshThreshold - 1 };
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void Combat_with_no_network_lines_for_long_enough_is_a_stall()
    {
        var s = Healthy() with { InCombat = true, SecondsInCombat = 45, SecondsSinceNetworkLine = 40 };
        Assert.Equal(StallKind.NoCombatData, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void A_parser_that_never_wrote_a_line_counts_as_silent()
    {
        var s = Healthy() with { InCombat = true, SecondsInCombat = 45, SecondsSinceNetworkLine = double.PositiveInfinity };
        Assert.Equal(StallKind.NoCombatData, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void Fresh_combat_is_given_time()
    {
        var s = Healthy() with { InCombat = true, SecondsInCombat = 10, SecondsSinceNetworkLine = 40 };
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void Combat_with_recent_lines_is_healthy()
    {
        var s = Healthy() with { InCombat = true, SecondsInCombat = 300, SecondsSinceNetworkLine = 2 };
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void Silence_out_of_combat_means_nothing()
    {
        var s = Healthy() with { InCombat = false, SecondsSinceNetworkLine = 3600 };
        Assert.Equal(StallKind.None, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void A_dead_scan_thread_is_named_first()
    {
        var s = Healthy() with { PendingRefreshes = 5000, InCombat = true, SecondsInCombat = 60, SecondsSinceNetworkLine = 60 };
        Assert.Equal(StallKind.ScanThreadStuck, ParserWatchdog.Evaluate(s));
    }

    [Fact]
    public void The_description_names_the_phase()
    {
        Assert.Contains("combatant rescan", ParserWatchdog.Describe(StallKind.ScanThreadStuck, "combatant rescan (zone changed)"));
        Assert.Equal("in combat with no combat data", ParserWatchdog.Describe(StallKind.NoCombatData, "x"));
    }
}

public class LogLineTypesTests
{
    [Theory]
    [InlineData(21, true)]
    [InlineData(22, true)]
    [InlineData(39, true)]
    [InlineData(42, true)]
    [InlineData(20, true)]
    [InlineData(40, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(261, false)]
    public void Only_packet_derived_types_prove_the_network_path(int type, bool expected)
    {
        Assert.Equal(expected, LogLineTypes.IsNetworkType(type));
    }

    [Fact]
    public void The_type_is_read_from_the_first_field()
    {
        Assert.Equal(21, LogLineTypes.TypeOf("21|2026-09-05T10:15:14.894+08:00|10001234|Name|..."));
        Assert.Equal(261, LogLineTypes.TypeOf("261|2026-09-05T10:02:18.181+08:00|Change|..."));
        Assert.Equal(0, LogLineTypes.TypeOf("00|2026-09-05T10:05:01.0+08:00|0029||text|hash"));
    }

    [Fact]
    public void An_act_style_time_prefix_is_skipped()
    {
        Assert.Equal(21, LogLineTypes.TypeOf("[10:15:14.894] 21|2026-09-05T10:15:14.894+08:00|..."));
    }

    [Fact]
    public void Malformed_lines_have_no_type()
    {
        Assert.Null(LogLineTypes.TypeOf(""));
        Assert.Null(LogLineTypes.TypeOf("not a line"));
        Assert.Null(LogLineTypes.TypeOf("[broken"));
        Assert.Null(LogLineTypes.TypeOf("1234|too long"));
    }
}
