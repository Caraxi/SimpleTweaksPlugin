using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Duty List Background")]
[TweakDescription("Adds a configurable background to the Duty List")]
[TweakAuthor("MidoriKami")]
[TweakVersion(2)]
[TweakReleaseVersion("1.8.7.0")]
[Changelog("1.8.7.1", "Improved tweak stability.")]
[Changelog("1.8.7.3", "Prevent crash when using Aestetician.")]
[Changelog("1.9.0.0", "Reimplemented with a new method to avoid crashes.")]
public unsafe class DutyListBackground : UiAdjustments.SubTweak {
    public Config TweakConfig { get; private set; } = null!;

    protected override void Enable() {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        if (Common.GetUnitBase("NamePlate", out var unitBase)) OnAddonSetup(unitBase);
    }

    protected override void Disable() {
        SaveConfig(TweakConfig);
        if (Common.GetUnitBase("NamePlate", out var unitBase)) OnAddonFinalize(unitBase);
    }

    [AddonPostSetup("NamePlate")]
    private void OnAddonSetup(AtkUnitBase* unitBase) {
        if (unitBase == null || unitBase->RootNode == null) return;
        var imageNode = UiHelper.MakeImageNode(CustomNodes.Get(nameof(DutyListBackground)), new UiHelper.PartInfo(0, 0, 0, 0));
        imageNode->AtkResNode.NodeFlags = NodeFlags.Enabled | NodeFlags.AnchorLeft | NodeFlags.Visible;
        imageNode->WrapMode = 1;
        imageNode->Flags = 0;
        UiHelper.LinkNodeAtEnd(&imageNode->AtkResNode, unitBase);
        if (Common.GetUnitBase("_ToDoList", out var todo)) {
            OnAddonUpdate(todo);
        }
    }

    [AddonFinalize("NamePlate")]
    private void OnAddonFinalize(AtkUnitBase* unitBase) {
        if (unitBase == null || unitBase->RootNode == null) return;
        var imageNode = GetImageNode(unitBase);
        if (imageNode != null) {
            UiHelper.UnlinkAndFreeImageNode(imageNode, unitBase);
        }
    }

    private AtkImageNode* GetImageNode(AtkUnitBase* unitBase) {
        return Common.GetNodeByID<AtkImageNode>(&unitBase->UldManager, CustomNodes.Get(nameof(DutyListBackground)), NodeType.Image);
    }

    private void OnAddonUpdate(AtkUnitBase* unitBase) {
        if (Common.GetUnitBase("NamePlate", out var namePlate)) {
            var imageNode = GetImageNode(namePlate);
            if (imageNode is not null) {
                if (unitBase == null || unitBase->RootNode == null) {
                    imageNode->AtkResNode.ToggleVisibility(false);
                    return;
                }

                imageNode->AtkResNode.ToggleVisibility(unitBase->IsVisible && unitBase->RootNode->IsVisible && (unitBase->VisibilityFlags & 1) == 0);
                imageNode->AtkResNode.SetWidth(unitBase->RootNode->GetWidth());
                imageNode->AtkResNode.SetHeight(unitBase->RootNode->GetHeight());
                imageNode->AtkResNode.SetPositionFloat(unitBase->X, unitBase->Y);
                imageNode->AtkResNode.Color.A = (byte)(TweakConfig.BackgroundColor.W * 255);
                imageNode->AtkResNode.AddRed = (byte)(TweakConfig.BackgroundColor.X * 255);
                imageNode->AtkResNode.AddGreen = (byte)(TweakConfig.BackgroundColor.Y * 255);
                imageNode->AtkResNode.AddBlue = (byte)(TweakConfig.BackgroundColor.Z * 255);
            }
        }
    }

    [FrameworkUpdate]
    private void OnFrameworkUpdate() {
        if (Common.GetUnitBase("_ToDoList", out var unitBase)) {
            OnAddonUpdate(unitBase);
        } else if (Common.GetUnitBase("NamePlate", out var namePlate)) {
            var imageNode = GetImageNode(namePlate);
            if (imageNode != null) {
                imageNode->AtkResNode.ToggleVisibility(false);
            }
        }
    }

    private void DrawConfig() {
        if (ImGui.ColorEdit4($"Background Color##ColorEdit", ref TweakConfig.BackgroundColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar)) {
            if (Common.GetUnitBase("_ToDoList", out var unitBase)) {
                OnAddonUpdate(unitBase);
            }
        }
    }

    public class Config : TweakConfig {
        public Vector4 BackgroundColor = new(0.0f, 0.0f, 0.0f, 0.40f);
    }
}
