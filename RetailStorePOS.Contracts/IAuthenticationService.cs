using System;

namespace RetailStorePOS.Contracts;

public interface IAuthenticationService
{
    bool IsLoggedIn { get; }
    
    bool CanManageSettings { get; }
    
    event EventHandler? LoginStateChanged;
}
