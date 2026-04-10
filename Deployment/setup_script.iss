; Script generated for Inno Setup script files.

#ifndef MyAppName
  #error MyAppName is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppVersion
  #error MyAppVersion is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppPublisher
  #error MyAppPublisher is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppExeName
  #error MyAppExeName is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyOutputBaseFilename
  #error MyOutputBaseFilename is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MySetupIconFile
  #error MySetupIconFile is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyLicenseFile
  #error MyLicenseFile is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyWizardImageFile
  #error MyWizardImageFile is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyWizardSmallImageFile
  #error MyWizardSmallImageFile is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyPublishDir
  #error MyPublishDir is required. Run build-uptodown-installer.ps1 so metadata is injected automatically.
#endif

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Require accepting terms
LicenseFile={#MyLicenseFile}
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
OutputDir=artifacts\installer
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile={#MySetupIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0.0
VersionInfoProductVersion={#MyAppVersion}.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
WizardImageFile={#MyWizardImageFile}
WizardSmallImageFile={#MyWizardSmallImageFile}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to the Nexill Retail Store POS Setup
WelcomeLabel2=This wizard will guide you through the installation of the Nexill point-of-sale system on your machine.%n%nIt is recommended that you close all other applications before starting the installation to ensure a smooth process.
SelectDirDesc=Choose Installation Directory
SelectDirLabel3=Nexill Setup will install Retail Store POS into the following location.
SelectTasksDesc=Customize Your Nexill Installation
SelectTasksLabel2=Select the additional shortcuts or tasks you would like Nexill Setup to perform, then click Next.
ReadyLabel1=Ready to Install Nexill POS
ReadyLabel2a=Click Install to begin the Nexill POS installation, or click Back to review your setup configuration.
FinishedHeadingLabel=Nexill Installation Complete
FinishedLabel=Nexill Setup has successfully installed Retail Store POS on your device. The application is ready to use.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Ship the full WinUI publish folder so the installer contains every runtime dependency.
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppPublisher}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app_icon.ico"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app_icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  PreferencesPath = '{localappdata}\RetailStorePos\preferences.json';

var
  InstallId: String;
  InstalledAtUtc: String;
  SetupCompletedAtUtc: String;
  SetupCompletedEventId: String;

function GenerateInstallId: String;
var
  TypeLib: Variant;
  RawGuid: String;
begin
  TypeLib := CreateOleObject('Scriptlet.TypeLib');
  RawGuid := TypeLib.Guid;
  Result := Copy(RawGuid, 2, Length(RawGuid) - 2);
end;

function JsonStringOrNull(const Value: String): String;
begin
  if Value = '' then
  begin
    Result := 'null';
  end
  else
  begin
    Result := '"' + Value + '"';
  end;
end;

function BuildInitialPreferencesJson(
  const InstallId,
  InstalledAtUtc,
  SetupCompletedAtUtc,
  SetupCompletedEventId: String): String;
begin
  Result :=
    '{' + #13#10 +
    '  "isDarkMode": false,' + #13#10 +
    '  "quickCash": [5.0, 10.0, 20.0],' + #13#10 +
    '  "installId": "' + InstallId + '",' + #13#10 +
    '  "installedAtUtc": ' + JsonStringOrNull(InstalledAtUtc) + ',' + #13#10 +
    '  "setupCompletedAtUtc": ' + JsonStringOrNull(SetupCompletedAtUtc) + ',' + #13#10 +
    '  "firstRunAt": null,' + #13#10 +
    '  "lastSeenAt": null,' + #13#10 +
    '  "launchCount": 0,' + #13#10 +
    '  "crashCount": 0,' + #13#10 +
    '  "lastUpdatePromptAt": null,' + #13#10 +
    '  "lastUpdateInstalledAt": null,' + #13#10 +
    '  "setupCompletedEventId": ' + JsonStringOrNull(SetupCompletedEventId) + #13#10 +
    '}' + #13#10;
end;

procedure WriteInitialPreferences;
var
  PreferencesDir: String;
begin
  PreferencesDir := ExpandConstant('{localappdata}\RetailStorePos');
  if not DirExists(PreferencesDir) then
  begin
    ForceDirectories(PreferencesDir);
  end;

  SaveStringToFile(
    ExpandConstant(PreferencesPath),
    BuildInitialPreferencesJson(
      InstallId,
      InstalledAtUtc,
      SetupCompletedAtUtc,
      SetupCompletedEventId),
    False);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  // Seed the local install identifier as soon as the user continues past the license page.
  if (CurPageID = wpLicense) then
  begin
    if InstallId = '' then
    begin
      InstallId := GenerateInstallId;
    end;

    WriteInitialPreferences();
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
  begin
    if InstallId = '' then
    begin
      InstallId := GenerateInstallId;
    end;

    if SetupCompletedAtUtc = '' then
    begin
      SetupCompletedAtUtc := GetDateTimeString('yyyy-mm-dd"T"hh:nn:ss', '-', ':');
    end;

    InstalledAtUtc := SetupCompletedAtUtc;

    if SetupCompletedEventId = '' then
    begin
      SetupCompletedEventId := GenerateInstallId;
    end;

    WriteInitialPreferences();
  end;
end;
