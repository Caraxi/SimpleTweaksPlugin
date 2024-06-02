using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class SmartNameplates : UiAdjustments.SubTweak {
    public override string Name => "Smart Nameplates";
    public override string Description => "Provides options to hide other player's nameplates in combat under certain conditions.";
    protected override string Author => "UnknownX";

    public class Configs : TweakConfig {
        public bool ShowHP = false;
        public bool IgnoreParty = false;
        public bool IgnoreAlliance = false;
        public bool IgnoreFriends = false;
        public bool IgnoreDead = false;
        public bool IgnoreTargets = false;
    }

    public Configs Config { get; private set; }
    
    private IntPtr targetManager = IntPtr.Zero;
    private delegate byte ShouldDisplayNameplateDelegate(IntPtr raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance);
    private HookWrapper<ShouldDisplayNameplateDelegate> shouldDisplayNameplateHook;
    private delegate byte GetTargetTypeDelegate(GameObject* actor);
    private GetTargetTypeDelegate GetTargetType;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        ImGui.Checkbox(LocString("Show HP") + "##SmartNameplatesShowHP", ref Config.ShowHP);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(LocString("ShopHpTooltip", "Will not hide HP bars for affected players."));

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted(LocString("IgnorePlayersNote", "The following options will disable the tweak for certain players."));
        ImGui.Checkbox(LocString("Ignore Party Members") + "##SmartNameplatesIgnoreParty", ref Config.IgnoreParty);
        ImGui.Checkbox(LocString("Ignore Alliance Members") + "##SmartNameplatesIgnoreAlliance", ref Config.IgnoreAlliance);
        ImGui.Checkbox(LocString("Ignore Friends") + "##SmartNameplatesIgnoreFriends", ref Config.IgnoreFriends);
        ImGui.Checkbox(LocString("Ignore Dead Players") + "##SmartNameplatesIgnoreDead", ref Config.IgnoreDead);
        ImGui.Checkbox(LocString("Ignore Targeted Players") + "##SmartNameplatesIgnoreTargets", ref Config.IgnoreTargets);
    };

    // returns 2 bits (b01 == display name, b10 == display hp)
    private unsafe byte ShouldDisplayNameplateDetour(IntPtr raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance) {
        if (actor->ObjectKind != ObjectKind.Pc) goto ReturnOriginal;
        var pc = (BattleChara*) actor;

        var targets = TargetSystem.Instance();

        if (actor == localPlayer // Ignore localplayer
            || pc->Character.InCombat == false
            || GetTargetType(actor) == 3

            || (Config.IgnoreParty && pc->Character.IsPartyMember)
            || (Config.IgnoreAlliance && pc->Character.IsAllianceMember)
            || (Config.IgnoreFriends && pc->Character.IsFriend)
            || (Config.IgnoreDead && pc->Character.CharacterData.Health == 0)

            // Ignore targets
            || (Config.IgnoreTargets && targets->Target == actor
                || targets->SoftTarget == actor
                || targets->FocusTarget == actor)) goto ReturnOriginal;

        return (byte)(Config.ShowHP ? (shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance) & ~1) : 0); // Ignore HP


        ReturnOriginal:
        return shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance);
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        targetManager = targetManager != IntPtr.Zero ? targetManager : Service.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 50 ?? 48 85 DB", 3); // Taken from Dalamud
        GetTargetType ??= Marshal.GetDelegateForFunctionPointer<GetTargetTypeDelegate>(Service.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B 01 48 8B F9 8B 1D"));
        shouldDisplayNameplateHook ??= Common.Hook(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 44 24 40 48 C7 85"), new ShouldDisplayNameplateDelegate(ShouldDisplayNameplateDetour));
        shouldDisplayNameplateHook?.Enable();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        shouldDisplayNameplateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        shouldDisplayNameplateHook?.Dispose();
        base.Dispose();
    }
}
