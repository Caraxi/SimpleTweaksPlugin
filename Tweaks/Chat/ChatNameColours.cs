using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
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

    public class Region {
        public class DataCentre {
            public string Name = string.Empty;
            public List<string> Worlds = new();
        }
        
        public string Name;
        public List<DataCentre> DataCentres;
    }
    
    public List<Region> Regions = new();

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

        var dcSheet = Service.Data.Excel.GetSheet<WorldDCGroupType>();
        
        if (uiColorSheet == null || worldSheet == null || dcSheet == null) {
            Ready = false;
            return;
        }

        
        Region GetRegion(byte regionId, string name) {
            return new Region() {
                Name = name,
                DataCentres = dcSheet
                    .Where(dc => dc.Region == regionId)
                    .Select(dc => new Region.DataCentre() {
                        Name = dc.Name.RawString,
                        Worlds = worldSheet
                            .Where(w => w.DataCenter.Row == dc.RowId && w.IsPublic)
                            .Select(w => w.Name.RawString)
                            .ToList()
                    })
                    .Where(dc => dc.Worlds.Count > 0)
                    .ToList()
            };
        }
        
        Regions = new List<Region>() {
            GetRegion(1, "JP"),
            GetRegion(2, "NA"),
            GetRegion(3, "EU"),
            GetRegion(4, "OCE"),
        };
        
        base.Setup();
    }


    private float serverListPopupWidth = 0;
    private bool comboOpen = false;
    
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
                
                var currentWorld = Service.ClientState.LocalPlayer.CurrentWorld.GameData?.Name.RawString;
                var currentRegion = Regions.Find(r => r.DataCentres.Any(dc => dc.Worlds.Contains(currentWorld)));
                var currentDc = currentRegion?.DataCentres?.Find(dc => dc.Worlds.Contains(currentWorld));

                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##inputNewPlayerName", ref inputNewPlayerName, 25);
                ImGui.TableNextColumn();
                
                ImGui.SetNextItemWidth(-1);

                ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 3);
                
                if (ImGui.BeginCombo("##inputServer", inputServerName, ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLarge)) {
                    
                    ImGui.Dummy(new Vector2(serverListPopupWidth, 1));
                    if (ImGui.BeginTabBar("serverSelectDC")) {
                        foreach (var r in Regions) {
                            
                            if (ImGui.BeginTabItem(r.Name)) {
                                if (ImGui.BeginTabBar("regionSelectDC")) {
                                    foreach (var dc in r.DataCentres.Where(dc => ImGui.BeginTabItem(dc.Name))) {
                                        foreach (var w in dc.Worlds.Where(w => ImGui.Selectable(w, inputServerName == w))) {
                                            inputServerName = w;
                                        }

                                        ImGui.EndTabItem();
                                    }

                                    ImGui.EndTabBar();
                                }

                                ImGui.EndTabItem();
                            }
                            if (r == currentRegion && comboOpen == false) ImGui.SetKeyboardFocusHere();
                        }

                        ImGui.EndTabBar();
                    }
                    if (ImGuiExt.GetWindowContentRegionSize().X > serverListPopupWidth) {
                        serverListPopupWidth = ImGuiExt.GetWindowContentRegionSize().X;
                    }
                    
                    comboOpen = true;
                    ImGui.EndCombo();
                } else {
                    comboOpen = false;
                }
                
                ImGui.PopStyleVar();
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
        Say, Yell, Shout, Echo, Debug,
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