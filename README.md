# Jeremy's Robotics Bulk Software Installer v1.4

WPF bulk installer that replaces the v1.2 batch script with a single elevated `.exe`, GUI progress/status indicators, and embedded installers.

## Requirements

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building only)

## Build

From this folder:

```powershell
.\build.ps1
```

Or manually:

```powershell
dotnet publish JRBSI/JRBSI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `JRBSI/bin/Release/net8.0-windows/win-x64/publish/JRBSI.exe`

## Features

- Single UAC prompt at launch (`requireAdministrator` manifest)
- Embedded installers: NI Package Manager, Cursor, Google Chrome
- Phoenix Tuner X via winget (requires internet)
- Per-package status: Pending, Installing..., Installed, Failed, Already Installed
- WPILib reminder popup after all packages finish

## Custom Icon

Replace `JRBSI/Assets/app.ico` and rebuild.

## Logs

Install logs are written to `%TEMP%\JRBSI\install.log`.

## WPILib

WPILib is not installed automatically. After other packages finish, a popup reminds you to install it from:

https://docs.wpilib.org/en/stable/docs/zero-to-robot/step-2/wpilib-setup.html
