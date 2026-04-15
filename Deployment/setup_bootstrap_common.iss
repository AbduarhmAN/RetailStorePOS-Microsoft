; Shared Inno Setup implementation for framework-dependent bootstrap installers.

#ifndef BootstrapIsOffline
  #error BootstrapIsOffline is required before including setup_bootstrap_common.iss.
#endif

#ifndef MyBootstrapAppId
  #error MyBootstrapAppId is required before including setup_bootstrap_common.iss.
#endif

#ifndef MyAppName
  #error MyAppName is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyAppVersion
  #error MyAppVersion is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyAppPublisher
  #error MyAppPublisher is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyAppExeName
  #error MyAppExeName is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyOutputBaseFilename
  #error MyOutputBaseFilename is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MySetupIconFile
  #error MySetupIconFile is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyLicenseFile
  #error MyLicenseFile is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWizardImageFile
  #error MyWizardImageFile is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWizardSmallImageFile
  #error MyWizardSmallImageFile is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyPublishDir
  #error MyPublishDir is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyDotNetDesktopRuntimeDisplayName
  #error MyDotNetDesktopRuntimeDisplayName is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyDotNetDesktopRuntimeMinimumVersion
  #error MyDotNetDesktopRuntimeMinimumVersion is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyDotNetDesktopRuntimeFilename
  #error MyDotNetDesktopRuntimeFilename is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyDotNetDesktopRuntimeSilentArgs
  #error MyDotNetDesktopRuntimeSilentArgs is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWindowsAppRuntimeDisplayName
  #error MyWindowsAppRuntimeDisplayName is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWindowsAppRuntimeMinimumVersion
  #error MyWindowsAppRuntimeMinimumVersion is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWindowsAppRuntimeFilename
  #error MyWindowsAppRuntimeFilename is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWindowsAppRuntimeSilentArgs
  #error MyWindowsAppRuntimeSilentArgs is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyWindowsAppRuntimePackageNamePattern
  #error MyWindowsAppRuntimePackageNamePattern is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyVisualCppRedistributableDisplayName
  #error MyVisualCppRedistributableDisplayName is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyVisualCppRedistributableFilename
  #error MyVisualCppRedistributableFilename is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#ifndef MyVisualCppRedistributableSilentArgs
  #error MyVisualCppRedistributableSilentArgs is required. Run the bootstrap build scripts so metadata is injected automatically.
#endif

#if BootstrapIsOffline == 0
  #ifndef MyDotNetDesktopRuntimeUrl
    #error MyDotNetDesktopRuntimeUrl is required for the online bootstrap installer.
  #endif

  #ifndef MyWindowsAppRuntimeUrl
    #error MyWindowsAppRuntimeUrl is required for the online bootstrap installer.
  #endif

  #ifndef MyVisualCppRedistributableUrl
    #error MyVisualCppRedistributableUrl is required for the online bootstrap installer.
  #endif
#else
  #ifndef MyDotNetDesktopRuntimeSourcePath
    #error MyDotNetDesktopRuntimeSourcePath is required for the offline bootstrap installer.
  #endif

  #ifndef MyWindowsAppRuntimeSourcePath
    #error MyWindowsAppRuntimeSourcePath is required for the offline bootstrap installer.
  #endif

  #ifndef MyVisualCppRedistributableSourcePath
    #error MyVisualCppRedistributableSourcePath is required for the offline bootstrap installer.
  #endif
#endif

