# WeldAvalonia

Open-source cross-platform application for calculating pollutant emissions from
welding operations.

The project is being gradually migrated from a legacy WinForms application to
[Avalonia UI](https://avaloniaui.net/) using the MVVM pattern and JSON files for
data storage.

## Technology stack

- C# and .NET 9
- Avalonia UI 12.1.0
- CommunityToolkit.Mvvm 8.4.2
- Eremex Avalonia Controls 1.4.34

Powered by https://eremexcontrols.net

## Current status

The repository contains the initial desktop application shell. Calculation
modules and JSON-based reference data will be migrated in small, independently
testable stages.

## Build and run

Install the .NET 9 SDK, then run:

```powershell
dotnet restore
dotnet run --project .\src\WeldAvalonia\WeldAvalonia.csproj
```

## License

This project is available under the [MIT License](LICENSE).
