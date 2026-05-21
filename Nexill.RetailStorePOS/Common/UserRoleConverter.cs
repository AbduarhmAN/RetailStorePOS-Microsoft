using System;
using Microsoft.UI.Xaml.Data;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Common;

public class UserRoleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isAdmin)
        {
            return isAdmin 
                ? LocalizationHelper.GetString("Users_Role_AdminLabel") 
                : LocalizationHelper.GetString("Users_Role_CashierLabel");
        }

        // Fallback for strings if someone passes "Administrator" or "Cashier"
        if (value is string role)
        {
            if (role == "Administrator") return LocalizationHelper.GetString("Users_Role_AdminLabel");
            if (role == "Cashier") return LocalizationHelper.GetString("Users_Role_CashierLabel");
        }

        return value ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
