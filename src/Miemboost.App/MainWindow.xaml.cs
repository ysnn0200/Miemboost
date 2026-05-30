using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Miemboost.Core.AppInfo;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;
using Miemboost.Core.Diagnostics;
using Miemboost.Core.Execution;
using Miemboost.Core.Games;
using Miemboost.Core.History;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Preflight;
using Miemboost.Core.Processes;
using Miemboost.Core.Safety;
using Miemboost.Windows.Diagnostics;
using Miemboost.Windows.Execution;
using Miemboost.Windows.Games;
using Miemboost.Windows.History;
using Miemboost.Windows.Memory;
using Miemboost.Windows.Power;
using Miemboost.Windows.Processes;
using Miemboost.Windows.Security;
using Miemboost.Windows.Services;

namespace Miemboost.App;

public partial class MainWindow : Window
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly JsonSystemSnapshotStore _snapshotStore;
    private readonly JsonOptimizationHistoryStore _historyStore;
    private readonly JsonGameProfileStore _gameProfileStore;
    private readonly OptimizationExecutor _executor;
    private readonly OptimizationRestorer _restorer;
    private readonly OptimizationPreflightService _preflightService;
    private readonly IProcessLifetimeReader _processLifetimeReader = new WindowsProcessLifetimeReader();
    private readonly DefaultPlanFactory _planFactory = new();
    private readonly BackgroundProcessAnalyzer _backgroundProcessAnalyzer = new();
    private readonly GameProfileMatcher _gameProfileMatcher = new();
    private readonly Queue<double> _cpuSamples = new();
    private readonly Queue<double> _memorySamples = new();
    private readonly Queue<double> _networkSamples = new();
    private readonly Queue<double> _gpuSamples = new();
    private OptimizationPlan? _lastPlan;
    private string? _lastSnapshotId;
    private int? _selectedGameProcessId;
    private IReadOnlyList<ProcessSnapshot> _latestProcesses = [];
    private IReadOnlyList<BackgroundCandidateChoice> _backgroundCandidateChoices = [];
    private IReadOnlyList<GameProfileChoice> _gameProfileChoices = [];
    private string? _activeGameProfileId;
    private GameProfile? _activeGameProfile;
    private BoostMode _selectedBoostMode = BoostMode.Balanced;
    private DispatcherTimer? _diagnosticsTimer;
    private DispatcherTimer? _autoRestoreTimer;
    private int? _boostedGameProcessId;
    private bool _isBoosting;
    private bool _isRestoring;
    private bool _isRefreshingDiagnostics;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        var versionInfo = AppVersionReader.Read(Assembly.GetExecutingAssembly());
        VersionText.Text = $"{versionInfo.ProductName} {versionInfo.InformationalVersion}";
        UpdateBoostModeButtons();

        var commandRunner = new ProcessWindowsCommandRunner();
        var powerPlanManager = new WindowsPowerPlanManager(commandRunner);
        var processPriorityManager = new WindowsProcessPriorityManager();
        var serviceManager = new ScWindowsServiceManager(commandRunner);
        var standbyMemoryManager = new WindowsStandbyMemoryManager();
        var privilegeChecker = new WindowsPrivilegeChecker();
        _diagnosticsService = new DiagnosticsService(
            new WindowsSystemDiagnosticsReader(),
            new WindowsNetworkDiagnosticsReader());

        var handlerRegistry = new OptimizationActionHandlerRegistry(
        [
            new PowerPlanSwitchActionHandler(powerPlanManager),
            new ProcessPriorityActionHandler(processPriorityManager),
            new BackgroundAppPauseActionHandler(processPriorityManager),
            new StandbyMemoryReleaseActionHandler(standbyMemoryManager),
            new NetworkDiagnosticsActionHandler(_diagnosticsService),
            new DnsCacheFlushActionHandler(commandRunner),
            new ServicePauseActionHandler(serviceManager)
        ]);

        _snapshotStore = new JsonSystemSnapshotStore(GetSnapshotDirectoryPath());
        _historyStore = new JsonOptimizationHistoryStore(GetHistoryFilePath());
        _gameProfileStore = new JsonGameProfileStore(GetGameProfilesFilePath());
        _executor = new OptimizationExecutor(
            new SafetyPolicy(),
            new WindowsSystemSnapshotFactory(powerPlanManager, processPriorityManager, serviceManager),
            _snapshotStore,
            handlerRegistry,
            privilegeChecker);
        _restorer = new OptimizationRestorer(handlerRegistry);
        _preflightService = new OptimizationPreflightService(new SafetyPolicy(), privilegeChecker);

        Loaded += async (_, _) =>
        {
            await RefreshDiagnosticsAsync();
            StartDiagnosticsTimer();
        };
        Loaded += async (_, _) => await RefreshHistoryAsync();
        Loaded += async (_, _) => await RefreshGameLibraryAsync();
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        InitializeTrayIcon();
        ShowBoostPreview();
    }

    private void StartDiagnosticsTimer()
    {
        _diagnosticsTimer?.Stop();
        _diagnosticsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _diagnosticsTimer.Tick += async (_, _) => await RefreshDiagnosticsAsync();
        _diagnosticsTimer.Start();
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
            Text = VersionText.Text.Length > 63 ? "Miemboost" : VersionText.Text,
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
        _diagnosticsTimer?.Stop();
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
        if (_isRefreshingDiagnostics)
        {
            return;
        }

        _isRefreshingDiagnostics = true;
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
        finally
        {
            _isRefreshingDiagnostics = false;
        }
    }

    private void RenderDiagnostics(DiagnosticsReport report)
    {
        CpuText.Text = $"{report.System.Cpu.UsagePercent:0}%";
        CpuDetailText.Text = FormatCpuDetails(report.System.Cpu);
        CpuBar.Value = report.System.Cpu.UsagePercent;

        GpuText.Text = report.System.Gpu.IsAvailable ? $"{report.System.Gpu.UsagePercent:0}%" : "--%";
        GpuDetailText.Text = report.System.Gpu.IsAvailable
            ? FormatGpuDetails(report.System.Gpu)
            : "当前系统未提供 GPU 计数器";
        GpuStatusText.Text = report.System.Gpu.IsAvailable ? "良好" : "不可用";
        GpuStatusDot.Fill = report.System.Gpu.IsAvailable
            ? (Media.Brush)FindResource("GoodBrush")
            : new Media.SolidColorBrush(Media.Color.FromRgb(100, 116, 139));
        GpuBar.Value = report.System.Gpu.IsAvailable ? report.System.Gpu.UsagePercent : 0;

        MemoryText.Text = $"{report.System.Memory.UsedPercent:0}%";
        MemoryDetailText.Text = $"{ToGb(report.System.Memory.UsedBytes):0.0} GB / {ToGb(report.System.Memory.TotalBytes):0.0} GB";
        MemoryBar.Value = report.System.Memory.UsedPercent;

        var networkReceiveBytesPerSecond = report.System.Processes.Sum(process => process.NetworkReceiveBytesPerSecond);
        var networkSendBytesPerSecond = report.System.Processes.Sum(process => process.NetworkSendBytesPerSecond);
        PingText.Text = report.Ping is null ? "-- ms" : $"{report.Ping.AverageLatencyMs:0} ms";
        var throughputText = $"↓{ToMb(networkReceiveBytesPerSecond):0.0} MB/s ↑{ToMb(networkSendBytesPerSecond):0.0} MB/s";
        NetworkDetailText.Text = report.Ping is null
            ? $"未检测网络  {throughputText}"
            : $"抖动 {report.Ping.JitterMs:0} ms  丢包 {report.Ping.PacketLossPercent:0.#}%  {throughputText}";
        DnsText.Text = report.Dns is null ? "DNS -- ms" : $"DNS {report.Dns.ElapsedMilliseconds} ms";
        RenderRealtimeSpectrums(
            cpuPercent: report.System.Cpu.UsagePercent,
            memoryPercent: report.System.Memory.UsedPercent,
            networkBytesPerSecond: networkReceiveBytesPerSecond + networkSendBytesPerSecond,
            gpuPercent: report.System.Gpu.IsAvailable ? report.System.Gpu.UsagePercent : 0);

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
            FindingsList.Items.Add(
                $"后台候选  {candidate.Name}  {ToMb(candidate.WorkingSetBytes):0} MB  TCP {candidate.EstablishedTcpConnectionCount}  ↓{ToMb(candidate.NetworkReceiveBytesPerSecond):0.0}/s ↑{ToMb(candidate.NetworkSendBytesPerSecond):0.0}/s，可考虑加入游戏配置。");
        }
    }

    private void RenderRealtimeSpectrums(
        double cpuPercent,
        double memoryPercent,
        double networkBytesPerSecond,
        double gpuPercent)
    {
        AddSample(_cpuSamples, cpuPercent);
        AddSample(_memorySamples, memoryPercent);
        AddSample(_networkSamples, Math.Clamp(networkBytesPerSecond / (5d * 1024d * 1024d) * 100d, 0, 100));
        AddSample(_gpuSamples, gpuPercent);

        RenderSparkline(CpuSparkline, _cpuSamples);
        RenderSparkline(MemorySparkline, _memorySamples);
        RenderSparkline(NetworkSparkline, _networkSamples);
        RenderSparkline(GpuSparkline, _gpuSamples);
    }

    private static void AddSample(Queue<double> samples, double value)
    {
        const int MaxSamples = 24;
        samples.Enqueue(Math.Clamp(value, 0, 100));
        while (samples.Count > MaxSamples)
        {
            samples.Dequeue();
        }
    }

    private static void RenderSparkline(System.Windows.Shapes.Polyline sparkline, IReadOnlyCollection<double> samples)
    {
        const double width = 148;
        const double height = 30;

        if (samples.Count == 0)
        {
            return;
        }

        var points = new PointCollection();
        var index = 0;
        var step = samples.Count <= 1 ? width : width / (samples.Count - 1);

        foreach (var sample in samples)
        {
            points.Add(new System.Windows.Point(index * step, height - sample / 100d * height + 2));
            index++;
        }

        sparkline.Points = points;
    }

    private async void Boost_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteBoostAsync();
    }

    private void ConservativeMode_Click(object sender, RoutedEventArgs e)
    {
        SetBoostMode(BoostMode.Conservative);
    }

    private void BalancedMode_Click(object sender, RoutedEventArgs e)
    {
        SetBoostMode(BoostMode.Balanced);
    }

    private void AggressiveMode_Click(object sender, RoutedEventArgs e)
    {
        SetBoostMode(BoostMode.Aggressive);
    }

    private void SetBoostMode(BoostMode mode)
    {
        if (_selectedBoostMode == mode)
        {
            return;
        }

        _selectedBoostMode = mode;
        UpdateBoostModeButtons();
        ShowBoostPreview();
    }

    private void UpdateBoostModeButtons()
    {
        UpdateModeButton(ConservativeModeButton, BoostMode.Conservative);
        UpdateModeButton(BalancedModeButton, BoostMode.Balanced);
        UpdateModeButton(AggressiveModeButton, BoostMode.Aggressive);
    }

    private void UpdateModeButton(System.Windows.Controls.Button button, BoostMode mode)
    {
        var isSelected = _selectedBoostMode == mode;
        button.Background = isSelected
            ? (Media.Brush)FindResource("AccentBrush")
            : new Media.SolidColorBrush(Media.Color.FromRgb(38, 50, 72));
        button.Foreground = isSelected
            ? Media.Brushes.White
            : new Media.SolidColorBrush(Media.Color.FromRgb(181, 192, 209));
        button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void ShowBoostPreview()
    {
        var plan = _planFactory.Create(
            _selectedBoostMode,
            gameProfileId: _activeGameProfileId,
            gameProcessId: _selectedGameProcessId,
            gameProfile: _activeGameProfile,
            processes: _latestProcesses);
        _lastPlan = plan;

        PlanList.Items.Clear();
        var preflight = _preflightService.Evaluate(plan);
        foreach (var item in preflight.Actions)
        {
            PlanList.Items.Add(
                $"{ToChinesePreflightStatus(item.Status)}  {ToChineseRisk(item.Action.RiskLevel)}  {item.Action.Title} - {item.Message}");
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
                $"{candidate.Name}  {ToMb(candidate.WorkingSetBytes):0} MB  TCP {candidate.EstablishedTcpConnectionCount}  ↓{ToMb(candidate.NetworkReceiveBytesPerSecond):0.0}/s ↑{ToMb(candidate.NetworkSendBytesPerSecond):0.0}/s"))
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
        if (_isBoosting || _isRestoring)
        {
            return;
        }

        _isBoosting = true;
        BoostButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;

        try
        {
            var plan = _lastPlan ?? _planFactory.Create(
                _selectedBoostMode,
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
        catch (Exception exception)
        {
            PlanList.Items.Clear();
            PlanList.Items.Add($"Boost 执行失败：{exception.Message}");
        }
        finally
        {
            _isBoosting = false;
            BoostButton.IsEnabled = true;
            RestoreButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastSnapshotId);
        }
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
        BoostButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        _autoRestoreTimer?.Stop();

        try
        {
            var snapshot = await _snapshotStore.GetAsync(_lastSnapshotId);
            if (snapshot is null)
            {
                PlanList.Items.Add("未找到可恢复快照。");
                return;
            }

            PlanList.Items.Clear();
            PlanList.Items.Add("正在按快照恢复...");

            var report = await _restorer.RestoreAsync(_lastPlan, snapshot);
            _lastSnapshotId = null;
            _boostedGameProcessId = null;
            SessionStateText.Text = "Restore completed. System changes were reverted where possible.";
            await _historyStore.AddAsync(OptimizationHistoryEntryFactory.FromRestore(report, _lastPlan.Mode));

            PlanList.Items.Clear();
            foreach (var result in report.Results)
            {
                PlanList.Items.Add($"{ToChineseExecutionStatus(result.Status)}  {result.ActionId} - {result.Message}");
            }

            await RefreshHistoryAsync();
        }
        catch (Exception exception)
        {
            PlanList.Items.Clear();
            PlanList.Items.Add($"恢复失败：{exception.Message}");
        }
        finally
        {
            _isRestoring = false;
            BoostButton.IsEnabled = true;
            RestoreButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastSnapshotId);
        }
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
            var notableDetail = entry.Details?
                .FirstOrDefault(detail => detail.Status != OptimizationExecutionStatus.Succeeded);
            var detailText = notableDetail is null ? string.Empty : $"  {notableDetail.ActionId}: {notableDetail.Message}";

            HistoryList.Items.Add(
                $"{ToChineseHistoryEvent(entry.EventType)}  {ToChineseExecutionStatus(entry.Status)}  " +
                $"{entry.CreatedAt.LocalDateTime:HH:mm:ss}  成功 {entry.SucceededCount} / 跳过 {entry.SkippedCount} / 失败 {entry.FailedCount}" +
                detailText);
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
        ApplyActiveProfileRecommendedMode();
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
        ApplyActiveProfileRecommendedMode();
        if (GameProcessCombo.ItemsSource is IEnumerable<ProcessChoice> processChoices)
        {
            GameProcessCombo.SelectedItem = processChoices
                .FirstOrDefault(choice => choice.ProcessId == _selectedGameProcessId);
        }
        UpdateActiveProfileText();
        ShowBoostPreview();
    }

    private void ApplyActiveProfileRecommendedMode()
    {
        if (_activeGameProfile is null)
        {
            return;
        }

        _selectedBoostMode = _activeGameProfile.RecommendedMode;
        UpdateBoostModeButtons();
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

    private static double ToMb(double bytes)
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

    private static string FormatGpuDetails(GpuSnapshot gpu)
    {
        var details = new List<string> { gpu.Source };

        if (gpu.DedicatedMemoryUsedBytes is not null)
        {
            var memoryText = gpu.DedicatedMemoryTotalBytes is null
                ? $"显存 {ToMb(gpu.DedicatedMemoryUsedBytes.Value):0} MB"
                : $"显存 {ToMb(gpu.DedicatedMemoryUsedBytes.Value):0} / {ToMb(gpu.DedicatedMemoryTotalBytes.Value):0} MB";
            details.Add(memoryText);
        }

        if (gpu.TemperatureCelsius is not null)
        {
            details.Add($"温度 {gpu.TemperatureCelsius:0}°C");
        }

        if (gpu.CoreClockMHz is not null)
        {
            details.Add($"核心 {gpu.CoreClockMHz:0} MHz");
        }

        if (gpu.MemoryClockMHz is not null)
        {
            details.Add($"显存频率 {gpu.MemoryClockMHz:0} MHz");
        }

        return string.Join("  ", details);
    }

    private static string FormatCpuDetails(CpuSnapshot cpu)
    {
        var details = new List<string> { $"{cpu.LogicalProcessorCount} 逻辑处理器" };

        if (cpu.CurrentClockMHz is not null)
        {
            details.Add(cpu.MaxClockMHz is null
                ? $"频率 {cpu.CurrentClockMHz:0} MHz"
                : $"频率 {cpu.CurrentClockMHz:0} / {cpu.MaxClockMHz:0} MHz");
        }

        details.Add(cpu.TemperatureCelsius is null
            ? "温度不可用"
            : $"温度 {cpu.TemperatureCelsius:0}°C");

        return string.Join("  ", details);
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

    private static string ToChinesePreflightStatus(OptimizationPreflightStatus status)
    {
        return status switch
        {
            OptimizationPreflightStatus.Ready => "就绪",
            OptimizationPreflightStatus.BlockedBySafety => "阻止",
            OptimizationPreflightStatus.RequiresAdministrator => "需管理员",
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
