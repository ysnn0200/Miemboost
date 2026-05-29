namespace Miemboost.Windows.Power;

public interface IWindowsCommandRunner
{
    Task<WindowsCommandResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default);
}
