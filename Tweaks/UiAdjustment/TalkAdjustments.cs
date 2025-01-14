using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using FFXIVClientStructs.Interop;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using System;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;
[TweakName("Talk Adjustments")]
[TweakAuthor("croizat")]
[TweakDescription("Allows further customisation to the Talk addon.")]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class TalkAdjustments : Tweak
{
    [TweakConfig] public Config TweakConfig { get; private set; }
    public class Config : TweakConfig
    {
        [TweakConfigOption("Style", 0, EditorSize = 150)]
        public TalkStyle Style = TalkStyle.Normal;

        public bool ShouldShowOnlyOverrideNormal() => Style != TalkStyle.Normal;
        [TweakConfigOption("Only Override Normal", 1, HelpText = "Set to only override the Talk style for talk boxes that were already normal styled", ConditionalDisplay = true)]
        public bool OnlyOverrideNormal = false;

        [TweakConfigOption("Log Channel", 2, HelpText = "Chat channel to log all text boxes to", EditorSize = 200)]
        public XivChatType LogChannel = XivChatType.None;

        [TweakConfigOption("Enable Global Auto Advance", 3, HelpText = "Enables the game's usual auto advance for all talk boxes")]
        public bool EnableGlobalAutoAdvance = false;

        public bool ShouldShowGlobalAutoAdvanceDelayMs() => EnableGlobalAutoAdvance;
        [TweakConfigOption("Global Auto Advance Delay (ms)", 4, HelpText = "Delay is in addition to the game's default delay.", IntMin = 0, IntMax = 100_000, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150, ConditionalDisplay = true)]
        public int GlobalAutoAdvanceDelayMs = 0;

        [TweakConfigOption("Enable Quick Auto Advance", 5, HelpText = "This overrides the global auto advance and its delay. This is meant as a fast forward.")]
        public bool EnableQuickAdvance = false;
    }

    public enum TalkStyle : byte
    {
        /// <summary> The normal style with a white background. </summary>
        Normal = 0,
        /// <summary> A style with lights on the top and bottom border. </summary>
        Lights = 2,
        /// <summary> A style used for when characters are shouting. </summary>
        Shout = 3,
        /// <summary> Shout but with flatter edges. </summary>
        FlatShout = 4,
        /// <summary> The style used when dragons (and some other NPCs) talk. </summary>
        Dragon = 5,
        /// <summary> The style used for Allagan machinery. </summary>
        Allagan = 6,
        /// <summary> The style used for system messages. </summary>
        System = 7,
        /// <summary> A mixture of the system message style and the dragon style. </summary>
        DragonSystem = 8,
        /// <summary> The system message style with a purple background. </summary>
        PurpleSystem = 9
    }

    private readonly struct TalkAtkValue
    {
        private readonly int _raw = 0;
        public static readonly TalkAtkValue Name = 0;
        public static readonly TalkAtkValue Text = 1;
        /// <remarks> See <see cref="TalkStyle"/> </remarks>
        public static readonly TalkAtkValue Style = 3;
        /// <remarks> enable: value != 0. Overrides <see cref="AutoAdvance"/> </remarks>
        public static readonly TalkAtkValue InputBlockingAutoAdvance = 4;
        public static readonly TalkAtkValue LogToChatChannel = 5;
        /// <remarks> enable: value != 0</remarks>
        public static readonly TalkAtkValue AutoAdvance = 6;
        public static readonly TalkAtkValue AutoAdvanceDelayMs = 7;

        private TalkAtkValue(int value) => _raw = value;
        public static implicit operator TalkAtkValue(int value) => new(value);
        public static implicit operator int(TalkAtkValue value) => value._raw;
    }

    [AddonPreRefresh("Talk")]
    private void OnRefresh(AddonArgs args)
    {
        if (args is AddonRefreshArgs { AtkValueSpan: var values })
        {
            try
            {
                if (!TweakConfig.OnlyOverrideNormal || values[TalkAtkValue.Style].UInt == 0)
                    if (TalkAtkValue.Style < values.Length)
                        values.GetPointer(TalkAtkValue.Style)->SetUInt((uint)TweakConfig.Style);

                if (TalkAtkValue.InputBlockingAutoAdvance < values.Length && TweakConfig.EnableQuickAdvance != default)
                    values.GetPointer(TalkAtkValue.InputBlockingAutoAdvance)->SetUInt(TweakConfig.EnableQuickAdvance ? 1u : 0u);

                if (TalkAtkValue.LogToChatChannel < values.Length && TweakConfig.LogChannel != default)
                    values.GetPointer(TalkAtkValue.LogToChatChannel)->SetUInt((uint)TweakConfig.LogChannel);

                if (TalkAtkValue.AutoAdvance < values.Length && TweakConfig.EnableGlobalAutoAdvance != default)
                    values.GetPointer(TalkAtkValue.AutoAdvance)->SetUInt(TweakConfig.EnableGlobalAutoAdvance ? 1u : 0u);

                if (TweakConfig.EnableGlobalAutoAdvance && TalkAtkValue.AutoAdvanceDelayMs < values.Length)
                    values.GetPointer(TalkAtkValue.AutoAdvanceDelayMs)->SetUInt((uint)TweakConfig.GlobalAutoAdvanceDelayMs);
            }
            catch (Exception e) { SimpleLog.Error(e, $"Something went wrong in {nameof(TalkAdjustments)}.{nameof(OnRefresh)}"); }
        }
    }
}
