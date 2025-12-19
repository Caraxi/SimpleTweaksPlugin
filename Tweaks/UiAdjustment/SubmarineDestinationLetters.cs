using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.Interop;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Label Submarine Destinations with Letters")]
[TweakDescription("Uses the standard A-Z lettering to identify submarine destinations for easier use with other tools.")]
[TweakVersion(2)]
[Changelog("1.10.9.4", "Rewritten to fix issues, re-enabled tweak.")]
public unsafe class SubmarineDestinationLetters : UiAdjustments.SubTweak, IDisabledTweak {
    public string DisabledMessage => "Tweak was implemented into the base game as of 7.4";
}
