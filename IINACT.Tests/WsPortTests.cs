using RainbowMage.OverlayPlugin.WebSocket;
using Xunit;

public class WsPortTests
{
    [Fact]
    public void The_overlays_port_wins_over_the_shared_config()
    {
        Assert.Equal(10501, WsPort.Choose(10501, 10502));
        Assert.Contains("this window's overlays", WsPort.Describe(10501, 10502));
    }

    [Fact]
    public void Without_overlays_the_config_port_is_used()
    {
        Assert.Equal(10502, WsPort.Choose(null, 10502));
        Assert.Equal(10502, WsPort.Choose(0, 10502));
        Assert.Contains("from the config", WsPort.Describe(null, 10502));
    }

    [Fact]
    public void A_failing_source_counts_as_absent()
    {
        var source = new WsPortSource(() => throw new InvalidOperationException("ipc not ready"));
        Assert.Null(source.Resolve());
        Assert.Equal(10501, new WsPortSource(() => 10501).Resolve());
    }
}

public class WsPortRebindTests
{
    [Fact]
    public void A_server_on_the_wrong_port_rebinds_once_the_overlays_port_is_known()
    {
        Assert.True(WsPort.NeedsRebind(bound: 10501, fromOverlays: 10502));
        Assert.False(WsPort.NeedsRebind(bound: 10502, fromOverlays: 10502));
    }

    [Fact]
    public void Nothing_happens_without_an_overlays_port()
    {
        Assert.False(WsPort.NeedsRebind(bound: 10501, fromOverlays: null));
        Assert.False(WsPort.NeedsRebind(bound: 10501, fromOverlays: 0));
        Assert.False(WsPort.NeedsRebind(bound: null, fromOverlays: null));
    }
}
