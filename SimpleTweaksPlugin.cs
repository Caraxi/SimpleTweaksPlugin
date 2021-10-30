using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Libc;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using FFXIVClientInterface;
using Newtonsoft.Json.Linq;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using XivCommon;
#if DEBUG
using System.Runtime.CompilerServices;
using SimpleTweaksPlugin.Debugging;
#endif

#pragma warning disable CS0659
namespace SimpleTweaksPlugin {
    public class SimpleTweaksPlugin : IDalamudPlugin {
        public string Name => "Simple Tweaks";
        public DalamudPluginInterface PluginInterface { get; private set; }
        public SimpleTweaksPluginConfig PluginConfig { get; private set; }

        public List<BaseTweak> Tweaks = new List<BaseTweak>();

        public IconManager IconManager { get; private set; }
        public XivCommonBase XivCommon { get; private set; }
        

        private bool drawConfigWindow = false;

        public string AssemblyLocation { get; private set; } = Assembly.GetExecutingAssembly().Location;

        internal Common Common;

        public static ClientInterface Client;

        public static SimpleTweaksPlugin Plugin { get; private set; }

        private CultureInfo setCulture = null;

        public bool LoadingTranslations { get; private set; } = false;

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
            
            PluginInterface.UiBuilder.Draw -= this.BuildUI;
            RemoveCommands();

            foreach (var t in Tweaks) {
                if (t.Enabled || t is SubTweakManager { AlwaysEnabled : true}) {
                    SimpleLog.Log($"Disable: {t.Name}");
                    t.Disable();
                }
                SimpleLog.Log($"Dispose: {t.Name}");
                t.Dispose();
            }
            Tweaks.Clear();
            Client.Dispose();
            #if DEBUG
            DebugManager.Dispose();
            #endif
            foreach (var hook in Common.HookList.Where(hook => !hook.IsDisposed)) {
                if (hook.IsEnabled) hook.Disable();
                hook.Dispose();
            }
            Common.HookList.Clear();
            this.XivCommon.Dispose();
        }

        public int UpdateFrom = -1;

        public SimpleTweaksPlugin(DalamudPluginInterface pluginInterface) {
            pluginInterface.Create<Service>();
            FFXIVClientStructs.Resolver.Initialize(Service.SigScanner.SearchBase);

            Plugin = this;
#if DEBUG
            SimpleLog.SetupBuildPath();
#endif
            this.PluginInterface = pluginInterface;

            Client = new ClientInterface(Service.SigScanner, Service.Data);
            
            this.PluginConfig = (SimpleTweaksPluginConfig)pluginInterface.GetPluginConfig() ?? new SimpleTweaksPluginConfig();
            this.PluginConfig.Init(this, pluginInterface);
            
            IconManager = new IconManager(pluginInterface);
            this.XivCommon = new XivCommonBase(Hooks.ContextMenu);
            SetupLocalization();
            
            UiHelper.Setup(Service.SigScanner);
            #if DEBUG
            DebugManager.SetPlugin(this);
            #endif

            Common = new Common();

            PluginInterface.UiBuilder.Draw += this.BuildUI;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;

            SetupCommands();

            var tweakList = new List<BaseTweak>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsAbstract)) {
                SimpleLog.Debug($"Initalizing Tweak: {t.Name}");
                try {
                    var tweak = (Tweak) Activator.CreateInstance(t);
                    tweak.InterfaceSetup(this, pluginInterface, PluginConfig);
                    if (tweak.CanLoad) {
                        tweak.Setup();
                        if (tweak.Ready && (PluginConfig.EnabledTweaks.Contains(t.Name) || tweak is SubTweakManager {AlwaysEnabled: true})) {
                            SimpleLog.Debug($"Enable: {t.Name}");
                            try {
                                tweak.Enable();
                            } catch (Exception ex) {
                                this.Error(tweak, ex, true, $"Error in Enable for '{tweak.Name}");
                            }
                        }

                        tweakList.Add(tweak);
                    }
                } catch (Exception ex) {
                    PluginLog.Error(ex, $"Failed loading tweak '{t.Name}'.");
                }
            }

            Tweaks = tweakList.OrderBy(t => t.Name).ToList();

