using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace CompasPb;

public static class PackageInfo
{
    /// <summary>
    /// C# library version (from assembly)
    /// </summary>
    public static readonly string Version =
        typeof(PackageInfo)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    /// <summary>
    /// COMPAS_PB (Python) version from external JSON file
    /// </summary>
    public static readonly string CompasPbVersion;

    static PackageInfo()
    {
        string filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Resources",
            "COMPAS_PB_VERSION.json"
        );

        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
            CompasPbVersion =
                data != null && data.TryGetValue("version", out var version) ? version : "unknown";
        }
        else
        {
            CompasPbVersion = "unknown";
        }
    }
}
