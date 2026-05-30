using Miemboost.Core.Diagnostics;
using Miemboost.Core.Models;
using Miemboost.Core.Processes;
using Miemboost.Core.Services;

namespace Miemboost.Core.Planning;

public static class DefaultActionCatalog
{
    public static readonly OptimizationActionDescriptor PowerPlanSwitch = new(
        Id: "power-plan.switch-high-performance",
        Title: "Switch power plan",
        Description: "Temporarily switch Windows to a high performance power plan.",
        Kind: OptimizationActionKind.PowerPlanSwitch,
        RiskLevel: RiskLevel.Safe,
        RequiresElevation: false,
        CanRestore: true);

    public static readonly OptimizationActionDescriptor GamePriorityHigh = new(
        Id: "process.game-priority-high",
        Title: "Prioritize game process",
        Description: "Set the selected game process priority to High while the game is running.",
        Kind: OptimizationActionKind.ProcessPriorityChange,
        RiskLevel: RiskLevel.Balanced,
        RequiresElevation: false,
        CanRestore: true);

    public static OptimizationActionDescriptor CreateGamePriorityHighAction(int processId)
    {
        return GamePriorityHigh with
        {
            Parameters = new Dictionary<string, string>
            {
                [ProcessPriorityActionParameters.ProcessId] = processId.ToString(),
                [ProcessPriorityActionParameters.TargetPriority] = ManagedProcessPriority.High.ToString()
            }
        };
    }

    public static readonly OptimizationActionDescriptor PauseApprovedBackgroundApps = new(
        Id: "process.pause-approved-background-apps",
        Title: "Pause approved background apps",
        Description: "Lower priority only for the background apps allowed by the active game profile.",
        Kind: OptimizationActionKind.BackgroundAppPause,
        RiskLevel: RiskLevel.Balanced,
        RequiresElevation: false,
        CanRestore: true);

    public static OptimizationActionDescriptor CreatePauseApprovedBackgroundAppsAction(
        IReadOnlyList<int> processIds)
    {
        return PauseApprovedBackgroundApps with
        {
            Parameters = new Dictionary<string, string>
            {
                [BackgroundAppPauseActionParameters.ProcessIds] = string.Join(";", processIds)
            }
        };
    }

    public static OptimizationActionDescriptor CreatePauseApprovedBackgroundAppsAction(
        IReadOnlyList<ProcessSnapshot> processes)
    {
        return PauseApprovedBackgroundApps with
        {
            Parameters = new Dictionary<string, string>
            {
                [BackgroundAppPauseActionParameters.ProcessIds] = string.Join(";", processes.Select(process => process.ProcessId)),
                [BackgroundAppPauseActionParameters.ProcessNames] = string.Join(";", processes.Select(process => process.Name))
            }
        };
    }

    public static readonly OptimizationActionDescriptor ReleaseStandbyMemory = new(
        Id: "memory.release-standby",
        Title: "Release Standby Memory",
        Description: "Release Windows Standby Memory once before the game session starts.",
        Kind: OptimizationActionKind.StandbyMemoryRelease,
        RiskLevel: RiskLevel.Balanced,
        RequiresElevation: true,
        CanRestore: false);

    public static readonly OptimizationActionDescriptor NetworkDiagnostics = new(
        Id: "network.diagnostics",
        Title: "Run network diagnostics",
        Description: "Measure ping, jitter, packet loss, DNS response, and background network usage.",
        Kind: OptimizationActionKind.NetworkDiagnostics,
        RiskLevel: RiskLevel.Safe,
        RequiresElevation: false,
        CanRestore: true);

    public static readonly OptimizationActionDescriptor FlushDnsCache = new(
        Id: "network.flush-dns-cache",
        Title: "Flush DNS cache",
        Description: "Clear the local Windows DNS resolver cache once before the game session starts.",
        Kind: OptimizationActionKind.DnsCacheFlush,
        RiskLevel: RiskLevel.Balanced,
        RequiresElevation: false,
        CanRestore: false);

    public static readonly OptimizationActionDescriptor PauseWindowsUpdateDownloads = new(
        Id: "service.pause-windows-update-downloads",
        Title: "Pause Windows update downloads",
        Description: "Temporarily stop Windows Update, BITS, and Delivery Optimization services when they are running.",
        Kind: OptimizationActionKind.ServicePause,
        RiskLevel: RiskLevel.Balanced,
        RequiresElevation: true,
        CanRestore: true,
        Parameters: new Dictionary<string, string>
        {
            [ServicePauseActionParameters.ServiceNames] = "wuauserv;BITS;DoSvc"
        });
}
