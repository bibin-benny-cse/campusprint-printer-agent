; Inno Setup Script for XeroxGo Agent
; Generates a clean 1-click installer: XeroxGoAgent-Setup.exe

#define MyAppName "XeroxGo Agent"
#define MyAppVersion "0.1.5"
#define MyAppPublisher "XeroxGo Technologies"
#define MyAppURL "https://xeroxgo.com"
#define MyAppExeName "XeroxGo.PrinterAgent.exe"

[Setup]
AppId={{D37F619C-5582-4BC2-91DE-7A419266F3A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=XeroxGoAgent-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Launch XeroxGo Agent automatically on Windows startup"; GroupDescription: "System Integration:"

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Configure Windows Startup Run key if user checked autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "XeroxGoPrinterAgent"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up local user config and temp logs automatically on uninstall
Type: filesandordirs; Name: "{localappdata}\XeroxGo"

[Code]
// Terminate running agent process before install or uninstall
function KillAgentProcess(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

function InitializeSetup(): Boolean;
begin
  KillAgentProcess();
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  KillAgentProcess();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove self-signed publisher certificate from CurrentUser trust stores
    Exec(ExpandConstant('{sys}\certutil.exe'), '-user -delstore "TrustedPublisher" "{#MyAppPublisher}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\certutil.exe'), '-user -delstore "Root" "{#MyAppPublisher}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
