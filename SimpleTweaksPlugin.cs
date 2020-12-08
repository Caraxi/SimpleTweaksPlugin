using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using ImGuiNET;

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
#if DEBUG
                PluginLog.Log($"Disable: {t.Name}");
#endif
                t.Disable();
#if DEBUG
                PluginLog.Log($"Dispose: {t.Name}");
#endif
                t.Dispose();
            }

            Tweaks.Clear();
        }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = (SimpleTweaksPluginConfig)pluginInterface.GetPluginConfig() ?? new SimpleTweaksPluginConfig();
            this.PluginConfig.Init(this, pluginInterface);

            Common = new Common(pluginInterface);

            PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;
            pluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;

            SetupCommands();

            var tweakList = new List<Tweak>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)))) {
                var tweak = (Tweak) Activator.CreateInstance(t);
                tweak.InterfaceSetup(this, pluginInterface, PluginConfig);
                tweak.Setup();
                if (PluginConfig.EnabledTweaks.Contains(t.Name)) {
#if DEBUG
                    PluginLog.Log($"Enable: {t.Name}");
#endif
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
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();

            if (errorList.Count > 0) {
                var errorsStillOpen = true;
                ImGui.Begin($"{Name}: Error!", ref errorsStillOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);

                foreach (var e in errorList) {
                    if (e.IsNew) {
                        e.IsNew = false;
                        e.Tweak.Disable();
                    }

                    ImGui.Text($"Error caught in {e.Tweak.Name}:");
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
            public Tweak Tweak;
            public Exception Exception;
            public bool IsNew = true;
            public bool Closed = false;
        }


        private readonly List<CaughtError> errorList = new List<CaughtError>();


        public void Error(Tweak tweak, Exception exception) {
            errorList.Insert(0, new CaughtError { Tweak = tweak, Exception = exception});
        }
    }
}
