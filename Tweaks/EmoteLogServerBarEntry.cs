using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Config;
using Dalamud.Game.Gui.Dtr;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakAuthor("Sythiri")]
[TweakName("Emote Log Status in Server Bar")]
[TweakDescription("Show the emote log status in the server bar.")]
[TweakReleaseVersion("1.9.3.0")]
public class EmoteLogServerBarEntry : Tweak {
    private IDtrBarEntry emoteLogBarEntry;

    protected override void Enable() {
        emoteLogBarEntry = Service.DtrBar.Get("EmoteLog");
        emoteLogBarEntry.OnClick += DtrEntryClicked;
        Service.GameConfig.UiConfigChanged += UiConfigChanged;
        SetDtrEntryText();
        base.Enable();
    }

    protected override void Disable() {
        emoteLogBarEntry.OnClick -= DtrEntryClicked;
        Service.GameConfig.UiConfigChanged -= UiConfigChanged;
        Service.DtrBar.Remove("EmoteLog");
        emoteLogBarEntry.Shown = false;
        base.Disable();
    }

    private void DtrEntryClicked(DtrInteractionEvent eventData) {
        var value = Service.GameConfig.UiConfig.GetBool("EmoteTextType");
        Service.GameConfig.UiConfig.Set("EmoteTextType", !value);
    }

    private void UiConfigChanged(object? sender, ConfigChangeEvent e) {
        if ((UiConfigOption)e.Option == UiConfigOption.EmoteTextType) {
            SetDtrEntryText();
        }
    }

    private void SetDtrEntryText() {
        var text = Service.GameConfig.UiConfig.GetBool("EmoteTextType") ? "" : "";
        emoteLogBarEntry.Text = $"Emotes: {text}";
    }
}
