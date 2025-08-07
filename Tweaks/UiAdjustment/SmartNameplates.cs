using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Smart Nameplates")]
[TweakDescription("Provides options to hide other player's nameplates in combat under certain conditions.")]
[TweakAuthor("UnknownX")]
[TweakAutoConfig]
public unsafe class SmartNameplates : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool ShowHP;
        public bool IgnoreParty;
        public bool IgnoreAlliance;
        public bool IgnoreFriends;
        public bool IgnoreDead;
        public bool IgnoreTargets;
    }

    [TweakConfig] public Configs Config { get; private set; }

    private delegate byte ShouldDisplayNameplateDelegate(RaptureAtkModule* raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 89 44 24 50 48 C7 83", DetourName = nameof(ShouldDisplayNameplateDetour))]
    private HookWrapper<ShouldDisplayNameplateDelegate> shouldDisplayNameplateHook;

    protected void DrawConfig() {
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
    }

    // returns 2 bits (b01 == display name, b10 == display hp)
    private byte ShouldDisplayNameplateDetour(RaptureAtkModule* raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance) {
        if (actor->ObjectKind != ObjectKind.Pc) goto ReturnOriginal;
        var pc = (BattleChara*)actor;

        var targets = TargetSystem.Instance();

        if (actor == localPlayer
            || pc->Character.InCombat == false 
            || ActionManager.ClassifyTarget(&pc->Character) == ActionManager.TargetCategory.Enemy 
            || (Config.IgnoreParty && pc->Character.IsPartyMember) 
            || (Config.IgnoreAlliance && pc->Character.IsAllianceMember) 
            || (Config.IgnoreFriends && pc->Character.IsFriend) 
            || (Config.IgnoreDead && pc->Character.CharacterData.Health == 0) 
            // Ignore targets
            || Config.IgnoreTargets && targets->Target == actor 
            || targets->SoftTarget == actor 
            || targets->FocusTarget == actor
        ) goto ReturnOriginal;

        return (byte)(Config.ShowHP ? shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance) & ~1 : 0); // Ignore HP

        ReturnOriginal:
        return shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance);
    }
}
