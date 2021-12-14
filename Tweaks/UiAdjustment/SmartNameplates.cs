﻿using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using ObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
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

        private Configs config;

        private const int statusFlagsOffset = 0x19A0;
        private IntPtr targetManager = IntPtr.Zero;
        private delegate byte ShouldDisplayNameplateDelegate(IntPtr raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance);
        private Hook<ShouldDisplayNameplateDelegate> shouldDisplayNameplateHook;
        private delegate byte GetTargetTypeDelegate(GameObject* actor);
        private GetTargetTypeDelegate GetTargetType;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            ImGui.Checkbox(LocString("Show HP") + "##SmartNameplatesShowHP", ref config.ShowHP);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(LocString("ShopHpTooltip", "Will not hide HP bars for affected players."));

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted(LocString("IgnorePlayersNote", "The following options will disable the tweak for certain players."));
            ImGui.Checkbox(LocString("Ignore Party Members") + "##SmartNameplatesIgnoreParty", ref config.IgnoreParty);
            ImGui.Checkbox(LocString("Ignore Alliance Members") + "##SmartNameplatesIgnoreAlliance", ref config.IgnoreAlliance);
            ImGui.Checkbox(LocString("Ignore Friends") + "##SmartNameplatesIgnoreFriends", ref config.IgnoreFriends);
            ImGui.Checkbox(LocString("Ignore Dead Players") + "##SmartNameplatesIgnoreDead", ref config.IgnoreDead);
            ImGui.Checkbox(LocString("Ignore Targeted Players") + "##SmartNameplatesIgnoreTargets", ref config.IgnoreTargets);
        };

        // returns 2 bits (b01 == display name, b10 == display hp)
        private unsafe byte ShouldDisplayNameplateDetour(IntPtr raptureAtkModule, GameObject* actor, GameObject* localPlayer, float distance) {
            if (actor->ObjectKind != (byte) ObjectKind.Pc) goto ReturnOriginal;
            var pc = (BattleChara*) actor;

            var targets = TargetSystem.Instance();

            if (actor == localPlayer // Ignore localplayer
                || (pc->Character.StatusFlags & (byte) StatusFlags.InCombat) == 0 // Alternate in combat flag
                || GetTargetType(actor) == 3

                || (config.IgnoreParty && (pc->Character.StatusFlags & (byte) StatusFlags.PartyMember) != 0) // Ignore party members
                || (config.IgnoreAlliance && (pc->Character.StatusFlags & (byte) StatusFlags.AllianceMember) != 0) // Ignore alliance members
                || (config.IgnoreFriends && (pc->Character.StatusFlags & (byte) StatusFlags.Friend) != 0) // Ignore friends
                || (config.IgnoreDead && pc->Character.Health == 0) // Ignore dead players

                // Ignore targets
                || (config.IgnoreTargets && targets->Target == actor
                    || targets->SoftTarget == actor
                    || targets->FocusTarget == actor)) goto ReturnOriginal;

            return (byte)(config.ShowHP ? (shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance) & ~1) : 0); // Ignore HP


            ReturnOriginal:
            return shouldDisplayNameplateHook.Original(raptureAtkModule, actor, localPlayer, distance);
        }

        public override void Enable() {
            config = LoadConfig<Configs>() ?? new Configs();
            targetManager = targetManager != IntPtr.Zero ? targetManager : Common.Scanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 50 ?? 48 85 DB", 3); // Taken from Dalamud
            GetTargetType ??= Marshal.GetDelegateForFunctionPointer<GetTargetTypeDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 83 F8 06 0F 87 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 8B C0"));
            shouldDisplayNameplateHook ??= new Hook<ShouldDisplayNameplateDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 89 44 24 40 48 C7 85"), ShouldDisplayNameplateDetour);
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
