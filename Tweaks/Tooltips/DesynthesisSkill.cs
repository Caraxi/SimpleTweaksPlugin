using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
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
        public bool Colour;
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

    private const ushort Red = 14;// 511;
    private const ushort Yellow = 514;
    private const ushort Green = 45;//42;
    private uint maxDesynthLevel = 590;

    public override void Setup()
    {
        foreach (var i in Service.Data.Excel.GetSheet<Item>())
        {
            if (i.Desynth > 0 && i.LevelItem.Row > maxDesynthLevel) maxDesynthLevel = i.LevelItem.Row;
        }
        base.Setup();
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

                ushort c = Red;
                if (desynthLevel >= maxDesynthLevel || desynthLevel >= item.LevelItem.Row + 50) {
                    c = Green;
                } else if (desynthLevel > item.LevelItem.Row) {
                    c = Yellow;
                }

                if (seStr != null && seStr.Payloads.Count > 0) {
                    if (seStr.Payloads.Last() is TextPayload textPayload && textPayload.Text != null) {
                        textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ");
                        textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ");
                        if (Config.Delta) {
                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(c));
                            seStr.Payloads.Add(new TextPayload($"({desynthDelta:+#;-#})"));
                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(0));
                        } else {
                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(c));
                            seStr.Payloads.Add(new TextPayload($"({MathF.Floor(desynthLevel):F0})"));
                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(0));
                        }
                        SetTooltipString(stringArrayData, useDescription ? ItemDescription : ExtractableProjectableDesynthesizable, seStr);
                    }
                }
            }
        }
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox(LocString("Desynthesis Delta") + $"###{GetType().Name}DesynthesisDelta", ref Config.Delta);
            hasChanged |= ImGui.Checkbox(LocString("Colour Value") + $"##{GetType().Name}ColourValue", ref Config.Colour);
    };
}