using System;

namespace RainbowMage.OverlayPlugin.WebSocket
{
    public static class WsPort
    {
        // Two game windows share one config file; the port that matters is the one this window's overlays dial.
        public static int Choose(int? fromOverlays, int configured) => fromOverlays is > 0 ? fromOverlays.Value : configured;

        public static bool NeedsRebind(int? bound, int? fromOverlays) => fromOverlays is > 0 && bound != fromOverlays;

        public static string Describe(int? fromOverlays, int configured) =>
            fromOverlays is > 0 ? $"port {fromOverlays} from this window's overlays" : $"port {configured} from the config";
    }

    public sealed class WsPortSource
    {
        private readonly Func<int?> resolve;

        public WsPortSource(Func<int?> resolve)
        {
            this.resolve = resolve;
        }

        public int? Resolve()
        {
            try { return resolve(); }
            catch (Exception) { return null; }
        }
    }
}
