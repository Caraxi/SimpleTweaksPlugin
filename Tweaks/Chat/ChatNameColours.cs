using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.ExtraPayloads;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static Dalamud.Game.Text.XivChatType;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Chat Name Colours")]
[TweakDescription("Gives players a random colour in chat, or set the name manually.")]
public class ChatNameColours : ChatTweaks.SubTweak {
    public class ForcedColour {
        public ushort ColourKey; // Legacy
        public Vector3? Color;
        public Vector3? Glow;
        public string PlayerName = string.Empty;
        public string WorldName = string.Empty;
    }

    public class ChannelConfig {
        public bool Sender = true;
        public bool Message = true;
    }

    public class Configs : TweakConfig {
        public List<ForcedColour> ForcedColours = new();
        public bool RandomColours = true;
        public bool LegacyColours;
        public bool ApplyDefaultColour;
        public ushort DefaultColourKey = 1;
        public Vector3 DefaultColour = Vector3.One;

        public ChannelConfig DefaultChannelConfig = new();
        public Dictionary<XivChatType, ChannelConfig> ChannelConfigs = new();
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
        if (!forceRandom && !Config.RandomColours) return Config.ApplyDefaultColour ? Config.DefaultColourKey : null;
        var key = (uint)$"{playerName}@{worldName}".GetStableHashCode();
        var defaultColourKey = nameColours[key % nameColours.Length];
        return defaultColourKey;
    }

    private Vector3? GetColor(string playerName, string worldName, bool forceRandom = false) {
        var forced = Config.ForcedColours.FirstOrDefault(f => f.PlayerName == playerName && f.WorldName == worldName);
        if (forced != null) return forced.Color;
        if (!forceRandom && !Config.RandomColours) return Config.ApplyDefaultColour ? Config.DefaultColour : null;
        var key = $"{playerName}@{worldName}".GetStableHashCode();
        var hue = new Random(key).NextSingle();
        var c = new Vector3();
        ImGui.ColorConvertHSVtoRGB(hue, 1, 1, ref c.X, ref c.Y, ref c.Z);
        return c;
    }

    private Vector3? GetGlow(string playerName, string worldName) {
        var forced = Config.ForcedColours.FirstOrDefault(f => f.PlayerName == playerName && f.WorldName == worldName);
        return forced?.Glow;
    }

    private Vector3 LegacyToNew(ushort legacyColourId) {
        
        
        
        if (Service.Data.GetExcelSheet<UIColor>().TryGetRow(legacyColourId, out var xivCol)) {
            var fb = (xivCol.Dark >> 8) & 255;
            var fg = (xivCol.Dark >> 16) & 255;
            var fr = (xivCol.Dark >> 24) & 255;
            return new Vector3(fr / 255f, fg / 255f, fb / 255f);
        }

        return Vector3.One;
    }
    
    protected override void Setup() {
        AddChangelog("1.8.8.1", "Fixed Chat2 exploding with new colour system. Tweak will still not work in Chat2, but it will not explode.");
        AddChangelog("1.8.8.0", "Fixed colour display when in party.");
        AddChangelog("1.8.8.0", "Extended range of possible colours.");
        AddChangelog("1.8.9.0", "Added option to give all undefined characters the same colour.");
        AddChangelog("1.8.9.0", "Added per channel configuration for colouring sender name and/or names in messages.");

        Region GetRegion(byte regionId, string name) => new() {
                Name = name, 
                DataCentres = Service.Data.Excel.GetSheet<WorldDCGroupType>()
                    .Where(dc => dc.Region == regionId)
                    .Select(dc => new Region.DataCentre {
                        Name = dc.Name.ExtractText(), 
                        Worlds = Service.Data.Excel.GetSheet<World>().Where(w => 
                            w.DataCenter.RowId == dc.RowId && w.IsPublic
                        ).Select(w => w.Name.ExtractText()).ToList()
                }).Where(dc => dc.Worlds.Count > 0).ToList()
            };

        Regions = [
            GetRegion(1, "JP"),
            GetRegion(2, "NA"),
            GetRegion(3, "EU"),
            GetRegion(4, "OCE")
        ];
    }

