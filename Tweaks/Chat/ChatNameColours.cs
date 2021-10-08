using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Libc;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using static Dalamud.Game.Text.XivChatType;

namespace SimpleTweaksPlugin.Tweaks.Chat {
    public unsafe class ChatNameColours : ChatTweaks.SubTweak {
        public override string Name => "Chat Name Colours";
        public override string Description => "Gives players a random colour in chat, or set the name manually.";
        public delegate void* PrintMessage(RaptureLogModule* chatManager, XivChatType xivChatType, IntPtr senderName, IntPtr message, uint senderId, byte param);
        private HookWrapper<PrintMessage> printChatHook;

        public class ForcedColour {
            public ushort ColourKey;
            public string PlayerName = string.Empty;
            public string WorldName = string.Empty;
        }

        public class Configs : TweakConfig {
            public List<ForcedColour> ForcedColours = new();
        }

        public Configs Config { get; private set; }

        private string inputNewPlayerName = string.Empty;
        private string inputServerName = string.Empty;
        private string addError = string.Empty;

        private ushort GetColourKey(string playerName, string worldName) {
            var forced = Config.ForcedColours.FirstOrDefault(f => f.PlayerName == playerName && f.WorldName == worldName);
            if (forced != null) return forced.ColourKey;
            var key = (uint) $"${playerName}@{worldName}".GetHashCode();
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

            if (ImGui.BeginTable("forcedPlayerNames", 4)) {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Server", ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
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

                    if (ImGui.Button($"-##{fc.PlayerName}@{fc.WorldName}", buttonSize)) {
                        var c = fc.ColourKey - 1U;
                        var foreground = 0U;
                        while (foreground == 0) {
                            if (c == 0) {
                                c = uiColorSheet.Max(i => i.RowId);
                            }

                            var uiColor = uiColorSheet.GetRow(c);
                            if (uiColor == null) {
                                c--;
                            } else {
                                if (uiColor.UIForeground != 0) {
                                    foreground = uiColor.UIForeground;
                                } else {
                                    c--;
                                }
                            }
                        }

                        fc.ColourKey = (ushort) c;
                    }
                    ImGui.SameLine();
                    int v = fc.ColourKey;
                    ImGui.SetNextItemWidth(50 * ImGui.GetIO().FontGlobalScale);
                    ImGui.InputInt($"##value{fc.PlayerName}@{fc.WorldName}", ref v, 0, 0);
                    ImGui.SameLine();
                    if (ImGui.Button($"+##{fc.PlayerName}@{fc.WorldName}", buttonSize)) {
                        var c = fc.ColourKey + 1U;
                        var foreground = 0U;
                        while (foreground == 0) {

                            var uiColor = uiColorSheet.GetRow(c);
                            if (uiColor == null) {
                                if (c > uiColorSheet.Max(i => i.RowId)) {
                                    c = 0;
                                } else {
                                    c++;
                                }
                            } else {
                                if (uiColor.UIForeground != 0) {
                                    foreground = uiColor.UIForeground;
                                } else {
                                    c++;
                                }
                            }
                        }

                        fc.ColourKey = (ushort) c;

                    }
                }

                if (del != null) {
                    Config.ForcedColours.Remove(del);
                }


                ImGui.TableNextColumn();
                if (ImGui.Button("+##newPlayerName", buttonSize)) {
                    addError = string.Empty;
                    if (Config.ForcedColours.Any(f => f.PlayerName == inputNewPlayerName && f.WorldName == inputServerName)) {
                        addError = "Name is already in list.";
                    } else {
                        Config.ForcedColours.Add(new ForcedColour() {
                            PlayerName = inputNewPlayerName,
                            WorldName = inputServerName,
                            ColourKey = GetColourKey(inputNewPlayerName, inputServerName)
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
            printChatHook ??= Common.Hook<PrintMessage>("E8 ?? ?? ?? ?? 4C 8B BC 24 ?? ?? ?? ?? 4D 85 F6", PrintMessageDetour);
            printChatHook?.Enable();
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
                    hasName = true;
                    waitingEnd = true;
                    var colourKey = GetColourKey(p.PlayerName, p.World.Name);
                    newPayloads.Add(p);
                    newPayloads.Add(new UIForegroundPayload(colourKey));
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
                Parse(ref message);
            }
        }

        private void* PrintMessageDetour(RaptureLogModule* raptureLogModule, XivChatType xivChatType, IntPtr senderName, IntPtr message, uint senderId, byte param) {
            try {
                if (chatTypes.Contains(xivChatType)) {
                    // Need to hook it manually to handle changing the name until API4
                    var stdSender = StdString.ReadFromPointer(senderName);
                    var parsedSender = SeString.Parse(stdSender.RawData);

                    if (Parse(ref parsedSender)) {
                        stdSender.RawData = parsedSender.Encode();
                        var allocatedString = Service.LibcFunction.NewString(stdSender.RawData);
                        var retVal = printChatHook.Original(raptureLogModule, xivChatType, allocatedString.Address, message, senderId, param);
                        allocatedString.Dispose();
                        return retVal;
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }


            return printChatHook.Original(raptureLogModule, xivChatType, senderName, message, senderId, param);
        }

        public override void Disable() {
            SaveConfig(Config);
            Service.Chat.ChatMessage -= HandleChatMessage;
            printChatHook?.Disable();
            base.Disable();
        }
    }
}
