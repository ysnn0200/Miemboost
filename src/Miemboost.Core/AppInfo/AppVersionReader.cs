using System.Reflection;

namespace Miemboost.Core.AppInfo;

public static class AppVersionReader
{
    public static AppVersionInfo Read(Assembly assembly)
    {
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? assembly.GetName().Name
            ?? "Miemboost";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        return new AppVersionInfo(
            ProductName: product,
            Version: assembly.GetName().Version?.ToString() ?? "0.0.0",
            InformationalVersion: informationalVersion);
    }
}
