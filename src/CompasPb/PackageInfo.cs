using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace CompasPb;

public static class PackageInfo
{
    public static readonly string Version;

    /// <summary>
    /// COMPAS_PB (Python) version embedded from resources/COMPAS_PB_VERSION.json at build time.
    /// </summary>
    static PackageInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream("COMPAS_PB_VERSION.json");

        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            var data = JObject.Parse(content).ToObject<Dictionary<string, string>>();
            if (data != null && data.TryGetValue("version", out var version))
            {
                Version = version;
                return;
            }
        }

        Version = "unknown";
    }
}
