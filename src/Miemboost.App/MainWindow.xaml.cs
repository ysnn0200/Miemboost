using System.IO;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Miemboost.Core.Diagnostics;
using Miemboost.Core.Execution;
using Miemboost.Core.Games;
using Miemboost.Core.History;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;
using Miemboost.Core.Safety;
using Miemboost.Windows.Diagnostics;
using Miemboost.Windows.Execution;
using Miemboost.Windows.Games;
using Miemboost.Windows.History;
using Miemboost.Windows.Power;
using Miemboost.Windows.Processes;

namespace Miemboost.App;

public partial class MainWindow : Window
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly JsonSystemSnapshotStore _snapshotStore;
    private readonly JsonOptimizationHistoryStore _historyStore;
    private readonly JsonGameProfileStore _gameProfileStore;
    private readonly OptimizationExecutor _executor;
    private readonly OptimizationRestorer _restorer;
    private readonly IProcessLifetimeReader _processLifetimeReader = new WindowsProcessLifetimeReader();
    private readonly DefaultPlanFactory _planFactory = new();
    private readonly BackgroundProcessAnalyzer _backgroundProcessAnalyzer = new();
    private readonly GameProfileMatcher _gameProfileMatcher = new();
    private OptimizationPlan? _lastPlan;
    private string? _lastSnapshotId;
    private int? _selectedGameProcessId;
    private IReadOnlyList<ProcessSnapshot> _latestProcesses = [];
    private IReadOnlyList<BackgroundCandidateChoice> _backgroundCandidateChoices = [];
    private IReadOnlyList<GameProfileChoice> _gameProfileChoices = [];
    private string? _activeGameProfileId;
    private GameProfile? _activeGameProfile;
    private DispatcherTimer? _autoRestoreTimer;
    private int? _boostedGameProcessId;
    private bool _isRestoring;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();

        var powerPlanManager = new WindowsPowerPlanManager();
        var processPriorityManager = new WindowsProcessPriorityManager();
        var handlerRegistry = new OptimizationActionHandlerRegistry(
        [
            new PowerPlanSwitchActionHandler(powerPlanManager),
            new ProcessPriorityActionHandler(processPriorityManager),
            new BackgroundAppPauseActionHandler(processPriorityManager)
        ]);

        _snapshotStore = new JsonSystemSnapshotStore(GetSnapshotDirectoryPath());
        _historyStore = new JsonOptimizationHistoryStore(GetHistoryFilePath());
        _gameProfileStore = new JsonGameProfileStore(GetGameProfilesFilePath());
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
        Loaded += async (_, _) => await RefreshGameLibraryAsync();
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        InitializeTrayIcon();
        ShowBoostPreview();
    }

    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show Miemboost", null, (_, _) => ShowFromTray());
        menu.Items.Add("Exit", null, async (_, _) =>
        {
            await RestoreBeforeExitAsync();
            _isExitRequested = true;
            Close();
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Miemboost",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private async Task RestoreBeforeExitAsync()
    {
        if (RestoreButton.IsEnabled)
        {
            await RestoreAsync();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _autoRestoreTimer?.Stop();
        _notifyIcon?.Dispose();
    }

    private void HideToTray()
    {
        Hide();
        SessionStateText.Text = "Miemboost is running in the tray. Restore remains available.";
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
            _latestProcesses = report.System.Processes;
            RenderProcessChoices(report.System.Processes);
            RenderBackgroundCandidateChoices(report.System.Processes);
            await AutoMatchRunningGameAsync(report.System.Processes);
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

        foreach (var candidate in _backgroundProcessAnalyzer.FindCandidates(report.System.Processes).Take(5))
        {
            FindingsList.Items.Add($"后台候选  {candidate.Name}  {ToMb(candidate.WorkingSetBytes):0} MB，可考虑加入游戏配置的允许暂停列表。");
        }
    }

    private async void Boost_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteBoostAsync();
    }

    private void ShowBoostPreview()
    {
        var plan = _planFactory.Create(
            BoostMode.Balanced,
            gameProfileId: _activeGameProfileId,
            gameProcessId: _selectedGameProcessId,
            gameProfile: _activeGameProfile,
            processes: _latestProcesses);
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

    private void RenderBackgroundCandidateChoices(IReadOnlyList<ProcessSnapshot> processes)
    {
        _backgroundCandidateChoices = _backgroundProcessAnalyzer.FindCandidates(processes)
            .Take(12)
            .Select(candidate => new BackgroundCandidateChoice(
                candidate.Name,
                $"{candidate.Name}  {ToMb(candidate.WorkingSetBytes):0} MB"))
            .ToArray();

        BackgroundCandidateCombo.ItemsSource = _backgroundCandidateChoices;
        BackgroundCandidateCombo.SelectedIndex = _backgroundCandidateChoices.Count > 0 ? 0 : -1;
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
        var plan = _lastPlan ?? _planFactory.Create(
            BoostMode.Balanced,
            gameProfileId: _activeGameProfileId,
            gameProcessId: _selectedGameProcessId,
            gameProfile: _activeGameProfile,
            processes: _latestProcesses);
        _lastPlan = plan;

        PlanList.Items.Clear();
        PlanList.Items.Add("正在保存快照并执行安全动作...");

        var report = await _executor.ExecuteAsync(plan);
        _lastSnapshotId = report.SnapshotId;
        RestoreButton.IsEnabled = true;
        await _historyStore.AddAsync(OptimizationHistoryEntryFactory.FromExecution(report, plan.Mode));
        StartAutoRestoreMonitor();

        PlanList.Items.Clear();
        PlanList.Items.Add($"快照：{report.SnapshotId}");
        foreach (var result in report.Results)
        {
            PlanList.Items.Add($"{ToChineseExecutionStatus(result.Status)}  {result.ActionId} - {result.Message}");
        }

        await RefreshHistoryAsync();
    }

    private void StartAutoRestoreMonitor()
    {
        _autoRestoreTimer?.Stop();
        _boostedGameProcessId = _selectedGameProcessId;

        if (_boostedGameProcessId is null || _activeGameProfile?.AutoRestoreOnExit != true)
        {
            SessionStateText.Text = "Boost completed. Manual restore is available.";
            return;
        }

        _autoRestoreTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoRestoreTimer.Tick += async (_, _) => await AutoRestoreTimerTickAsync();
        _autoRestoreTimer.Start();
        SessionStateText.Text = $"Boost active. Auto-restore is watching PID {_boostedGameProcessId}.";
    }

    private async Task AutoRestoreTimerTickAsync()
    {
        if (_isRestoring || _boostedGameProcessId is null)
        {
            return;
        }

        if (_processLifetimeReader.IsRunning(_boostedGameProcessId.Value))
        {
            return;
        }

        _autoRestoreTimer?.Stop();
        SessionStateText.Text = "Game exit detected. Restoring snapshot...";
        PlanList.Items.Add("Detected game exit, restoring snapshot...");
        await RestoreAsync();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        await RestoreAsync();
    }

    private async void SaveSelectedGame_Click(object sender, RoutedEventArgs e)
    {
        await SaveSelectedGameAsync();
    }

    private async Task SaveSelectedGameAsync()
    {
        if (_selectedGameProcessId is null)
        {
            GameLibraryList.Items.Add("请先选择一个游戏进程。");
            return;
        }

        var process = _latestProcesses.FirstOrDefault(process => process.ProcessId == _selectedGameProcessId.Value);
        if (process is null)
        {
            GameLibraryList.Items.Add("当前选择的进程已不存在。");
            return;
        }

        var executablePath = process.MainModulePath ?? process.Name;
        var profile = GameProfileFactory.Create(process.Name, executablePath);
        await _gameProfileStore.SaveAsync(profile);
        _activeGameProfileId = profile.Id;
        _activeGameProfile = profile;
        await RefreshGameLibraryAsync();
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private async void AllowBackgroundCandidate_Click(object sender, RoutedEventArgs e)
    {
        await AddAllowedBackgroundCandidateAsync();
    }

    private async Task AddAllowedBackgroundCandidateAsync()
    {
        if (string.IsNullOrWhiteSpace(_activeGameProfileId))
        {
            GameLibraryList.Items.Add("请先保存一个游戏配置。");
            return;
        }

        if (BackgroundCandidateCombo.SelectedItem is not BackgroundCandidateChoice choice)
        {
            GameLibraryList.Items.Add("请先选择一个后台候选。");
            return;
        }

        var profiles = await _gameProfileStore.ListAsync();
        var profile = profiles.FirstOrDefault(profileItem => profileItem.Id == _activeGameProfileId);
        if (profile is null)
        {
            GameLibraryList.Items.Add("当前游戏配置不存在。");
            return;
        }

        var updated = GameProfileUpdater.AddAllowedBackgroundProcess(profile, choice.ProcessName);
        await _gameProfileStore.SaveAsync(updated);
        _activeGameProfile = updated;
        _activeGameProfileId = updated.Id;
        await RefreshGameLibraryAsync();
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private async void DeleteActiveGame_Click(object sender, RoutedEventArgs e)
    {
        await DeleteActiveGameAsync();
    }

    private async Task DeleteActiveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(_activeGameProfileId))
        {
            GameLibraryList.Items.Add("No active profile to delete.");
            return;
        }

        await _gameProfileStore.DeleteAsync(_activeGameProfileId);
        _activeGameProfileId = null;
        _activeGameProfile = null;
        await RefreshGameLibraryAsync();
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private async Task RestoreAsync()
    {
        if (_isRestoring)
        {
            return;
        }

        if (_lastPlan is null || string.IsNullOrWhiteSpace(_lastSnapshotId))
        {
            return;
        }

        _isRestoring = true;
        _autoRestoreTimer?.Stop();

        var snapshot = await _snapshotStore.GetAsync(_lastSnapshotId);
        if (snapshot is null)
        {
            PlanList.Items.Add("未找到可恢复快照。");
            _isRestoring = false;
            return;
        }

        PlanList.Items.Clear();
        PlanList.Items.Add("正在按快照恢复...");

        var report = await _restorer.RestoreAsync(_lastPlan, snapshot);
        RestoreButton.IsEnabled = false;
        _boostedGameProcessId = null;
        SessionStateText.Text = "Restore completed. System changes were reverted where possible.";
        await _historyStore.AddAsync(OptimizationHistoryEntryFactory.FromRestore(report, _lastPlan.Mode));

        PlanList.Items.Clear();
        foreach (var result in report.Results)
        {
            PlanList.Items.Add($"{ToChineseExecutionStatus(result.Status)}  {result.ActionId} - {result.Message}");
        }

        await RefreshHistoryAsync();
        _isRestoring = false;
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

    private async Task RefreshGameLibraryAsync()
    {
        var profiles = await _gameProfileStore.ListAsync();
        UpdateActiveProfileText();

        GameLibraryList.Items.Clear();
        if (profiles.Count == 0)
        {
            GameLibraryList.ItemsSource = null;
            GameLibraryList.Items.Add("还没有保存的游戏。");
            return;
        }

        _gameProfileChoices = profiles
            .Take(8)
            .Select(profile => new GameProfileChoice(
                profile.Id,
                $"{profile.Name}  {profile.RecommendedMode}  自动恢复 {(profile.AutoRestoreOnExit ? "开" : "关")}  允许暂停 {profile.AllowedBackgroundProcessNames.Count}"))
            .ToArray();

        GameLibraryList.Items.Clear();
        GameLibraryList.ItemsSource = _gameProfileChoices;
        GameLibraryList.SelectedItem = _gameProfileChoices.FirstOrDefault(choice => choice.ProfileId == _activeGameProfileId);
    }

    private async void GameLibraryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GameLibraryList.SelectedItem is not GameProfileChoice choice)
        {
            return;
        }

        var profiles = await _gameProfileStore.ListAsync();
        var profile = profiles.FirstOrDefault(profileItem => profileItem.Id == choice.ProfileId);
        if (profile is null)
        {
            return;
        }

        _activeGameProfile = profile;
        _activeGameProfileId = profile.Id;
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private async Task AutoMatchRunningGameAsync(IReadOnlyList<ProcessSnapshot> processes)
    {
        if (_activeGameProfile is not null || _selectedGameProcessId is not null)
        {
            return;
        }

        var profiles = await _gameProfileStore.ListAsync();
        var match = _gameProfileMatcher.FindRunningMatch(profiles, processes);
        if (match is null)
        {
            return;
        }

        _activeGameProfile = match.Profile;
        _activeGameProfileId = match.Profile.Id;
        _selectedGameProcessId = match.ProcessId;
        if (GameProcessCombo.ItemsSource is IEnumerable<ProcessChoice> processChoices)
        {
            GameProcessCombo.SelectedItem = processChoices
                .FirstOrDefault(choice => choice.ProcessId == _selectedGameProcessId);
        }
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private void UpdateActiveProfileText()
    {
        ActiveProfileText.Text = _activeGameProfile is null
            ? "Current profile: none"
            : $"Current profile: {_activeGameProfile.Name} / allowed {_activeGameProfile.AllowedBackgroundProcessNames.Count}";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
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

    private static string GetGameProfilesFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Miemboost",
            "games.json");
    }

    private sealed record ProcessChoice(int? ProcessId, string DisplayName);

    private sealed record BackgroundCandidateChoice(string ProcessName, string DisplayName);

    private sealed record GameProfileChoice(string ProfileId, string DisplayName);
}
