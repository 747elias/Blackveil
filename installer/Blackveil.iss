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
DefaultDirName={autopf}\Blackveil
DefaultGroupName=Blackveil
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableFinishedPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=no
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
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "autostart"; Description: "Blackveil mit Windows starten"; Flags: unchecked; Check: IsPerUserInstall

[Files]
Source: "{#SourceDir}\Blackveil.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Blackveil"; Filename: "{app}\Blackveil.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Blackveil"; ValueData: """{app}\Blackveil.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue; Check: IsPerUserInstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Blackveil"; Tasks: not autostart; Flags: deletevalue uninsdeletevalue; Check: IsPerUserInstall

[Run]
Filename: "{app}\Blackveil.exe"; Description: "Blackveil im Infobereich starten"; Flags: nowait postinstall skipifsilent runasoriginaluser

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
    ScopeText := 'Für alle Benutzer'
  else
    ScopeText := 'Nur für dich';
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
  WizardForm.WelcomeLabel1.Caption := 'Dein Bildschirm.' + #13#10 + 'Dein Moment.';
  WizardForm.WelcomeLabel2.Caption := 'Blackveil blendet deine Bildschirme auf Tastendruck aus und hält gewöhnliche Eingaben zurück.' + #13#10 + #13#10 +
    'Wähle die Installation für dein Konto oder für alle Benutzer. Deine Einstellungen bleiben immer persönlich.';
  ScopeHint := TNewStaticText.Create(WizardForm);
  ScopeHint.Parent := WizardForm.SelectTasksPage;
  ScopeHint.AutoSize := False;
  ScopeHint.WordWrap := True;
  ScopeHint.SetBounds(ScaleX(0), ScaleY(125), WizardForm.SelectTasksPage.ClientWidth, ScaleY(85));
  ScopeHint.Font.Color := $7E705E;
  if IsAdminInstallMode then
    ScopeHint.Caption := 'Systemweite Installation: Den persönlichen Autostart kannst du nach der Installation im Blackveil-Tray-Menü aktivieren. Updates brauchen eine Windows-Bestätigung.'
  else
    ScopeHint.Caption := 'Alles bleibt in deinem Konto. Blackveil startet auf Wunsch nur im Hintergrund - ohne Schwarzbild.';
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
