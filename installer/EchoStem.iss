; Inno Setup script for EchoStem
; Produces a single-file installer: EchoStem-Setup-<version>.exe
;
; Usage (CI):
;   iscc /DMyAppVersion=2.0.12-Beta1 /DSourceDir=<abs path>\EchoStem\bin\Release installer\EchoStem.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\EchoStem\bin\Release"
#endif

#define MyAppName "EchoStem"
#define MyAppPublisher "C-Cat"
#define MyAppExeName "EchoStem.exe"
#define MyAppURL "https://github.com/iCCat001/EchoStem"

; EchoStem is a new product (new AppId). The old Dopamine installation is detected separately so the
; user can remove it; its user data is left alone and can be migrated by EchoStem on first start.
#define LegacyAppId "{75ba9e1e-9eff-4a8e-845e-125dc4318c3b}_is1"

[Setup]
AppId={{246b503a-4f94-4639-a85b-318783018937}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=EchoStem-Setup-{#MyAppVersion}
SetupIconFile=..\EchoStem\EchoStem.ico
VersionInfoDescription={#MyAppName} {#MyAppVersion} Setup
VersionInfoProductName={#MyAppName}
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Windows.winmd,System.Runtime.WindowsRuntime.dll"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
english.UninstallLegacyDopamine=An old Dopamine installation was found.%n%nDo you want to uninstall it before installing EchoStem?%n%nYour music library and settings are kept and can be migrated by EchoStem on first start.
chinesesimplified.UninstallLegacyDopamine=检测到已安装旧版 Dopamine。%n%n是否在安装 EchoStem 之前卸载旧版？%n%n你的音乐库与设置会被保留，EchoStem 首次启动时可以迁移它们。

[Code]
{ Returns the uninstall command of the old Dopamine installation, or an empty string. }
function GetLegacyDopamineUninstallString(): String;
var
  UninstallKey: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#LegacyAppId}';

  if not RegQueryStringValue(HKLM32, UninstallKey, 'UninstallString', Result) then
  begin
    if not RegQueryStringValue(HKLM64, UninstallKey, 'UninstallString', Result) then
    begin
      RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', Result);
    end;
  end;
end;

{ Stops a running process so its files can be replaced during the installation/uninstallation. }
procedure KillProcess(FileName: String);
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im "' + FileName + '" /t', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure UninstallLegacyDopamine();
var
  UninstallString: String;
  ResultCode: Integer;
begin
  { Never block an unattended installation with a dialog. }
  if WizardSilent then
  begin
    exit;
  end;

  UninstallString := GetLegacyDopamineUninstallString();

  if UninstallString = '' then
  begin
    exit;
  end;

  if MsgBox(ExpandConstant('{cm:UninstallLegacyDopamine}'), mbConfirmation, MB_YESNO or MB_DEFBUTTON1) <> IDYES then
  begin
    exit;
  end;

  KillProcess('Dopamine.exe');

  if not Exec(RemoveQuotes(UninstallString), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
              SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('The old Dopamine version could not be uninstalled automatically. Please uninstall it manually.',
           mbInformation, MB_OK);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  UninstallLegacyDopamine();

  { Close a running EchoStem instance so its files can be replaced. }
  KillProcess('{#MyAppExeName}');

  Result := '';
end;
