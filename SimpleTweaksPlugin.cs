using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using FFXIVClientInterface;
using Newtonsoft.Json.Linq;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
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

        public List<Tweak> Tweaks = new List<Tweak>();

        public IconManager IconManager { get; private set; }
        

        private bool drawConfigWindow = false;

        public string AssemblyLocation { get; private set; } = Assembly.GetExecutingAssembly().Location;

        internal Common Common;

        public static ClientInterface Client;
        
        public void Dispose() {
            SimpleLog.Debug("Dispose");
            
            PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
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
        }

        public int UpdateFrom = -1;

        public void Initialize(DalamudPluginInterface pluginInterface) {
#if DEBUG
            SimpleLog.SetupBuildPath();
#endif
            this.PluginInterface = pluginInterface;

            Client = new ClientInterface(pluginInterface.TargetModuleScanner, pluginInterface.Data);
            
            this.PluginConfig = (SimpleTweaksPluginConfig)pluginInterface.GetPluginConfig() ?? new SimpleTweaksPluginConfig();
            this.PluginConfig.Init(this, pluginInterface);
            
            IconManager = new IconManager(pluginInterface);
            
            UiHelper.Setup(pluginInterface.TargetModuleScanner);
            #if DEBUG
            DebugManager.SetPlugin(this);
            #endif
            
            if (PluginConfig.Version < 3) {
                SimpleLog.Information($"Updating Config: {PluginConfig.Version} -> 3");
                dynamic oldConfig = null;
                try {
                    var oldConfigPath = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "..", "SimpleTweaksPlugin.json");
                    if (File.Exists(oldConfigPath)) {
                        var oldConfigText = File.ReadAllText(oldConfigPath);
                        // SimpleLog.Log(oldConfigText);
                        oldConfig = JObject.Parse(oldConfigText);
                    }
                } catch (Exception ex) {
                    SimpleLog.Error(ex);
                }
                
                UpdateFrom = PluginConfig.Version;
                PluginConfig.Version = 3;
                
                var moveTweaks = new Dictionary<string, string>() {
                    { "UiAdjustments@DisableChatMovement", "ChatTweaks@DisableChatMovement" },
                    { "UiAdjustments@DisableChatResize", "ChatTweaks@DisableChatResize" },
                    { "UiAdjustments@DisableChatAutoscroll", "ChatTweaks@DisableChatAutoscroll" },
                    { "UiAdjustments@RenameChatTabs", "ChatTweaks@RenameChatTabs" },
                    { "ClickableLinks", "ChatTweaks@ClickableLinks" },
                };

                foreach (var t in moveTweaks) {
                    if (PluginConfig.EnabledTweaks.Contains(t.Key)) {
                        PluginConfig.EnabledTweaks.Remove(t.Key);
                        PluginConfig.EnabledTweaks.Add(t.Value);
                        
                    }
                }

                if (oldConfig != null) {
                    try {
                        PluginConfig.ChatTweaks.DisableChatAutoscroll.DisablePanel0 = oldConfig.UiAdjustments.DisableChatAutoscroll.DisablePanel0;
                        PluginConfig.ChatTweaks.DisableChatAutoscroll.DisablePanel1 = oldConfig.UiAdjustments.DisableChatAutoscroll.DisablePanel1;
                        PluginConfig.ChatTweaks.DisableChatAutoscroll.DisablePanel2 = oldConfig.UiAdjustments.DisableChatAutoscroll.DisablePanel2;
                        PluginConfig.ChatTweaks.DisableChatAutoscroll.DisablePanel3 = oldConfig.UiAdjustments.DisableChatAutoscroll.DisablePanel3;
                    } catch (Exception ex) {
                        SimpleLog.Error(ex);
                    }
                    
                    try {
                        PluginConfig.ChatTweaks.RenameChatTabs.ChatTab0Name = oldConfig.UiAdjustments.RenameChatTabs.ChatTab0Name;
                        PluginConfig.ChatTweaks.RenameChatTabs.ChatTab1Name = oldConfig.UiAdjustments.RenameChatTabs.ChatTab1Name;
                        PluginConfig.ChatTweaks.RenameChatTabs.DoRenameTab0 = oldConfig.UiAdjustments.RenameChatTabs.DoRenameTab0;
                        PluginConfig.ChatTweaks.RenameChatTabs.DoRenameTab1 = oldConfig.UiAdjustments.RenameChatTabs.DoRenameTab1;
                    } catch (Exception ex) {
                        SimpleLog.Error(ex);
                    }
                    
                }
                

                PluginConfig.Save();
            }
            
            Common = new Common(pluginInterface);

            PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;
            pluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;

            SetupCommands();

            var tweakList = new List<Tweak>();

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

        public void SetupCommands() {
            PluginInterface.CommandManager.AddHandler("/tweaks", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}",
                ShowInHelp = true
            });
        }

        public void OnConfigCommandHandler(object command, object args) {
#if DEBUG
            if (args is string argString && argString == "Debug") {
                DebugManager.Enabled = !DebugManager.Enabled;
                return;
            }
#endif
            drawConfigWindow = !drawConfigWindow;
        }

        public void RemoveCommands() {
            PluginInterface.CommandManager.RemoveHandler("/tweaks");
        }

        private void BuildUI() {
#if DEBUG
            if (DebugManager.Enabled) {
                DebugManager.DrawDebugWindow(ref DebugManager.Enabled);
            }
#endif

            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
            
            if (ShowErrorWindow) {
                if (ErrorList.Count > 0) {
                    var errorsStillOpen = true;
                    ImGui.Begin($"{Name}: Error!", ref errorsStillOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);

                    for (var i = 0; i < ErrorList.Count && i < 5; i++) {
                        var e = ErrorList[i];

                        if (e.IsNew && e.Tweak != null) {
                            e.IsNew = false;
                            e.Tweak.Disable();
                        }

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
