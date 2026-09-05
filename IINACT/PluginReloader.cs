using System.Collections;
using System.Reflection;
using Dalamud.Plugin;

namespace IINACT;

// Dalamud has no public reload API; this walks the same internals ECommons relies on.
internal static class PluginReloader
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static Task Reload(IDalamudPluginInterface pluginInterface, string internalName)
    {
        var dalamud = pluginInterface.GetType().Assembly;
        var managerType = dalamud.GetType("Dalamud.Plugin.Internal.PluginManager", throwOnError: true)!;
        var serviceType = dalamud.GetType("Dalamud.Service`1", throwOnError: true)!.MakeGenericType(managerType);
        var manager = serviceType.GetMethod("Get", Any, Type.EmptyTypes)?.Invoke(null, null)
                      ?? throw new InvalidOperationException("Dalamud's plugin manager is not available");
        var plugins = managerType.GetProperty("InstalledPlugins", Any)?.GetValue(manager) as IEnumerable
                      ?? throw new InvalidOperationException("Dalamud's plugin list is not readable");
        foreach (var plugin in plugins)
        {
            var type = plugin.GetType();
            if (type.GetProperty("InternalName", Any)?.GetValue(plugin) as string != internalName)
                continue;
            var reload = type.GetMethod("ReloadAsync", Any, Type.EmptyTypes)
                         ?? throw new InvalidOperationException("this Dalamud build has no ReloadAsync");
            return (Task)reload.Invoke(plugin, null)!;
        }
        throw new InvalidOperationException($"{internalName} is not in Dalamud's plugin list");
    }
}
