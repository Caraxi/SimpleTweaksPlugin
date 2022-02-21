using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using static Dalamud.Game.Text.XivChatType;

namespace SimpleTweaksPlugin.Tweaks.Chat; 

public unsafe class ChatNameColours : ChatTweaks.SubTweak {
    public override string Name => "Chat Name Colours";
    public override string Description => "Gives players a random colour in chat, or set the name manually.";

    public class ForcedColour {
        public ushort ColourKey;
        public string PlayerName = string.Empty;
        public string WorldName = string.Empty;
    }

    public class Configs : TweakConfig {
        public List<ForcedColour> ForcedColours = new();
        public bool RandomColours = true;
    }

    public Configs Config { get; private set; }

    private string inputNewPlayerName = string.Empty;
    private string inputServerName = string.Empty;
    private string addError = string.Empty;

    private ushort? GetColourKey(string playerName, string worldName, bool forceRandom = false) {
        var forced = Config.ForcedColours.FirstOrDefault(f => f.PlayerName == playerName && f.WorldName == worldName);
        if (forced != null) return forced.ColourKey;
        if (!forceRandom && !Config.RandomColours) return null;
        var key = (uint) $"{playerName}@{worldName}".GetStableHashCode();
        var defaultColourKey = nameColours[key % nameColours.Length];
        return defaultColourKey;
    }

    private ExcelSheet<UIColor> uiColorSheet;
    private ExcelSheet<World> worldSheet;

    public override void Setup() {

        this.uiColorSheet = Service.Data.Excel.GetSheet<UIColor>();
        this.worldSheet = Service.Data.Excel.GetSheet<World>();

        if (uiColorSheet == null || worldSheet == null) {
            Ready = false;
            return;
        }

        base.Setup();
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        var buttonSize = new Vector2(22, 22) * ImGui.GetIO().FontGlobalScale;

        ImGui.Checkbox(LocString("RandomColours", "Use random colours for unlisted players"), ref Config.RandomColours);

        if (ImGui.BeginTable("forcedPlayerNames", 4)) {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
            ImGui.TableSetupColumn(LocString("Player Name"), ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("Server"), ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("");

            ImGui.TableHeadersRow();

            ForcedColour del = null;
            foreach (var fc in Config.ForcedColours ) {
                ImGui.TableNextColumn();

                if (ImGui.Button($"X##{fc.PlayerName}@{fc.WorldName}", buttonSize)) {
                    del = fc;
                }

                ImGui.TableNextColumn();

                var xivCol = Service.Data.Excel.GetSheet<UIColor>()?.GetRow(fc.ColourKey)?.UIForeground;

                Vector4 fColor;
                if (xivCol != null) {
                    var fa = xivCol.Value & 255;
                    var fb = (xivCol.Value >> 8) & 255;
                    var fg = (xivCol.Value >> 16) & 255;
                    var fr = (xivCol.Value >> 24) & 255;

                    fColor = new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);
                } else {
                    fColor = new Vector4(1);
                }


                ImGui.PushStyleColor(ImGuiCol.Text, fColor);
                ImGui.Text($"{fc.PlayerName}");
                ImGui.PopStyleColor();
                ImGui.TableNextColumn();
                ImGui.Text($"{fc.WorldName}");
                ImGui.TableNextColumn();
                ImGuiExt.UiColorPicker($"##picker_{fc.PlayerName}@{fc.WorldName}", ref fc.ColourKey);

            }

            if (del != null) {
                Config.ForcedColours.Remove(del);
            }


            ImGui.TableNextColumn();
            if (ImGui.Button("+##newPlayerName", buttonSize)) {
                addError = string.Empty;
                if (Config.ForcedColours.Any(f => f.PlayerName == inputNewPlayerName && f.WorldName == inputServerName)) {
                    addError = LocString("NameAlreadyAddedError", "Name is already in list.");
                } else {
                    Config.ForcedColours.Add(new ForcedColour() {
                        PlayerName = inputNewPlayerName,
                        WorldName = inputServerName,
                        ColourKey = GetColourKey(inputNewPlayerName, inputServerName, true) ?? 0
                    });
                    SaveConfig(Config);
                    inputNewPlayerName = string.Empty;
                }
            }
            ImGui.TableNextColumn();

            if (Service.ClientState?.LocalPlayer != null) {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##inputNewPlayerName", ref inputNewPlayerName, 25);
                ImGui.TableNextColumn();

                var serverList = worldSheet.Where(w => w.DataCenter.Row == Service.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Row).Select(w => w.Name.ToString()).ToList();
                var serverIndex = serverList.IndexOf(inputServerName);
                if (serverIndex == -1) {
                    serverIndex = serverList.IndexOf(Service.ClientState.LocalPlayer.CurrentWorld.GameData.Name.ToString());
                    inputServerName = serverList[serverIndex];
                }

                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo("##inputServer", ref serverIndex, serverList.ToArray(), serverList.Count)) {
                    inputServerName = serverList[serverIndex];
                }

                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), addError);
            }

            ImGui.EndTable();
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        
        Service.Chat.ChatMessage += HandleChatMessage;
        base.Enable();
    }
    
    private readonly XivChatType[] chatTypes = {
        NoviceNetwork, TellIncoming, TellOutgoing,
        Say, Yell, Shout, Echo,
        Party, Alliance, CrossParty, PvPTeam,
        Ls1, Ls2, Ls3, Ls4,
        Ls5, Ls6, Ls7, Ls8,
        CrossLinkShell1, CrossLinkShell2, CrossLinkShell3, CrossLinkShell4,
        CrossLinkShell5, CrossLinkShell6, CrossLinkShell7, CrossLinkShell8,
        FreeCompany, CustomEmote, StandardEmote,
    };

    private readonly ushort[] nameColours = { 9, 25, 32, 35, 37, 39, 41, 42, 45, 48, 52, 56, 57, 65, 500, 502, 504, 506, 508, 517, 522, 524, 527, 541, 573 };

    private bool Parse(ref SeString seString) {
        var hasName = false;
        var newPayloads = new List<Payload>();
        var waitingEnd = false;
        
        foreach (var payload in seString.Payloads) {
            if (payload is PlayerPayload p) {
                newPayloads.Add(p);
                var colourKey = GetColourKey(p.PlayerName, p.World.Name);
                if (colourKey != null) {
                    hasName = true;
                    waitingEnd = true;
                    newPayloads.Add(new UIForegroundPayload(colourKey.Value));
                }
                continue;
            }
            newPayloads.Add(payload);
            if (waitingEnd) {
                newPayloads.Add(new UIForegroundPayload(0));
                waitingEnd = false;
            }
        }

        if (hasName) {
            if (waitingEnd) {
                newPayloads.Add(new UIForegroundPayload(0));
            }

            seString =  new SeString(newPayloads);
            return true;
        }

        return false;
    }

    private void HandleChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (chatTypes.Contains(type)) {
            Parse(ref sender);
            Parse(ref message);
        }
    }

    public override void Disable() {
        SaveConfig(Config);
        Service.Chat.ChatMessage -= HandleChatMessage;
        base.Disable();
    }
}