#ifndef AppVersion
  #error AppVersion muss aus Directory.Build.props übergeben werden.
#endif
#ifndef SourceDir
  #error SourceDir fehlt.
#endif
#ifndef OutputDir
  #error OutputDir fehlt.
#endif

[Setup]
AppId={{3E29D02D-B2C1-44D7-8E90-C53380C6D534}
AppName=Blackveil
AppVersion={#AppVersion}
AppVerName=Blackveil {#AppVersion}
AppPublisher=Elias Hahn
AppPublisherURL=https://github.com/747elias/Blackveil
DefaultDirName={localappdata}\Programs\Blackveil
DefaultGroupName=Blackveil
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Blackveil-Setup-{#AppVersion}
SetupIconFile=..\src\Blackveil\blackveil.ico
UninstallDisplayIcon={app}\Blackveil.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Blackveil.Singleton
LicenseFile=..\LICENSE

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "autostart"; Description: "Blackveil mit Windows starten"; Flags: unchecked

[Files]
Source: "{#SourceDir}\Blackveil.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Blackveil"; Filename: "{app}\Blackveil.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Blackveil"; ValueData: """{app}\Blackveil.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Blackveil"; Tasks: not autostart; Flags: deletevalue uninsdeletevalue

[Run]
Filename: "{app}\Blackveil.exe"; Description: "Blackveil im Infobereich starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{localappdata}\Blackveil\settings.json"
Type: filesandordirs; Name: "{localappdata}\Blackveil\logs"

[Code]
procedure InitializeWizard;
var
  ExistingRun: String;
begin
  if (Pos('/TASKS=', Uppercase(GetCmdTail)) > 0) or (Pos('/MERGETASKS=', Uppercase(GetCmdTail)) > 0) then Exit;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
    'Blackveil', ExistingRun) and (ExistingRun <> '') then
    WizardSelectTasks('autostart')
  else
    WizardSelectTasks('!autostart');
end;
