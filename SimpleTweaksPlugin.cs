using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using Dalamud;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Utility;
using Task = System.Threading.Tasks.Task;
#if DEBUG
using System.Runtime.CompilerServices;
#endif

#pragma warning disable CS0659
namespace SimpleTweaksPlugin {
    public class SimpleTweaksPlugin : IDalamudPlugin {
        public string Name => "Simple Tweaks";
        public SimpleTweaksPluginConfig PluginConfig { get; private set; }

        public List<TweakProvider> TweakProviders = new();

        public IconManager IconManager { get; private set; }

        public string AssemblyLocation { get; private set; } = Assembly.GetExecutingAssembly().Location;
        
        public static SimpleTweaksPlugin Plugin { get; private set; }

        private CultureInfo setCulture = null;

        public bool LoadingTranslations { get; private set; } = false;

        public IEnumerable<BaseTweak> Tweaks => TweakProviders.Where(tp => !tp.IsDisposed).SelectMany(tp => tp.Tweaks).OrderBy(t => t.Name);

        public readonly ConfigWindow ConfigWindow = new ConfigWindow();
        public readonly DebugWindow DebugWindow = new DebugWindow();
        public readonly WindowSystem WindowSystem = new WindowSystem("SimpleTweaksPlugin");
        public readonly Changelog ChangelogWindow = new();
        
        
        internal CultureInfo Culture {
            get {
                if (setCulture != null) return setCulture;
                if (string.IsNullOrEmpty(PluginConfig.CustomCulture)) return setCulture = CultureInfo.CurrentUICulture;

                try {
                    var culture = CultureInfo.GetCultureInfo(PluginConfig.CustomCulture);
                    return setCulture = culture;
                } catch {
                    //
                }

                return setCulture = CultureInfo.CurrentUICulture;
            }
            set => setCulture = value;
        }

        public void Dispose() {
            SimpleLog.Debug("Dispose");
            Service.Framework.Update -= FrameworkOnUpdate;
            Service.PluginInterface.UiBuilder.Draw -= this.BuildUI;
            RemoveCommands();

            foreach (var t in TweakProviders.Where(t => !t.IsDisposed)) {
                t.Dispose();
            }
            TweakProviders.Clear();
            DebugManager.Dispose();
            foreach (var hook in Common.HookList.Where(hook => !hook.IsDisposed)) {
                if (hook.IsEnabled) hook.Disable();
                hook.Dispose();
            }
            Common.HookList.Clear();
            Common.Shutdown();
            TooltipManager.Destroy();
            SimpleEvent.Destroy();
        }

        public int UpdateFrom = -1;

        public SimpleTweaksPlugin(IDalamudPluginInterface pluginInterface) {
            Plugin = this;
            pluginInterface.Create<Service>();
            pluginInterface.Create<SimpleLog>();
            pluginInterface.Create<Common>();
            
            this.PluginConfig = (SimpleTweaksPluginConfig)Service.PluginInterface.GetPluginConfig() ?? new SimpleTweaksPluginConfig();
            this.PluginConfig.Init(this);
            
#if !DEBUG
            SimpleLog.SetupBuildPath();
            Task.Run(() => {
                FFXIVClientStructs.Interop.Resolver.GetInstance.SetupSearchSpace(Service.SigScanner.SearchBase);
                FFXIVClientStructs.Interop.Resolver.GetInstance.Resolve();
                UpdateBlacklist();
                Service.Framework.RunOnFrameworkThread(Initialize);
            });
#else
            Task.Run(() => {
                UpdateBlacklist();
                Service.Framework.RunOnFrameworkThread(Initialize);
            });
#endif
        }


