using Newtonsoft.Json.Linq;

namespace RainbowMage.OverlayPlugin.EventSources
{
    // The parser announces the zone and player as log lines the moment it starts; an event source
    // attached after that (a plugin restart mid-session) must rebuild them from the repository.
    public static class StartupReplay
    {
        public static JObject ChangeZone(uint? territoryId, string zoneName)
        {
            if (territoryId is null or 0)
                return null;
            return JObject.FromObject(new { type = "ChangeZone", zoneID = territoryId.Value, zoneName = zoneName ?? "" });
        }

        public static JObject ChangePrimaryPlayer(uint charId, string charName)
        {
            if (charId == 0 || string.IsNullOrEmpty(charName))
                return null;
            return JObject.FromObject(new { type = "ChangePrimaryPlayer", charID = charId, charName });
        }
    }
}
