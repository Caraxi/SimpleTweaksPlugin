using Dalamud.Game.Config;
using Dalamud.Game.Gui.Dtr;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin;

[TweakAuthor("Sythiri")]
public unsafe class EmoteLogServerBarEntry : Tweak
{
    public override string Name => "Emote Log Status in Server Bar";
    public override string Description => "Show the emote log status in the server bar.";
    private DtrBarEntry _emoteLogBarEntry;

    protected override void Enable()
    {
        _emoteLogBarEntry = Plugin.DtrBar.Get("EmoteLog");
        _emoteLogBarEntry.OnClick += DtrEntryClicked;
        Service.GameConfig.UiConfigChanged += UiConfigChanged;
        SetDtrEntryText();
        base.Enable();
    }

    protected override void Disable()
    {
        _emoteLogBarEntry.OnClick -= DtrEntryClicked;
        Service.GameConfig.UiConfigChanged -= UiConfigChanged;
        Plugin.DtrBar.Remove("EmoteLog");
        _emoteLogBarEntry.Shown = false;
        base.Disable();
    }

    public override void Dispose()
    {
        base.Dispose();
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
