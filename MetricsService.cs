using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using Newtonsoft.Json;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin; 

public static unsafe class MetricsService {
    private static SimpleTweaksPlugin Plugin => SimpleTweaksPlugin.Plugin;
    private static SimpleTweaksPluginConfig Config => Plugin.PluginConfig;
    
    public static void ReportMetrics(bool allowFirstUse = false) {
        if (Config.AnalyticsOptOut) return;
        
        var identifier = Config.MetricsIdentifier;
        if (string.IsNullOrEmpty(identifier) || identifier.Length != 64) {
            if (!allowFirstUse) return;
            try {

                var idStr = "SimpleTweaksMetrics:";

                var userDir = Framework.Instance()->UserPath;

                var dir = new DirectoryInfo(userDir);
                
                if (!dir.Exists) return;

                DirectoryInfo? oldest = null;
                
                foreach (var d in dir.GetDirectories()) {
                    if (!d.Name.StartsWith("FFXIV_CHR")) continue;
                    if (oldest == null || d.CreationTime < oldest.CreationTime) oldest = d;
                }

                if (oldest == null) return;

                idStr += oldest.Name[16..];

                using var hash = SHA256.Create();
                var result = hash.ComputeHash(Encoding.UTF8.GetBytes(idStr));
                Config.MetricsIdentifier = identifier = BitConverter.ToString(result).Replace("-", "");
                Config.Save();
                
                SimpleLog.Verbose($"Created Metrics User Hash: [{identifier.Length}] {identifier}");
            } catch (Exception ex) {
                SimpleTweaksPlugin.Plugin.Error(ex, "Error reporting metrics.");
                return;
            }
        }

        if (string.IsNullOrEmpty(identifier) || identifier.Length != 64) return;


        var payload = new MetricsPayload {
            Identifier = identifier
        };


        void ParseTweakList(IEnumerable<BaseTweak> tweaks) {
            foreach (var t in tweaks) {
                if (t.TweakProvider is CustomTweakProvider) continue;
                
#if DEBUG
                if (ImGui.GetIO().KeyShift) {
                    if (t is not SubTweakManager { AlwaysEnabled: true }) payload.TweakNames.Add(t.Key, t.Name);
                }
#endif
                if (t is SubTweakManager stm) {
                    
                    if (t.Enabled || stm.AlwaysEnabled) {
                        if (!stm.AlwaysEnabled) {
                            payload.EnabledTweaks.Add(t.Key);

                        }
                        ParseTweakList(stm.GetTweakList());
                    }
                    continue;
                }
                
                if (!t.Enabled) continue;
                payload.EnabledTweaks.Add(t.Key);

            }
        }
        
        ParseTweakList(Plugin.Tweaks);
        
        var json = JsonConvert.SerializeObject(payload, Formatting.None, new JsonSerializerSettings() {
            TypeNameHandling = TypeNameHandling.None
        });

        Common.HttpClient.PostAsync("https://metrics.simpletweaks.app/report", new StringContent(json)).ConfigureAwait(false);
    }
    
    public class MetricsPayload {
        public string Identifier;
        public List<string> EnabledTweaks = new();

#if DEBUG
        public bool ShouldSerializeTweakNames() => TweakNames.Count > 0;
        public Dictionary<string, string> TweakNames = new();
#endif

    }
}