    private float serverListPopupWidth;
    private bool comboOpen;

    protected void DrawConfig() {
        var buttonSize = new Vector2(22, 22) * ImGui.GetIO().FontGlobalScale;
        ImGui.Checkbox(LocString("LegacyColours", "Use old colour limits."), ref Config.LegacyColours);
        if (ImGui.Checkbox(LocString("RandomColours", "Use random colours for unlisted players"), ref Config.RandomColours)) {
            Config.ApplyDefaultColour = false;
        }

        if (ImGui.Checkbox(LocString("ApplyDefaultColour", "Use a specific colour for unlisted players"), ref Config.ApplyDefaultColour)) {
            Config.RandomColours = false;
        }

        ImGui.SameLine();

        if (Config.LegacyColours) {
            ImGuiExt.UiColorPicker($"##picker_default", ref Config.DefaultColourKey);
        } else {
            ImGui.ColorEdit3($"##picker_default", ref Config.DefaultColour, ImGuiColorEditFlags.NoInputs);
        }

        if (ImGui.BeginTable("forcedPlayerNames", 4)) {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
            ImGui.TableSetupColumn(LocString("Player Name"), ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("Server"), ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("");

            ImGui.TableHeadersRow();

            ForcedColour del = null;
            foreach (var fc in Config.ForcedColours) {
                ImGui.TableNextColumn();

                if (ImGui.Button($"X##{fc.PlayerName}@{fc.WorldName}", buttonSize)) {
                    del = fc;
                }

                ImGui.TableNextColumn();

                Vector4 fColor;
                if (!Config.LegacyColours) {
                    fc.Color ??= LegacyToNew(fc.ColourKey);
                    fColor = new Vector4(fc.Color ?? Vector3.One, 1);
                } else {
                    if (Service.Data.Excel.GetSheet<UIColor>().TryGetRow(fc.ColourKey, out var xivCol)) {
                        var fa = xivCol.Dark & 255;
                        var fb = (xivCol.Dark >> 8) & 255;
                        var fg = (xivCol.Dark >> 16) & 255;
                        var fr = (xivCol.Dark >> 24) & 255;

                        fColor = new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);
                    } else {
                        fColor = new Vector4(1);
                    }
                }

                ImGui.PushStyleColor(ImGuiCol.Text, fColor);
                ImGui.Text($"{fc.PlayerName}");
                ImGui.PopStyleColor();
                ImGui.TableNextColumn();
                ImGui.Text($"{fc.WorldName}");
                ImGui.TableNextColumn();
                if (!Config.LegacyColours) {
                    if (fc.Color == null) {
                        if (Service.Data.Excel.GetSheet<UIColor>().TryGetRow(fc.ColourKey, out var xivCol)) {
                            var fb = (xivCol.Dark >> 8) & 255;
                            var fg = (xivCol.Dark >> 16) & 255;
                            var fr = (xivCol.Dark >> 24) & 255;
                            fc.Color = new Vector3(fr / 255f, fg / 255f, fb / 255f);
                        } else {
                            fc.Color = Vector3.One;
                        }
                    }

                    var v = fc.Color.Value;
                    if (ImGui.ColorEdit3($"##picker_{fc.PlayerName}@{fc.WorldName}", ref v, ImGuiColorEditFlags.NoInputs)) {
                        fc.Color = v;
                    }

                    ImGui.SameLine();
                    if (fc.Glow == null) {
                        if (ImGui.SmallButton($"add glow##addGlow_{fc.PlayerName}@{fc.WorldName}")) {
                            fc.Glow = Vector3.One;
                        }
                    } else {
                        var g = fc.Glow.Value;
                        if (ImGui.ColorEdit3($"##picker_glow_{fc.PlayerName}@{fc.WorldName}", ref g, ImGuiColorEditFlags.NoInputs)) {
                            fc.Glow = g;
                        }

                        ImGui.SameLine();
                        if (ImGui.SmallButton($"remove glow##removeGlow_{fc.PlayerName}@{fc.WorldName}")) {
                            fc.Glow = null;
                        }
                    }
                } else {
                    ImGuiExt.UiColorPicker($"##picker_{fc.PlayerName}@{fc.WorldName}", ref fc.ColourKey);
                }
            }

            if (del != null) {
                Config.ForcedColours.Remove(del);
            }

            if (Service.Objects.LocalPlayer != null) {
                ImGui.TableNextColumn();
                if (ImGui.Button("+##newPlayerName", buttonSize)) {
                    addError = string.Empty;
                    if (Config.ForcedColours.Any(f => f.PlayerName == inputNewPlayerName && f.WorldName == inputServerName)) {
                        addError = LocString("NameAlreadyAddedError", "Name is already in list.");
                    } else {
                        var colourKey = GetColourKey(inputNewPlayerName, inputServerName, true) ?? 0;
                        Config.ForcedColours.Add(new ForcedColour() {
                            PlayerName = inputNewPlayerName, WorldName = inputServerName, ColourKey = colourKey, Color = Config.LegacyColours || ImGui.GetIO().KeyShift ? LegacyToNew(colourKey) : GetColor(inputNewPlayerName, inputServerName, true) ?? Vector3.One,
                        });
                        SaveConfig(Config);
                        inputNewPlayerName = string.Empty;
                    }
                }

                ImGui.TableNextColumn();

                var currentWorld = Service.Objects.LocalPlayer.CurrentWorld.Value.Name.ExtractText();
                var currentRegion = Regions.Find(r => r.DataCentres.Any(dc => dc.Worlds.Contains(currentWorld)));

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

                var target = Service.Targets.SoftTarget ?? Service.Targets.Target;
                if (target is IPlayerCharacter pc && !Config.ForcedColours.Any(f => f.PlayerName == pc.Name.TextValue && f.WorldName == pc.HomeWorld.Value.Name.ExtractText())) {
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    if (ImGui.Button($"Add {pc.Name.TextValue}")) {
                        var colourKey = GetColourKey(pc.Name.TextValue, pc.HomeWorld.Value.Name.ExtractText(), true) ?? 0;
                        Config.ForcedColours.Add(new ForcedColour() {
                            PlayerName = pc.Name.TextValue, WorldName = pc.HomeWorld.Value.Name.ExtractText(), ColourKey = colourKey, Color = Config.LegacyColours || ImGui.GetIO().KeyShift ? LegacyToNew(colourKey) : GetColor(pc.Name.TextValue, pc.HomeWorld.Value.Name.ExtractText(), true) ?? Vector3.One,
                        });
                        SaveConfig(Config);
                    }
                }
            }

            ImGui.EndTable();
        }

        if (ImGui.CollapsingHeader("Chat Channel Config")) {
            if (ImGui.BeginTable("chatChannelConfig", 3)) {
                ImGui.TableSetupColumn("Channel");
                ImGui.TableSetupColumn("Colour Sender Name");
                ImGui.TableSetupColumn("Colour Names in Message");
                ImGui.TableHeadersRow();

                foreach (var channel in Config.ChannelConfigs.Keys) {
                    var v = Config.ChannelConfigs[channel];
                    DrawChannelConfig(channel, ref v);
                    if (v == null) {
                        Config.ChannelConfigs.Remove(channel);
                    }
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.BeginCombo("##selectChannel", "Add Channel...")) {
                    var eValues = Enum.GetValues<XivChatType>();

                    foreach (var v in eValues) {
                        if (Config.ChannelConfigs.ContainsKey(v)) continue;
                        if (v is None or Debug) continue;

                        if (ImGui.Selectable($"{v.GetDetails()?.FancyName ?? $"{v}"}")) {
                            Config.ChannelConfigs.TryAdd(v, new ChannelConfig { Message = Config.DefaultChannelConfig.Message, Sender = Config.DefaultChannelConfig.Sender });
                        }
                    }

                    ImGui.EndCombo();
                }

                DrawChannelConfig(None, ref Config.DefaultChannelConfig);
                ImGui.EndTable();
            }
        }
    }

