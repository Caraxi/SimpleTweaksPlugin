using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;
[TweakName("Custom FC Chest Tabs")]
[TweakDescription("Allows renaming of each tab in the Free Company chest (client side only).")]
[TweakAuthor("croizat")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class RenameFCChestTabs : UiAdjustments.SubTweak
{
    private Configuration Config { get; set; } = null!;

    public class Configuration : TweakConfig {
        public string TabOne = string.Empty;
        public string TabTwo = string.Empty;
        public string TabThree = string.Empty;
        public string TabFour = string.Empty;
        public string TabFive = string.Empty;
    }

    protected void DrawConfig(ref bool hasChanged) {
        if (ImGui.InputText("Tab One", ref Config.TabOne, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            hasChanged = true;
        if (ImGui.InputText("Tab Two", ref Config.TabTwo, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            hasChanged = true;
        if (ImGui.InputText("Tab Three", ref Config.TabThree, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            hasChanged = true;
        if (ImGui.InputText("Tab Four", ref Config.TabFour, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            hasChanged = true;
        if (ImGui.InputText("Tab Five", ref Config.TabFive, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            hasChanged = true;
    }

    protected override void Enable()
    {
        base.Enable();
        Config = LoadConfig<Configuration>() ?? new Configuration();
    }

    protected override void Disable()
    {
        SaveConfig(Config);
        base.Disable();
    }

    [AddonPreDraw("FreeCompanyChest")]
    private void AddonPrewDraw(AtkUnitBase* atkUnitBase) {
        if (Config.TabOne != string.Empty)
        {
            var node = Common.GetNodeByIDChain(atkUnitBase->GetRootNode(), 1, 9, 10, 9);
            if (node is not null)
                node->GetAsAtkTextNode()->NodeText.SetString(Config.TabOne);
        }
        if (Config.TabTwo != string.Empty)
        {
            var node = Common.GetNodeByIDChain(atkUnitBase->GetRootNode(), 1, 9, 11, 9);
            if (node is not null)
                node->GetAsAtkTextNode()->NodeText.SetString(Config.TabTwo);
        }
        if (Config.TabThree != string.Empty)
        {
            var node = Common.GetNodeByIDChain(atkUnitBase->GetRootNode(), 1, 9, 12, 9);
            if (node is not null)
                node->GetAsAtkTextNode()->NodeText.SetString(Config.TabThree);
        }
        if (Config.TabFour != string.Empty)
        {
            var node = Common.GetNodeByIDChain(atkUnitBase->GetRootNode(), 1, 9, 13, 9);
            if (node is not null)
                node->GetAsAtkTextNode()->NodeText.SetString(Config.TabFour);
        }
        if (Config.TabFive != string.Empty)
        {
            var node = Common.GetNodeByIDChain(atkUnitBase->GetRootNode(), 1, 9, 14, 9);
            if (node is not null)
                node->GetAsAtkTextNode()->NodeText.SetString(Config.TabFive);
        }
    }
}
