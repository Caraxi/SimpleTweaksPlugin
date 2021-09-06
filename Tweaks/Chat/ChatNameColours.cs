﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal.Libc;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using static Dalamud.Game.Text.XivChatType;

namespace SimpleTweaksPlugin.Tweaks.Chat
{
    public unsafe class ChatNameColours : ChatTweaks.SubTweak
    {
        public override string Name => "Chat Name Colours";

        public override string Description => "Gives players a random colour in chat, or set the name manually.";

        public delegate void* PrintMessage(RaptureLogModule* chatManager, XivChatType xivChatType, IntPtr senderName, IntPtr message, uint senderId, byte param);
        private HookWrapper<PrintMessage> printChatHook;

        public class ForcedColour
        {
            public ushort ColourKey;
            public string PlayerName = string.Empty;
            public string WorldName = string.Empty;
        }

        public class Configs : TweakConfig
        {
            public List<ForcedColour> ForcedColours   = new();
            public bool               RoleColorInDuty = true;
        }

        public Configs Config { get; private set; }

        private string inputNewPlayerName = string.Empty;
        private string inputServerName = string.Empty;
        private string addError = string.Empty;

        private ushort GetColourKey(XivChatType type, string playerName, string worldName)
        {
            var forced = Config.ForcedColours.FirstOrDefault(f => f.PlayerName == playerName && f.WorldName == worldName);
            if (forced != null) return forced.ColourKey;

            var dutyChat = type == Party;
            if (dutyChat && Config.RoleColorInDuty && PluginInterface.ClientState.Condition[ConditionFlag.BoundByDuty])
            {
                foreach (var partyMember in PluginInterface.ClientState.Actors)
                {
                    if (playerName == partyMember.Name)
                    {
                        var character = partyMember as PlayerCharacter;
                        var role = character?.ClassJob.GameData.Role ?? 0;

                        if (role > 0 && role - 1 < roleColors.Length)
                        {
                            return roleColors[role - 1];
                        }
                    }
                }

                return unknownColor;
            }

            var key = (uint)$"${playerName}@{worldName}".GetHashCode();
            var defaultColourKey = nameColours[key % nameColours.Length];
            return defaultColourKey;
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
        {

            var buttonSize = new Vector2(22, 22) * ImGui.GetIO().FontGlobalScale;

            if (ImGui.Checkbox("Role Colors in Duty", ref Config.RoleColorInDuty))
            {
                SaveConfig(Config);
            }
            ImGui.Text("Color players in Duty by their roles instead. Yellow will be used for all players while Duty is still loading.");

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Force player colors:");

            if (ImGui.BeginTable("forcedPlayerNames", 4))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Server", ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("");

                ImGui.TableHeadersRow();

                ForcedColour del = null;
                foreach (var fc in Config.ForcedColours)
                {
                    ImGui.TableNextColumn();

                    if (ImGui.Button($"X##{fc.PlayerName}@{fc.WorldName}", buttonSize))
                    {
                        del = fc;
                    }

                    ImGui.TableNextColumn();

                    var xivCol = PluginInterface.Data.Excel.GetSheet<UIColor>().GetRow(fc.ColourKey).UIForeground;

                    var fa = xivCol & 255;
                    var fb = (xivCol >> 8) & 255;
                    var fg = (xivCol >> 16) & 255;
                    var fr = (xivCol >> 24) & 255;

                    var fColor = new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);

                    ImGui.PushStyleColor(ImGuiCol.Text, fColor);
                    ImGui.Text($"{fc.PlayerName}");
                    ImGui.PopStyleColor();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{fc.WorldName}");
                    ImGui.TableNextColumn();

                    if (ImGui.Button($"-##{fc.PlayerName}@{fc.WorldName}", buttonSize))
                    {
                        var c = fc.ColourKey - 1U;
                        var foreground = 0U;
                        while (foreground == 0)
                        {
                            if (c == 0)
                            {
                                c = PluginInterface.Data.Excel.GetSheet<UIColor>().Max(i => i.RowId);
                            }

                            var uiColor = PluginInterface.Data.Excel.GetSheet<UIColor>().GetRow(c);
                            if (uiColor == null)
                            {
                                c--;
                            }
                            else
                            {
                                if (uiColor.UIForeground != 0)
                                {
                                    foreground = uiColor.UIForeground;
                                }
                                else
                                {
                                    c--;
                                }
                            }
                        }

                        fc.ColourKey = (ushort)c;
                    }
                    ImGui.SameLine();
                    int v = fc.ColourKey;
                    ImGui.SetNextItemWidth(50 * ImGui.GetIO().FontGlobalScale);
                    ImGui.InputInt($"##value{fc.PlayerName}@{fc.WorldName}", ref v, 0, 0);
                    ImGui.SameLine();
                    if (ImGui.Button($"+##{fc.PlayerName}@{fc.WorldName}", buttonSize))
                    {
                        var c = fc.ColourKey + 1U;
                        var foreground = 0U;
                        while (foreground == 0)
                        {

                            var uiColor = PluginInterface.Data.Excel.GetSheet<UIColor>().GetRow(c);
                            if (uiColor == null)
                            {
                                if (c > PluginInterface.Data.Excel.GetSheet<UIColor>().Max(i => i.RowId))
                                {
                                    c = 0;
                                }
                                else
                                {
                                    c++;
                                }
                            }
                            else
                            {
                                if (uiColor.UIForeground != 0)
                                {
                                    foreground = uiColor.UIForeground;
                                }
                                else
                                {
                                    c++;
                                }
                            }
                        }

                        fc.ColourKey = (ushort)c;

                    }
                }

                if (del != null)
                {
                    Config.ForcedColours.Remove(del);
                }


                ImGui.TableNextColumn();
                if (ImGui.Button("+##newPlayerName", buttonSize))
                {
                    addError = string.Empty;
                    if (Config.ForcedColours.Any(f => f.PlayerName == inputNewPlayerName && f.WorldName == inputServerName))
                    {
                        addError = "Name is already in list.";
                    }
                    else
                    {
                        Config.ForcedColours.Add(new ForcedColour()
                        {
                            PlayerName = inputNewPlayerName,
                            WorldName = inputServerName,
                            ColourKey = GetColourKey(XivChatType.Say, inputNewPlayerName, inputServerName)
                        });
                        SaveConfig(Config);
                        inputNewPlayerName = string.Empty;
                    }
                }
                ImGui.TableNextColumn();

                if (PluginInterface.ClientState?.LocalPlayer != null)
                {
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##inputNewPlayerName", ref inputNewPlayerName, 25);
                    ImGui.TableNextColumn();

                    var serverList = PluginInterface.Data.Excel.GetSheet<World>().Where(w => w.DataCenter.Row == PluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData.DataCenter.Row).Select(w => w.Name.ToString()).ToList();
                    var serverIndex = serverList.IndexOf(inputServerName);
                    if (serverIndex == -1)
                    {
                        serverIndex = serverList.IndexOf(PluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData.Name.ToString());
                        inputServerName = serverList[serverIndex];
                    }

                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##inputServer", ref serverIndex, serverList.ToArray(), serverList.Count))
                    {
                        inputServerName = serverList[serverIndex];
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), addError);
                }


