using System;
using System.Runtime.InteropServices;

namespace RetailStorePOS.UI.Common.Services;

public static class WindowInteropService
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();
}
