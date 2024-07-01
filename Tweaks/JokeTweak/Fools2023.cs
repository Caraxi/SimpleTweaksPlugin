using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.JokeTweak;

public unsafe class Fools2023 : JokeTweaks.SubTweak {
    public override string Name => "April Fools 2023";
    public override string Description => "Re-enable the April Fools 2023 Features";

    public class Configs : TweakConfig {
        public int HeartRatio = 50;
        public int SleepRatio = 50;
        public int TreeRatio = 1;
        public int NoneRatio = 0;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree =>
        (ref bool hasChanged) => {

            bool Editor(string name, ref int value) {
                var c = ImGui.SliderInt(name, ref value, 0, Math.Max(100, value + 10));
                ImGui.SameLine();
                if (value == 0) {
                    ImGui.TextDisabled("Disabled");
                } else {
                    ImGui.TextDisabled($"({((value / (float)GetRatioTotal()) * 100):F2}%%)");
                }
                
                return c;
            }

            hasChanged |= Editor("Heart Ratio", ref Config.HeartRatio);
            hasChanged |= Editor("Sleep Ratio", ref Config.SleepRatio);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Does not display in certain circumstances.");
            hasChanged |= Editor("Tree Ratio", ref Config.TreeRatio);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Does not display in certain circumstances.");
            hasChanged |= Editor("No Effect Ratio", ref Config.NoneRatio);
        };

    public const ushort HeartEffect = 227;
    public const ushort SleepEffect = 3;
    public const ushort TreeEffect = 937;
    public const ushort NoneEffect = 4;

    private ulong GetRatioTotal(bool includeSpecial = true) {
        ulong total = 0;
        if (Config.HeartRatio > 0) total += (uint)Config.HeartRatio;
        if (Config.SleepRatio > 0 && includeSpecial) total += (uint)Config.SleepRatio;
        if (Config.TreeRatio > 0 && includeSpecial) total += (uint)Config.TreeRatio;
        if (Config.NoneRatio > 0) total += (uint)Config.NoneRatio;
        return total;
        
    }

    public override void Setup() {
        AddChangelogNewTweak("1.8.6.0");
        base.Setup();
    }

    private ushort RandomStatus(bool canUseSpecial = true) {
        if (canUseSpecial && Common.GetUnitBase("CharaMake", out _)) canUseSpecial = false;

        var random = new Random().NextInt64(0, (long)GetRatioTotal(canUseSpecial));
        
        if (Config.HeartRatio > 0) {
            if (random < Config.HeartRatio) return HeartEffect;
            random -= Config.HeartRatio;
        }

        if (Config.SleepRatio > 0 && canUseSpecial) {
            if (random < Config.SleepRatio) return SleepEffect;
            random -= Config.SleepRatio;
        }

        if (Config.TreeRatio > 0 && canUseSpecial) {
            if (random < Config.TreeRatio) return TreeEffect;
        }

        return NoneEffect;
    }
    
    private readonly ushort[] statuses = { SleepEffect, HeartEffect, TreeEffect, NoneEffect };
    
    public void FrameworkUpdate() {
        if (Service.Condition[ConditionFlag.OccupiedInQuestEvent]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene78]) return;
        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;
        
        for (var i = 200; i < 243; i++) {
            if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
            var o = (BattleChara*) GameObjectManager.Instance()->Objects.IndexSorted.GetPointer(i);
            if (o == null) continue;
            
            if (statuses.Any(s => o->GetStatusManager()->HasStatus(s))) continue;

            var r = RandomStatus(i >= 240);
            if (r == 0) continue;
            o->GetStatusManager()->AddStatus(r);
        }
    }

    protected override void Enable() {
        Common.FrameworkUpdate += FrameworkUpdate;
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    protected override void Disable() {
        Common.FrameworkUpdate -= FrameworkUpdate;
        SaveConfig(Config);


        Service.Framework.RunOnTick(() => {
            for (var i = 200; i < 243; i++) {
                if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
                var o = (BattleChara*) GameObjectManager.Instance()->Objects.IndexSorted.GetPointer(i);
                if (o == null) continue;
                foreach (var s in statuses) {
                    if (o->GetStatusManager()->HasStatus(s)) {
                        o->GetStatusManager()->RemoveStatus(s);
                    }
                }
            }
        });
        
        base.Disable();
    }
}