        private void UpdateBlacklist() {
            try {
                // Update Tweak Blacklist
                var httpClient = Common.HttpClient;
                var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, "https://raw.githubusercontent.com/Caraxi/SimpleTweaksPlugin/main/tweakBlacklist.txt"));
                if (response.StatusCode != HttpStatusCode.OK) return;
                var asStringTask = response.Content.ReadAsStringAsync();
                asStringTask.Wait();
                if (!asStringTask.IsCompletedSuccessfully) return;
                var blacklistedTweaksString = asStringTask.Result;
                SimpleLog.Log("Tweak Blacklist:\n" + blacklistedTweaksString);
                var blacklistedTweaks = new List<string>();
                foreach (var l in blacklistedTweaksString.Split("\n")) {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    blacklistedTweaks.Add(l.Trim());
                }
                PluginConfig.BlacklistedTweaks = blacklistedTweaks;
            } catch {
                //
            }
        }
        
        private void Initialize() {

            IconManager = new IconManager();

            SetupLocalization();

            UiHelper.Setup(Service.SigScanner);

            DebugManager.SetPlugin(this);

            Common.Setup();

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(DebugWindow);
            WindowSystem.AddWindow(ChangelogWindow);
            Changelog.AddGeneralChangelogs();
            
            Service.PluginInterface.UiBuilder.Draw += this.BuildUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;

            SetupCommands();

            var simpleTweakProvider = new TweakProvider(Assembly.GetExecutingAssembly());
            simpleTweakProvider.LoadTweaks();
            TweakProviders.Add(simpleTweakProvider);

            foreach (var provider in PluginConfig.CustomTweakProviders) {
                LoadCustomProvider(provider);
            }


#if DEBUG
            if (!PluginConfig.DisableAutoOpen) {
                DebugWindow.IsOpen = true;
                ConfigWindow.IsOpen = true;
            }
#endif
            DebugManager.Reload();


            Service.Framework.Update += FrameworkOnUpdate;
            
            MetricsService.ReportMetrics();
        }
        

        private void FrameworkOnUpdate(IFramework framework) => Common.InvokeFrameworkUpdate();

        public void SetupLocalization() {
            this.PluginConfig.Language ??= Service.ClientState.ClientLanguage switch {
                ClientLanguage.English => "en",
                ClientLanguage.French => "fr",
                ClientLanguage.German => "de",
                ClientLanguage.Japanese => "ja",
                _ => "en"
            };

            Loc.LoadLanguage(PluginConfig.Language);
            foreach (var t in Tweaks) t.LanguageChanged();
        }

        public void SetupCommands() {
            Service.Commands.AddHandler("/tweaks", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}",
                ShowInHelp = true
            });
        }

        private void OnOpenConfig() {
            if (ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl) {
                DebugWindow.UnCollapseOrToggle();
                return;
            }
            OnConfigCommandHandler(null, null);
        }

        public void OnConfigCommandHandler(object command, object args) {
            if (args is string argString) {
                if (argString == "Debug") {
                    DebugWindow.UnCollapseOrToggle();
                    return;
                }

                if (!string.IsNullOrEmpty(argString.Trim())) {
                    var splitArgString = argString.Split(' ');
                    switch (splitArgString[0].ToLowerInvariant()) {
                        case "cl":
                        case "changes":
                        case "changelog": {
                            ChangelogWindow.IsOpen = !ChangelogWindow.IsOpen;
                            return;
                        }
                        case "t":
                        case "toggle": {
                            if (splitArgString.Length < 2) {
                                Service.Chat.PrintError("/tweaks toggle <tweakid>");
                                return;
                            }
                            var tweak = GetTweakById(splitArgString[1]);
                            if (tweak != null) {
                                if (tweak.Enabled) {
                                    tweak.InternalDisable();
                                    if (PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Remove(tweak.Key);
                                    }

                                    Service.NotificationManager.AddNotification(new Notification { Content = $"Disabled {tweak.Name}", Title = "Simple Tweaks", Type = NotificationType.Info });
                                } else {
                                    tweak.InternalEnable();
                                    if (!PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Add(tweak.Key);
                                    }
                                    Service.NotificationManager.AddNotification(new Notification { Content = $"Enabled {tweak.Name}", Title = "Simple Tweaks", Type = NotificationType.Info });}
                                PluginConfig.Save();
                                return;
                            }

                            Service.Chat.PrintError($"\"{splitArgString[1]}\" is not a valid tweak id.");
                            return;
                        }
                        case "e":
                        case "enable": {
                            if (splitArgString.Length < 2) {
                                Service.Chat.PrintError("/tweaks enable <tweakid>");
                                return;
                            }
                            var tweak = GetTweakById(splitArgString[1]);
                            if (tweak != null) {
                                if (!tweak.Enabled) {
                                    tweak.InternalEnable();
                                    if (!PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Add(tweak.Key);
                                    }
                                    Service.NotificationManager.AddNotification(new Notification { Content = $"Enabled {tweak.Name}", Title = "Simple Tweaks", Type = NotificationType.Info });
                                    PluginConfig.Save();
                                }
                                return;
                            }

                            Service.Chat.PrintError($"\"{splitArgString[1]}\" is not a valid tweak id.");
                            return;
                        }
                        case "d":
                        case "disable": {
                            if (splitArgString.Length < 2) {
                                Service.Chat.PrintError("/tweaks disable <tweakid>");
                                return;
                            }
                            var tweak = GetTweakById(splitArgString[1]);
                            if (tweak != null) {
                                if (tweak.Enabled) {
                                    tweak.InternalDisable();
                                    if (PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Remove(tweak.Key);
                                    }
                                    Service.NotificationManager.AddNotification(new Notification { Content = $"Disabled {tweak.Name}", Title = "Simple Tweaks", Type = NotificationType.Info });
                                    PluginConfig.Save();
                                }
                                return;
                            }

                            Service.Chat.PrintError($"\"{splitArgString[1]}\" is not a valid tweak id.");
                            return;
                        }
                        case "f":
                        case "find": {
                            if (splitArgString.Length < 2) {
                                Service.Chat.PrintError("/tweaks find <tweakid>");
                                return;
                            }
                            var tweak = GetTweakById(splitArgString[1]);
                            if (tweak != null) {
                                PluginConfig.FocusTweak(tweak);
                                return;
                                
                            }
                            Service.Chat.PrintError($"\"{splitArgString[1]}\" is not a valid tweak id.");

                            return;
                        }
                        default: {
                            var tweak = GetTweakById(splitArgString[0]);
                            if (tweak != null) {
                                tweak.HandleBasicCommand(splitArgString.Skip(1).ToArray());
                                return;
                            }
                            
                            Service.Chat.PrintError($"\"{splitArgString[1]}\" is not a valid tweak id.");
                            return;
                        }

                    }
                }
            }

            ConfigWindow.UnCollapseOrToggle();
        }

        public BaseTweak? GetTweakById(string s, IEnumerable<BaseTweak>? tweakList = null) {
            tweakList ??= Tweaks;

            foreach (var t in tweakList) {
                if (string.Equals(t.Key, s, StringComparison.InvariantCultureIgnoreCase)) return t;
                if (t is SubTweakManager stm) {
                    var fromSub = GetTweakById(s, stm.GetTweakList());
                    if (fromSub != null) return fromSub;
                }
            }

            return null;
        }

        public T GetTweak<T>(IEnumerable<BaseTweak>? tweakList = null) where T : BaseTweak {
            tweakList ??= Tweaks;
            foreach (var t in tweakList) {
                if (t is T tweak) return tweak;
                if (t is SubTweakManager stm) {
                    var fromSub = GetTweak<T>(stm.GetTweakList());
                    if (fromSub != null) return fromSub;
                }
            }
            return null;
        }
        

        public void SaveAllConfig() {
            PluginConfig.Save();
            foreach (var tp in TweakProviders.Where(tp => !tp.IsDisposed)) {
                foreach (var t in tp.Tweaks) {
                    t.RequestSaveConfig();
                }
            }
        }

        public void RemoveCommands() {
            Service.Commands.RemoveHandler("/tweaks");
        }

        private void BuildUI() {
            
            
            
            foreach (var e in ErrorList.Where(e => e.IsNew && e.Tweak != null)) {
                e.IsNew = false;
                e.Tweak.InternalDisable();
                Service.NotificationManager.AddNotification(new Notification { Content = $"{e.Tweak.Name} has been disabled due to an error.", Title = "Simple Tweaks", Type = NotificationType.Error }); 
            }

            WindowSystem.Draw();

            if (ShowErrorWindow) {
                if (ErrorList.Count > 0) {
                    var errorsStillOpen = true;
                    ImGui.Begin($"{Name}: Error!", ref errorsStillOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);

                    for (var i = 0; i < ErrorList.Count && i < 5; i++) {
                        var e = ErrorList[i];
                        ImGui.Text($"Error caught in {(e.Manager != null ? $"{e.Manager.Name}@" : "")}{(e.Tweak != null ? e.Tweak.Name : "Tweak Loader")}:");
                        if (!string.IsNullOrEmpty(e.Message)) {
                            ImGui.Text(e.Message);
                        }
                        ImGui.Text($"{e.Exception}");

                        if (ImGui.Button($"Clear this Error###clearErrorButton{i}")) {
                            e.Closed = true;
                        }

                        if (e.Count > 1) {
                            ImGui.SameLine();
                            ImGui.Text($"This error has occured {e.Count} times.");
                        }


                        ImGui.Separator();
                    }

                    if (ErrorList.Count > 5) {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), $"{ErrorList.Count - 5} Additional Errors");
                    }

                    ErrorList.RemoveAll(e => e.Closed);

                    ImGui.End();

                    if (!errorsStillOpen) {
                        ErrorList.Clear();
                        ShowErrorWindow = false;
                    }
                } else {
                    ShowErrorWindow = false;
                }
            }

            if (Service.PluginInterface.IsDevMenuOpen && (Service.PluginInterface.IsDev || PluginConfig.ShowInDevMenu)) {
                if (ImGui.BeginMainMenuBar()) {
                    if (ImGui.MenuItem("Simple Tweaks")) {
                        if (ImGui.GetIO().KeyShift) {
                            DebugWindow.UnCollapseOrToggle();
                        } else {
                            ConfigWindow.UnCollapseOrToggle();
                        }
                    }
                    ImGui.EndMainMenuBar();
                }
            }
        }


        internal class CaughtError {
            public BaseTweak Tweak = null;
            public SubTweakManager Manager = null;
            public Exception Exception;
            public bool IsNew = true;
            public bool Closed = false;
            public string Message = string.Empty;
            public ulong Count = 1;
            public override bool Equals(object obj) {
                if (obj is CaughtError otherError) {
                    if (otherError.Manager != Manager) return false;
                    if (otherError.Tweak != Tweak) return false;
                    if (otherError.Message != this.Message) return false;
                    if ($"{otherError.Exception}" != $"{Exception}") return false;
                    return true;
                }

                return false;
            }
        }

        internal bool ShowErrorWindow = false;

        internal readonly List<CaughtError> ErrorList = new List<CaughtError>();
