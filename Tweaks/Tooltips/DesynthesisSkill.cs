using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public class DesynthesisSkill : TooltipTweaks.SubTweak {
    public override string Name => "Show Desynthesis Skill";
    public override string Description => "Shows your current desynthesis level when viewing a desynthesizable item.";

    private readonly uint[] desynthesisInDescription = { 46, 56, 65, 66, 67, 68, 69, 70, 71, 72 };

    public class Configs : TweakConfig {
        public bool Delta;
    }
        
    public Configs Config { get; private set; }

    private ExcelSheet<ExtendedItem> itemSheet;

    public override void Enable() {
        itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
        if (itemSheet == null) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var id = Service.GameGui.HoveredItem;
        if (id < 2000000) {
            id %= 500000;

            var item = itemSheet.GetRow((uint)id);
            if (item != null && item.Desynth > 0) {
                var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(item.ClassJobRepair.Row);
                var desynthDelta = item.LevelItem.Row - desynthLevel;
                var useDescription = desynthesisInDescription.Contains(item.ItemSearchCategory.Row);

                var seStr = GetTooltipString(stringArrayData, useDescription ? ItemDescription : ExtractableProjectableDesynthesizable);

                if (seStr != null && seStr.Payloads.Count > 0) {
                    if (seStr.Payloads.Last() is TextPayload textPayload) {
                        if (Config.Delta) {
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#}");
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#})");
                        } else {
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                        }
                        SetTooltipString(stringArrayData, useDescription ? ItemDescription : ExtractableProjectableDesynthesizable, seStr);
                    }
                }
            }
        }
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox(LocString("Desynthesis Delta") + $"###{GetType().Name}DesynthesisDelta", ref Config.Delta);
    };
}