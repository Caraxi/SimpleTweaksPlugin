using System;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Structs;
using Dalamud.Hooking;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class SmartNameplates : UiAdjustments.SubTweak {
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

        private Configs config;

        private const int statusFlagsOffset = 0x19A0;
        private IntPtr targetManager = IntPtr.Zero;
        private delegate byte ShouldDisplayNameplateDelegate(IntPtr raptureAtkModule, IntPtr actor, IntPtr localPlayer, float distance);
        private Hook<ShouldDisplayNameplateDelegate> shouldDisplayNameplateHook;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            ImGui.Checkbox("Show HP##SmartNameplatesShowHP", ref config.ShowHP);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Will not hide HP bars for affected players.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("The following options will disable the tweak for certain players.");
            ImGui.Checkbox("Ignore Party Members##SmartNameplatesIgnoreParty", ref config.IgnoreParty);
            ImGui.Checkbox("Ignore Alliance Members##SmartNameplatesIgnoreAlliance", ref config.IgnoreAlliance);
            ImGui.Checkbox("Ignore Friends##SmartNameplatesIgnoreFriends", ref config.IgnoreFriends);
            ImGui.Checkbox("Ignore Dead Players##SmartNameplatesIgnoreDead", ref config.IgnoreDead);
            ImGui.Checkbox("Ignore Targeted Players##SmartNameplatesIgnoreTargets", ref config.IgnoreTargets);
        };

        // Crashes the game if ANY Dalamud Actor is created from within it, which is why everything is using offsets
        // returns 2 bits (b01 == display name, b10 == display hp)
        private unsafe byte ShouldDisplayNameplateDetour(IntPtr raptureAtkModule, IntPtr actor, IntPtr localPlayer, float distance) {
            var actorStatusFlags = *(byte*)(actor + statusFlagsOffset);

            // true is a placeholder for config
            if (actor == localPlayer // Ignore localplayer
                || (*(byte*)(localPlayer + statusFlagsOffset) & 2) == 0 // Alternate in combat flag
                || *(ObjectKind*)(actor + ActorOffsets.ObjectKind) != ObjectKind.Player // Ignore nonplayers

                || (config.IgnoreParty && (actorStatusFlags & 16) > 0) // Ignore party members
                || (config.IgnoreAlliance && (actorStatusFlags & 32) > 0) // Ignore alliance members
                || (config.IgnoreFriends && (actorStatusFlags & 64) > 0) // Ignore friends
                || (config.IgnoreDead && *(int*)(actor + ActorOffsets.CurrentHp) == 0) // Ignore dead players

                // Ignore targets
                || (config.IgnoreTargets && (*(IntPtr*)(targetManager + TargetOffsets.CurrentTarget) == actor
                    || *(IntPtr*)(targetManager + TargetOffsets.SoftTarget) == actor
                    || *(IntPtr*)(targetManager + TargetOffsets.FocusTarget) == actor)))
                return shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance);

            return (byte)(config.ShowHP ? (shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance) & ~1) : 0); // Ignore HP
        }

        public override void Enable() {
            config = LoadConfig<Configs>() ?? new Configs();
            targetManager = targetManager != IntPtr.Zero ? targetManager : Common.Scanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 50 ?? 48 85 DB", 3); // Taken from Dalamud
            shouldDisplayNameplateHook ??= new Hook<ShouldDisplayNameplateDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 89 44 24 40 48 C7 85 88 15 02 00 00 00 00 00"), new ShouldDisplayNameplateDelegate(ShouldDisplayNameplateDetour));
            shouldDisplayNameplateHook?.Enable();
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(config);
            shouldDisplayNameplateHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            shouldDisplayNameplateHook?.Dispose();
            base.Dispose();
        }
    }
}
