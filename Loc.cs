using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SimpleTweaksPlugin; 

public class LocalizedString {
    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }
}

internal static class Loc {
    private static SortedDictionary<string, LocalizedString> _localizationStrings = new();
    private static string currentLanguage = "en";

    internal static void LoadLanguage(string langCode) {
        currentLanguage = "en";
        _localizationStrings = new SortedDictionary<string, LocalizedString>();
        if (langCode == "en") return;
        if (langCode == "DEBUG") {
            currentLanguage = "DEBUG";
            return;
        }

        string json = null;
        var locDir = Service.PluginInterface.GetPluginLocDirectory();
        if (string.IsNullOrWhiteSpace(locDir)) return;


        var langFile = Path.Combine(locDir, $"{langCode}/strings.json");
        if (File.Exists(langFile)) {
            json = File.ReadAllText(langFile);
        } else {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SimpleTweaksPlugin.Localization.{langCode}.json");
            if (s != null) {
                using var sr = new StreamReader(s);
                json = sr.ReadToEnd();
                if (!Directory.Exists(locDir)) Directory.CreateDirectory(locDir);
                File.WriteAllText(langFile, json);
            }
        }

        if (!string.IsNullOrWhiteSpace(json)) {
            _localizationStrings = JsonConvert.DeserializeObject<SortedDictionary<string, LocalizedString>>(json);
            currentLanguage = langCode;
        }
    }

    internal static string Localize(string key, string fallbackValue, string description = null) {
        if (currentLanguage == "DEBUG") return $"#{key}#";
        try {
            return _localizationStrings[key].Message;
        } catch {

            _localizationStrings[key] = new LocalizedString() {
                Message = fallbackValue,
                Description = description ?? $"{key} - {fallbackValue}"
            };
            return fallbackValue;
        }
    }

    internal static string ExportLoadedDictionary() {
        return JsonConvert.SerializeObject(_localizationStrings, Formatting.Indented);
    }

    internal static void ImportDictionary(string json) {
        try {
            _localizationStrings = JsonConvert.DeserializeObject<SortedDictionary<string, LocalizedString>>(json);
        } catch {
            //
        }
    }

    public static void ClearCache() {
        _localizationStrings.Clear();
    }

    public static void UpdateTranslations() {
        DownloadError = null;
        var downloadPath = Service.PluginInterface.GetPluginLocDirectory();
        var zipFile = Path.Join(downloadPath, "loc.zip");
        Task.Run(() => {
            LoadingTranslations = true;
            try {
                var webClient = new WebClient();
                webClient.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
                webClient.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                webClient.DownloadFile(new Uri("https://crowdin.com/backend/download/project/simpletweaks.zip"), zipFile);
                ZipFile.ExtractToDirectory(zipFile, downloadPath, true);

                File.Delete(zipFile);

                LoadingTranslations = false;
            } catch (Exception ex) {
                LoadingTranslations = false;
                DownloadError = ex;
            }

        });
    }

    public static bool LoadingTranslations { get; private set; }
    public static Exception DownloadError { get; private set; }
}