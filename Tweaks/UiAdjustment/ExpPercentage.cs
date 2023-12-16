using System;
using System.Linq;
using Dalamud;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 


[Changelog("1.9.4.0", "Added the ability to show rested experience without showing experience percentage.")]
public unsafe class ExpPercentage : UiAdjustments.SubTweak {
    public override string Name => "Show Experience Percentage";

    public override string Description => "Calculate and display the current EXP percentage on the EXP bar.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Decimals", EditorSize = 140, IntMin = 0, IntMax = 3, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
        public int Decimals = 1;

        [TweakConfigOption("Only show percentage")]
        public bool PercentageOnly;

        [TweakConfigOption("Show Rested Experience")]
        public bool ShowRestedExperience;
        
        [TweakConfigOption("Show only Rested Experience as percentage", 2, ConditionalDisplay = true)]
        public bool NoExpPercentage;
        public bool ShouldShowNoExpPercentage() => PercentageOnly == false && ShowRestedExperience;
    }

    public Configs Config { get; private set; }
    public override bool UseAutoConfig => true;

    private delegate void* AddonExpOnUpdateDelegate(AtkUnitBase* addonExp, NumberArrayData** numberArrayData, StringArrayData** stringArrayData, void* a4);
    private HookWrapper<AddonExpOnUpdateDelegate> addonExpOnUpdateHook;

    protected override void Enable() {
        addonExpOnUpdateHook ??= Common.Hook<AddonExpOnUpdateDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 30 48 8B 72 18", AddonExpOnUpdateDetour);
        addonExpOnUpdateHook?.Enable();

        Config = LoadConfig<Configs>() ?? new Configs();
        ConfigChanged();
        base.Enable();
    }

    private string LevelTextConvert(int level) {
        return level.ToString().Aggregate("", (current, chr) => current + (char)(chr switch {
            '0' => SeIconChar.Number0,
            '1' => SeIconChar.Number1,
            '2' => SeIconChar.Number2,
            '3' => SeIconChar.Number3,
            '4' => SeIconChar.Number4,
            '5' => SeIconChar.Number5,
            '6' => SeIconChar.Number6,
            '7' => SeIconChar.Number7,
            '8' => SeIconChar.Number8,
            '9' => SeIconChar.Number9,
            _ => SeIconChar.Circle
        }));
    }

    private void* AddonExpOnUpdateDetour(AtkUnitBase* addonExp, NumberArrayData** numberArrays, StringArrayData** stringArrays, void* a4) {
        var stringArray = stringArrays[2];
        if (stringArray == null) goto ReturnOriginal;
        var strPtr = stringArray->StringArray[69];
        if (strPtr == null) goto ReturnOriginal;
        var numberArray = numberArrays[3];
        if (numberArray == null) goto ReturnOriginal;

        var ret =  addonExpOnUpdateHook.Original(addonExp, numberArrays, stringArrays, a4);

        try {
            var textNode = addonExp->GetTextNodeById(4);
            if (textNode == null) goto ReturnOriginal;

            var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
            if (Plugin.GetTweak<HideExperienceBar>() is { Ready: true, Enabled: true } && str.TextValue.Contains("-/-")) return ret;
            var percent = 1f;
            if (!str.TextValue.Contains("-/-")) percent = numberArray->IntArray[16] / (float) numberArray->IntArray[18];
            percent *= 100f;
            if (Config.PercentageOnly) {
                var classJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow((uint)numberArray->IntArray[26]);
                if (classJob != null) {
                    str.Payloads.Clear();
                    str.Append(classJob.Abbreviation.ToDalamudString());

                    var levelIcon = Service.ClientState.ClientLanguage switch {
                        ClientLanguage.French => SeIconChar.LevelFr,
                        ClientLanguage.German => SeIconChar.LevelDe,
                        _ => SeIconChar.LevelEn
                    };

                    str.Append($"  {(char)levelIcon}{LevelTextConvert(numberArray->IntArray[24])}    ");

                    if (percent < 100f) {
                        str.Append(Service.ClientState.ClientLanguage switch {
                            ClientLanguage.French => "Exp: ",
                            ClientLanguage.German => "",
                            _ => "EXP "
                        });
                        str.Append($"{percent.ToString($"F{Config.Decimals}", Culture)}%");
                    }

                }
            } else if (percent < 100 && !(Config.ShowRestedExperience && Config.NoExpPercentage)) {
                str.Payloads.Add(new TextPayload($" ({percent.ToString($"F{Config.Decimals}", Culture)}%)"));
            }

            if (Config.ShowRestedExperience) {
                var restedExperience = (numberArray->IntArray[19] / (float) numberArray->IntArray[18]) / 0.03f;
                str.Append($"  {(char) SeIconChar.ExperienceFilled} {restedExperience.ToString($"F{Config.Decimals}", Culture)}%");
            }

            textNode->SetText(str.Encode());
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return ret;
        ReturnOriginal:
        return addonExpOnUpdateHook.Original(addonExp, numberArrays, stringArrays, a4);
    }

    protected override void ConfigChanged() {
        var addonExp = Common.GetUnitBase("_Exp");
        if (addonExp != null) {
            var atkArrayDataHolder = Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            addonExp->OnUpdate(atkArrayDataHolder.NumberArrays, atkArrayDataHolder.StringArrays);
        }
    }

    protected override void Disable() {
        SaveConfig(Config);
        addonExpOnUpdateHook?.Disable();
        ConfigChanged();
        base.Disable();
    }

    public override void Dispose() {
        addonExpOnUpdateHook?.Dispose();
        base.Dispose();
    }
}