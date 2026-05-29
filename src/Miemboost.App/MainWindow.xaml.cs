using System.Windows;
using Miemboost.Core.Diagnostics;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Windows.Diagnostics;

namespace Miemboost.App;

public partial class MainWindow : Window
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly DefaultPlanFactory _planFactory = new();

    public MainWindow()
    {
        InitializeComponent();

        _diagnosticsService = new DiagnosticsService(
            new WindowsSystemDiagnosticsReader(),
            new WindowsNetworkDiagnosticsReader());

        Loaded += async (_, _) => await RefreshDiagnosticsAsync();
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

    private void BoostPreview_Click(object sender, RoutedEventArgs e)
    {
        ShowBoostPreview();
    }

    private void ShowBoostPreview()
    {
        var plan = _planFactory.Create(BoostMode.Balanced);

        PlanList.Items.Clear();
        foreach (var action in plan.Actions)
        {
            PlanList.Items.Add($"{ToChineseRisk(action.RiskLevel)}  {action.Title} - {(action.CanRestore ? "可恢复" : "不可恢复")}");
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
}
