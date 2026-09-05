using RainbowMage.OverlayPlugin.EventSources;
using Xunit;

public class StartupReplayTests
{
    [Fact]
    public void A_known_zone_becomes_the_same_event_the_log_line_would_have_produced()
    {
        var ev = StartupReplay.ChangeZone(693, "Deltascape V3.0")!;
        Assert.Equal("ChangeZone", (string?)ev["type"]);
        Assert.Equal(693u, ev["zoneID"]!.ToObject<uint>());
        Assert.Equal("Deltascape V3.0", (string?)ev["zoneName"]);
    }

    [Fact]
    public void No_zone_means_nothing_to_replay()
    {
        Assert.Null(StartupReplay.ChangeZone(null, "x"));
        Assert.Null(StartupReplay.ChangeZone(0, "x"));
    }

    [Fact]
    public void A_missing_zone_name_does_not_block_the_id()
    {
        Assert.Equal("", (string?)StartupReplay.ChangeZone(693, null)!["zoneName"]);
    }

    [Fact]
    public void The_primary_player_is_replayed_only_once_known()
    {
        Assert.Null(StartupReplay.ChangePrimaryPlayer(0, "Someone"));
        Assert.Null(StartupReplay.ChangePrimaryPlayer(0x10001234, null));
        var ev = StartupReplay.ChangePrimaryPlayer(0x10001234, "Someone")!;
        Assert.Equal("ChangePrimaryPlayer", (string?)ev["type"]);
        Assert.Equal(0x10001234u, ev["charID"]!.ToObject<uint>());
        Assert.Equal("Someone", (string?)ev["charName"]);
    }
}
