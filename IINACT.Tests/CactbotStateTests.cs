using Newtonsoft.Json.Linq;
using RainbowMage.OverlayPlugin;
using RainbowMage.OverlayPlugin.EventSources;
using Xunit;

public class CactbotStateTests
{
    private sealed class Receiver : IEventReceiver
    {
        public string Name => "test overlay";
        public List<JObject> Got { get; } = new();
        public void HandleEvent(JObject e) => Got.Add(e);
    }

    private static JObject Event(string type, string zone) =>
        new() { ["type"] = type, ["detail"] = new JObject { ["zoneName"] = zone } };

    [Fact]
    public void An_overlay_that_subscribes_late_gets_the_last_state_at_once()
    {
        var dispatcher = new EventDispatcher(new TinyIoCContainer());
        JObject? cached = null;
        dispatcher.RegisterEventType("onZoneChangedEvent", () => cached);

        cached = Event("onZoneChangedEvent", "Deltascape V3.0");
        dispatcher.DispatchEvent(cached);

        var late = new Receiver();
        dispatcher.Subscribe("onZoneChangedEvent", late);

        var got = Assert.Single(late.Got);
        Assert.Equal("Deltascape V3.0", got["detail"]!["zoneName"]);
    }

    [Fact]
    public void A_plain_event_is_not_replayed_to_a_late_subscriber()
    {
        var dispatcher = new EventDispatcher(new TinyIoCContainer());
        dispatcher.RegisterEventType("onLogEvent");
        dispatcher.DispatchEvent(Event("onLogEvent", "x"));

        var late = new Receiver();
        dispatcher.Subscribe("onLogEvent", late);

        Assert.Empty(late.Got);
    }

    [Fact]
    public void Nothing_is_replayed_before_the_first_state_event()
    {
        var dispatcher = new EventDispatcher(new TinyIoCContainer());
        JObject? cached = null;
        dispatcher.RegisterEventType("onZoneChangedEvent", () => cached);

        var early = new Receiver();
        dispatcher.Subscribe("onZoneChangedEvent", early);

        Assert.Empty(early.Got);
    }

    [Fact]
    public void The_state_events_are_the_ones_cactbot_requests_once()
    {
        Assert.Equal(new[] { "onGameExistsEvent", "onGameActiveChangedEvent", "onInCombatChangedEvent", "onZoneChangedEvent", "onPlayerChangedEvent" },
                     CactbotStateEvents.Names);
        Assert.True(CactbotStateEvents.IsState("onZoneChangedEvent"));
        Assert.False(CactbotStateEvents.IsState("onLogEvent"));
    }
}
