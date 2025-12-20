using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Utility;

public static class TestUtil {
    public record TestLogEntry(string Message, Vector4? Colour = null) {
        public DateTime Time { get; } = DateTime.Now;
        public List<TestLogEntry> Extra = [];

        public TestLogEntry AddExtra(string text, Vector4? colour = null, Action? click = null) {
            var e = new TestLogEntry(text, colour);
            Extra.Add(e);
            scroll = true;
            e.Click = click;
            
            return e;
        }

        public Action? Click { get; set; } = () => { };

    }

    public static bool IsRunning { get; private set; }


    public static string StateString { get; private set; } = "Not Ready";
    public static bool IsReady { get; private set; }

    private static List<TestLogEntry> LogEntries = new();
    private static bool scroll;

    private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    
    public static void Ready() {
        IsReady = true;
        StateString = "Not Started";
        #if TEST
        Start().ConfigureAwait(false);
        #endif
    }
    
    public static void Draw() {

        if (cancellationTokenSource.IsCancellationRequested) {
            IsRunning = false;
            StateString = "Cancelled";
        }

        using (ImRaii.Disabled(!IsRunning)) {
            if (ImGui.SmallButton("Cancel")) {
                cancellationTokenSource.Cancel();
            }
        }
        ImGui.SameLine();
        
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

    public static TestLogEntry Log(string message, Vector4? colour = null, Action? click = null) {
        var e = new TestLogEntry(message, colour);
        LogEntries.Add(e);
        scroll = true;
        e.Click = click;
        return e;
    }

    private static void FileLog(string message) {
        var path = Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), "test.log");
        File.AppendAllText(path, $"{message}\n");
    }

    private static uint throttle = 50;
    
    public static async Task Start() {
       
        FileLog($"Starting Test @ {DateTime.Now}");

        try {
            if (!IsReady) return;
            if (IsRunning) return;
            
            IsRunning = true;
            async Task RunTest(BaseTweak tweak) {
                if (tweak is IDisabledTweak) return;
                if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested) return;
                try {

                    Service.Chat.Print(new XivChatEntry()
                    {
                        Type = XivChatType.Echo,
                        Message = $"[{tweak.Key}] Starting Test"
                    });
                    
                    FileLog($"[{tweak.Key}] Starting Test");
                    
                    if (!tweak.CanLoad) {
                        FileLog($"[{tweak.Key}] Unable to load");
                        throw new Exception("Cannot Load");
                    }

                    if (!tweak.Ready) {
                        FileLog($"[{tweak.Key}] Setup");
                        await Service.Framework.RunOnTick(tweak.SetupInternal);
                    }
                    
                    if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                    if (cancellationTokenSource.IsCancellationRequested) return;

                    var wasEnabled = tweak.Enabled;
                    
                    if (!tweak.Enabled) {
                        FileLog($"[{tweak.Key}] Enable Tweak");
                        StateString = $"Enabling Tweak: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.InternalEnable, delayTicks: 1);
                        
                        if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                        if (cancellationTokenSource.IsCancellationRequested) return;
                    }
                    
                    if (tweak.Enabled) {
                        FileLog($"[{tweak.Key}] Running Tests");
                        StateString = $"Running Test: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.Test, delayTicks: 1);

                        if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                        if (cancellationTokenSource.IsCancellationRequested) return;

                        if (tweak is SubTweakManager stm) {
                            FileLog($"[{tweak.Key}] Running Subtweak Tests");
                            SimpleTweaksPluginConfig.RebuildTweakList();
                            
                            if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                            if (cancellationTokenSource.IsCancellationRequested) return;
                            
                            foreach (var subTweak in stm.GetTweakList()) {
                                await RunTest(subTweak);
                            }
                        }
                    }
                    
                    if (throttle > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttle), cancellationTokenSource.Token);
                    if (cancellationTokenSource.IsCancellationRequested) return;
                    
                    if (!wasEnabled && tweak is not SubTweakManager { AlwaysEnabled: true}) {
                        FileLog($"[{tweak.Key}] Disable");
                        StateString = $"Disabling Tweak: {tweak.Name}";
                        await Service.Framework.RunOnTick(tweak.InternalDisable, delayTicks: 1);
                    }
                    
                } catch (Exception ex) {
                    FileLog($"[{tweak.Key}] Failed - {ex.Message}");
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

    public static void Cancel() {
        cancellationTokenSource.Cancel();
    }
}
