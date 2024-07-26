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

[TweakName("Show Desynthesis Skill")]
[TweakDescription("Shows your current desynthesis level when viewing a desynthesizable item.")]
public class DesynthesisSkill : TooltipTweaks.SubTweak {
    private readonly uint[] desynthesisInDescription = { 46, 56, 65, 66, 67, 68, 69, 70, 71, 72 };

    public class Configs : TweakConfig {
        public bool Delta;
        public bool Colour;
    }

    public Configs Config { get; private set; }

    private ExcelSheet<ExtendedItem> itemSheet;

    protected override void Enable() {
        itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
        if (itemSheet == null) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }

    private const ushort Red = 14; // 511;
    private const ushort Yellow = 514;
    private const ushort Green = 45; //42;
    private uint maxDesynthLevel = 590;

    protected override void Setup() {
        foreach (var i in Service.Data.Excel.GetSheet<Item>()) {
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
                    // Turns out .Last() is broken when mixed with AllaganTools for items where the Desynth value is in ItemDescription, as I think AT Adds to the payloads.
                    // And with the original change for colouring, we switched from a simple replace to adding payloads.
                    // For some reason this gets called twice, and with the above change, would result in us adding the desynth skill twice.
                    var textPayload = seStr.Payloads.OfType<TextPayload>().Where(p => p.Text != null).LastOrDefault(p => p.Text.Contains($": {item.LevelItem.Row},00") || p.Text.Contains($": {item.LevelItem.Row}.00") || p.Text.Contains($":{item.LevelItem.Row}.00"));
                    if (textPayload != null) {
                        // Until we fix AllaganTools, if we're in an ItemDescription, just Replace (and unfortunately don't colour)
                        if (useDescription) {
                            if (Config.Delta) {
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#;-0})");
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#;-0})");
                            } else {
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({MathF.Floor(desynthLevel):F0})");
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({MathF.Floor(desynthLevel):F0})");
                            }
                        } else {
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} <split>");
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} <split>");

                            // Since we're not doing a replace anymore, we need to split the string, as sometimes there's text after the skill level
                            var parts = textPayload.Text.Split("<split>");
                            textPayload.Text = parts[0];

                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(c));
                            seStr.Payloads.Add(new TextPayload(Config.Delta ? $"({desynthDelta:+#;-#;-0})" : $"({MathF.Floor(desynthLevel):F0})"));
                            if (Config.Colour) seStr.Payloads.Add(new UIForegroundPayload(0));

                            if (parts.Length > 1)
                                seStr.Payloads.Add(new TextPayload(parts[1]));
                        }

                        try {
                            SetTooltipString(stringArrayData, useDescription ? ItemDescription : ExtractableProjectableDesynthesizable, seStr);
                        } catch (Exception ex) {
                            Plugin.Error(this, ex);
                        }
                    }
                }
            }
        }
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox(LocString("Desynthesis Delta") + $"###{GetType().Name}DesynthesisDelta", ref Config.Delta);
        hasChanged |= ImGui.Checkbox(LocString("Colour Value") + $"##{GetType().Name}ColourValue", ref Config.Colour);
    }
}
