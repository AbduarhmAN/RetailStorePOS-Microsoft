using System;

namespace RetailStorePOS.Contracts;

public interface INavigationRegistry
{
    void RegisterRoute(string route, Type viewType);
    
    void NavigateTo(string route, object? parameter = null);
}
