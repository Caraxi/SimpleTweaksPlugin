using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.TweakSystem;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using System;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin; 

[TweakName("Logos Tooltips")]
[TweakDescription("Adds which kind of Logos Mnenes you can obtain from a Logogram in its tooltip.")]
[TweakAuthor("Khayle")]
[TweakCategory(TweakCategory.Tooltip)]
public unsafe class LogosTooltip : TooltipTweaks.SubTweak {
    private readonly Dictionary<uint, uint[]> Logograms = new()
    {
        /* Conceptual Logogram */
        [24007] = new uint[] { 
            24015, // Wisdom of the Aetherweaver
            24016, // Wisdom of the Martialist
            24017, // Wisdom of the Platebearer
            24037, // Incense L
            24033, // Cure L
            24030, // Backstep L
            24024  // Paralyze L
        },
        /* Fundamental Logogram */
        [24008] = new uint[] { 
            24022, // Protect L
            24036, // Esuna L
            24038, // Raise L
            24028, // Feint L
            24031  // Tranquilizer L
        },
        /* Curative Logogram */
        [24009] = new uint[] {
            24019, // Wisdom of the Ordained
            24034  // Cure L II
        },
        /* Offensive Logogram */
        [24010] = new uint[] {
            24020, // Wisdom of the Skirmisher
            24032  // Bloodbath L
        },
        /* Protective Logogram */
        [24011] = new uint[] { 
            24018, // Wisdom of the Guardian
            24021  // Spirit of the Remembered
        },
        /* Tactical Logogram */
        [24012] = new uint[] { 
            24025, // Featherfoot L
            24029  // Stealth L
        },
        /* Mitigative Logogram */
        [24013] = new uint[] {
            24023, // Shell L
            24035  // Stoneskin L
        },
        /* Inimical Logogram */
        [24014] = new uint[] { 
            24026, // Spirit Dart L
            24027  // Dispel L
        },
        /* Obscure Logogram */
        [24809] = new uint[] { 
            24810, // Wisdom of the Breathtaker
            24811, // Eagle Eye Shot L
            24812, // Double Edge L
            24813, // Magic Burst L
        },

    };

    string containsString = Service.ClientState.ClientLanguage switch
    {
		Dalamud.Game.ClientLanguage.English => "Potential memories contained:",
		Dalamud.Game.ClientLanguage.German => "Kann folgende Logos Kommandos enthalten:",
		Dalamud.Game.ClientLanguage.French => "Son identification peut octroyer",
        Dalamud.Game.ClientLanguage.Japanese => "鑑定結果候補:",
        _ => "Potential memories contained:"
    };


    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        if (Logograms.TryGetValue(Item.ItemId, out var logosList))
        {
            string stringLogos = "";
            foreach (var logos in logosList)
            {
                var logosItem = Service.Data.Excel.GetSheet<Item>()?.FirstOrNull(a => a.RowId == Item.ItemId);
                if (logosItem == null) return;
				var logosMemory = Service.Data.Excel.GetSheet<Item>()?.FirstOrNull(a => a.RowId == logos);
				stringLogos += $"{logosMemory.Value.Name.ExtractText()}, ";
            }
			stringLogos = $"{stringLogos.Substring(0, stringLogos.Length - 2)}.";
            var description = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription);

            if (description.TextValue.Contains(containsString)) return; // Don't append when it already exists.

            description.Payloads.Add(RawPayload.LinkTerminator);

            description.Payloads.Add(new NewLinePayload());
            description.Payloads.Add(new UIForegroundPayload(500));
            description.Payloads.Append(new UIGlowPayload(7));
            description.Payloads.Add(new TextPayload($"{containsString} {stringLogos}\r"));
            description.Payloads.Append(new UIGlowPayload(0));
            description.Payloads.Add(new UIForegroundPayload(0));
            try
            {
                SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription, description);
            }
            catch (Exception ex)
            {
                SimpleLog.Error(ex);
                Plugin.Error(this, ex);
            }

        }
    }
}