#if DEBUG
            DebugManager.Enabled = true;
            drawConfigWindow = true;
#endif

        }

        public void SetupLocalization() {
            this.PluginConfig.Language ??= Service.ClientState.ClientLanguage switch {
                ClientLanguage.English => "en",
                ClientLanguage.French => "fr",
                ClientLanguage.German => "de",
                ClientLanguage.Japanese => "ja",
                _ => "en"
            };

            Loc.LoadLanguage(PluginConfig.Language);
        }

        public void SetupCommands() {
            Service.Commands.AddHandler("/tweaks", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}",
                ShowInHelp = true
            });
        }

        private void OnOpenConfig() => OnConfigCommandHandler(null, null);
        public void OnConfigCommandHandler(object command, object args) {
            if (args is string argString) {
#if DEBUG
                if (argString == "Debug") {
                    DebugManager.Enabled = !DebugManager.Enabled;
                    return;
                }
#endif
                if (!string.IsNullOrEmpty(argString.Trim())) {
                    var splitArgString = argString.Split(' ');
                    switch (splitArgString[0].ToLowerInvariant()) {
                        case "t":
                        case "toggle": {
                            if (splitArgString.Length < 2) {
                                Service.Chat.PrintError("/tweaks toggle <tweakid>");
                                return;
                            }
                            var tweak = GetTweakById(splitArgString[1]);
                            if (tweak != null) {
                                if (tweak.Enabled) {
                                    tweak.Disable();
                                    if (PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Remove(tweak.Key);
                                    }
                                    Service.PluginInterface.UiBuilder.AddNotification($"Disabled {tweak.Name}", "Simple Tweaks", NotificationType.Info);
                                } else {
                                    tweak.Enable();
                                    if (!PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Add(tweak.Key);
                                    }
                                    Service.PluginInterface.UiBuilder.AddNotification($"Enabled {tweak.Name}", "Simple Tweaks", NotificationType.Info);
                                }
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
                                    tweak.Enable();
                                    if (!PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Add(tweak.Key);
                                    }
                                    Service.PluginInterface.UiBuilder.AddNotification($"Enabled {tweak.Name}", "Simple Tweaks", NotificationType.Info);
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
                                    tweak.Disable();
                                    if (PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                                        PluginConfig.EnabledTweaks.Remove(tweak.Key);
                                    }
                                    Service.PluginInterface.UiBuilder.AddNotification($"Disabled {tweak.Name}", "Simple Tweaks", NotificationType.Info);
                                    PluginConfig.Save();
                                }
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

            drawConfigWindow = !drawConfigWindow;
            if (!drawConfigWindow) {
                SaveAllConfig();
            }
        }

        public BaseTweak GetTweakById(string s, List<BaseTweak> tweakList = null) {
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

        public void SaveAllConfig() {
            foreach (var t in Tweaks) {
                t.RequestSaveConfig();
            }
        }

        public void RemoveCommands() {
            Service.Commands.RemoveHandler("/tweaks");
        }

        private void BuildUI() {
            foreach (var e in ErrorList.Where(e => e.IsNew && e.Tweak != null)) {
                e.IsNew = false;
                e.Tweak.Disable();
                Service.PluginInterface.UiBuilder.AddNotification($"{e.Tweak.Name} has been disabled due to an error.", "Simple Tweaks", NotificationType.Error, 5000);
            }

#if DEBUG
            if (DebugManager.Enabled) {
                DebugManager.DrawDebugWindow(ref DebugManager.Enabled);
            }
#endif
            var windowWasOpen = drawConfigWindow;
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();

            if (windowWasOpen && !drawConfigWindow) {
                SaveAllConfig();
            }
            
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
        public void Error(BaseTweak tweak, Exception exception, bool allowContinue = false, string message = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = "" ) {

            SimpleLog.Error($"Exception in '{tweak.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")), callerFilePath, callerMemberName, callerLineNumber);
            SimpleLog.Error($"{exception}", callerFilePath, callerMemberName, callerLineNumber);
#else
        public void Error(BaseTweak tweak, Exception exception, bool allowContinue = false, string message="") {
            SimpleLog.Error($"Exception in '{tweak.Name}'" + (string.IsNullOrEmpty(message) ? "" : ($": {message}")));
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
    }
}