#if DEBUG
        public void Error(Exception ex, string message = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = "") {
            Error(null, ex, true, message, callerFilePath, callerLineNumber, callerMemberName);
        }
        
        public void Error(BaseTweak tweak, Exception exception, bool allowContinue = false, string message = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = "" ) {
            if (tweak != null) {
                SimpleLog.Error($"Exception in '{tweak.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")), callerFilePath, callerMemberName, callerLineNumber);
            } else {
                SimpleLog.Error("Exception in SimpleTweaks framework. "+ (string.IsNullOrEmpty(message) ? "" : ($": {message}")), callerFilePath, callerMemberName, callerLineNumber);
            }
            SimpleLog.Error($"{exception}", callerFilePath, callerMemberName, callerLineNumber);
#else

        public void Error(Exception ex, string message = "") {
            Error(null, ex, true, message);
        }

        public void Error(BaseTweak tweak, Exception exception, bool allowContinue = false, string message="") {
            if (tweak == null) {
                SimpleLog.Error($"Exception in SimpleTweaks framework. " + (string.IsNullOrEmpty(message) ? "" : ($": {message}")));
            } else {
                SimpleLog.Error($"Exception in '{tweak.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")));
            }
            
            
            SimpleLog.Error($"{exception}");
#endif
            var err = new CaughtError {
                Tweak = tweak,
                Exception = exception,
                IsNew = !allowContinue,
                Message = message
            };

            var i = ErrorList.IndexOf(err);
            if (i >= 0) {
                ErrorList[i].Count++;
                ErrorList[i].IsNew = ErrorList[i].IsNew || err.IsNew;
            } else {
                ErrorList.Insert(0, err);
            }

            if (ErrorList.Count > 50) {
                ErrorList.RemoveRange(50, ErrorList.Count - 50);
            }
        }

