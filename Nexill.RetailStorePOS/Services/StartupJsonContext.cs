using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetailStorePOS.App.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LocalPreferences), TypeInfoPropertyName = nameof(LocalPreferences))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "StringDictionary")]
internal sealed partial class StartupJsonContext : JsonSerializerContext
{
}
