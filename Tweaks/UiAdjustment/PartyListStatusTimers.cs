using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class PartyListStatusTimers : UiAdjustments.SubTweak {
    public override string Name => "Party List Status Timers";
    public override string Description => "Show timers on status effects on the party list.";

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PartyStatus {
        [FieldOffset(0x00)] public uint StatusID;
        [FieldOffset(0x04)] public ushort Countdown;
        [FieldOffset(0x0C)] public byte IsFromLocalPlayer;
    }

    public class PartyMemberStatusCache {
        public uint StatusID { get; private set; }
        public Status Data { get; private set; }
        public bool IsFromLocalPlayer { get; private set; }

        public float Countdown {
            get {
                if (Data is { IsPermanent: true }) return 0;
                var t = lastCountdown - ((float)timer.Elapsed.TotalSeconds - 1);
                return t < 0 ? 0 : t;
            }
        }

        public void Set(uint statusId, ushort countdown, bool fromLocal) {
            if (!timer.IsRunning || StatusID != statusId || countdown != lastCountdown) timer.Restart();
            if (this.StatusID != statusId) Data = Service.Data.Excel.GetSheet<Status>().GetRow(statusId);
            this.StatusID = statusId;
            this.lastCountdown = countdown;
            this.IsFromLocalPlayer = fromLocal;
        }

        public void Clear() {
            this.StatusID = 0;
            this.Data = null;
            this.lastCountdown = 0;
            timer.Stop();
        }

        private ushort lastCountdown;
        private readonly Stopwatch timer = new Stopwatch();
    }

    private delegate void UpdatePartyListStatusEffects(void* agentHud, NumberArrayData* numberArrayData, StringArrayData* stringArrayData, PartyStatus* statusList, int statusCount, int startIndex, int a7, int a8, int a9);

    private HookWrapper<UpdatePartyListStatusEffects> updatePartyListStatusEffectsHook;


    private delegate void UpdateSlotStatusEffects(void* agentHud, NumberArrayData* numberArrayData, StringArrayData* stringArrayData, uint objectId, ulong a5, int slotIndex);

    private HookWrapper<UpdateSlotStatusEffects> updateSlotHook;


    internal static readonly PartyMemberStatusCache[] PartyStatusArray = new PartyMemberStatusCache[80];

    private void UpdatePartyListStatusEffectsDetour(void* agentHud, NumberArrayData* numberArrayData, StringArrayData* stringArrayData, PartyStatus* statusList, int statusCount, int startIndex, int a7, int a8, int a9) {
        updatePartyListStatusEffectsHook?.Original(agentHud, numberArrayData, stringArrayData, statusList, statusCount, startIndex, a7, a8, a9);
        try {
            var i = (startIndex - 23) / 39;
            if (i is < 0 or >= 8) return;
            for (var s = 0; s < statusCount; s++) {
                PartyStatusArray[i * 10 + s].Set(statusList[s].StatusID, statusList[s].Countdown, statusList[s].IsFromLocalPlayer != 0);
            }

            for (var s = statusCount; s < 10; s++) {
                PartyStatusArray[i * 10 + s].Clear();
            }
        } catch (Exception ex){
            SimpleLog.Error(ex);
        }
    }

    public override void Setup() {

        for (var i = 0; i < 80; i++) {
            PartyStatusArray[i] = new PartyMemberStatusCache();
        }

        base.Setup();
    }

    public override void Enable() {
        updatePartyListStatusEffectsHook ??= Common.Hook<UpdatePartyListStatusEffects>("E8 ?? ?? ?? ?? 0F BF 8F ?? ?? ?? ?? 4D 8B 07", UpdatePartyListStatusEffectsDetour);
        updatePartyListStatusEffectsHook?.Enable();

        updateSlotHook ??= Common.Hook<UpdateSlotStatusEffects>("E8 ?? ?? ?? ?? 45 8B 46 74", UpdateSlotDetour);
        updateSlotHook?.Enable();
        Service.Framework.Update += FrameworkUpdate;
        base.Enable();
    }

    private readonly ulong?[] updateValues = new ulong?[8];
    private readonly uint?[] objIds = new uint?[8];

    private void UpdateSlotDetour(void* agentHud, NumberArrayData* numberArrayData, StringArrayData* stringArrayData, uint objectid, ulong a5, int slotindex) {
        if (slotindex is >= 0 and <= 7) {
            updateValues[slotindex] = a5;
            objIds[slotindex] = objectid;
        }
        updateSlotHook.Original(agentHud, numberArrayData, stringArrayData, objectid, a5, slotindex);
    }

    private readonly Stopwatch lastUpdate = new();

    private void FrameworkUpdate(Framework framework) {
        try {
            if (!lastUpdate.IsRunning) lastUpdate.Start();
            if (lastUpdate.ElapsedMilliseconds >= 100) {
                lastUpdate.Restart();
                Update();
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void Update(bool reset = false) {


        var partyList = Common.GetUnitBase<AddonPartyList>();
        if (partyList == null) return;

        for (var i = 1; i < partyList->MemberCount && i < 8; i++) {
            if (objIds[i] != null && updateValues[i] != null) {
                var uiModule = Common.UIModule;
                var atkArrayDataHolder = uiModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
                var agentHud = uiModule->GetAgentModule()->GetAgentByInternalID(4);
                updateSlotHook.Original(agentHud, atkArrayDataHolder.NumberArrays[4], atkArrayDataHolder.StringArrays[3], objIds[i].Value, updateValues[i].Value, i);
            }
        }

        for (var pi = 0; pi < 8; pi++) {
            var partySlot = partyList->PartyMember[pi];
            for (var si = 0; si < 10; si++) {
                var statusSlot = partySlot.StatusIcon[si];
                if (statusSlot == null) continue;

                AtkTextNode* timerNode = null;
                AtkResNode* lastNode = null;
                for (var n = 0; n < statusSlot->AtkComponentBase.UldManager.NodeListCount; n++) {
                    var node = statusSlot->AtkComponentBase.UldManager.NodeList[n];
                    if (node == null) continue;
                    lastNode = node;
                    if (node->NodeID == CustomNodes.PartyListStatusTimer) {
                        timerNode = node->GetAsAtkTextNode();
                        break;
                    }
                }

                if (timerNode == null && reset) continue;

                if (timerNode != null && reset) {
                    timerNode->AtkResNode.NodeID = 99990000;
                    if (timerNode->AtkResNode.NextSiblingNode != null) timerNode->AtkResNode.NextSiblingNode->PrevSiblingNode = timerNode->AtkResNode.PrevSiblingNode;
                    if (timerNode->AtkResNode.PrevSiblingNode != null) timerNode->AtkResNode.PrevSiblingNode->NextSiblingNode = timerNode->AtkResNode.PrevSiblingNode;
                    if (timerNode->AtkResNode.ParentNode == (AtkResNode*)timerNode) timerNode->AtkResNode.ParentNode = timerNode->AtkResNode.PrevSiblingNode;
                    timerNode->AtkResNode.ParentNode = null;
                    timerNode->AtkResNode.NextSiblingNode = null;
                    timerNode->AtkResNode.PrevSiblingNode = null;
                    statusSlot->AtkComponentBase.UldManager.UpdateDrawNodeList();
                    timerNode->AtkResNode.Destroy(true);
                    continue;
                }

                if (timerNode == null) {
                    var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
                    if (newTextNode != null) {
                        IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
                        newTextNode->Ctor();
                        timerNode = newTextNode;

                        newTextNode->AtkResNode.Type = NodeType.Text;
                        newTextNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
                        newTextNode->AtkResNode.DrawFlags = 0;
                        newTextNode->AtkResNode.SetWidth(24);
                        newTextNode->AtkResNode.SetHeight(17);

                        newTextNode->LineSpacing = 12;
                        newTextNode->AlignmentFontType = 4;
                        newTextNode->FontSize = 12;
                        newTextNode->TextFlags = (byte)(TextFlags.AutoAdjustNodeSize | TextFlags.Edge);
                        newTextNode->TextFlags2 = 0;

                        newTextNode->AtkResNode.NodeID = CustomNodes.PartyListStatusTimer;

                        newTextNode->AtkResNode.Color.A = 0xFF;
                        newTextNode->AtkResNode.Color.R = 0xFF;
                        newTextNode->AtkResNode.Color.G = 0xFF;
                        newTextNode->AtkResNode.Color.B = 0xFF;

                        newTextNode->AtkResNode.SetPositionShort(0, 24);

                        lastNode->PrevSiblingNode = (AtkResNode*) newTextNode;
                        newTextNode->AtkResNode.NextSiblingNode = lastNode;
                        newTextNode->AtkResNode.ParentNode = lastNode->ParentNode;
                        statusSlot->AtkComponentBase.UldManager.UpdateDrawNodeList();

                        timerNode->TextColor.A = 0xFF;
                        timerNode->TextColor.G = 0xFF;
                        timerNode->EdgeColor.A = 0xFF;
                    } else {
                        continue;
                    }
                }

                var statusCache = PartyStatusArray[pi * 10 + si];

                if (statusCache.Countdown <= 1) {
                    timerNode->AtkResNode.ToggleVisibility(false);
                } else {
                    timerNode->AtkResNode.ToggleVisibility(true);

                    timerNode->TextColor.R = (byte) (statusCache.IsFromLocalPlayer ? 0xC9 : 0xFF);
                    timerNode->TextColor.B = (byte) (statusCache.IsFromLocalPlayer ? 0xFE : 0xFF);

                    timerNode->EdgeColor.R = (byte) (statusCache.IsFromLocalPlayer ? 0x0A : 0x33);
                    timerNode->EdgeColor.G = (byte) (statusCache.IsFromLocalPlayer ? 0x5F : 0x33);
                    timerNode->EdgeColor.B = (byte) (statusCache.IsFromLocalPlayer ? 0x24 : 0x33);
                    switch (statusCache.Countdown) {
                        case > 3600:
                            timerNode->SetText($"{(int)statusCache.Countdown/3600}h");
                            break;
                        case > 60:
                            timerNode->SetText($"{(int)statusCache.Countdown/60}m");
                            break;
                        default:
                            timerNode->SetText($"{(int)statusCache.Countdown}");
                            break;
                    }
                }
            }
        }
    }

#if DEBUG
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        ImGui.BeginTable("statusTimersDebugTable", 11, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Resizable, new Vector2(-1, 400));
        ImGui.TableSetupColumn("Member", ImGuiTableColumnFlags.WidthFixed, 150);
        for (var i = 1; i <= 10; i++) {
            ImGui.TableSetupColumn($"#{i}", ImGuiTableColumnFlags.WidthFixed, 100);
        }
        ImGui.TableHeadersRow();
        ImGui.TableNextColumn();

        for (var i = 0; i < 8; i++) {

            ImGui.Text($"#{i+1}");
            ImGui.TableNextColumn();
            for (var s = 0; s < 10; s++) {

                var status = PartyStatusArray[i * 10 + s];

                if (status.StatusID != 0) {
                    ImGui.Text($"{status.Data.Name}");
                    ImGui.Text($"{status.StatusID}");
                    ImGui.Text($"{status.Countdown}");
                }

                if (!(i == 7 && s == 9)) ImGui.TableNextColumn();
            }
        }

        ImGui.EndTable();
    };
#endif

    public override void Disable() {
        updatePartyListStatusEffectsHook?.Disable();
        updateSlotHook?.Disable();
        Service.Framework.Update -= FrameworkUpdate;
        try {
            Update(true);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        base.Disable();
    }

    public override void Dispose() {
        updatePartyListStatusEffectsHook?.Dispose();
        updateSlotHook?.Dispose();
        base.Dispose();
    }
}