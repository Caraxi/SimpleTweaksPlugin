using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Alt + F4 Exit Game")]
[TweakDescription("Pressing Alt + F4 will cause the game to close safely.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class ExitGame : Tweak {
    [FrameworkUpdate]
    private void FrameworkUpdate() {
        if (!Service.ClientState.IsLoggedIn) return;
        
        if (UIInputData.Instance()->IsKeyDown(SeVirtualKey.MENU) && UIInputData.Instance()->IsKeyPressed(SeVirtualKey.F4)) {
            UIModule.Instance()->ExecuteMainCommand(24);
        }
    }
}