using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnTeamcraft : ItemHotkey {
    public class HotkeyConfig : ItemHotkeyConfig {
        public bool ForceBrowser;
    }

    public new HotkeyConfig Config => base.Config as HotkeyConfig;

    protected override string Name => "View on Teamcraft";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.T];

    private bool teamcraftLocalFailed;

    public override void DrawExtraConfig() {
        ImGui.SameLine();
        if (ImGui.Checkbox("Browser Only", ref Config.ForceBrowser)) SaveConfig();
    }

    public override void OnTriggered(Item item) {
        if (teamcraftLocalFailed || Config.ForceBrowser) {
            Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{item.RowId}");
            return;
        }

        Task.Run(async () => {
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try {
                await Common.HttpClient.GetAsync($"http://localhost:14500/db/en/item/{item.RowId}", timeoutSource.Token);
                SimpleLog.Log("Teamcraft API Open worked");
            } catch {
                SimpleLog.Log("Teamcraft API Open failed");
                if (System.IO.Directory.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffxiv-teamcraft"))) {
                    SimpleLog.Log("Open Application");
                    Common.OpenBrowser($"teamcraft:///db/en/item/{item.RowId}");
                } else {
                    SimpleLog.Log("Open Browser");
                    teamcraftLocalFailed = true;
                    Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{item.RowId}");
                }
            }
        });
    }
}
