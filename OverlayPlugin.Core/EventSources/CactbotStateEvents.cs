using System.Collections.Generic;

namespace RainbowMage.OverlayPlugin.EventSources
{
    // Cactbot asks for these once at page load; an overlay that connects later must get them on subscribe.
    public static class CactbotStateEvents
    {
        public static readonly List<string> Names = new()
        {
            "onGameExistsEvent",
            "onGameActiveChangedEvent",
            "onInCombatChangedEvent",
            "onZoneChangedEvent",
            "onPlayerChangedEvent",
        };

        public static bool IsState(string eventName) => Names.Contains(eventName);
    }
}
