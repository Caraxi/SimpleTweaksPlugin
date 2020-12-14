using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace SimpleTweaksPlugin {
    public class SimpleTweaksPlugin : IDalamudPlugin {
        public string Name => "Simple Tweaks";
        public DalamudPluginInterface PluginInterface { get; private set; }
        public SimpleTweaksPluginConfig PluginConfig { get; private set; }

        public List<Tweak> Tweaks = new List<Tweak>();

        private bool drawConfigWindow = false;

        public string AssemblyLocation { get; private set; } = Assembly.GetExecutingAssembly().Location;

        internal Common Common;

        public void Dispose() {
            PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
            RemoveCommands();

            foreach (var t in Tweaks) {
                SimpleLog.Debug($"Disable: {t.Name}");
                t.Disable();
                SimpleLog.Debug($"Dispose: {t.Name}");
                t.Dispose();
            }

            Tweaks.Clear();
        }

        public int UpdateFrom = -1;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            SimpleLog.SetupBuildPath();
            this.PluginInterface = pluginInterface;
            this.PluginConfig = (SimpleTweaksPluginConfig)pluginInterface.GetPluginConfig() ?? new SimpleTweaksPluginConfig();
            this.PluginConfig.Init(this, pluginInterface);


            if (PluginConfig.Version < 2) {
                UpdateFrom = PluginConfig.Version;
                PluginConfig.Version = 2;
                PluginConfig.Save();
            }

            Common = new Common(pluginInterface);

            PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;
            pluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;

            SetupCommands();

            var tweakList = new List<Tweak>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsAbstract)) {
                SimpleLog.Debug($"Initalizing Tweak: {t.Name}");
                var tweak = (Tweak)Activator.CreateInstance(t);
                tweak.InterfaceSetup(this, pluginInterface, PluginConfig);
                tweak.Setup();
                if (PluginConfig.EnabledTweaks.Contains(t.Name)) {
                    SimpleLog.Debug($"Enable: {t.Name}");
                    tweak.Enable();
                }
                tweakList.Add(tweak);
                
            }

            Tweaks = tweakList.OrderBy(t => t.Name).ToList();

#if DEBUG
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
            drawConfigWindow = !drawConfigWindow;
        }

        public void RemoveCommands() {
            PluginInterface.CommandManager.RemoveHandler("/tweaks");
        }

        private void BuildUI() {

            if (UpdateFrom >= 0) {

                bool stillOpen = true;

                ImGui.SetNextWindowSizeConstraints(new Vector2(500, 50) * ImGui.GetIO().FontGlobalScale, new Vector2(500, 500));
                ImGui.Begin("Simple Tweaks: Updated", ref stillOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse);
                ImGui.SetWindowFontScale(1.3f);
                
                ImGui.Text($"Thank you for updating {Name}.");
                ImGui.Separator();
                ImGui.TextWrapped("Due to a major rework on internal systems some settings have changed and you may need to reenable or reset some things.");
                ImGui.Separator();
                if (UpdateFrom < 2) {
                    ImGui.TextWrapped($"With version 1.2 of {Name} the Tooltip Tweaks have been completely reworked and will all be disabled due to the config for them changing completely.");
                    ImGui.Separator();
                    ImGui.TextWrapped("Some tweaks have been moved into 'Sub Tweaks' of the new UI Adjustments category and will require reenabling.");
                    
                    ImGui.Separator();
                }

                ImGui.Text("I apologise for any inconveniance caused by this update.");
                ImGui.SetWindowFontScale(1f);
                ImGui.End();

                if (!stillOpen) {
                    UpdateFrom = -1;
                }

            }



            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();

            if (errorList.Count > 0) {
                var errorsStillOpen = true;
                ImGui.Begin($"{Name}: Error!", ref errorsStillOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);

                foreach (var e in errorList) {
                    if (e.IsNew) {
                        e.IsNew = false;
                        e.Tweak.Disable();
                    }

                    

                    ImGui.Text($"Error caught in {(e.Manager!=null ? $"{e.Manager.Name}@":"")}{e.Tweak.Name}:");
                    ImGui.Text($"{e.Exception}");

                    if (ImGui.Button("Ok")) {
                        e.Closed = true;
                    }

                    ImGui.Separator();
                }

                errorList.RemoveAll(e => e.Closed);

                ImGui.End();

                if (!errorsStillOpen) {
                    errorList.Clear();
                }
            }
        }

        private class CaughtError {
            public BaseTweak Tweak;
            public SubTweakManager Manager = null;
            public Exception Exception;
            public bool IsNew = true;
            public bool Closed = false;
        }


        private readonly List<CaughtError> errorList = new List<CaughtError>();

        public void Error(BaseTweak tweak, Exception exception, bool allowContinue = false) {
            errorList.Insert(0, new CaughtError { Tweak = tweak, Exception = exception, IsNew = !allowContinue});
        }

        public void Error(SubTweakManager manager, BaseTweak tweak, Exception exception, bool allowContinue = false) {
            errorList.Insert(0, new CaughtError { Tweak = tweak, Manager = manager, Exception = exception, IsNew = !allowContinue});
        }
    }
}
