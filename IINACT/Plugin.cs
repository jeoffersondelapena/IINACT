using System.Diagnostics;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using IINACT.Network;
using IINACT.Windows;
using Machina.FFXIV;
using Machina.FFXIV.Headers.Opcodes;

namespace IINACT;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "IINACT";
    public Version Version { get; }

    private const string MainWindowCommandName = "/iinact";
    private const string EndEncCommandName = "/endenc";
    public readonly WindowSystem WindowSystem = new("IINACT");
    
    internal IDalamudPluginInterface PluginInterface { get; }
    internal ICommandManager CommandManager { get; }
    internal IClientState ClientState { get; }
    internal IDataManager DataManager { get; }
    internal IChatGui ChatGui { get; }
    internal IFramework Framework { get; }
    internal ICondition Condition { get; }
    internal IGameInteropProvider GameInteropProvider { get; }
    internal ISigScanner SigScanner { get; }
    internal INotificationManager NotificationManager { get; }
    public static IPluginLog Log { get; private set; } = null!;
    public static DiagLog? Diag { get; private set; }

    internal Configuration Configuration { get; }
    private TextToSpeechProvider TextToSpeechProvider { get; }
    private MainWindow MainWindow { get; }
    internal FileDialogManager FileDialogManager { get; }
    private ZoneDownHookManager ZoneDownHookManager { get; }
    private IpcProviders IpcProviders { get; }

    private FfxivActPluginWrapper FfxivActPluginWrapper { get; }
    private RainbowMage.OverlayPlugin.PluginMain OverlayPlugin { get; set; }
    private RainbowMage.OverlayPlugin.WebSocket.ServerController? WebSocketServer { get; set; }
    internal string OverlayPluginStatus => OverlayPlugin.Status;
    private PluginLogTraceListener PluginLogTraceListener { get; }
    private HttpClient HttpClient { get; }

    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private long lastWatchdogTick;
    private long lastHeartbeat;
    private double combatSince = -1;
    private bool restartRequested;
    private volatile bool startupSettled;

    public Plugin(IDalamudPluginInterface pluginInterface,
                  ICommandManager commandManager,
                  IClientState clientState,
                  IDataManager dataManager,
                  IChatGui chatGui,
                  IFramework framework,
                  ICondition condition,
                  IPluginLog pluginLog,
                  IGameInteropProvider gameInteropProvider,
                  ISigScanner sigScanner,
                  INotificationManager notificationManager)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        DataManager = dataManager;
        ClientState = clientState;
        ChatGui = chatGui;
        Framework = framework;
        Condition = condition;
        GameInteropProvider = gameInteropProvider;
        SigScanner = sigScanner;
        NotificationManager = notificationManager;
        Log = pluginLog;
        Diag = OpenDiagLog();

        OpcodeManager.Instance.SetRegion(DataManager.Language.ToString() == "ChineseSimplified"
                                             ? GameRegion.Chinese
                                             : GameRegion.Global);

        var createZoneDownHookManager = Task.Run(() 
            => new ZoneDownHookManager(NotificationManager, GameInteropProvider));
        Version = Assembly.GetExecutingAssembly().GetName().Version!;

        FileDialogManager = new FileDialogManager();

        HttpClient = new HttpClient();
        
        var fetchDeps =
            new FetchDependencies.FetchDependencies(Version, PluginInterface.AssemblyLocation.Directory!.FullName,
                                                    DataManager.Language.ToString() == "ChineseSimplified", HttpClient);
        
        fetchDeps.GetFfxivPlugin();
        
        PluginLogTraceListener = new PluginLogTraceListener();
        Trace.Listeners.Add(PluginLogTraceListener);

        Advanced_Combat_Tracker.ActGlobals.Init();
        Advanced_Combat_Tracker.ActGlobals.oFormActMain = new Advanced_Combat_Tracker.FormActMain(Log);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        this.TextToSpeechProvider = new TextToSpeechProvider(Configuration);
        Advanced_Combat_Tracker.ActGlobals.oFormActMain.LogFilePath = Configuration.LogFilePath;

        FfxivActPluginWrapper = new FfxivActPluginWrapper(Configuration, DataManager.Language, ChatGui, Framework, Condition);
        Diag?.Write($"IINACT {Version} (fork) on FFXIV_ACT_Plugin {typeof(FFXIV_ACT_Plugin.FFXIV_ACT_Plugin).Assembly.GetName().Version}, "
                    + $"game {ZoneDownHookManager.GetRunningGameVersion()}, pid {Environment.ProcessId}, territory {ClientState.TerritoryType}");
        Task.Run(() => NetworkLogCleanup.Cleanup(Configuration));
        OverlayPlugin = InitOverlayPlugin();

        IpcProviders = new IpcProviders(PluginInterface)
        {
            // Not healthy until the start-up follow-ups ran, so a caller waiting on it prints last.
            Healthy = () => startupSettled && ParserWatchdog.Evaluate(Sample()) == StallKind.None,
            Status = () => FfxivActPluginWrapper.Summary(),
            Restart = reason =>
            {
                if (restartRequested)
                    return false;
                RestartParser(reason);
                return true;
            },
        };

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(MainWindowCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Displays the IINACT main window; also: status, dump, restart, autorestart on|off, ws start|stop, log start|stop"
        });

        CommandManager.AddHandler(EndEncCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Ends the current encounter IINACT is parsing"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        
        if (clientState.IsPvP)
            EnterPvP();
        else
            LeavePvP();
        
        ClientState.EnterPvP += EnterPvP;
        ClientState.LeavePvP += LeavePvP;

        ZoneDownHookManager = createZoneDownHookManager.Result;

        Framework.Update += WatchdogTick;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        Condition.ConditionChange += OnConditionChange;
        AnnounceRestart();
    }

    private DiagLog? OpenDiagLog()
    {
        try
        {
            DateTime started;
            try { started = Process.GetCurrentProcess().StartTime; }
            catch (Exception) { started = DateTime.Now; }
            return new DiagLog(Path.Combine(PluginInterface.ConfigDirectory.FullName, "diag"), started, Environment.ProcessId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "diagnostic log unavailable");
            return null;
        }
    }

    private void OnTerritoryChanged(uint territory)
    {
        Diag?.Write($"territory {territory}; {FfxivActPluginWrapper.Summary()}");
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat)
            Diag?.Write($"combat {(value ? "started" : "ended")}; {FfxivActPluginWrapper.Summary()}");
    }

    public void Dispose()
    {
        Framework.Update -= WatchdogTick;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Condition.ConditionChange -= OnConditionChange;
        Diag?.Write("plugin unloading");
        ClientState.EnterPvP -= EnterPvP;
        ClientState.LeavePvP -= LeavePvP;
        IpcProviders.Dispose();
        ZoneDownHookManager.Dispose();
        
        FfxivActPluginWrapper.Dispose();
        OverlayPlugin.DeInitPlugin();
        Trace.Listeners.Remove(PluginLogTraceListener);

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(MainWindowCommandName);
        CommandManager.RemoveHandler(EndEncCommandName);

        Advanced_Combat_Tracker.ActGlobals.Dispose();
        Diag?.Dispose();
        Diag = null;
    }

    private RainbowMage.OverlayPlugin.PluginMain InitOverlayPlugin()
    {
        var container = new RainbowMage.OverlayPlugin.TinyIoCContainer();
        
        var logger = new RainbowMage.OverlayPlugin.Logger(Log);
        container.Register(logger);
        container.Register<RainbowMage.OverlayPlugin.ILogger>(logger);

        container.Register(HttpClient);
        container.Register(FileDialogManager);
        container.Register(PluginInterface);

        var overlayPlugin = new RainbowMage.OverlayPlugin.PluginMain(
            PluginInterface.AssemblyLocation.Directory!.FullName, logger, container);
        container.Register(overlayPlugin);
        Advanced_Combat_Tracker.ActGlobals.oFormActMain.OverlayPluginContainer = container;
        
        Task.Run(() =>
        {
            overlayPlugin.InitPlugin(PluginInterface.ConfigDirectory.FullName);

            var registry = container.Resolve<RainbowMage.OverlayPlugin.Registry>();
            MainWindow.OverlayPresets = registry.OverlayTemplates;
            WebSocketServer = container.Resolve<RainbowMage.OverlayPlugin.WebSocket.ServerController>();
            MainWindow.Server = WebSocketServer;
            IpcProviders.Server = WebSocketServer;
            IpcProviders.OverlayIpcHandler = container.Resolve<RainbowMage.OverlayPlugin.Handlers.Ipc.IpcHandlerController>();
            MainWindow.OverlayPluginConfig = container.Resolve<RainbowMage.OverlayPlugin.IPluginConfig>();
            MainWindow.OverlayPluginEventConfig = container.Resolve<RainbowMage.OverlayPlugin.EventSources.BuiltinEventConfig>();
            RefreshOverlayPagesIfMidSession();
            startupSettled = true;
        });

        return overlayPlugin;
    }

    private void OnCommand(string command, string args)
    {
        if (command == EndEncCommandName)
        {
            Advanced_Combat_Tracker.ActGlobals.oFormActMain.EndCombat(false);
            return;
        }
            
        switch (args) 
        {
            case "start": //deprecated
            case "ws start":
                WebSocketServer?.Start();
                break;
            case "stop": //deprecated
            case "ws stop":
                WebSocketServer?.Stop();
                break;
            case "log start":
                Configuration.WriteLogFile = true;
                Configuration.Save();
                break;
            case "log stop":
                Configuration.WriteLogFile = false;
                Configuration.Save();
                break;
            case "log pvp start":
                Configuration.DisablePvp = false;
                Configuration.Save();
                break;
            case "log pvp stop":
                Configuration.DisablePvp = true;
                Configuration.Save();
                break;
            case "restart":
                RestartParser("requested");
                break;
            case "status":
                ChatGui.Print($"IINACT: {FfxivActPluginWrapper.Summary()}; auto-restart {(Configuration.AutoRestart ? "on" : "off")}.");
                break;
            case "dump":
                Diag?.Write($"dump requested: {FfxivActPluginWrapper.Summary()}; overlay plugin '{OverlayPlugin.Status}'; "
                            + $"in combat {Condition[ConditionFlag.InCombat]}; territory {ClientState.TerritoryType}; uptime {uptime.Elapsed.TotalSeconds:F0}s");
                ChatGui.Print(Diag is null ? "IINACT: no diagnostic log is open." : $"IINACT: diagnostics written to {Diag.Path}");
                break;
            case "autorestart on":
            case "autorestart off":
                Configuration.AutoRestart = args.EndsWith("on");
                Configuration.Save();
                ChatGui.Print($"IINACT: automatic parser restart {(Configuration.AutoRestart ? "on" : "off")}.");
                break;
            default:
                MainWindow.IsOpen = true;
                break;
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
        FileDialogManager.Draw();
    }

    public void DrawConfigUI()
    {
        MainWindow.IsOpen = true;
    }

    internal void SetChatMessageLoggingEnabled(bool enabled)
    {
        FfxivActPluginWrapper.SetChatMessageLoggingEnabled(enabled);
    }

    // A meter page that lost the websocket while the parser was away does not re-subscribe when it
    // returns; at a cold boot the pages load after the server anyway.
    private void RefreshOverlayPagesIfMidSession()
    {
        if (!ClientState.IsLoggedIn)
            return;
        Thread.Sleep(2000);
        try
        {
            var reload = PluginInterface.GetIpcSubscriber<bool>("Browsingway.ReloadOverlays");
            Diag?.Write(reload.InvokeFunc() ? "asked Browsingway to reload the overlay pages" : "Browsingway declined to reload the overlay pages");
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError)
        {
            Diag?.Write("Browsingway is not loaded; overlay pages not reloaded");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "overlay page reload failed");
            Diag?.Write($"overlay page reload failed: {ex.Message}");
        }
    }

    private void WatchdogTick(IFramework framework)
    {
        var now = Environment.TickCount64;
        if (now - lastWatchdogTick < 1000)
            return;
        lastWatchdogTick = now;

        var sample = Sample();
        var inCombat = sample.InCombat;
        if (now - lastHeartbeat >= 60_000)
        {
            lastHeartbeat = now;
            Diag?.Write($"heartbeat: {FfxivActPluginWrapper.Summary()}; in combat {inCombat}; territory {ClientState.TerritoryType}");
        }

        var kind = ParserWatchdog.Evaluate(sample);
        if (kind == StallKind.None || !Configuration.AutoRestart || restartRequested)
            return;

        var reason = ParserWatchdog.Describe(kind, FfxivActPluginWrapper.ScanPhase);
        Log.Warning($"[Watchdog] {reason}: {sample.PendingRefreshes} pending refreshes, "
                    + $"{sample.SecondsInCombat:F0}s in combat, {sample.SecondsSinceNetworkLine:F0}s since the last network line, "
                    + $"territory {ClientState.TerritoryType}");
        Diag?.Write($"watchdog: {reason}; {FfxivActPluginWrapper.Summary()}; {sample.SecondsInCombat:F0}s in combat");
        RestartParser(reason);
    }

    private WatchdogSample Sample()
    {
        var inCombat = Condition[ConditionFlag.InCombat];
        if (!inCombat)
            combatSince = -1;
        else if (combatSince < 0)
            combatSince = uptime.Elapsed.TotalSeconds;

        return new WatchdogSample(
            FfxivActPluginWrapper.PendingRefreshes,
            inCombat,
            inCombat ? uptime.Elapsed.TotalSeconds - combatSince : 0,
            FfxivActPluginWrapper.SecondsSinceNetworkLine,
            uptime.Elapsed.TotalSeconds);
    }

    private void RestartParser(string reason)
    {
        restartRequested = true;
        Diag?.Write($"restart requested: {reason}");
        ChatGui.Print($"IINACT: parser stalled ({reason}) - restarting.");
        Configuration.LastAutoRestart = DateTime.UtcNow;
        Configuration.LastRestartReason = reason;
        Configuration.Save();
        Task.Run(async () =>
        {
            try
            {
                await PluginReloader.Reload(PluginInterface, "IINACT");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Watchdog] reload failed");
                Diag?.Write($"restart FAILED: {ex.GetType().Name}: {ex.Message}");
                restartRequested = false;
                Configuration.LastAutoRestart = null;
                Configuration.Save();
                try
                {
                    ChatGui.PrintError("IINACT: automatic restart failed - run /xldisableplugintemp IINACT, then /xlenableplugintemp IINACT.");
                }
                catch (Exception)
                {
                    // The plugin may already be half unloaded; the log line above is what survives.
                }
            }
        });
    }

    private void AnnounceRestart()
    {
        if (Configuration.LastAutoRestart is not { } when || DateTime.UtcNow - when > TimeSpan.FromMinutes(3))
            return;
        Diag?.Write($"back after an automatic restart ({Configuration.LastRestartReason})");
        ChatGui.Print($"IINACT: parser restarted ({Configuration.LastRestartReason}). "
                      + "Overlays reconnect on their own; use the Restart overlays macro if one stays blank.");
        Configuration.LastAutoRestart = null;
        Configuration.LastRestartReason = null;
        Configuration.Save();
    }

    private void EnterPvP()
    {
        if (Configuration is not { DisablePvp: true, DisableWritingPvpLogFile: false })
            return;

        Configuration.DisableWritingPvpLogFile = true;
    }

    private void LeavePvP()
    {
        Configuration.DisableWritingPvpLogFile = false;
    }
}
