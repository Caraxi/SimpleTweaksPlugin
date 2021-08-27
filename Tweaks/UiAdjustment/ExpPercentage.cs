using System;
using System.Text.RegularExpressions;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ExpPercentage : UiAdjustments.SubTweak {
        public override string Name => "Show Experience Percentage";

        public class Configs : TweakConfig {
            [TweakConfigOption("Decimals", EditorSize = 140, IntMin = 0, IntMax = 3, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
            public int Decimals = 1;
        }

        public Configs Config { get; private set; }
        public override bool UseAutoConfig => true;

        private delegate void* AddonExpOnUpdateDelegate(AtkUnitBase* addonExp, NumberArrayData** numberArrayData, StringArrayData** stringArrayData, void* a4);

        private HookWrapper<AddonExpOnUpdateDelegate> addonExpOnUpdateHook;

        private Regex regexPattern;

        public override void Enable() {

            regexPattern = External.ClientState.ClientLanguage switch {
                ClientLanguage.French => new Regex(@"Exp:(\d+)/(\d+)"),
                ClientLanguage.German => new Regex(@"(\d+)/(\d+)"),
                _ => new Regex(@"EXP(\d+)/(\d+)")
            };

            addonExpOnUpdateHook ??= Common.Hook<AddonExpOnUpdateDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 30 48 8B 72 18", AddonExpOnUpdateDetour, false);
            addonExpOnUpdateHook?.Enable();

            Config = LoadConfig<Configs>() ?? new Configs();



            base.Enable();
        }

        private void* AddonExpOnUpdateDetour(AtkUnitBase* addonExp, NumberArrayData** numberArrays, StringArrayData** stringArrays, void* a4) {
            var stringArray = stringArrays[2];
            if (stringArray == null) goto ReturnOriginal;
            var strPtr = stringArray->StringArray[69];
            if (strPtr == null) goto ReturnOriginal;

            var ret =  addonExpOnUpdateHook.Original(addonExp, numberArrays, stringArrays, a4);

            try {
                var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
                var searchString = str.TextValue.Replace(",", "").Replace(".", "").Replace(" ", "");
                var values = regexPattern.Match(searchString);
                if (values.Success) {
                    var expC = float.Parse(values.Groups[1].Value);
                    var expR = float.Parse(values.Groups[2].Value);
                    var percent = (expC / expR) * 100;
                    SimpleLog.Log($"{expC} / {expR} = {percent}");
                    str.Payloads.Add(new TextPayload($" ({percent.ToString($"F{Config.Decimals}", Culture)}%)"));
                    var textNode = addonExp->GetTextNodeById(4);
                    if (textNode == null) goto ReturnOriginal;
                    textNode->SetText(str.Encode());
                }
            } catch {
                //
            }

            return ret;
            ReturnOriginal:
            return addonExpOnUpdateHook.Original(addonExp, numberArrays, stringArrays, a4);
        }

        public override void Disable() {
            addonExpOnUpdateHook?.Disable();
            SaveConfig(Config);
            base.Disable();
        }

        public override void Dispose() {
            addonExpOnUpdateHook?.Dispose();
            base.Dispose();
        }
    }
}