    public void DrawChannelConfig(XivChatType type, ref ChannelConfig config) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        if (type == None) {
            ImGui.TextDisabled("All Other Channels");
        } else {
            if (ImGuiComponents.IconButton($"##{type}_delete", FontAwesomeIcon.Trash)) {
                config = null;
                return;
            }

            ImGui.SameLine();
            ImGui.Text(type.GetDetails()?.FancyName ?? $"{type}");
        }

        ImGui.TableNextColumn();
        ImGui.Checkbox($"##{type}_sender", ref config.Sender);
        ImGui.TableNextColumn();
        ImGui.Checkbox($"##{type}_message", ref config.Message);
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        Service.Chat.ChatMessage += HandleChatMessage;
        base.Enable();
    }

    private readonly XivChatType[] chatTypes = [NoviceNetwork, TellIncoming, TellOutgoing, Say, Yell, Shout, Echo, Debug, Party, Alliance, CrossParty, PvPTeam, Ls1, Ls2, Ls3, Ls4, Ls5, Ls6, Ls7, Ls8, CrossLinkShell1, CrossLinkShell2, CrossLinkShell3, CrossLinkShell4, CrossLinkShell5, CrossLinkShell6, CrossLinkShell7, CrossLinkShell8, FreeCompany, CustomEmote, StandardEmote];

    private readonly ushort[] nameColours = [9, 25, 32, 35, 37, 39, 41, 42, 45, 48, 52, 56, 57, 65, 500, 502, 504, 506, 508, 517, 522, 524, 527, 541, 573];

    private void Parse(ref SeString seString) {
        var hasName = false;
        var newPayloads = new List<Payload>();
        PlayerPayload? waitingBegin = null;

        foreach (var payload in seString.Payloads) {
            if (payload is PlayerPayload p) {
                newPayloads.Add(p);
                waitingBegin = p;
                continue;
            }

            if (payload is TextPayload tp && waitingBegin != null && tp.Text != null && tp.Text.Trim().Contains(' ')) {
                if (!Config.LegacyColours) {
                    var colour = GetColor(waitingBegin.PlayerName, waitingBegin.World.Value.Name.ExtractText());
                    var glow = GetGlow(waitingBegin.PlayerName, waitingBegin.World.Value.Name.ExtractText());
                    if (colour != null) {
                        hasName = true;
                        newPayloads.Add(new ColorPayload(colour.Value).AsRaw());
                        if (glow != null) newPayloads.Add(new GlowPayload(glow.Value).AsRaw());
                        newPayloads.Add(tp);
                        if (glow != null) newPayloads.Add(new GlowEndPayload().AsRaw());
                        newPayloads.Add(new ColorEndPayload().AsRaw());
                    } else {
                        newPayloads.Add(tp);
                    }
                } else {
                    var colourKey = GetColourKey(waitingBegin.PlayerName, waitingBegin.World.Value.Name.ExtractText());
                    if (colourKey != null) {
                        hasName = true;
                        newPayloads.Add(new UIForegroundPayload(colourKey.Value));
                        newPayloads.Add(tp);
                        newPayloads.Add(new UIForegroundPayload(0));
                    } else {
                        newPayloads.Add(tp);
                    }
                }

                waitingBegin = null;
                continue;
            }

            newPayloads.Add(payload);
        }

        if (hasName) {
            seString = new SeString(newPayloads);
        }
    }

    private void HandleChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (Config.ChannelConfigs.TryGetValue(type, out var channelConfig) && channelConfig != null) {
            if (chatTypes.Contains(type)) {
                if (channelConfig.Sender) Parse(ref sender);
                if (channelConfig.Message) Parse(ref message);
            }
        } else if (chatTypes.Contains(type)) {
            if (Config.DefaultChannelConfig.Sender) Parse(ref sender);
            if (Config.DefaultChannelConfig.Message) Parse(ref message);
        }
    }

    protected override void Disable() {
        SaveConfig(Config);
        Service.Chat.ChatMessage -= HandleChatMessage;
        base.Disable();
    }
}
