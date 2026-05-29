namespace Miemboost.Core.Diagnostics;

public interface IProtectedProcessClassifier
{
    bool IsProtectedCandidate(string processName, string? path);
}
