using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CompasPb
{
    public static class PackageInfo
    {
        public static readonly string Version;

        static PackageInfo()
        {
            string currentDir = Directory.GetCurrentDirectory();
            string filePath = Path.Combine(currentDir, "Resources", "COMPAS_PB_VERSION.json");
            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
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
}
