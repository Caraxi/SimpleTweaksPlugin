using System;
using System.Collections.Generic;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Improved Chat Font Sizes")]
[TweakDescription("Allows you to change the font size for the chat windows beyond the default limits, and allows docked chat tabs to keep their font size separate from the main tab.")]
[TweakAutoConfig]
public unsafe class ImprovedFontSizes : ChatTweaks.SubTweak {
    public const byte MinimumFontSize = 8; // Game crashes below 8
    public const byte MaximumFontSize = 80;

    public class Configs : TweakConfig {
        public int[] FontSize;
    }

    private delegate void SetFontSizeDelegate(byte* chatLogPanelWithOffset, byte fontSize);

    [TweakHook, Signature("40 53 48 83 EC 30 48 8B D9 88 51 48", DetourName = nameof(SetFontSizeDetour))]
    private HookWrapper<SetFontSizeDelegate> setFontSizeHook;

    [TweakHook(typeof(RaptureLogModule), nameof(RaptureLogModule.ShowLogMessage), nameof(ShowLogMessageDetour), AutoEnable = false)]
    private HookWrapper<RaptureLogModule.Delegates.ShowLogMessage> showLogMessageHook;

    public Configs Config { get; private set; }

    private readonly uint[] originalFontSize = [12, 12, 12, 12];

    private void RefreshFontSizes(bool restoreOriginal = false) {
        try {
            showLogMessageHook?.Enable();

            for (var i = 0; i < 4; i++) {
                var configOption = i switch {
                    1 => UiConfigOption.LogFontSizeLog2,
                    2 => UiConfigOption.LogFontSizeLog3,
                    3 => UiConfigOption.LogFontSizeLog4,
                    _ => UiConfigOption.LogFontSize,
                };
                uint v;
                if (restoreOriginal) {
                    v = originalFontSize[i];
                } else {
                    if (Service.GameConfig.TryGet(configOption, out v)) {
                        v++;
                        if (v > 36) v = 12;
                    }
                }

                Service.GameConfig.Set(configOption, v);
            }
        } finally {
            showLogMessageHook?.Disable();
        }
    }

    protected void DrawConfig(ref bool hasChanged) {
        if (Config.FontSize is not { Length: 4 }) Config.FontSize = [12, 12, 12, 12];

        if (Common.GetUnitBase("ConfigCharacter", out _)) {
            ImGui.Text("Please close the character config window to make changes to this tweak.");
            if (ImGui.Button("Close It")) {
                Service.Framework.RunOnFrameworkThread(() => { AgentModule.Instance()->GetAgentByInternalId(AgentId.ConfigCharacter)->Hide(); });
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
    }

    protected override void AfterEnable() {
        for (var i = 0; i < 4; i++) {
            var configOption = i switch {
                1 => UiConfigOption.LogFontSizeLog2,
                2 => UiConfigOption.LogFontSizeLog3,
                3 => UiConfigOption.LogFontSizeLog4,
                _ => UiConfigOption.LogFontSize,
            };
            if (Service.GameConfig.TryGet(configOption, out originalFontSize[i])) continue;
            Plugin.Error(this, new Exception("Failed to load config values."));
            return;
        }

        if (Common.GetUnitBase("ConfigCharaChatLogDetail", out var unitBase)) ToggleFontSizeConfigDropDowns(unitBase, false);
        RefreshFontSizes();
    }

    private void ShowLogMessageDetour(RaptureLogModule* logModule, uint logMessage) {
        // No toast spam please
        if (logMessage == 801) return;
        showLogMessageHook.Original(logModule, logMessage);
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
                TooltipManager.RemoveTooltip(unitBase, &tnc->AtkResNode);
                tnc->SetText(txt.RawData);
            } else {
                var str = txt.ToDalamudString().Append(new List<Payload>() { new UIForegroundPayload(3), new TextPayload(" (Managed by Simple Tweaks)"), new UIForegroundPayload(0) });
                TooltipManager.AddTooltip(unitBase, &tnc->AtkResNode, $"Setting managed by Simple Tweak:\n  - {LocalizedName}");
                tnc->SetText(str.Encode());
            }

            tnc->ResizeNodeForCurrentText();

            ddc->SetEnabledState(toggle);
        }
    }

    [AddonPostSetup("ConfigCharaChatLogDetail")]
    private void OnAddonSetup(AtkUnitBase* addon) {
        ToggleFontSizeConfigDropDowns(addon, false);
    }

    private void SetFontSizeDetour(byte* chatLogPanelWithOffset, byte fontSize) {
        try {
            if (Config.FontSize is { Length: 4 }) {
                var chatLogPanel = (AddonChatLogPanel*)(chatLogPanelWithOffset - 0x278);
                if (Config.FontSize != null) {
                    for (var i = 0; i < 4; i++) {
                        var panel = Common.GetUnitBase<AddonChatLogPanel>($"ChatLogPanel_{i}");
                        if (panel == null) continue;
                        if (panel == chatLogPanel) {
                            fontSize = (byte)Math.Clamp(Config.FontSize[i], MinimumFontSize, MaximumFontSize);
                            break;
                        }
                    }
                }
            }

            setFontSizeHook.Original(chatLogPanelWithOffset, fontSize);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void AfterDisable() {
        if (Common.GetUnitBase("ConfigCharaChatLogDetail", out var unitBase)) ToggleFontSizeConfigDropDowns(unitBase, true);
        RefreshFontSizes(true);
    }
}
