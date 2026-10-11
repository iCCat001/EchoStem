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

[Tasks]
Name: "associate"; Description: "Associate {#MyAppName} with supported audio files and add the ""Play with {#MyAppName}"" folder menu"; GroupDescription: "Windows integration:"; Flags: checkedonce
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Dopamine.Packager.exe,Dopamine.Packager.exe.config,Dopamine.Packager.pdb,PackagerConfiguration.xml,Dopamine.wxs,Windows.winmd,System.Runtime.WindowsRuntime.dll"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Registry]
; --- Application registration (appears in "Open with") ---
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#MyAppName} audio player"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "SOFTWARE\Dopamine\Capabilities"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey

; --- Capabilities file associations ---
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aiff"; ValueData: "Dopamine.aiff"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m3u"; ValueData: "Dopamine.m3u"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zpl"; ValueData: "Dopamine.zpl"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "Dopamine.mp3"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "Dopamine.ogg"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "Dopamine.wma"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "Dopamine.flac"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "Dopamine.m4a"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Dopamine\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "Dopamine.opus"; Flags: uninsdeletevalue; Tasks: associate

; --- ProgIDs (aiff) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.aiff"; ValueType: string; ValueData: "Dopamine aiff file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.aiff\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Aiff.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.aiff\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (m3u) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m3u"; ValueType: string; ValueData: "Dopamine m3u file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m3u\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\M3u.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m3u\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (zpl) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.zpl"; ValueType: string; ValueData: "Dopamine zpl file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.zpl\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Zpl.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.zpl\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (mp3) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.mp3"; ValueType: string; ValueData: "Dopamine mp3 file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.mp3\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Mp3.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.mp3\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (ogg) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.ogg"; ValueType: string; ValueData: "Dopamine ogg file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.ogg\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Ogg.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.ogg\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (wma) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.wma"; ValueType: string; ValueData: "Dopamine wma file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.wma\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Wma.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.wma\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (flac) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.flac"; ValueType: string; ValueData: "Dopamine flac file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.flac\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Flac.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.flac\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (m4a) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m4a"; ValueType: string; ValueData: "Dopamine m4a file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m4a\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\M4a.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.m4a\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- ProgIDs (opus) ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.opus"; ValueType: string; ValueData: "Dopamine opus file"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.opus\DefaultIcon"; ValueType: string; ValueData: "{app}\Icons\Opus.ico,0"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Dopamine.opus\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

; --- Folder context menu: "Play with Dopamine" ---
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\shell\PlayWithDopamine"; ValueType: string; ValueData: "Play with {#MyAppName}"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\shell\PlayWithDopamine\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Close a running instance so its files can be replaced. }
  Exec('taskkill.exe', '/f /im {#MyAppExeName} /t', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
