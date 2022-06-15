using System;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Keys;
using ImGuiNET;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class ViewOnTeamcraft : ItemHotkey {

    public class HotkeyConfig : ItemHotkeyConfig {
        public bool ForceBrowser = false;
    }
    
    public new HotkeyConfig Config => base.Config as HotkeyConfig;
    
    public override string Name => "View on Teamcraft";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.T};

    
    private bool teamcraftLocalFailed;

    public override void DrawExtraConfig() {
        ImGui.SameLine();
        if (ImGui.Checkbox("Browser Only", ref Config.ForceBrowser)) SaveConfig();
    }

    public override void OnTriggered(ExtendedItem item) {
        if (teamcraftLocalFailed || Config.ForceBrowser) {
            Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{item.RowId}");
            return;
        }
        Task.Run(() => {
            try {
                var wr = WebRequest.CreateHttp($"http://localhost:14500/db/en/item/{item.RowId}");
                wr.Timeout = 500;
                wr.Method = "GET";
                wr.GetResponse().Close();
            } catch {
                try {
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffxiv-teamcraft"))) {
                        Common.OpenBrowser($"teamcraft:///db/en/item/{item.RowId}");
                    } else {
                        teamcraftLocalFailed = true;
                        Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{item.RowId}");
                    }
                } catch {
                    teamcraftLocalFailed = true;
                    Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{item.RowId}");
                }
            }
        });
    }
}

