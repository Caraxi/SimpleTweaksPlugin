using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using FFXIVClientStructs.Interop;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Talk Adjustments")]
[TweakAuthor("croizat")]
[TweakDescription("Allows further customisation to the Talk addon.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.8.0")]
public unsafe class TalkAdjustments : Tweak {
    [TweakConfig] public Config TweakConfig { get; private set; }

    public class Config : TweakConfig {
        [TweakConfigOption("Style", 0, EditorSize = 150)]
        public TalkStyle Style = TalkStyle.Normal;

        public bool ShouldShowOnlyOverrideNormal() => Style != TalkStyle.Normal;

        [TweakConfigOption("Only Override Normal", 1, HelpText = "Set to only override the Talk style for talk boxes that were already normal styled", ConditionalDisplay = true)]
        public bool OnlyOverrideNormal;

        [TweakConfigOption("Log Channel", 2, HelpText = "Chat channel to log all text boxes to", EditorSize = 200)]
        public XivChatType LogChannel = XivChatType.None;
    }

    public enum TalkStyle : byte {
        [EnumTooltip("The normal style with a white background.")]
        Normal = 0,
        [EnumTooltip("A style with lights on the top and bottom border.")]
        Lights = 2,
        [EnumTooltip("A style used for when characters are shouting.")]
        Shout = 3,
        [EnumTooltip("Shout but with flatter edges.")]
        FlatShout = 4,
        [EnumTooltip("The style used when dragons (and some other NPCs) talk.")]
        Dragon = 5,
        [EnumTooltip("The style used for Allagan machinery.")]
        Allagan = 6,
        [EnumTooltip("The style used for system messages.")]
        System = 7,
        [EnumTooltip("A mixture of the system message style and the dragon style.")]
        DragonSystem = 8,
        [EnumTooltip("The system message style with a purple background.")]
        PurpleSystem = 9
    }

    private readonly struct TalkAtkValue {
        public const int Name = 0;
        public const int Text = 1;
        /// <remarks> See <see cref="TalkStyle"/> </remarks>
        public const int Style = 3;
        /// <remarks> enable: value != 0. Overrides <see cref="AutoAdvance"/> </remarks>
        public const int InputBlockingAutoAdvance = 4;
        public const int LogToChatChannel = 5;
        /// <remarks> enable: value != 0</remarks>
        public const int AutoAdvance = 6;
        public const int AutoAdvanceDelayMs = 7;
    }

    [AddonPreRefresh("Talk")]
    private void OnRefresh(AddonArgs args) {
        if (args is not AddonRefreshArgs refreshArgs || refreshArgs.AtkValueCount < TalkAtkValue.LogToChatChannel) return;
        try {
            var style = (AtkValue*) refreshArgs.AtkValueEnumerable.Skip(TalkAtkValue.Style).First().Address;
            var logToChatChannel = (AtkValue*) refreshArgs.AtkValueEnumerable.Skip(TalkAtkValue.LogToChatChannel).First().Address;
            if ((!TweakConfig.OnlyOverrideNormal || style->UInt == 0) && TalkAtkValue.Style < refreshArgs.AtkValueCount)
                style->SetUInt((uint)TweakConfig.Style);
            if (TalkAtkValue.LogToChatChannel < refreshArgs.AtkValueCount && TweakConfig.LogChannel != default)
                logToChatChannel->SetUInt((uint)TweakConfig.LogChannel);
        } catch (Exception e) {
            SimpleLog.Error(e, $"Something went wrong in {nameof(TalkAdjustments)}.{nameof(OnRefresh)}");
        }
    }
}
