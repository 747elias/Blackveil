#ifndef AppVersion
  #error AppVersion must be supplied from Directory.Build.props.
#endif
#ifndef SourceDir
  #error SourceDir is required.
#endif
#ifndef OutputDir
  #error OutputDir is required.
#endif

[Setup]
AppId={{3E29D02D-B2C1-44D7-8E90-C53380C6D534}
AppName=Blackveil
AppVersion={#AppVersion}
AppVerName=Blackveil {#AppVersion}
AppPublisher=Elias Hahn
AppPublisherURL=https://github.com/747elias/Blackveil
DefaultDirName={autopf}\Blackveil
DefaultGroupName=Blackveil
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableFinishedPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=no
UsePreviousLanguage=no
ShowLanguageDialog=no
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
WizardSizePercent=110
WizardImageFile=assets\wizard.bmp
WizardSmallImageFile=assets\wizard-small.bmp
WizardImageStretch=yes
WizardImageBackColor=$21160C
WizardSmallImageBackColor=$21160C
CloseApplications=yes
RestartApplications=no
AppMutex=Blackveil.Singleton
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start Blackveil with Windows"; Flags: unchecked; Check: IsPerUserInstall

[Files]
Source: "{#SourceDir}\Blackveil.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Blackveil"; Filename: "{app}\Blackveil.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Blackveil"; ValueData: """{app}\Blackveil.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue; Check: IsPerUserInstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Blackveil"; Tasks: not autostart; Flags: deletevalue uninsdeletevalue; Check: IsPerUserInstall

[Run]
Filename: "{app}\Blackveil.exe"; Description: "Launch Blackveil in the system tray"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
Type: files; Name: "{localappdata}\Blackveil\settings.json"; Check: IsPerUserInstall
Type: files; Name: "{localappdata}\Blackveil\update-settings.json"; Check: IsPerUserInstall
Type: files; Name: "{localappdata}\Blackveil\update-state.json"; Check: IsPerUserInstall
Type: files; Name: "{localappdata}\Blackveil\update-result.json"; Check: IsPerUserInstall
Type: filesandordirs; Name: "{localappdata}\Blackveil\updates"; Check: IsPerUserInstall
Type: filesandordirs; Name: "{localappdata}\Blackveil\logs"; Check: IsPerUserInstall

[Code]
function IsPerUserInstall: Boolean;
begin
  Result := not IsAdminInstallMode;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  ScopeText: String;
begin
  if IsAdminInstallMode then
    ScopeText := 'All users'
  else
    ScopeText := 'Just you';
  WizardForm.Caption := 'Blackveil {#AppVersion} · ' + ScopeText;
end;

procedure InitializeWizard;
var
  ExistingRun: String;
  ScopeHint: TNewStaticText;
begin
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.Font.Size := 10;
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Color := $332718;
  WizardForm.FinishedHeadingLabel.Font.Size := 22;
  WizardForm.PageNameLabel.Font.Color := $687700;
  WizardForm.PageDescriptionLabel.Font.Color := $7E705E;
  WizardForm.WelcomeLabel1.Caption := 'Your reveal.' + #13#10 + 'Your timing.';
  WizardForm.WelcomeLabel2.Caption := 'Keep your video out of sight until the right moment. One shortcut covers every display in black and holds ordinary input.' + #13#10 + #13#10 +
    'Install just for you or for all users. Your preferences always stay personal.';
  ScopeHint := TNewStaticText.Create(WizardForm);
  ScopeHint.Parent := WizardForm.SelectTasksPage;
  ScopeHint.AutoSize := False;
  ScopeHint.WordWrap := True;
  ScopeHint.SetBounds(ScaleX(0), ScaleY(125), WizardForm.SelectTasksPage.ClientWidth, ScaleY(85));
  ScopeHint.Font.Color := $7E705E;
  if IsAdminInstallMode then
    ScopeHint.Caption := 'System-wide installation: Enable personal startup later in the Blackveil tray menu. Updates require Windows administrator approval.'
  else
    ScopeHint.Caption := 'Everything stays in your account. Optional Windows startup runs in the background without turning blackout on.';
  if IsAdminInstallMode then Exit;
  if (Pos('/TASKS=', Uppercase(GetCmdTail)) > 0) or (Pos('/MERGETASKS=', Uppercase(GetCmdTail)) > 0) then Exit;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
    'Blackveil', ExistingRun) and (ExistingRun <> '') then
    WizardSelectTasks('autostart')
  else
    WizardSelectTasks('!autostart');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExistingRun, OwnExe: String;
begin
  if CurUninstallStep = usPostUninstall then begin
    OwnExe := '"' + ExpandConstant('{app}\Blackveil.exe') + '"';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Blackveil', ExistingRun) then
      if Pos(Lowercase(OwnExe), Lowercase(ExistingRun)) = 1 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Blackveil');
  end;
end;
