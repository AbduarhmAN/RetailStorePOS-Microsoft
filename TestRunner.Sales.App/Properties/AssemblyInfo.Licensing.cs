using System.Runtime.CompilerServices;

// Allow the LicensingTests project to exercise the internal canonicalization
// and JWK-import code paths directly. Public surface of the licensing module
// stays minimal; tests only need the internals.
[assembly: InternalsVisibleTo("LicensingTests")]
