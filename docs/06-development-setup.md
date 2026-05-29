# Development Setup

## Required Tools

- Git.
- .NET 8 SDK or later.

## Local Commands

```powershell
dotnet restore src/Miemboost.Tests/Miemboost.Tests.csproj
dotnet build src/Miemboost.Tests/Miemboost.Tests.csproj
dotnet test src/Miemboost.Tests/Miemboost.Tests.csproj
```

## Current Machine Note

The current development machine does not have the .NET SDK available on `PATH`, so local build and test commands cannot run yet.

GitHub Actions is configured to run restore, build, and tests on `windows-latest` for every push to `main`.
