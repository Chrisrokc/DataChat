; DataChat Windows Installer (Inno Setup 6)
; Build with: iscc DataChat.iss
; Assumes build/out/win-x64 contains a self-contained single-file publish.

#define AppName       "DataChat"
#define AppVersion    "1.0.0"
#define AppPublisher  "DataChat"
#define AppExeName    "DataChat.Web.exe"
#define ServiceName   "DataChat"
#define HttpPort      "5159"

[Setup]
AppId={{6F3E7C9F-AF34-4F1A-9D13-2F3F6C5C9A51}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL=https://github.com/
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\build\installers
OutputBaseFilename=DataChat-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
WizardStyle=modern
LicenseFile=..\..\LICENSE
; To customize the installer icon, drop a datachat.ico into assets/ and uncomment the next line.
;SetupIconFile=assets\datachat.ico
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "firewallrule"; Description: "Allow DataChat through Windows Firewall on port {#HttpPort}"; GroupDescription: "Networking:"

[Files]
Source: "..\..\build\out\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "scripts\postinstall.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "scripts\uninstall.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
; Optional: drop assets\datachat.ico to enable branded shortcuts
Source: "assets\datachat.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Dirs]
Name: "{app}\logs"; Permissions: users-modify
Name: "{app}\uploads"; Permissions: users-modify
Name: "{app}\data-protection-keys"; Permissions: users-modify

[Icons]
Name: "{group}\DataChat"; Filename: "cmd.exe"; Parameters: "/c start http://localhost:{#HttpPort}"; Comment: "Open DataChat in your browser"
Name: "{group}\Uninstall DataChat"; Filename: "{uninstallexe}"
Name: "{commondesktop}\DataChat"; Filename: "cmd.exe"; Parameters: "/c start http://localhost:{#HttpPort}"; Tasks: desktopicon

[Run]
; 1. Write connection string to appsettings.Production.json (bundled path only)
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\postinstall.ps1"" -InstallDir ""{app}"" -DbMode ""{code:GetDbMode}"" -HttpPort {#HttpPort}"; StatusMsg: "Configuring DataChat..."; Flags: runhidden waituntilterminated

; 2. Install & start Windows Service
Filename: "sc.exe"; Parameters: "create {#ServiceName} binPath= ""\""{app}\{#AppExeName}\"""" start= auto DisplayName= ""DataChat""" ; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description {#ServiceName} ""DataChat - Enterprise AI chat with RAG capabilities""" ; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated

; 3. Open firewall (optional task)
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""DataChat HTTP"" dir=in action=allow protocol=TCP localport={#HttpPort}"; Flags: runhidden waituntilterminated; Tasks: firewallrule

; 4. Open browser to Setup Wizard
Filename: "cmd.exe"; Parameters: "/c start http://localhost:{#HttpPort}"; Flags: nowait postinstall skipifsilent; Description: "Open DataChat in browser"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopSvc"
Filename: "sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DelSvc"
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""DataChat HTTP"""; Flags: runhidden waituntilterminated; RunOnceId: "DelFw"

[Code]
var
  DbModePage: TInputOptionWizardPage;

procedure InitializeWizard();
begin
  DbModePage := CreateInputOptionPage(wpSelectTasks,
    'Database Setup',
    'Choose how DataChat will connect to SQL Server 2025.',
    'DataChat requires SQL Server 2025 (VECTOR type support). Pick an option below.'#13#10 +
    'You can always change the connection later from the Setup Wizard.',
    True, False);
  DbModePage.Add('Bundled: download and install SQL Server 2025 Express (local)');
  DbModePage.Add('Connect to an existing SQL Server 2025 (configure in Setup Wizard)');
  DbModePage.Values[1] := True;
end;

function GetDbMode(Param: string): string;
begin
  if DbModePage.Values[0] then
    Result := 'bundled'
  else
    Result := 'existing';
end;
