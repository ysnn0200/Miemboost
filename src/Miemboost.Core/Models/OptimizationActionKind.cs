namespace Miemboost.Core.Models;

public enum OptimizationActionKind
{
    Diagnostics,
    PowerPlanSwitch,
    ProcessPriorityChange,
    BackgroundAppPause,
    StandbyMemoryRelease,
    DnsCacheFlush,
    NetworkDiagnostics,
    ServicePause,
    GameInjection,
    GameMemoryScan,
    RenderHook,
    PacketModification,
    AntiCheatInterference,
    InputAutomation
}