                ImGui.EndTable();
            }
            ;
        };

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            printChatHook ??= Common.Hook<PrintMessage>("E8 ?? ?? ?? ?? 4C 8B BC 24 ?? ?? ?? ?? 4D 85 F6", PrintMessageDetour, false);
            printChatHook?.Enable();
            PluginInterface.Framework.Gui.Chat.OnChatMessage += HandleChatMessage;
            base.Enable();
        }

        private readonly XivChatType[] chatTypes =
        {
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

        // tank, melee dps, ranged dps, healer
        private readonly ushort[] roleColors = { 37, 524, 524, 42 };

        private readonly ushort unknownColor = 25;

        private bool Parse(XivChatType type, ref SeString seString)
        {
            var hasName = false;

            var newPayloads = new List<Payload>();

            var waitingEnd = false;

            foreach (var payload in seString.Payloads)
            {
                if (payload is PlayerPayload p)
                {
                    hasName = true;
                    waitingEnd = true;
                    var colourKey = GetColourKey(type, p.PlayerName, p.World.Name);
                    newPayloads.Add(p);
                    newPayloads.Add(new UIForegroundPayload(PluginInterface.Data, colourKey));
                    continue;
                }
                newPayloads.Add(payload);
                if (waitingEnd)
                {
                    newPayloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                    waitingEnd = false;
                }
            }

            if (hasName)
            {
                if (waitingEnd)
                {
                    newPayloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                }

                seString = new SeString(newPayloads);
                return true;
            }

            return false;
        }

        private void HandleChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (chatTypes.Contains(type))
            {
                Parse(type, ref message);
            }
        }

        private void* PrintMessageDetour(RaptureLogModule* raptureLogModule, XivChatType xivChatType, IntPtr senderName, IntPtr message, uint senderId, byte param)
        {
            try
            {
                if (chatTypes.Contains(xivChatType))
                {
                    // Need to hook it manually to handle changing the name until API4
                    var stdSender = StdString.ReadFromPointer(senderName);
                    var parsedSender = PluginInterface.SeStringManager.Parse(stdSender.RawData);

                    if (Parse(xivChatType, ref parsedSender))
                    {
                        stdSender.RawData = parsedSender.Encode();
                        var allocatedString = PluginInterface.Framework.Libc.NewString(stdSender.RawData);
                        var retVal = printChatHook.Original(raptureLogModule, xivChatType, allocatedString.Address, message, senderId, param);
                        allocatedString?.Dispose();
                        return retVal;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLog.Error(ex);
            }


            return printChatHook.Original(raptureLogModule, xivChatType, senderName, message, senderId, param);
        }

        public override void Disable()
        {
            SaveConfig(Config);
            PluginInterface.Framework.Gui.Chat.OnChatMessage -= HandleChatMessage;
            printChatHook?.Disable();
            base.Disable();
        }
    }
}
