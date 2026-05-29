using System.Security.Principal;
using Miemboost.Core.Security;

namespace Miemboost.Windows.Security;

public sealed class WindowsPrivilegeChecker : IPrivilegeChecker
{
    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
