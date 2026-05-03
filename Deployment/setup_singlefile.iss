; Dedicated Inno Setup script for the single-file standalone EXE lane.

#ifndef MyAppName
  #error MyAppName is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppVersion
  #error MyAppVersion is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppPublisher
  #error MyAppPublisher is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppExeName
  #error MyAppExeName is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyAppExeSourcePath
  #error MyAppExeSourcePath is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyOutputBaseFilename
  #error MyOutputBaseFilename is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MySetupIconFile
  #error MySetupIconFile is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyLicenseFile
  #error MyLicenseFile is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyWizardImageFile
  #error MyWizardImageFile is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

#ifndef MyWizardSmallImageFile
  #error MyWizardSmallImageFile is required. Run build-singlefile-installer.ps1 so metadata is injected automatically.
#endif

[Setup]
AppId={{14A5B548-6B24-447E-86DA-D96F702D4D4F}
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
OutputDir=artifacts\installer-singlefile
OutputBaseFilename={#MyOutputBaseFilename}
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
Source: "{#MyAppExeSourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppPublisher}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
#include "upgrade_remove_previous_versions.inc.iss"
#include "uninstall_remove_userdata.inc.iss"
