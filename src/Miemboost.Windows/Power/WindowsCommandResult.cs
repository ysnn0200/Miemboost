namespace Miemboost.Windows.Power;

public sealed record WindowsCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
