using System.IO;
using System.Windows;
using Miemboost.Core.Diagnostics;
using Miemboost.Core.Execution;
using Miemboost.Core.History;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Safety;
using Miemboost.Windows.Diagnostics;
using Miemboost.Windows.Execution;
using Miemboost.Windows.History;
using Miemboost.Windows.Power;
using Miemboost.Windows.Processes;

namespace Miemboost.App;

public partial class MainWindow : Window
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly JsonSystemSnapshotStore _snapshotStore;
    private readonly JsonOptimizationHistoryStore _historyStore;
    private readonly OptimizationExecutor _executor;
    private readonly OptimizationRestorer _restorer;
    private readonly DefaultPlanFactory _planFactory = new();
    private OptimizationPlan? _lastPlan;
    private string? _lastSnapshotId;
    private int? _selectedGameProcessId;

    public MainWindow()
    {
        InitializeComponent();

        var powerPlanManager = new WindowsPowerPlanManager();
        var processPriorityManager = new WindowsProcessPriorityManager();
        var handlerRegistry = new OptimizationActionHandlerRegistry(
        [
            new PowerPlanSwitchActionHandler(powerPlanManager),
            new ProcessPriorityActionHandler(processPriorityManager)
        ]);

        _snapshotStore = new JsonSystemSnapshotStore(GetSnapshotDirectoryPath());
        _historyStore = new JsonOptimizationHistoryStore(GetHistoryFilePath());
        _executor = new OptimizationExecutor(
            new SafetyPolicy(),
            new WindowsSystemSnapshotFactory(powerPlanManager, processPriorityManager),
            _snapshotStore,
            handlerRegistry);
        _restorer = new OptimizationRestorer(handlerRegistry);
        _diagnosticsService = new DiagnosticsService(
            new WindowsSystemDiagnosticsReader(),
            new WindowsNetworkDiagnosticsReader());

        Loaded += async (_, _) => await RefreshDiagnosticsAsync();
        Loaded += async (_, _) => await RefreshHistoryAsync();
        ShowBoostPreview();
    }

    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            RefreshText.Text = "检测中";

            var report = await _diagnosticsService.CaptureAsync(new DiagnosticsCaptureOptions(
                PingTarget: "1.1.1.1",
                DnsHostName: "www.github.com",
                PingSampleCount: 4,
                PingTimeout: TimeSpan.FromMilliseconds(900)));

            RenderDiagnostics(report);
            RenderProcessChoices(report.System.Processes);
        }
        catch (Exception exception)
        {
            OverallStatusText.Text = "异常";
            FindingCountText.Text = exception.Message;
            RefreshText.Text = "检测失败";
        }
    }

    private void RenderDiagnostics(DiagnosticsReport report)
    {
        CpuText.Text = $"{report.System.Cpu.UsagePercent:0}%";
        CpuDetailText.Text = $"{report.System.Cpu.LogicalProcessorCount} 逻辑处理器";
        CpuBar.Value = report.System.Cpu.UsagePercent;

        MemoryText.Text = $"{report.System.Memory.UsedPercent:0}%";
        MemoryDetailText.Text = $"{ToGb(report.System.Memory.UsedBytes):0.0} GB / {ToGb(report.System.Memory.TotalBytes):0.0} GB";
        MemoryBar.Value = report.System.Memory.UsedPercent;

        PingText.Text = report.Ping is null ? "-- ms" : $"{report.Ping.AverageLatencyMs:0} ms";
        NetworkDetailText.Text = report.Ping is null
            ? "未检测网络"
            : $"抖动 {report.Ping.JitterMs:0} ms  丢包 {report.Ping.PacketLossPercent:0.#}%";
        DnsText.Text = report.Dns is null ? "DNS -- ms" : $"DNS {report.Dns.ElapsedMilliseconds} ms";

        OverallStatusText.Text = ToChineseStatus(report.Summary.OverallSeverity);
        FindingCountText.Text = $"{report.Summary.Findings.Count} 个建议";
        RefreshText.Text = $"耗时 {report.Elapsed.TotalMilliseconds:0} ms";

        FindingsList.Items.Clear();
        if (report.Summary.Findings.Count == 0)
        {
            FindingsList.Items.Add("当前状态良好，没有需要立即处理的项目。");
        }
        else
        {
            foreach (var finding in report.Summary.Findings.Take(8))
            {
                FindingsList.Items.Add($"{ToChineseStatus(finding.Severity)}  {finding.Title} - {finding.Description}");
            }
        }
    }

    private async void Boost_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteBoostAsync();
    }

    private void ShowBoostPreview()
    {
        var plan = _planFactory.Create(BoostMode.Balanced, gameProcessId: _selectedGameProcessId);
        _lastPlan = plan;

        PlanList.Items.Clear();
        foreach (var action in plan.Actions)
        {
            PlanList.Items.Add($"{ToChineseRisk(action.RiskLevel)}  {action.Title} - {(action.CanRestore ? "可恢复" : "不可恢复")}");
        }
    }

    private void RenderProcessChoices(IReadOnlyList<ProcessSnapshot> processes)
    {
        var previousSelection = _selectedGameProcessId;
        var options = new List<ProcessChoice>
        {
            new(null, "未选择游戏进程")
        };

        options.AddRange(processes
            .Where(process => !process.IsProtectedCandidate)
            .Where(process => !string.Equals(process.Name, "Miemboost", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(process => process.WorkingSetBytes)
            .Take(12)
            .Select(process => new ProcessChoice(
                process.ProcessId,
                $"{process.Name}  PID {process.ProcessId}  {ToMb(process.WorkingSetBytes):0} MB")));

        GameProcessCombo.ItemsSource = options;
        GameProcessCombo.SelectedItem = options.FirstOrDefault(option => option.ProcessId == previousSelection) ?? options[0];
    }

    private void GameProcessCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GameProcessCombo.SelectedItem is ProcessChoice choice)
        {
            _selectedGameProcessId = choice.ProcessId;
            ShowBoostPreview();
        }
    }

    private async Task ExecuteBoostAsync()
    {
        var plan = _lastPlan ?? _planFactory.Create(BoostMode.Balanced, gameProcessId: _selectedGameProcessId);
        _lastPlan = plan;

        PlanList.Items.Clear();
        PlanList.Items.Add("正在保存快照并执行安全动作...");

        var report = await _executor.ExecuteAsync(plan);
        _lastSnapshotId = report.SnapshotId;
        RestoreButton.IsEnabled = true;
        await _historyStore.AddAsync(OptimizationHistoryEntryFactory.FromExecution(report, plan.Mode));

        PlanList.Items.Clear();
        PlanList.Items.Add($"快照：{report.SnapshotId}");
        foreach (var result in report.Results)
        {
            PlanList.Items.Add($"{ToChineseExecutionStatus(result.Status)}  {result.ActionId} - {result.Message}");
        }

        await RefreshHistoryAsync();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        await RestoreAsync();
    }

    private async Task RestoreAsync()
    {
        if (_lastPlan is null || string.IsNullOrWhiteSpace(_lastSnapshotId))
        {
            return;
        }

        var snapshot = await _snapshotStore.GetAsync(_lastSnapshotId);
        if (snapshot is null)
        {
            PlanList.Items.Add("未找到可恢复快照。");
            return;
        }

        PlanList.Items.Clear();
        PlanList.Items.Add("正在按快照恢复...");

        var report = await _restorer.RestoreAsync(_lastPlan, snapshot);
        RestoreButton.IsEnabled = false;
        await _historyStore.AddAsync(OptimizationHistoryEntryFactory.FromRestore(report, _lastPlan.Mode));

        PlanList.Items.Clear();
        foreach (var result in report.Results)
        {
            PlanList.Items.Add($"{ToChineseExecutionStatus(result.Status)}  {result.ActionId} - {result.Message}");
        }

        await RefreshHistoryAsync();
    }

    private async Task RefreshHistoryAsync()
    {
        var entries = await _historyStore.ListRecentAsync(limit: 8);

        HistoryList.Items.Clear();
        if (entries.Count == 0)
        {
            HistoryList.Items.Add("暂无记录");
            return;
        }

        foreach (var entry in entries)
        {
            HistoryList.Items.Add(
                $"{ToChineseHistoryEvent(entry.EventType)}  {ToChineseExecutionStatus(entry.Status)}  " +
                $"{entry.CreatedAt.LocalDateTime:HH:mm:ss}  成功 {entry.SucceededCount} / 跳过 {entry.SkippedCount} / 失败 {entry.FailedCount}");
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static double ToGb(ulong bytes)
    {
        return bytes / 1024d / 1024d / 1024d;
    }

    private static double ToMb(long bytes)
    {
        return bytes / 1024d / 1024d;
    }

    private static string ToChineseStatus(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Good => "良好",
            DiagnosticSeverity.Notice => "可优化",
            DiagnosticSeverity.Warning => "警告",
            DiagnosticSeverity.Critical => "异常",
            _ => "未知"
        };
    }

    private static string ToChineseRisk(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.Safe => "安全",
            RiskLevel.Balanced => "平衡",
            RiskLevel.Aggressive => "激进",
            RiskLevel.Forbidden => "禁止",
            _ => "未知"
        };
    }

    private static string ToChineseExecutionStatus(OptimizationExecutionStatus status)
    {
        return status switch
        {
            OptimizationExecutionStatus.Succeeded => "完成",
            OptimizationExecutionStatus.Skipped => "跳过",
            OptimizationExecutionStatus.Failed => "失败",
            _ => "未知"
        };
    }

    private static string ToChineseHistoryEvent(OptimizationHistoryEventType eventType)
    {
        return eventType switch
        {
            OptimizationHistoryEventType.Boost => "Boost",
            OptimizationHistoryEventType.Restore => "恢复",
            _ => "未知"
        };
    }

    private static string GetSnapshotDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Miemboost",
            "Snapshots");
    }

    private static string GetHistoryFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Miemboost",
            "history.json");
    }

    private sealed record ProcessChoice(int? ProcessId, string DisplayName);
}
