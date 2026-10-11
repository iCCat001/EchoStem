; Inno Setup script for EchoStem (product name: Dopamine)
; Produces a single-file installer: Dopamine-Setup-<version>.exe
;
; Usage (CI):
;   iscc /DMyAppVersion=2.0.11-Beta1 /DSourceDir=<abs path>\Dopamine\bin\Release installer\Dopamine.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\Dopamine\bin\Release"
#endif

#define MyAppName "Dopamine"
#define MyAppPublisher "Digimezzo"
#define MyAppExeName "Dopamine.exe"
#define MyAppURL "https://github.com/iCCat001/EchoStem"

[Setup]
AppId={{75ba9e1e-9eff-4a8e-845e-125dc4318c3b}
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
OutputBaseFilename=Dopamine-Setup-{#MyAppVersion}
SetupIconFile=..\Dopamine\Dopamine.ico
VersionInfoDescription={#MyAppName} {#MyAppVersion} Setup
VersionInfoProductName={#MyAppName}
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Dopamine.Packager.exe,Dopamine.Packager.exe.config,Dopamine.Packager.pdb,PackagerConfiguration.xml,Dopamine.wxs,Windows.winmd,System.Runtime.WindowsRuntime.dll"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Close a running instance so its files can be replaced. }
  Exec('taskkill.exe', '/f /im {#MyAppExeName} /t', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
