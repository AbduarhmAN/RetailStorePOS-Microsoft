; Offline bootstrapper installer for framework-dependent deployment.

#define BootstrapIsOffline 1
#define MyBootstrapAppId "{{4B9E55FA-6A17-4B55-94A5-B7916D4D6A84}"

[Setup]
OutputDir=artifacts\installer-bootstrap-offline
OutputBaseFilename={#MyOutputBaseFilename}

#include "setup_bootstrap_common.iss"
