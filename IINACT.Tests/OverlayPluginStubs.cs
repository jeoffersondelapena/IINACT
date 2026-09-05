// The dispatcher only needs a logger from the container and message strings from Resources; these
// stand in for the real ones so it can be compiled into the tests on a Mac.
namespace RainbowMage.OverlayPlugin
{
    internal class TinyIoCContainer
    {
        public T Resolve<T>() => (T)(object)new SilentLogger();
    }

    internal class SilentLogger : ILogger
    {
        public void Log(LogLevel level, string message) { }
        public void Log(LogLevel level, string format, params object[] args) { }
        public void RegisterListener(Action<LogEntry> listener) { }
        public void ClearListener() { }
    }

    internal static class Resources
    {
        public static string DuplicateHandlerError => "duplicate handler {0}";
        public static string EventHandlerException => "handler exception {0} {1} {2}";
        public static string JsHandlerCallException => "js handler exception {0}";
        public static string MissingEventDispatchError => "missing event {0}";
        public static string MissingEventSubError => "missing event {0}";
        public static string MissingHandlerError => "missing handler {0}";
        public static string OverlayApiInvalidHandlerCall => "invalid call {0}";
        public static string OverlayApiMissingEventsField => "missing events {0}";
        public static string OverlayApiMissingEventsFieldUnsub => "missing events {0}";
        public static string OverlayApiSubscribed => "subscribed {0} {1}";
    }
}
