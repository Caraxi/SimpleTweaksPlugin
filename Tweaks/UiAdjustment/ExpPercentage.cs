using System;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Show Experience Percentage")]
[TweakDescription("Calculate and display the current EXP percentage on the EXP bar.")]
[TweakAutoConfig]
[Changelog("1.9.4.0", "Added the ability to show rested experience without showing experience percentage.")]
[Changelog(UnreleasedVersion, "Added option to move EXP or Chain Bonus above the EXP bar.")]
public unsafe class ExpPercentage : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Decimals", EditorSize = 140, IntMin = 0, IntMax = 3, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
        public int Decimals = 1;

        [TweakConfigOption("Only show percentage")]
        public bool PercentageOnly;

        [TweakConfigOption("Show Rested Experience")]
        public bool ShowRestedExperience;

        [TweakConfigOption("Show only Rested Experience as percentage", 2, ConditionalDisplay = true)]
        public bool NoExpPercentage;

        [TweakConfigOption("Move EXP text above bar", 3)]
        public bool MoveExp;
        
        [TweakConfigOption("Move chain bonus text above bar", 4)]
        public bool MoveChainBonus;
        
        public bool ShouldShowNoExpPercentage() => PercentageOnly == false && ShowRestedExperience;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
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

    [AddonPostRequestedUpdate("_Exp")]
    private void UpdateExpDisplay(AtkUnitBase* addonExp) {
        MoveText(addonExp, 4, Config.MoveExp);
        MoveText(addonExp, 5, Config.MoveChainBonus);
        var stringArray = AtkStage.Instance()->GetStringArrayData()[2];

        if (stringArray == null) return;
        var strPtr = stringArray->StringArray[69];
        if (strPtr == null) return;
        var numberArray = AtkStage.Instance()->GetNumberArrayData()[3];
        if (numberArray == null) return;

        try {
            var textNode = addonExp->GetTextNodeById(4);
            if (textNode == null) return;

            var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
            if (Plugin.GetTweak<HideExperienceBar>() is { Ready: true, Enabled: true } && str.TextValue.Contains("-/-")) return;
            var percent = 1f;
            if (!str.TextValue.Contains("-/-")) percent = numberArray->IntArray[16] / (float)numberArray->IntArray[18];
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
                var restedExperience = (numberArray->IntArray[19] / (float)numberArray->IntArray[18]) / 0.03f;
                str.Append($"  {(char)SeIconChar.ExperienceFilled} {restedExperience.ToString($"F{Config.Decimals}", Culture)}%");
            }

            textNode->SetText(str.Encode());
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void ConfigChanged() {
        if (Common.GetUnitBase("_Exp", out var addonExp)) {
            UpdateExpDisplay(addonExp);
        }
    }

    private void MoveText(AtkUnitBase* unitBase, uint nodeId, bool above) {
        if (unitBase == null) return;
        var node = unitBase->GetNodeById(nodeId);
        if (node == null) return;
        node->SetYFloat(above ? 0 : 20);
    }
    

    protected override void Disable() {
        if (Common.GetUnitBase("_Exp", out var addonExp)) {
            addonExp->OnRequestedUpdate(AtkStage.Instance()->GetNumberArrayData(), AtkStage.Instance()->GetStringArrayData());
            MoveText(addonExp, 4, false);
            MoveText(addonExp, 5, false);
        }
    }
}
