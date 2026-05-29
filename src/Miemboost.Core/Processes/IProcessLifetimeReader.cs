namespace Miemboost.Core.Processes;

public interface IProcessLifetimeReader
{
    bool IsRunning(int processId);
}
