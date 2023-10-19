using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Alt + F4 Exit Game")]
[TweakDescription("Pressing Alt + F4 will cause the game to close safely.")]
[TweakAuthor("MidoriKami")]
public unsafe class ExitGame : Tweak {
    [FrameworkUpdate]
    private void FrameworkUpdate() {
        if (!Service.ClientState.IsLoggedIn) return;
        
        if (UIInputData.Instance()->IsKeyDown(UIInputData.VirtualKey.MENU) && UIInputData.Instance()->IsKeyPressed(UIInputData.VirtualKey.F4)) {
            UIModule.Instance()->ExecuteMainCommand(24);
        }
    }
}