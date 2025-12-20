using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Newtonsoft.Json;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin; 

public class LocalizedString {
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;

    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
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

        string? json = null;
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
            _localizationStrings = JsonConvert.DeserializeObject<SortedDictionary<string, LocalizedString>>(json) ?? [];
            currentLanguage = langCode;
        }
    }

    internal static string Localize(string key, string fallbackValue, string? description = null) {
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
            _localizationStrings = JsonConvert.DeserializeObject<SortedDictionary<string, LocalizedString>>(json) ?? [];
        } catch {
            //
        }
    }

    public static void ClearCache() {
        _localizationStrings.Clear();
    }


    private class CrowdinManifest {
        [JsonProperty("files")] public string[] Files;
        [JsonProperty("languages")] public string[] Languages;
        [JsonProperty("timestamp")] public ulong Timestamp;
        [JsonProperty("content")] public Dictionary<string, string[]> Content;
    }
    
    
    public static void UpdateTranslations(bool force = false, Action? callback = null) {
        DownloadError = null;
        var downloadPath = Service.PluginInterface.GetPluginLocDirectory();
        var config = SimpleTweaksPlugin.Plugin.PluginConfig;
        Task.Run(async () => {
            LoadingTranslations = true;
            try {
                var httpClient = Common.HttpClient;



                if (DateTime.Now - config.LanguageListUpdate > TimeSpan.FromMinutes(60) || force) {
                    Service.NotificationManager.AddNotification(new Notification() { Content = "Updating Language List", Minimized = true, InitialDuration = TimeSpan.FromSeconds(4)});
                    var manifestJson = await httpClient.GetStringAsync("https://distributions.crowdin.net/a20076cbde84bba34152668i8hw/manifest.json");
                    var manifest = JsonConvert.DeserializeObject<CrowdinManifest>(manifestJson);
                    if (manifest == null) return;
                    foreach (var l in manifest.Languages) {
                        config.LanguageUpdates.TryAdd(l, DateTime.MinValue);
                    }
                    
                    SimpleLog.Warning(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    config.LanguageListUpdate = DateTime.Now;
                }


                if (config.LanguageUpdates.TryGetValue(config.Language, out var updateTime)) {
                    if (DateTime.Now - updateTime > TimeSpan.FromMinutes(60) || force) {
                        Service.NotificationManager.AddNotification(new Notification() { Content = $"Updating Language: {config.Language}", Minimized = true, InitialDuration = TimeSpan.FromSeconds(4)});
                        var languageJson = await httpClient.GetStringAsync($"https://distributions.crowdin.net/a20076cbde84bba34152668i8hw/content/{config.Language}/strings.json");
                        var savePath = Path.Join(downloadPath, config.Language, "strings.json");

                        var dir = Path.GetDirectoryName(savePath);

                        if (!string.IsNullOrWhiteSpace(dir)) {
                            new DirectoryInfo(dir).Create();
                            await File.WriteAllTextAsync(savePath, languageJson);
                        }
                    }
                }
                
                
                
                if (callback != null) await Service.Framework.RunOnTick(callback);
                LoadingTranslations = false;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                LoadingTranslations = false;
                DownloadError = ex;
            }
        });
    }

    public static bool LoadingTranslations { get; private set; }
    public static Exception? DownloadError { get; private set; }
}