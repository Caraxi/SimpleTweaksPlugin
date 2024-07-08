using Dalamud.Game.Config;
using Dalamud.Game.Gui.Dtr;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin;

[TweakAuthor("Sythiri")]
[TweakName("Emote Log Status in Server Bar")]
[TweakDescription("Show the emote log status in the server bar.")]
[TweakReleaseVersion("1.9.3.0")]
public class EmoteLogServerBarEntry : Tweak
{
    private IDtrBarEntry _emoteLogBarEntry;

    protected override void Enable()
    {
        _emoteLogBarEntry = Service.DtrBar.Get("EmoteLog");
        _emoteLogBarEntry.OnClick += DtrEntryClicked;
        Service.GameConfig.UiConfigChanged += UiConfigChanged;
        SetDtrEntryText();
        base.Enable();
    }

    protected override void Disable()
    {
        _emoteLogBarEntry.OnClick -= DtrEntryClicked;
        Service.GameConfig.UiConfigChanged -= UiConfigChanged;
        Service.DtrBar.Remove("EmoteLog");
        _emoteLogBarEntry.Shown = false;
        base.Disable();
    }

    private void DtrEntryClicked()
    {
        var value = Service.GameConfig.UiConfig.GetBool("EmoteTextType");
        Service.GameConfig.UiConfig.Set("EmoteTextType", !value);
    }

    private void UiConfigChanged(object sender, ConfigChangeEvent e)
    {
        if ((UiConfigOption)e.Option == UiConfigOption.EmoteTextType)
        {
            SetDtrEntryText();
        }
    }

    private void SetDtrEntryText()
    {
        string text = Service.GameConfig.UiConfig.GetBool("EmoteTextType") ? "" : "";
        _emoteLogBarEntry.Text = $"Emotes: {text}";
    }

}
