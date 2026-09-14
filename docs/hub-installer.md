# ConfigForge Hub installer

`ConfigForgeHubSetup-<version>.exe`, attached to each [GitHub Release](https://github.com/chiouyazo/ConfigForge/releases) tagged `v*`, installs the Hub as a Windows Service.

## What it does

- Installs a self-contained win-x64 build to `%ProgramFiles%\ConfigForge Hub`.
- Registers a service named `ConfigForgeHub` (display name "ConfigForge Hub"), startup type Automatic, and starts it.
- The service's own instance list (`instances.json`) lives under `%ProgramData%\ConfigForge Hub\App_Data`, not under Program Files, since the service account cannot write there. A local `dotnet run`/F5 session keeps using the project's own `App_Data` folder instead - see `WindowsServiceHelpers.IsWindowsService()` in `Program.cs`.
- Uninstalling (via "Apps & features") stops and removes the service; the `ProgramData` folder (and its `instances.json`) is left in place.

## Default configuration

Ships with `appsettings.json` binding to `http://127.0.0.1:5801` (localhost only). To change the port or expose it beyond localhost, edit `appsettings.json` in the install directory (or set the `Kestrel__Endpoints__Http__Url` environment variable for the service) and restart the service - Kestrel endpoint configuration always wins over a `--urls` command-line argument, so that is the one place to change it once installed.

## Building it locally

```powershell
dotnet publish src/ConfigForge.Hub.Web -c Release -r win-x64 --self-contained -o installer/publish -p:Version=0.1.0
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "/DAppVersion=0.1.0" installer\ConfigForgeHub.iss
```

Needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`ISCC.exe`) on PATH or at its default install location.
