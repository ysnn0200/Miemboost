# Release

## Local Publish

Run:

```powershell
.\scripts\publish-windows.ps1
```

The output folder is:

```text
artifacts\publish\Miemboost-win-x64
```

Run:

```powershell
artifacts\publish\Miemboost-win-x64\Miemboost.exe
```

## Runtime Mode

The default publish is framework-dependent, which keeps the output smaller but requires .NET 8 Desktop Runtime on the target machine.

For a larger self-contained build:

```powershell
.\scripts\publish-windows.ps1 -SelfContained
```

## Current Packaging Status

This is a portable publish folder, not an installer yet. Code signing and installer creation are later release steps.
