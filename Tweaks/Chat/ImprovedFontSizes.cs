using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class ImprovedFontSizes : ChatTweaks.SubTweak {
    
    public const byte MinimumFontSize = 8; // Game crashes below 8
    public const byte MaximumFontSize = 80;
    
    public override string Name => "Improved Font Sizes";
    public override string Description => "Allows you to change the font size for the chat windows beyond the default limits, and allows docked chat tabs to keep their font size separate from the main tab.";

    public class Configs : TweakConfig {
        public int[] FontSize;
    }

    private delegate void SetFontSizeDelegate(byte* chatLogPanelWithOffset, byte fontSize);
    private HookWrapper<SetFontSizeDelegate> setFontSizeHook;

    private delegate void* ShowLogMessageDelegate(RaptureLogModule* logModule, uint logMessage);
    private HookWrapper<ShowLogMessageDelegate> showLogMessageHook;

    public Configs Config { get; private set; }

    private readonly int[] originalFontSize = new int[4] { 12, 12, 12, 12 };

    private void RefreshFontSizes(bool restoreOriginal = false) {
        try {
            showLogMessageHook?.Enable();
            var a = Common.GetAgent(AgentId.ConfigCharacter);
            var c = ConfigModule.Instance();

            for (var i = 0; i < 4; i++) {
                var configOption = i switch {
                    1 => ConfigOption.LogFontSizeLog2,
                    2 => ConfigOption.LogFontSizeLog3,
                    3 => ConfigOption.LogFontSizeLog4,
                    _ => ConfigOption.LogFontSize,
                };

                var optionIndex = c->GetIndex(configOption);
                if (optionIndex != null) {
                    // Force game to refresh the fonts
                    int v;
                    if (restoreOriginal) {
                        v = originalFontSize[i];
                    } else {
                        v = c->GetIntValue(configOption);
                        v++;
                        if (v > 36) v = 12;
                    }

                    Common.SendEvent(a, 0, 18, optionIndex, v, 0);
                }
            }

            Common.SendEvent(a, 0, 0);
        } finally {
            showLogMessageHook?.Disable();
        }
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        if (Config.FontSize is not { Length: 4 }) Config.FontSize = new int[4] { 12, 12, 12, 12 };

        if (Common.GetUnitBase("ConfigCharacter", out var configCharacter)) {
            ImGui.Text("Please close the character config window to make changes to this tweak.");
            if (ImGui.Button("Close It")) {
                configCharacter->Hide(true);
            }
            return;
        }
        
        
        if (ImGui.BeginTable("extendedChatSettingsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
            ImGui.TableSetupColumn("Panel", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Font Size", ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (var i = 0; i < 4; i++) {
                ImGui.TableNextColumn();
                ImGui.Text($"Chat Panel {i + 1}");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderInt($"##chatPanelFontSize_{i}", ref Config.FontSize[i], MinimumFontSize, MaximumFontSize)) {
                    Config.FontSize[i] = Math.Clamp(Config.FontSize[i], MinimumFontSize, MaximumFontSize);
                    RefreshFontSizes();
                    hasChanged = true;
                }
                ImGui.TableNextColumn();
            }

            ImGui.EndTable();
        }
    };
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        for (var i = 0; i < 4; i++) {
            var configOption = i switch {
                1 => ConfigOption.LogFontSizeLog2,
                2 => ConfigOption.LogFontSizeLog3,
                3 => ConfigOption.LogFontSizeLog4,
                _ => ConfigOption.LogFontSize,
            };
            originalFontSize[i] = ConfigModule.Instance()->GetIntValue(configOption);
        }


        setFontSizeHook = Common.Hook<SetFontSizeDelegate>("40 53 48 83 EC 30 48 8B D9 88 51 48", SetFontSizeDetour);
        setFontSizeHook?.Enable();

        showLogMessageHook = Common.Hook<ShowLogMessageDelegate>("E8 ?? ?? ?? ?? 44 03 FB", ShowLogMessageDetour);

        if (Common.GetUnitBase("ConfigCharaChatLogDetail", out var unitBase)) ToggleFontSizeConfigDropDowns(unitBase, false);
        
        Common.AddonSetup += OnAddonSetup;
        RefreshFontSizes();
        base.Enable();
    }

    private void* ShowLogMessageDetour(RaptureLogModule* logModule, uint logMessage) {
        // No toast spam please
        if (logMessage == 801) return null;
        return showLogMessageHook.Original(logModule, logMessage);
    }

    private void ToggleFontSizeConfigDropDowns(AtkUnitBase* unitBase, bool toggle) {
        if (unitBase == null) return;

        var txt = Service.Data.Excel.GetSheet<Addon>()?.GetRow(7802)?.Text;
        if (txt == null) return;

        for (var i = 0U; i < 4; i++) {
            var n = unitBase->GetNodeById(7U + i);
            if (n == null) continue;
            var cn = n->GetComponent();
            if (cn == null) continue;
            var ddn = cn->UldManager.SearchNodeById(5);
            if (ddn == null) continue;
            var ddc = ddn->GetComponent();
            if (ddc == null) continue;
            var tn = cn->UldManager.SearchNodeById(4);
            if (tn == null) continue;
            var tnc = tn->GetAsAtkTextNode();
            if (tnc == null) continue;

            if (toggle) {
                tnc->SetText(txt.RawData);
            } else {
                var str = txt.ToDalamudString().Append(new List<Payload>() {
                    new UIForegroundPayload(3),
                    new TextPayload(" (Managed by Simple Tweaks)"),
                    new UIForegroundPayload(0)
                });
                tnc->SetText(str.Encode());
            }
            
            ddc->SetEnabledState(toggle);
        }
    }
    
    private void OnAddonSetup(SetupAddonArgs obj) {
        if (obj.AddonName != "ConfigCharaChatLogDetail") return;
        ToggleFontSizeConfigDropDowns(obj.Addon, false);
    }

    private void SetFontSizeDetour(byte* chatLogPanelWithOffset, byte fontSize) {
        try {
            if (Config.FontSize is { Length: 4 }) {
                var chatLogPanel = (AddonChatLogPanel*)(chatLogPanelWithOffset - 0x268);
                if (Config.FontSize != null) {
                    for (var i = 0; i < 4; i++) {
                        var panel = Common.GetUnitBase<AddonChatLogPanel>($"ChatLogPanel_{i}");
                        if (panel == null) continue;
                        if (panel == chatLogPanel) {
                            fontSize = (byte) Math.Clamp(Config.FontSize[i], MinimumFontSize, MaximumFontSize);
                            break;
                        }
                    }
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        setFontSizeHook.Original(chatLogPanelWithOffset, fontSize);
    }

    public override void Disable() {
        setFontSizeHook?.Disable();
        showLogMessageHook?.Disable();
        Common.AddonSetup -= OnAddonSetup;
        if (Common.GetUnitBase("ConfigCharaChatLogDetail", out var unitBase)) ToggleFontSizeConfigDropDowns(unitBase, true);
        SaveConfig(Config);
        RefreshFontSizes(true);
        base.Disable();
    }

    public override void Dispose() {
        setFontSizeHook?.Dispose();
        showLogMessageHook?.Dispose();
        base.Dispose();
    }
}

