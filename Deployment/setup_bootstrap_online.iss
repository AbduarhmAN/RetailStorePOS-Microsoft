; Online bootstrapper installer for framework-dependent deployment.

#define BootstrapIsOffline 0
#define MyBootstrapAppId "{{8F6D3500-9364-4F6D-A77A-4A997D60A41A}"

[Setup]
OutputDir=artifacts\installer-bootstrap-online
OutputBaseFilename={#MyOutputBaseFilename}

#include "setup_bootstrap_common.iss"
