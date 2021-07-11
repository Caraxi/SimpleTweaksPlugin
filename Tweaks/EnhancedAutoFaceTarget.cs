using System;
using System.Runtime.InteropServices;
using Dalamud;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class EnhancedAutoFaceTarget : Tweak {
        public override string Name => "Enhanced Auto Face Target";
        public override string Description => "Changes the auto face target setting to only apply when necessary.";
        protected override string Author => "UnknownX";

        public class Configs : TweakConfig {
            public bool FixCones = false;
        }

        private Configs config;

        private IntPtr changeAddress = IntPtr.Zero;
        private byte[] originalBytes = new byte[5];

        private static readonly uint[] coneActions = {
            //7418, // Flamethrower (MCH) (causes tracking and thus fails the skill if the target moves)
            11402, // Flame Thrower (BLU)
            11390, // Aqua Breath
            11414, // Level 5 Petrify
            11383, // Snort
            11399, // The Look
            11388, // Bad Breath
            11422, // Ink Jet
            11428, // Mountain Buster
            11430, // Glass Dance
            18296, // Protean Wave
            18297, // Northerlies
            18299, // Kaltstrahl
            18323, // Surpanakha
            23283, // Malediction of Water
            //23288, // Phantom Flurry (causes tracking and thus fails the skill if the target moves)
            //23289, // Phantom Flurry (finisher) (server rejects this)
        };
        private delegate IntPtr GetActionInfoDelegate(uint actionInfoID);
        private GetActionInfoDelegate GetActionInfo;

        private unsafe void ModifyCones(bool enable) {
            GetActionInfo ??= Marshal.GetDelegateForFunctionPointer<GetActionInfoDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 6B F6 0D"));
            foreach (var id in coneActions) {
                var actionInfo = (byte*)GetActionInfo(id);
                *(actionInfo + 0x32) = (byte)(enable ? *(actionInfo + 0x24) : 0); // Set range to radius
                var targetFlags = *(actionInfo + 0x35);
                *(actionInfo + 0x35) = (byte)(enable ? (targetFlags | 32) : (targetFlags & ~32)); // Adds target enemies bit
                //var targetRequirements = *(actionInfo + 0x36);
                //*(actionInfo + 0x36) = (byte)(enable ? (targetRequirements | 16) : (targetRequirements & ~16)); // Adds face target requirement bit (all of these have this already)
            }
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            if (ImGui.Checkbox("Add Face Target to Cones##FixCones", ref config.FixCones))
                ModifyCones(config.FixCones);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Will make certain actions face enemies if targeted, such as Bad Breath or Surpanakha.");
        };

        public override void Enable() {
            if (Enabled) return;

            config = LoadConfig<Configs>() ?? new Configs();

            changeAddress = Common.Scanner.ScanText("41 80 7E 2F 06 75 1E 48 8D 0D");
            if (SafeMemory.ReadBytes(changeAddress, 5, out originalBytes)) {
                if (SafeMemory.WriteBytes(changeAddress, new byte[] {0x41, 0xF6, 0x46, 0x36, 0x10})) { // cmp byte ptr [r14+2Fh], 6 -> test byte ptr [r14+36h], 10
                    base.Enable();
                } else {
                    SimpleLog.Error("Failed to write new instruction");
                }
            } else {
                SimpleLog.Error("Failed to read original instruction");
            }

            if (config.FixCones)
                ModifyCones(true);
        }

        public override void Disable() {
            if (!Enabled) return;

            SaveConfig(config);

            if (!SafeMemory.WriteBytes(changeAddress, originalBytes)) {
                SimpleLog.Error("Failed to write original instruction");
            }

            if (config.FixCones)
                ModifyCones(false);

            base.Disable();
        }

        public override void Dispose() {
            Disable();
            base.Dispose();
        }
    }
}

