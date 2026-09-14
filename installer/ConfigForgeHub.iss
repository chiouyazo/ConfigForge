#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

[Setup]
AppId={{2F6C8C2E-6F0B-4A2B-9C0E-0B3E7C7B7A21}
AppName=ConfigForge Hub
AppVersion={#AppVersion}
AppPublisher=ConfigForge
AppPublisherURL=https://github.com/chiouyazo/ConfigForge
DefaultDirName={autopf}\ConfigForge Hub
DefaultGroupName=ConfigForge Hub
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ConfigForgeHubSetup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\ConfigForge.Hub.Web.exe
WizardStyle=modern
LicenseFile=..\LICENSE

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "scripts\install-service.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "scripts\uninstall-service.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install-service.ps1"""; \
    StatusMsg: "Registering the ConfigForge Hub service..."; \
    Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\uninstall-service.ps1"""; \
    RunOnceId: "UninstallConfigForgeHubService"; \
    Flags: runhidden waituntilterminated
