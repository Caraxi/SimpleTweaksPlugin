using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class LargeCooldownCounter : UiAdjustments.SubTweak {

    private delegate byte ActionBarBaseUpdate(AddonActionBarBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);

    private HookWrapper<ActionBarBaseUpdate> actionBarBaseUpdateHook;
    private HookWrapper<ActionBarBaseUpdate> doubleCrossBarUpdateHook;

    public override string Name => "Large Cooldown Counter";
    public override string Description => "Increases the size of cooldown counters on hotbars.";

    public override void Enable() {
        actionBarBaseUpdateHook ??= Common.Hook<ActionBarBaseUpdate>("E8 ?? ?? ?? ?? 83 BB ?? ?? ?? ?? ?? 75 09", ActionBarBaseUpdateDetour);
        doubleCrossBarUpdateHook ??= Common.Hook<ActionBarBaseUpdate>("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B 7A 30", DoubleCrossBarBaseUpdateDetour);
        Config = LoadConfig<Configs>() ?? new Configs();
        actionBarBaseUpdateHook?.Enable();
        doubleCrossBarUpdateHook?.Enable();
        actionManager = ActionManager.Instance();
        base.Enable();
    }

    private byte ActionBarBaseUpdateDetour(AddonActionBarBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        var ret = actionBarBaseUpdateHook.Original(atkUnitBase, numberArrayData, stringArrayData);
        try {
            Update(atkUnitBase);
        } catch {
            //
        }
        return ret;
    }

    private byte DoubleCrossBarBaseUpdateDetour(AddonActionBarBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        var ret = doubleCrossBarUpdateHook.Original(atkUnitBase, numberArrayData, stringArrayData);
        try {
            Update(atkUnitBase);
        } catch {
            //
        }
        return ret;
    }

    private readonly string[] allActionBars = {
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
    };
    public class Configs : TweakConfig {
        public Font Font = Font.Default;
        public int FontSizeAdjust;
        public Vector4 CooldownColour = new(1, 1, 1, 1);
        public Vector4 CooldownEdgeColour = new(0.2F, 0.2F, 0.2F, 1);
        public Vector4 InvalidColour = new(0.85f, 0.25f, 0.25f, 1);
        public Vector4 InvalidEdgeColour = new(0.34f, 0, 0, 1);
    }

    public Configs Config { get; private set; }
        
    public enum Font {
        Default,
        FontB,
        FontC,
        FontD,
    }
        
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
        if (ImGui.BeginCombo(LocString("Font") + "###st_uiAdjustment_largeCooldownCounter_fontSelect", $"{Config.Font}")) {
            foreach (var f in (Font[])Enum.GetValues(typeof(Font))) {
                if (ImGui.Selectable($"{f}##st_uiAdjustment_largeCooldownCount_fontOption", f == Config.Font)) {
                    Config.Font = f;
                    hasChanged = true;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.SliderInt(LocString("Font Size Adjust") + "##st_uiAdjustment_largEcooldownCounter_fontSize", ref Config.FontSizeAdjust, -15, 30);
        hasChanged |= ImGui.ColorEdit4(LocString("Text Colour") + "##largeCooldownCounter", ref Config.CooldownColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Edge Colour") + "##largeCooldownCounter", ref Config.CooldownEdgeColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Out Of Range Colour") + "##largeCooldownCounter", ref Config.InvalidColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Out Of Range Edge Colour") + "##largeCooldownCounter", ref Config.InvalidEdgeColour);


    };

    private void UpdateAll(bool reset = false) {
        foreach (var actionBar in allActionBars) {
            var ab = (AddonActionBarBase*) Service.GameGui.GetAddonByName(actionBar, 1);
            Update(ab, reset);
        }
    }

    private void Update(AddonActionBarBase* ab, bool reset = false) {
        if (ab == null || ab->ActionBarSlotsAction == null) return;
        var hotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        var name = Marshal.PtrToStringUTF8(new IntPtr(ab->AtkUnitBase.Name));
        if (name == null) return;

        for (var i = 0; i < ab->HotbarSlotCount; i++) {
            var slot = ab->ActionBarSlotsAction[i];

            var slotStruct = hotbarModule->HotBar[ab->HotbarID]->Slot[i];

            if (name.StartsWith("_ActionDoubleCross")) {
                var dcBar = (AddonActionDoubleCrossBase*)ab;
                slotStruct = hotbarModule->HotBar[dcBar->BarTarget]->Slot[i + (dcBar->UseLeftSide != 0 ? 0 : 8)];
            }

            if (name == "_ActionCross") {
                var cBar = (AddonActionCross*)ab;
                var ex = cBar->ExpandedHoldControls;
                if (ex > 0) {                           // char config setting for this returns 0-19, but here it returns 1-20 so that 0 can represent non-ex bar
                    if (i < 4 || i > 11) reset = true;  // ignore first and last 4 slots (non-visible)
                    else
                    {
                        reset = false;                                                      
                        var exBarTarget = (ex < 17) ? (((ex - 1) >> 1) + 10) :                          // ex value  1-16 = left/right sides of cross bars 1-8 (IDs 10-17)
                                                      (ab->HotbarID + (ex < 19 ? 1 : -1) - 2) % 8 + 10; //          17-20 = "Cycle" options (uses bar before/after main active bar)
                        var exUseLeftSide = (ex < 17 ? ex : ex + 1) & 1;                                // for some reason, the Cycle options invert the left/right pattern
                        slotStruct = hotbarModule->HotBar[exBarTarget]->Slot[i + (exUseLeftSide != 0 ? -4 : 4)];
                    }
                }
            }

            if ((slot.PopUpHelpTextPtr != null || reset) && slot.Icon != null) {
                UpdateIcon(slot.Icon, slotStruct, reset);
            }
        }
    }

    private byte DefaultFontSize => Config.Font switch {
        Font.FontB => 14,
        Font.FontC => 15,
        Font.FontD => 34,
        _ => 18,
    };

    private ActionManager* actionManager;

    private byte GetFontSize() {
        var s = (Config.FontSizeAdjust * 2) + DefaultFontSize;
        if (s < 4) s = 4;
        if (s > 255) s = 255;
        return (byte) s;
    }
        
    private int GetRecastGroup(HotbarSlotType type, uint id, HotBarSlot* slot) {
        var recastGroup = 0;

        switch (type) {
            case HotbarSlotType.Action:
                var adjustedActionId = type == HotbarSlotType.Action ? actionManager->GetAdjustedActionId(id) : id;
                recastGroup = actionManager->GetRecastGroup(1, adjustedActionId);
                if (recastGroup == 57) recastGroup = 0;
                break;
            case HotbarSlotType.Item:
                recastGroup = actionManager->GetRecastGroup(2, id);

                break;
            case HotbarSlotType.GeneralAction:
                recastGroup = actionManager->GetRecastGroup(5, id);
                break;
            case HotbarSlotType.Macro:
                if (slot->IconTypeA != HotbarSlotType.Macro) {
                    recastGroup = GetRecastGroup(slot->IconTypeA, slot->IconA, slot);
                }
                break;
        }

        return recastGroup;
    }

    private (float range, uint error) GetRangeError(HotbarSlotType type, uint id, HotBarSlot* slot, GameObject* source, GameObject* target) {
        var range = 0f;
        var rangeError = 0U;

        switch (type) {
            case HotbarSlotType.Action:
                var actionId = actionManager->GetAdjustedActionId(id);
                range = ActionManager.GetActionRange(actionId);
                rangeError = ActionManager.GetActionInRangeOrLoS(actionId, source, target);
                break;
            case HotbarSlotType.Macro:
                if (slot->IconTypeA != HotbarSlotType.Macro) {
                    return GetRangeError(slot->IconTypeA, slot->IconA, slot, source, target);
                }
                break;
        }
        return (range, rangeError);
    }

    private void UpdateIcon(AtkComponentNode* iconComponent, HotBarSlot* slotStruct, bool reset = false) {
        if (iconComponent == null) return;
        var cooldownTextNode = (AtkTextNode*)iconComponent->Component->UldManager.NodeList[13];
        if (cooldownTextNode->AtkResNode.Type != NodeType.Text) return;
        if (reset == false && (cooldownTextNode->AtkResNode.Flags & 0x10) != 0x10) return;
        if (cooldownTextNode == null) return;

        if (reset == false) {
            if (slotStruct == null) {
                reset = true;
            } else {
                int recastGroup = GetRecastGroup(slotStruct->CommandType, slotStruct->CommandId, slotStruct);
                if (recastGroup is 0) {
                    reset = true;
                } else {
                    var recastDetail = actionManager->GetRecastGroupDetail(recastGroup);
                    if (recastDetail == null || recastDetail->IsActive == 0) reset = true;
                }
            }
        }

        if (reset) {
            cooldownTextNode->AtkResNode.X = 3;
            cooldownTextNode->AtkResNode.Y = 37;
            cooldownTextNode->AtkResNode.Width = 48;
            cooldownTextNode->AtkResNode.Height = 12;
            cooldownTextNode->AlignmentFontType = (byte)AlignmentType.Left;
            cooldownTextNode->FontSize = 12;
        } else {
            cooldownTextNode->AtkResNode.X = 0;
            cooldownTextNode->AtkResNode.Y = 0;
            cooldownTextNode->AtkResNode.Width = 46;
            cooldownTextNode->AtkResNode.Height = 46;
            cooldownTextNode->AlignmentFontType = (byte)((0x10 * (byte) Config.Font) | (byte) AlignmentType.Center);
            cooldownTextNode->FontSize = GetFontSize();

            var self = GameObjectManager.GetGameObjectByIndex(0);
            if (self != null) {
                var currentTarget = TargetSystem.Instance()->GetCurrentTarget();
                if (currentTarget == null) currentTarget = self;
                var (range, rangeError) = GetRangeError(slotStruct->CommandType, slotStruct->CommandId, slotStruct, self, currentTarget);
                if (range > 0 && rangeError == 566) { // Out of Range
                    cooldownTextNode->TextColor.R = (byte)(Config.InvalidColour.X * 255f);
                    cooldownTextNode->TextColor.G = (byte)(Config.InvalidColour.Y * 255f);
                    cooldownTextNode->TextColor.B = (byte)(Config.InvalidColour.Z * 255f);
                    cooldownTextNode->TextColor.A = (byte)(Config.InvalidColour.W * 255f);

                    cooldownTextNode->EdgeColor.R = (byte)(Config.InvalidEdgeColour.X * 255f);
                    cooldownTextNode->EdgeColor.G = (byte)(Config.InvalidEdgeColour.Y * 255f);
                    cooldownTextNode->EdgeColor.B = (byte)(Config.InvalidEdgeColour.Z * 255f);
                    cooldownTextNode->EdgeColor.A = (byte)(Config.InvalidEdgeColour.W * 255f);

                } else {
                    cooldownTextNode->TextColor.R = (byte)(Config.CooldownColour.X * 255f);
                    cooldownTextNode->TextColor.G = (byte)(Config.CooldownColour.Y * 255f);
                    cooldownTextNode->TextColor.B = (byte)(Config.CooldownColour.Z * 255f);
                    cooldownTextNode->TextColor.A = (byte)(Config.CooldownColour.W * 255f);

                    cooldownTextNode->EdgeColor.R = (byte)(Config.CooldownEdgeColour.X * 255f);
                    cooldownTextNode->EdgeColor.G = (byte)(Config.CooldownEdgeColour.Y * 255f);
                    cooldownTextNode->EdgeColor.B = (byte)(Config.CooldownEdgeColour.Z * 255f);
                    cooldownTextNode->EdgeColor.A = (byte)(Config.CooldownEdgeColour.W * 255f);
                }
            }
        }
            
        cooldownTextNode->AtkResNode.Flags_2 |= 0x1;
    }

    public override void Disable() {
        actionBarBaseUpdateHook?.Disable();
        doubleCrossBarUpdateHook?.Disable();
        SaveConfig(Config);
        UpdateAll(true);
        base.Disable();
    }

    public override void Dispose() {
        actionBarBaseUpdateHook?.Dispose();
        doubleCrossBarUpdateHook?.Dispose();
        base.Dispose();
    }
}
