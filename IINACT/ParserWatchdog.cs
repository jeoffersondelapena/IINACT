namespace IINACT;

public enum StallKind
{
    None,
    ScanThreadStuck,
    NoCombatData,
}

/// <summary>What the watchdog sees once a second.</summary>
public readonly record struct WatchdogSample(
    int PendingRefreshes,
    bool InCombat,
    double SecondsInCombat,
    double SecondsSinceNetworkLine,
    double SecondsSinceStart);

public static class ParserWatchdog
{
    // The framework thread hands the scan thread one refresh per frame and a live scan thread
    // drains them within a frame or two, so a backlog this deep means it has stopped consuming.
    public const int StuckRefreshThreshold = 300;

    // Auto-attacks alone produce ability lines every few seconds, so this much combat without a
    // single network-derived line is a dead parser, not a quiet fight.
    public const double CombatSilenceSeconds = 30;

    // Covers start-up and the reload itself, so a parser that is still coming up is never judged.
    public const double StartupGraceSeconds = 90;

    public static StallKind Evaluate(in WatchdogSample s)
    {
        if (s.SecondsSinceStart < StartupGraceSeconds)
            return StallKind.None;
        if (s.PendingRefreshes >= StuckRefreshThreshold)
            return StallKind.ScanThreadStuck;
        if (s.InCombat && s.SecondsInCombat >= CombatSilenceSeconds && s.SecondsSinceNetworkLine >= CombatSilenceSeconds)
            return StallKind.NoCombatData;
        return StallKind.None;
    }

    public static string Describe(StallKind kind, string scanPhase) => kind switch
    {
        StallKind.ScanThreadStuck => $"memory scan thread stuck in '{scanPhase}'",
        StallKind.NoCombatData => "in combat with no combat data",
        _ => "no stall",
    };
}