[Setup]
AppId={#MyBootstrapAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#MyLicenseFile}
PrivilegesRequired=lowest
SetupIconFile={#MySetupIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0.0
VersionInfoProductVersion={#MyAppVersion}.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
WizardImageFile={#MyWizardImageFile}
WizardSmallImageFile={#MyWizardSmallImageFile}
Compression=lzma2/ultra64
SolidCompression=yes
UseSetupLdr=x64
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
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
#if BootstrapIsOffline != 0
Source: "{#MyDotNetDesktopRuntimeSourcePath}"; Flags: dontcopy
Source: "{#MyWindowsAppRuntimeSourcePath}"; Flags: dontcopy
Source: "{#MyVisualCppRedistributableSourcePath}"; Flags: dontcopy
#endif

[Icons]
Name: "{autoprograms}\{#MyAppPublisher}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app_icon.ico"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app_icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  NewLine = #13#10;
  PreferencesPath = '{localappdata}\RetailStorePos\preferences.json';
  ResultCodeUserCanceled = 1223;
  ResultCodeRebootRequired = 3010;
  ResultCodeRebootInitiated = 1641;

var
  InstallId: String;
  InstalledAtUtc: String;
  SetupCompletedAtUtc: String;
  SetupCompletedEventId: String;
  PrerequisiteSelectionPage: TWizardPage;
  PrerequisiteIntroLabel: TNewStaticText;
  DotNetDesktopRuntimeCheckBox: TNewCheckBox;
  VisualCppRedistributableCheckBox: TNewCheckBox;
  WindowsAppRuntimeCheckBox: TNewCheckBox;
  MissingDotNetDesktopRuntime: Boolean;
  MissingVisualCppRedistributable: Boolean;
  MissingWindowsAppRuntime: Boolean;
  SelectedDotNetDesktopRuntime: Boolean;
  SelectedVisualCppRedistributable: Boolean;
  SelectedWindowsAppRuntime: Boolean;

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

function ExtractVersionPart(const VersionText: String; const PartIndex: Integer): Integer;
var
  CurrentPart: Integer;
  I: Integer;
  PartStart: Integer;
  PartText: String;
begin
  Result := 0;
  CurrentPart := 0;
  PartStart := 1;

  for I := 1 to Length(VersionText) + 1 do
  begin
    if (I > Length(VersionText)) or (VersionText[I] = '.') then
    begin
      if CurrentPart = PartIndex then
      begin
        PartText := Copy(VersionText, PartStart, I - PartStart);
        if PartText = '' then
        begin
          Result := 0;
        end;
        try
          Result := StrToInt(PartText);
        except
          Result := 0;
        end;
        exit;
      end;

      CurrentPart := CurrentPart + 1;
      PartStart := I + 1;
    end;
  end;
end;

function CompareVersionText(const LeftValue: String; const RightValue: String): Integer;
var
  I: Integer;
  LeftPart: Integer;
  RightPart: Integer;
begin
  for I := 0 to 3 do
  begin
    LeftPart := ExtractVersionPart(LeftValue, I);
    RightPart := ExtractVersionPart(RightValue, I);

    if LeftPart < RightPart then
    begin
      Result := -1;
      exit;
    end;

    if LeftPart > RightPart then
    begin
      Result := 1;
      exit;
    end;
  end;

  Result := 0;
end;

function RemoveTrailingWildcard(const Value: String): String;
var
  WildcardPosition: Integer;
begin
  WildcardPosition := Pos('*', Value);
  if WildcardPosition > 0 then
  begin
    Result := Copy(Value, 1, WildcardPosition - 1);
  end
  else
  begin
    Result := Value;
  end;

  while (Length(Result) > 0) and (Result[Length(Result)] = '.') do
  begin
    Delete(Result, Length(Result), 1);
  end;
end;

function StartsWithText(const Prefix: String; const Value: String): Boolean;
begin
  if Length(Prefix) = 0 then
  begin
    Result := False;
    exit;
  end;

  Result :=
    (Length(Value) >= Length(Prefix)) and
    (CompareText(Copy(Value, 1, Length(Prefix)), Prefix) = 0);
end;

function HasRegistrySubkeyVersionAtLeast(
  const RootKey: Integer;
  const SubKeyName: String;
  const MinimumVersion: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if not RegGetSubkeyNames(RootKey, SubKeyName, Names) then
  begin
    exit;
  end;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if CompareVersionText(Names[I], MinimumVersion) >= 0 then
    begin
      Result := True;
      exit;
    end;
  end;
end;

function IsDotNetDesktopRuntimeInstalled: Boolean;
begin
  Result :=
    HasRegistrySubkeyVersionAtLeast(
      HKLM,
      'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
      '{#MyDotNetDesktopRuntimeMinimumVersion}') or
    HasRegistrySubkeyVersionAtLeast(
      HKLM,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
      '{#MyDotNetDesktopRuntimeMinimumVersion}');
end;

function IsVisualCppRedistributableInstalled: Boolean;
var
  InstalledValue: Cardinal;
begin
  Result :=
    (RegQueryDWordValue(
      HKLM64,
      'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
      'Installed',
      InstalledValue) and (InstalledValue = 1)) or
    (RegQueryDWordValue(
      HKLM32,
      'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
      'Installed',
      InstalledValue) and (InstalledValue = 1));
end;

function HasWindowsAppRuntimePackage(
  const RootKey: Integer;
  const SubKeyName: String;
  const PackageNamePattern: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  PatternPrefix: String;
begin
  Result := False;
  PatternPrefix := RemoveTrailingWildcard(PackageNamePattern);

  if not RegGetSubkeyNames(RootKey, SubKeyName, Names) then
  begin
    exit;
  end;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if StartsWithText(PatternPrefix, Names[I]) and (Pos('_x64__', Lowercase(Names[I])) > 0) then
    begin
      Result := True;
      exit;
    end;
  end;
end;

function IsWindowsAppRuntimeInstalled: Boolean;
begin
  Result :=
    HasWindowsAppRuntimePackage(
      HKLM64,
      'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications',
      '{#MyWindowsAppRuntimePackageNamePattern}') or
    HasWindowsAppRuntimePackage(
      HKLM64,
      'SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages',
      '{#MyWindowsAppRuntimePackageNamePattern}');
end;

procedure RefreshMissingPrerequisites;
begin
  MissingDotNetDesktopRuntime := not IsDotNetDesktopRuntimeInstalled;
  MissingVisualCppRedistributable := not IsVisualCppRedistributableInstalled;
  MissingWindowsAppRuntime := not IsWindowsAppRuntimeInstalled;
end;

function HasMissingPrerequisites: Boolean;
begin
  Result :=
    MissingDotNetDesktopRuntime or
    MissingVisualCppRedistributable or
    MissingWindowsAppRuntime;
end;

procedure ConfigurePrerequisiteCheckBox(
  CheckBox: TNewCheckBox;
  const IsVisible: Boolean;
  const Caption: String;
  const IsChecked: Boolean;
  var CurrentTop: Integer);
begin
  CheckBox.Visible := IsVisible;
  if not IsVisible then
  begin
    exit;
  end;

  CheckBox.Caption := Caption;
  CheckBox.Checked := IsChecked;
  CheckBox.Top := CurrentTop;
  CurrentTop := CurrentTop + CheckBox.Height + ScaleY(10);
end;

function BuildPrerequisiteSelectionIntroText: String;
begin
  Result :=
    'Retail Store POS detected missing Microsoft components on this computer.' + NewLine + NewLine +
    'These components are important for the app to install and start correctly.' + NewLine +
    'They are selected by default, and you can check or uncheck them before continuing.';

#if BootstrapIsOffline == 0
  Result := Result + NewLine + NewLine +
    'If you keep them selected, setup will download the missing components directly from Microsoft during installation.';
#else
  Result := Result + NewLine + NewLine +
    'If you keep them selected, setup will install the bundled Microsoft prerequisite packages during installation.';
#endif
end;

procedure UpdatePrerequisiteSelectionPage;
var
  CurrentTop: Integer;
begin
  RefreshMissingPrerequisites;

  PrerequisiteIntroLabel.Caption := BuildPrerequisiteSelectionIntroText;
  CurrentTop := PrerequisiteIntroLabel.Top + PrerequisiteIntroLabel.Height + ScaleY(12);

  ConfigurePrerequisiteCheckBox(
    DotNetDesktopRuntimeCheckBox,
    MissingDotNetDesktopRuntime,
    '{#MyDotNetDesktopRuntimeDisplayName}',
    SelectedDotNetDesktopRuntime,
    CurrentTop);

  ConfigurePrerequisiteCheckBox(
    VisualCppRedistributableCheckBox,
    MissingVisualCppRedistributable,
    '{#MyVisualCppRedistributableDisplayName}',
    SelectedVisualCppRedistributable,
    CurrentTop);

  ConfigurePrerequisiteCheckBox(
    WindowsAppRuntimeCheckBox,
    MissingWindowsAppRuntime,
    '{#MyWindowsAppRuntimeDisplayName}',
    SelectedWindowsAppRuntime,
    CurrentTop);
end;

procedure CapturePrerequisiteSelections;
begin
  if MissingDotNetDesktopRuntime then
  begin
    SelectedDotNetDesktopRuntime := DotNetDesktopRuntimeCheckBox.Checked;
  end
  else
  begin
    SelectedDotNetDesktopRuntime := False;
  end;

  if MissingVisualCppRedistributable then
  begin
    SelectedVisualCppRedistributable := VisualCppRedistributableCheckBox.Checked;
  end
  else
  begin
    SelectedVisualCppRedistributable := False;
  end;

  if MissingWindowsAppRuntime then
  begin
    SelectedWindowsAppRuntime := WindowsAppRuntimeCheckBox.Checked;
  end
  else
  begin
    SelectedWindowsAppRuntime := False;
  end;
end;

function BuildSkippedPrerequisitesPrompt: String;
var
  ItemsText: String;
begin
  ItemsText := '';

  if MissingDotNetDesktopRuntime and (not SelectedDotNetDesktopRuntime) then
  begin
    ItemsText := ItemsText + ' - {#MyDotNetDesktopRuntimeDisplayName}' + NewLine;
  end;

  if MissingVisualCppRedistributable and (not SelectedVisualCppRedistributable) then
  begin
    ItemsText := ItemsText + ' - {#MyVisualCppRedistributableDisplayName}' + NewLine;
  end;

  if MissingWindowsAppRuntime and (not SelectedWindowsAppRuntime) then
  begin
    ItemsText := ItemsText + ' - {#MyWindowsAppRuntimeDisplayName}' + NewLine;
  end;

  Result :=
    'You left these missing components unchecked:' + NewLine + NewLine +
    ItemsText + NewLine +
    'Retail Store POS may not install or start correctly without them.' + NewLine + NewLine +
    'Do you want to continue anyway?';
end;

function HasUncheckedMissingPrerequisites: Boolean;
begin
  Result :=
    (MissingDotNetDesktopRuntime and (not SelectedDotNetDesktopRuntime)) or
    (MissingVisualCppRedistributable and (not SelectedVisualCppRedistributable)) or
    (MissingWindowsAppRuntime and (not SelectedWindowsAppRuntime));
end;

function AcquirePrerequisiteInstaller(
  const DisplayName: String;
  const FileName: String;
#if BootstrapIsOffline == 0
  const Url: String;
#endif
  var FailureMessage: String): String;
var
  ResultCode: Integer;
begin
  Result := ExpandConstant('{tmp}\' + FileName);

#if BootstrapIsOffline != 0
  try
    ExtractTemporaryFile(FileName);
  except
    FailureMessage := 'Setup could not extract the bundled prerequisite installer for ' + DisplayName + '.';
    Result := '';
    exit;
  end;
#else
  try
    DownloadTemporaryFile(Url, FileName, '', nil);
  except
    FailureMessage := 'Setup could not download the Microsoft prerequisite installer for ' + DisplayName + '.';
    Result := '';
    exit;
  end;

  if not FileExists(Result) then
  begin
    FailureMessage := 'Setup downloaded an incomplete prerequisite installer for ' + DisplayName + '.';
    Result := '';
    exit;
  end;
#endif
end;

function RunPrerequisiteInstaller(
  const InstallerPath: String;
  const InstallerArguments: String;
  const DisplayName: String;
  var NeedsRestart: Boolean;
  var FailureMessage: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;

  if not ShellExec(
    'runas',
    InstallerPath,
    InstallerArguments,
    '',
    SW_SHOWNORMAL,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    if ResultCode = ResultCodeUserCanceled then
    begin
      FailureMessage := 'Setup stopped because elevation for ' + DisplayName + ' was canceled.';
    end
    else
    begin
      FailureMessage := 'Setup could not launch ' + DisplayName + '.';
    end;
    exit;
  end;

  if ResultCode = 0 then
  begin
    Result := True;
    exit;
  end;

  if (ResultCode = ResultCodeRebootRequired) or (ResultCode = ResultCodeRebootInitiated) then
  begin
    NeedsRestart := True;
    FailureMessage :=
      DisplayName + ' was installed, but Windows must be restarted before Retail Store POS can be installed.';
    exit;
  end;

  FailureMessage := DisplayName + ' installation failed with exit code ' + IntToStr(ResultCode) + '.';
end;

function EnsureDotNetDesktopRuntime(
  var NeedsRestart: Boolean;
  var FailureMessage: String): Boolean;
var
  InstallerPath: String;
begin
  Result := True;
  if IsDotNetDesktopRuntimeInstalled then
  begin
    exit;
  end;

  InstallerPath := AcquirePrerequisiteInstaller(
    '{#MyDotNetDesktopRuntimeDisplayName}',
    '{#MyDotNetDesktopRuntimeFilename}',
#if BootstrapIsOffline == 0
    '{#MyDotNetDesktopRuntimeUrl}',
#endif
    FailureMessage);

  if InstallerPath = '' then
  begin
    Result := False;
    exit;
  end;

  if not RunPrerequisiteInstaller(
    InstallerPath,
    '{#MyDotNetDesktopRuntimeSilentArgs}',
    '{#MyDotNetDesktopRuntimeDisplayName}',
    NeedsRestart,
    FailureMessage) then
  begin
    Result := False;
    exit;
  end;

  if not IsDotNetDesktopRuntimeInstalled then
  begin
    FailureMessage := '{#MyDotNetDesktopRuntimeDisplayName} did not appear as installed after setup completed.';
    Result := False;
  end;
end;

function EnsureVisualCppRedistributable(
  var NeedsRestart: Boolean;
  var FailureMessage: String): Boolean;
var
  InstallerPath: String;
begin
  Result := True;
  if IsVisualCppRedistributableInstalled then
  begin
    exit;
  end;

  InstallerPath := AcquirePrerequisiteInstaller(
    '{#MyVisualCppRedistributableDisplayName}',
    '{#MyVisualCppRedistributableFilename}',
#if BootstrapIsOffline == 0
    '{#MyVisualCppRedistributableUrl}',
#endif
    FailureMessage);

  if InstallerPath = '' then
  begin
    Result := False;
    exit;
  end;

  if not RunPrerequisiteInstaller(
    InstallerPath,
    '{#MyVisualCppRedistributableSilentArgs}',
    '{#MyVisualCppRedistributableDisplayName}',
    NeedsRestart,
    FailureMessage) then
  begin
    Result := False;
    exit;
  end;

  if not IsVisualCppRedistributableInstalled then
  begin
    FailureMessage := '{#MyVisualCppRedistributableDisplayName} did not appear as installed after setup completed.';
    Result := False;
  end;
end;

function EnsureWindowsAppRuntime(
  var NeedsRestart: Boolean;
  var FailureMessage: String): Boolean;
var
  InstallerPath: String;
begin
  Result := True;
  if IsWindowsAppRuntimeInstalled then
  begin
    exit;
  end;

  InstallerPath := AcquirePrerequisiteInstaller(
    '{#MyWindowsAppRuntimeDisplayName}',
    '{#MyWindowsAppRuntimeFilename}',
#if BootstrapIsOffline == 0
    '{#MyWindowsAppRuntimeUrl}',
#endif
    FailureMessage);

  if InstallerPath = '' then
  begin
    Result := False;
    exit;
  end;

  if not RunPrerequisiteInstaller(
    InstallerPath,
    '{#MyWindowsAppRuntimeSilentArgs}',
    '{#MyWindowsAppRuntimeDisplayName}',
    NeedsRestart,
    FailureMessage) then
  begin
    Result := False;
    exit;
  end;

  if not IsWindowsAppRuntimeInstalled then
  begin
    FailureMessage := '{#MyWindowsAppRuntimeDisplayName} did not appear as installed after setup completed.';
    Result := False;
  end;
end;

function EnsurePrerequisites(var NeedsRestart: Boolean; var FailureMessage: String): Boolean;
begin
  Result := True;
  RefreshMissingPrerequisites;

  if MissingDotNetDesktopRuntime and SelectedDotNetDesktopRuntime then
  begin
    if not EnsureDotNetDesktopRuntime(NeedsRestart, FailureMessage) then
    begin
      Result := False;
      exit;
    end;
  end;

  RefreshMissingPrerequisites;
  if MissingVisualCppRedistributable and SelectedVisualCppRedistributable then
  begin
    if not EnsureVisualCppRedistributable(NeedsRestart, FailureMessage) then
    begin
      Result := False;
      exit;
    end;
  end;

  RefreshMissingPrerequisites;
  if MissingWindowsAppRuntime and SelectedWindowsAppRuntime then
  begin
    if not EnsureWindowsAppRuntime(NeedsRestart, FailureMessage) then
    begin
      Result := False;
      exit;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  FailureMessage: String;
begin
  Result := '';

  if not EnsurePrerequisites(NeedsRestart, FailureMessage) then
  begin
    Result := FailureMessage;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (CurPageID = PrerequisiteSelectionPage.ID) then
  begin
    CapturePrerequisiteSelections;
    if HasUncheckedMissingPrerequisites then
    begin
      if MsgBox(BuildSkippedPrerequisitesPrompt, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then
      begin
        Result := False;
        exit;
      end;
    end;
  end;

  if (CurPageID = wpLicense) then
  begin
    if InstallId = '' then
    begin
      InstallId := GenerateInstallId;
    end;

    WriteInitialPreferences();
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;

  if (PageID = PrerequisiteSelectionPage.ID) then
  begin
    UpdatePrerequisiteSelectionPage;
    Result := not HasMissingPrerequisites;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = PrerequisiteSelectionPage.ID) then
  begin
    UpdatePrerequisiteSelectionPage;
  end;
end;

procedure InitializeWizard;
begin
  SelectedDotNetDesktopRuntime := True;
  SelectedVisualCppRedistributable := True;
  SelectedWindowsAppRuntime := True;

  PrerequisiteSelectionPage := CreateCustomPage(
    wpSelectDir,
    'Microsoft Prerequisites',
    'Review required Microsoft components before continuing.');

  PrerequisiteIntroLabel := TNewStaticText.Create(WizardForm);
  PrerequisiteIntroLabel.Parent := PrerequisiteSelectionPage.Surface;
  PrerequisiteIntroLabel.Left := 0;
  PrerequisiteIntroLabel.Top := 0;
  PrerequisiteIntroLabel.Width := PrerequisiteSelectionPage.SurfaceWidth;
  PrerequisiteIntroLabel.Height := ScaleY(88);
  PrerequisiteIntroLabel.AutoSize := False;
  PrerequisiteIntroLabel.WordWrap := True;

  DotNetDesktopRuntimeCheckBox := TNewCheckBox.Create(WizardForm);
  DotNetDesktopRuntimeCheckBox.Parent := PrerequisiteSelectionPage.Surface;
  DotNetDesktopRuntimeCheckBox.Left := 0;
  DotNetDesktopRuntimeCheckBox.Width := PrerequisiteSelectionPage.SurfaceWidth;

  VisualCppRedistributableCheckBox := TNewCheckBox.Create(WizardForm);
  VisualCppRedistributableCheckBox.Parent := PrerequisiteSelectionPage.Surface;
  VisualCppRedistributableCheckBox.Left := 0;
  VisualCppRedistributableCheckBox.Width := PrerequisiteSelectionPage.SurfaceWidth;

  WindowsAppRuntimeCheckBox := TNewCheckBox.Create(WizardForm);
  WindowsAppRuntimeCheckBox.Parent := PrerequisiteSelectionPage.Surface;
  WindowsAppRuntimeCheckBox.Left := 0;
  WindowsAppRuntimeCheckBox.Width := PrerequisiteSelectionPage.SurfaceWidth;
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
