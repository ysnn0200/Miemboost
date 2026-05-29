using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Miemboost.Core.Memory;

namespace Miemboost.Windows.Memory;

public sealed class WindowsStandbyMemoryManager : IStandbyMemoryManager
{
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;

    public Task<StandbyMemoryReleaseResult> ReleaseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAdministrator())
        {
            return Task.FromResult(new StandbyMemoryReleaseResult(
                Succeeded: false,
                Message: "Administrator permission is required to release Windows Standby Memory."));
        }

        var command = MemoryPurgeStandbyList;
        var status = NtSetSystemInformation(
            SystemMemoryListInformation,
            ref command,
            Marshal.SizeOf<int>());

        if (status != 0)
        {
            return Task.FromResult(new StandbyMemoryReleaseResult(
                Succeeded: false,
                Message: $"Windows rejected the Standby Memory release request. NTSTATUS: 0x{status:X8}."));
        }

        return Task.FromResult(new StandbyMemoryReleaseResult(
            Succeeded: true,
            Message: "Released Windows Standby Memory once."));
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(
        int systemInformationClass,
        ref int systemInformation,
        int systemInformationLength);
}
