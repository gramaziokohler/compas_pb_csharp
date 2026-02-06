using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

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
        string currentDir = Directory.GetCurrentDirectory();
        string filePath = Path.Combine(currentDir, "Resources", "COMPAS_PB_VERSION.json");
        if (File.Exists(filePath))
        {
            string content = File.ReadAllText(filePath);
            var data = JObject.Parse(content).ToObject<Dictionary<string, string>>();
            if (data != null && data.ContainsKey("version"))
            {
                Version = data["version"];
                return;
            }
            Version = "unknown";
        }
        else
        {
            Version = "unknown";
        }
    }
}
