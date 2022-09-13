using System;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class IslandQuickCollectAndResume : Tweak {

    public override string Name => "Quick Collect from Island Mammets";
    public override string Description => "Hold a modifier key to collect yield from produce producer and creature comforter.";

    public class Configs : TweakConfig {
        public bool Shift = true;
        public bool Ctrl;
        public bool Alt;
    }

    private delegate ushort* OpenContextMenu(AgentContext* agent, byte a2, byte a3);
    private HookWrapper<OpenContextMenu> openContextMenuHook;
    
    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Modifier Keys to Collect:");
        ImGui.Dummy(Vector2.Zero);
        ImGui.Indent();
        ImGui.BeginGroup();
        hasChanged |= ImGui.Checkbox("Shift", ref Config.Shift);
        hasChanged |= ImGui.Checkbox("Ctrl", ref Config.Ctrl);
        hasChanged |= ImGui.Checkbox("Alt", ref Config.Alt);
        ImGui.EndGroup();
        var s = ImGui.GetItemRectSize();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(min - ImGui.GetStyle().ItemSpacing, max + ImGui.GetStyle().ItemSpacing, 0x99999999);
        ImGui.SameLine();
        ImGui.BeginGroup();
        var s2 = ImGui.CalcTextSize(" + RIGHT CLICK");
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(new Vector2(s.Y / 2 - s2.Y / 2));
        ImGui.Text(" + RIGHT CLICK");
        ImGui.PopStyleVar();
        
        if (!(Config.Shift || Config.Ctrl || Config.Alt)) {
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFF3333DD);
            ImGui.Text( "  At least one modifier key must be enabled.");
            ImGui.PopStyleColor();
        }
        
        ImGui.EndGroup();
        ImGui.Unindent();
    };
    
    private string collectTextGarden = "Collect Yield & Resume Gardening Services";
    private string collectTextPasture = "Collect Leavings & Resume Caretaking Services";
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        var collectRowGarden = Service.Data.Excel.GetSheet<Addon>()?.GetRow(15339);
        if (collectRowGarden != null) collectTextGarden = collectRowGarden.Text?.RawString ?? "Collect Yield & Resume Gardening Services";

        var collectRowPasture = Service.Data.Excel.GetSheet<Addon>()?.GetRow(15221);
        if (collectRowPasture != null) collectTextPasture = collectRowPasture.Text?.RawString ?? "Collect Leavings & Resume Caretaking Services";

        openContextMenuHook ??= Common.Hook<OpenContextMenu>("E8 ?? ?? ?? ?? 45 88 7C 24", OpenContextDetour);
        openContextMenuHook?.Enable();
        base.Enable();
    }

    private bool HotkeyIsHeld => (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt) && (Config.Ctrl || Config.Shift || Config.Alt);
    
    private ushort* OpenContextDetour(AgentContext* agent, byte a2, byte a3) {
        var retVal = openContextMenuHook.Original(agent, a2, a3);
        if (!HotkeyIsHeld) return retVal;
        
        try {
            var count = agent->MainContextMenu.EventParamSpan[0].UInt;
            for (var i = 0; i < count; i++) {
                var contextItemParam = agent->MainContextMenu.EventParamSpan[7 + i];
                if (contextItemParam.Type != ValueType.AllocatedString) {
                    Plugin.Error(this, new Exception("Unexpected value"));
                    return retVal;
                }
                var contextItemName = contextItemParam.ValueString();

                if (contextItemName == collectTextGarden || contextItemName == collectTextPasture) {
                    var addonId = agent->AgentInterface.GetAddonID();
                    if (addonId == 0) return retVal;
                    var addon = Common.GetAddonByID(addonId);
                    if (addon == null) return retVal;
                    
                    Common.GenerateCallback(addon, 0, i, 0U, 0, 0);
                    agent->AgentInterface.Hide();
                    UiHelper.Close(addon);
                    return retVal;
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        return retVal;
    }

    public override void Disable() {
        openContextMenuHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        openContextMenuHook?.Dispose();
        base.Dispose();
    }
}