#if DEBUG
        public void Error(SubTweakManager manager, BaseTweak tweak, Exception exception, bool allowContinue = false, string message = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = "") {
            SimpleLog.Error($"Exception in '{tweak.Name}' @ '{manager.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")), callerFilePath, callerMemberName, callerLineNumber);
            SimpleLog.Error($"{exception}", callerFilePath, callerMemberName, callerLineNumber);
#else
        public void Error(SubTweakManager manager, BaseTweak tweak, Exception exception, bool allowContinue = false, string message = "") {
            SimpleLog.Error($"Exception in '{tweak.Name}' @ '{manager.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")));
            SimpleLog.Error($"{exception}");
#endif
            var err = new CaughtError {
                Tweak = tweak,
                Manager = manager,
                Exception = exception,
                IsNew = !allowContinue,
                Message = message
            };

            var i = ErrorList.IndexOf(err);
            if (i >= 0) {
                ErrorList[i].Count++;
                ErrorList[i].IsNew = ErrorList[i].IsNew || err.IsNew;
            } else {
                ErrorList.Insert(0, err);
            }

            if (ErrorList.Count > 50) {
                ErrorList.RemoveRange(50, ErrorList.Count - 50);
            }
        }

        public void LoadCustomProvider(CustomTweakProviderConfig provider) {
            if (!provider.Enabled) return;
            var path = provider.Assembly;
            if (!File.Exists(path)) return;
            TweakProviders.RemoveAll(t => t.IsDisposed);
            var tweakProvider = new CustomTweakProvider(provider);
            tweakProvider.LoadTweaks();
            TweakProviders.Add(tweakProvider);
            Loc.ClearCache();
            DebugManager.Reload();
        }
    }
}
