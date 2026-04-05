using System.Collections.Concurrent;
using System.IO;
using Microsoft.Web.WebView2.Core;
using RetailStorePOS.Data;

namespace RetailStorePOS.App;

internal static class WebView2EnvironmentFactory
{
    private static readonly ConcurrentDictionary<string, Task<CoreWebView2Environment>> Environments = new(StringComparer.OrdinalIgnoreCase);

    public static CoreWebView2EnvironmentOptions CreateShellOptions()
    {
        return new CoreWebView2EnvironmentOptions(
            additionalBrowserArguments: "--renderer-process-limit=1 " +
                                        "--disable-features=AudioServiceOutOfProcess,NetworkServiceInProcess,Translate,OptimizationHints,MediaRouter " +
                                        "--disable-extensions " +
                                        "--disable-gpu-shader-disk-cache " +
                                        "--js-flags=\"--max-old-space-size=96\"");
    }

    public static Task<CoreWebView2Environment> PreloadAsync(string profileFolderName, CoreWebView2EnvironmentOptions options)
    {
        return CreateAsync(profileFolderName, options);
    }

    public static Task<CoreWebView2Environment> CreateAsync(string profileFolderName, CoreWebView2EnvironmentOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolderName);
        ArgumentNullException.ThrowIfNull(options);

        return Environments.GetOrAdd(profileFolderName, _ => CreateEnvironmentCoreAsync(profileFolderName, options));
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentCoreAsync(string profileFolderName, CoreWebView2EnvironmentOptions options)
    {
        var userDataFolder = AppDataPaths.Combine("WebView2", profileFolderName);
        Directory.CreateDirectory(userDataFolder);

        return CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
    }
}
