using Microsoft.UI.Xaml;
using RetailStorePOS.UI.Common;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TestRunner.Login.App;

public partial class App : Application
{
    private Window? m_window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Force the application and Win32 process into Arabic
        ForceArabicLanguage();

        // Use a sandbox database for the Test Runner so we always get a fresh Start
        LoginRuntime.Initialize("TestRunnerLoginDb");

        m_window = new MainWindow();
        m_window.Activate();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetProcessPreferredUILanguages(uint dwFlags, string pwszLanguagesBuffer, out uint pdwNumLanguages);

    private const uint MUI_LANGUAGE_NAME = 0x8;

    private void ForceArabicLanguage()
    {
        try
        {
            SetProcessPreferredUILanguages(MUI_LANGUAGE_NAME, "ar-SA\0", out _);
            LocalizationService.Instance.SetLanguage("ar-SA");
        }
        catch { }
    }

}
