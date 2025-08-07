using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Utility;

public static class TestUtil {
    public record TestLogEntry(string Message, Vector4? Colour = null) {
        private static ulong counter;
        
        public DateTime Time { get; } = DateTime.Now;
        public List<TestLogEntry> Extra = [];

        public TestLogEntry AddExtra(string text, Vector4? colour = null, Action click = null) {
            var e = new TestLogEntry(text, colour);
            Extra.Add(e);
            scroll = true;
            e.Click = click;
            
            return e;
        }

        public Action Click { get; set; } = () => { };

    }

    public static bool IsRunning { get; private set; }


    public static string StateString { get; private set; } = "Not Ready";
    public static bool IsReady { get; private set; }

    private static List<TestLogEntry> LogEntries = new();
    private static bool scroll;
    
    public static void Ready() {
        IsReady = true;
        StateString = "Not Started";
        #if TEST
        Start().ConfigureAwait(false);
        #endif
    }
    
    public static void Draw() {
        ImGui.TextColored(IsRunning ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow, IsRunning ? "Test Running" : "Test Not Running");
        ImGui.SameLine();
        ImGui.Text(" : ");
        ImGui.SameLine();
        ImGui.Text(StateString);
        if (ImGui.BeginChild("log", ImGui.GetContentRegionAvail(), true)) {

            void ShowEntry(TestLogEntry entry) {
                if (entry.Extra.Count > 0) {

                    var treeOpen = false;
                    using (ImRaii.PushColor(ImGuiCol.Text, entry.Colour ?? ImGuiColors.DalamudWhite)) {
                        treeOpen = ImGui.TreeNode(entry.Message);
                    }

                    if (treeOpen) {
                        foreach (var extraEntry in entry.Extra) {
                            ShowEntry(extraEntry);
                        }

                        ImGui.TreePop();
                    }
                } else {
                    ImGui.TextColored(entry.Colour ?? ImGuiColors.DalamudWhite, entry.Message);
                    if (entry.Click != null && ImGui.IsItemClicked()) {
                        entry.Click();
                    }
                }
            }
            
            
            foreach (var logEntry in LogEntries) {
                ShowEntry(logEntry);
            }

            if (scroll) {
                scroll = false;
                ImGui.SetScrollHereY();
            }
        }
        ImGui.EndChild();
    }

    public static TestLogEntry Log(string message, Vector4? colour = null, Action click = null) {
        var e = new TestLogEntry(message, colour);
        LogEntries.Add(e);
        scroll = true;
        e.Click = click;
        return e;
    }
    
    public static async Task Start() {
       
       
        try {
            if (!IsReady) return;
            if (IsRunning) return;
            
            IsRunning = true;
            async Task RunTest(BaseTweak tweak) {
                try {

                    if (!tweak.CanLoad) {
                        throw new Exception("Cannot Load");
                    }

                    if (!tweak.Ready) {
                        await Service.Framework.RunOnTick(tweak.SetupInternal);
                    }

                    var wasEnabled = tweak.Enabled;
                    
                    if (!tweak.Enabled) {
                        StateString = $"Enabling Tweak: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.InternalEnable, delayTicks: 1);
                    }
                    
                    if (tweak.Enabled) {
                        StateString = $"Running Test: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.Test, delayTicks: 1);


                        if (tweak is SubTweakManager stm) {
                            SimpleTweaksPluginConfig.RebuildTweakList();
                            foreach (var subTweak in stm.GetTweakList()) {
                                await RunTest(subTweak);
                            }
                        }
                        
                    }
                    
                    if (!wasEnabled && tweak is not SubTweakManager { AlwaysEnabled: true}) {
                        StateString = $"Disabling Tweak: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.InternalDisable, delayTicks: 1);
                    }
                    
                } catch (Exception ex) {
                    var l =  Log($" - Tweak '{tweak.Name}' Failed Test [{tweak.Key}]\n\t\t{ex.Message}", ImGuiColors.DalamudRed);
                    if (ex.Message.StartsWith("Failed to find Text signature")) {
                        var sig = ex.Message.Split("(").Last().Split(")").First();
                        l.AddExtra($"{sig}", ImGuiColors.DalamudYellow, () => ImGui.SetClipboardText(sig));
                    }
                    l.AddExtra($"{ex}", ImGuiColors.DalamudRed);
                    
                    
                    
                }
            }
            
            

            foreach (var tweak in SimpleTweaksPlugin.Plugin.Tweaks) {
                await RunTest(tweak);
            }
            
        } catch (Exception e) {
            Log($"Test Runner Crashed: - {e.Message}",  ImGuiColors.DalamudRed);
        }

        StateString = "Finished Test.";
        IsRunning = false;
        SimpleTweaksPlugin.Plugin.PluginConfig.RefreshSearch();
        SimpleTweaksPluginConfig.RebuildTweakList();
    }
}
