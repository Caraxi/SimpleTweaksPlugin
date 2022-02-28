using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using SimpleTweaksPlugin.TweakSystem;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public bool ShouldSerializeRetainerNotes() => RetainerNotes != null;
        public RetainerNotes.Configs RetainerNotes = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class RetainerNotes : UiAdjustments.SubTweak {
        public const int MaxRetainers = 12;

        #region Config
        public class Configs : TweakConfig {
            public string[] RetainerNote = new string[MaxRetainers];
            public ushort Width = 110;
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            int noteWidth = Config.Width;
            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt(LocString("ColumnWidth", "Width of the note column"), ref noteWidth);
            Config.Width = (ushort)Math.Max(20, Math.Min(300, noteWidth));

            for (int i = 1; i <= MaxRetainers; i++) {
                Config.RetainerNote[i - 1] ??= String.Empty;

                ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputTextWithHint(LocString("RetainerNote", "Note for retainer n. {0}").Format(i) + $"###RetainerNoteN{i}", LocString("Empty", "Empty"), ref Config.RetainerNote[i - 1], 60);
            }
        };
        #endregion

        public Configs Config { get; private set; }

        public override string Name => "Retainer notes";
        public override string Description => "Adds a note to the individual retainers in the retainer window.";
        protected override string Author => "Nesswen";

        private const ushort OldWidth = 910;
        private ushort AddedWidth => Config?.Width ?? 110;
        private ushort NewWidth => (ushort)(OldWidth + AddedWidth);

        #region Detour fields
        private delegate void UpdateRetainerDelegate(void* a1, int index, void* a3, void* a4);
        private delegate void* AddonSetupDelegate(void* a1, AtkUnitBase* a2, void* a3);

        private UpdateRetainerDelegate replacementUpdateRetainerDelegate;
        private void* updateRetainerPointer;
        private HookWrapper<AddonSetupDelegate> addonSetupHook;
        private UpdateRetainerDelegate updateRetainer;
        #endregion

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.UiAdjustments.RetainerNotes ?? new Configs();

            replacementUpdateRetainerDelegate = UpdateRetainerLineDetour;
            updateRetainerPointer = (void*)Common.Scanner.ScanText("40 53 56 41 56 48 83 EC 30 48 8B B1");
            updateRetainer = Marshal.GetDelegateForFunctionPointer<UpdateRetainerDelegate>(new IntPtr(updateRetainerPointer));
            addonSetupHook ??= Common.Hook("E8 ?? ?? ?? ?? 41 B1 1E", new AddonSetupDelegate(SetupDetour));
            addonSetupHook?.Enable();

            base.Enable();
        }

        private void* SetupDetour(void* a1, AtkUnitBase* a2, void* a3) {
            if (a3 == updateRetainerPointer) {
                var ptr = Marshal.GetFunctionPointerForDelegate(replacementUpdateRetainerDelegate);
                a3 = (void*)ptr;
            }
            return addonSetupHook.Original(a1, a2, a3);
        }

        private void UpdateRetainerLineDetour(void* a1, int index, void* a3, void* a4) {
            updateRetainer(a1, index, a3, a4);
            UpdateRetainerList();
        }

        private bool UpdateRetainerList() {
            try {
                var atkUnitBase = Common.GetUnitBase("RetainerList");
                if (atkUnitBase == null) return false;
                if ((atkUnitBase->Flags & 0x20) != 0x20) return false;

                var listNode = Common.GetNodeByID<AtkComponentNode>(atkUnitBase->UldManager, RetainerWindowNodeIds.RetainerList.Id);
                if (listNode == null || (ushort)listNode->AtkResNode.Type < 1000) return false;

                var windowNode = Common.GetNodeByID<AtkComponentNode>(atkUnitBase->UldManager, RetainerWindowNodeIds.Window.Id);

                if (windowNode->AtkResNode.Width == OldWidth) {
                    // Resize the window and add note header

                    ResizeAndAddHeaderToRetainerWindow(atkUnitBase);

                    // Adjust individual retainers

                    var retainerManager = RetainerManager.Instance();

                    for (uint i = 1; i <= 12; i++) {
                        var retainerRow = Common.GetNodeByID<AtkComponentNode>(listNode->Component->UldManager, RetainerListNodeIds.GetNthRetainer(i).Id);
                        if (retainerRow == null || !retainerRow->AtkResNode.IsVisible) continue;

                        ResizeAndAddColumnToRetainerRow(retainerRow, i);
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Debug(ex.ToString());
                return false;
            }

            return true;
        }

        private void ResizeAndAddHeaderToRetainerWindow(AtkUnitBase* atkUnitBase) {
            void SetWidth(RetainerWindowNodeIds node, int width) => UiHelper.SetSize(Common.GetNodeByID(atkUnitBase->UldManager, node.Id), width, null);
            void SetX(RetainerWindowNodeIds node, int x) => UiHelper.SetPosition(Common.GetNodeByID(atkUnitBase->UldManager, node.Id), x, null);

            // Resize

            SetWidth(RetainerWindowNodeIds.WindowContainer, NewWidth);
            SetWidth(RetainerWindowNodeIds.Window, NewWidth);
            SetWidth(RetainerWindowNodeIds.RetainerList, NewWidth - 24);
            SetWidth(RetainerWindowNodeIds.StatusBarRes, NewWidth);
            SetWidth(RetainerWindowNodeIds.StatusBarContractSuspended, NewWidth - 140);
            SetX(RetainerWindowNodeIds.RetainerReorderButton, NewWidth - 42);
            SetWidth(RetainerWindowNodeIds.Unknown, NewWidth - 24);
            SetX(RetainerWindowNodeIds.VentureCoinRes, NewWidth - 200);

            SetWidth(RetainerWindowNodeIds.RetainerListHeaderRes, NewWidth - 30);
            SetX(RetainerWindowNodeIds.RetainerListHeaderVenture, NewWidth - 230);
            SetX(RetainerWindowNodeIds.RetainerListHeaderMarketboard, NewWidth - 400);
            SetX(RetainerWindowNodeIds.RetainerListHeaderGil, NewWidth - 500);

            // Create header for "Note"

            UiHelper.ExpandNodeList(atkUnitBase, 1);
            var lastHeader = Common.GetNodeByID<AtkTextNode>(atkUnitBase->UldManager, RetainerWindowNodeIds.RetainerListHeaderName.Id);
            var inventoryHeader = Common.GetNodeByID<AtkTextNode>(atkUnitBase->UldManager, RetainerWindowNodeIds.RetainerListHeaderInventory.Id);

            var newPosition = inventoryHeader->AtkResNode.X + inventoryHeader->AtkResNode.Width + 20;

            var newHeaderItem = UiHelper.CloneNode(inventoryHeader);
            newHeaderItem->AtkResNode.NodeID = RetainerWindowNodeIds.FirstFreeNodeId;
            newHeaderItem->AtkResNode.X = newPosition;
            newHeaderItem->AtkResNode.Width = AddedWidth;
            newHeaderItem->AtkResNode.ParentNode = inventoryHeader->AtkResNode.ParentNode;
            newHeaderItem->AtkResNode.NextSiblingNode = (AtkResNode*)lastHeader;
            lastHeader->AtkResNode.PrevSiblingNode = (AtkResNode*)newHeaderItem;

            newHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newHeaderItem->NodeText.BufSize);
            newHeaderItem->SetText("Note");

            atkUnitBase->UldManager.NodeList[atkUnitBase->UldManager.NodeListCount++] = (AtkResNode*)newHeaderItem;
        }

        private void ResizeAndAddColumnToRetainerRow(AtkComponentNode* retainerRow, uint idx) {
            UiHelper.SetSize(retainerRow, NewWidth - 32, null);

            void SetWidth(RetainerListRowNodeIds node, int width) => UiHelper.SetSize(Common.GetNodeByID(retainerRow->Component->UldManager, node.Id), width, null);
            void SetX(RetainerListRowNodeIds node, int x) => UiHelper.SetPosition(Common.GetNodeByID(retainerRow->Component->UldManager, node.Id), x, null);

            // Resize row

            SetWidth(RetainerListRowNodeIds.Res, NewWidth - 32);
            SetWidth(RetainerListRowNodeIds.Collider, NewWidth - 32);
            SetX(RetainerListRowNodeIds.VentureStatus, NewWidth - 212);
            SetX(RetainerListRowNodeIds.MarketBoardStatus, NewWidth - 397);
            SetX(RetainerListRowNodeIds.LocationIcon, NewWidth - 419);
            SetX(RetainerListRowNodeIds.GilAmount, NewWidth - 540);
            SetX(RetainerListRowNodeIds.GilIcon, NewWidth - 562);

            // Add note column

            var lastRowItem = Common.GetNodeByID<AtkTextNode>(retainerRow->Component->UldManager, RetainerListRowNodeIds.Name.Id);
            var inventoryStatusRowItem = Common.GetNodeByID<AtkTextNode>(retainerRow->Component->UldManager, RetainerListRowNodeIds.InventoryStatus.Id);

            var newItemPosition = inventoryStatusRowItem->AtkResNode.X + inventoryStatusRowItem->AtkResNode.Width + 26;

            // all nodes in the row are children of RetainerListRowNodeIds.Container, but I can't extend that list, so I am using the retainer row as a parent node
            UiHelper.ExpandNodeList(retainerRow, 1);

            var newRowItem = UiHelper.CloneNode(inventoryStatusRowItem);
            newRowItem->AtkResNode.NodeID = RetainerListRowNodeIds.FirstFreeNodeId;
            newRowItem->AtkResNode.X = newItemPosition;
            newRowItem->AtkResNode.Width = AddedWidth;
            newRowItem->AtkResNode.ParentNode = (AtkResNode*)retainerRow;
            newRowItem->AtkResNode.NextSiblingNode = (AtkResNode*)lastRowItem;
            lastRowItem->AtkResNode.PrevSiblingNode = (AtkResNode*)newRowItem;

            newRowItem->AlignmentFontType = (byte)AlignmentType.Left;
            newRowItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newRowItem->NodeText.BufSize);
            newRowItem->SetText(Config.RetainerNote[idx - 1] ?? String.Empty);

            retainerRow->Component->UldManager.NodeList[retainerRow->Component->UldManager.NodeListCount++] = (AtkResNode*)newRowItem;
        }

        private void CloseRetainerList() {
            var atkUnitBase = Common.GetUnitBase("RetainerList");
            if (atkUnitBase == null) return;
            UiHelper.Close(atkUnitBase, true);
        }

        public override void Disable() {
            addonSetupHook?.Disable();
            CloseRetainerList();
            SaveConfig(Config);
            base.Disable();
        }

        public override void Dispose() {
            addonSetupHook?.Dispose();
            base.Dispose();
        }

        #region UI Node indices
        private class RetainerWindowNodeIds {
            public static readonly RetainerWindowNodeIds WindowContainer = new(0, 1);
            public static readonly RetainerWindowNodeIds Window = new(1, 25);
            public static readonly RetainerWindowNodeIds RetainerList = new(2, 24);
            public static readonly RetainerWindowNodeIds StatusBarRes = new(3, 16);
            public static readonly RetainerWindowNodeIds StatusBarContractSuspended = new(4, 23);
            public static readonly RetainerWindowNodeIds StatusBarVentureStatus1 = new(5, 22);
            public static readonly RetainerWindowNodeIds StatusBarVentureStatus2 = new(6, 21);
            public static readonly RetainerWindowNodeIds StatusBarVentureLabel = new(7, 20);
            public static readonly RetainerWindowNodeIds StatusBarMarketboardStatus1 = new(8, 19);
            public static readonly RetainerWindowNodeIds StatusBarMarketboardStatus2 = new(9, 18);
            public static readonly RetainerWindowNodeIds StatusBarMarketboardLabel = new(10, 17);
            public static readonly RetainerWindowNodeIds BottomHorizontalListBorder = new(11, 15);
            public static readonly RetainerWindowNodeIds TopHorizontalListBorder = new(12, 14);
            public static readonly RetainerWindowNodeIds RetainerReorderButton = new(13, 13);
            public static readonly RetainerWindowNodeIds RetainerListHeaderRes = new(14, 6);
            public static readonly RetainerWindowNodeIds RetainerListHeaderVenture = new(15, 12);
            public static readonly RetainerWindowNodeIds RetainerListHeaderMarketboard = new(16, 11);
            public static readonly RetainerWindowNodeIds RetainerListHeaderGil = new(17, 10);
            public static readonly RetainerWindowNodeIds RetainerListHeaderInventory = new(18, 9);
            public static readonly RetainerWindowNodeIds RetainerListHeaderClassJob = new(19, 8);
            public static readonly RetainerWindowNodeIds RetainerListHeaderName = new(20, 7);
            public static readonly RetainerWindowNodeIds Unknown = new(21, 5);
            public static readonly RetainerWindowNodeIds VentureCoinRes = new(22, 2);
            public static readonly RetainerWindowNodeIds VentureCoinIcon = new(23, 4);
            public static readonly RetainerWindowNodeIds VentureCoinAmount = new(24, 3);

            public const int FirstFreeNodeId = 26;

            public uint ChildIndex { get; }
            public uint Id { get; }

            protected RetainerWindowNodeIds(uint index, uint id) {
                ChildIndex = index;
                Id = id;
            }
        }

        private class RetainerListNodeIds {
            public static readonly RetainerListNodeIds ScrollBar = new(0, 5);
            public static readonly RetainerListNodeIds Retainer1 = new(1, 4);
            public static readonly RetainerListNodeIds Retainer2 = new(2, 41001);
            public static readonly RetainerListNodeIds Retainer3 = new(3, 41002);
            public static readonly RetainerListNodeIds Retainer4 = new(4, 41003);
            public static readonly RetainerListNodeIds Retainer5 = new(5, 41004);
            public static readonly RetainerListNodeIds Retainer6 = new(6, 41005);
            public static readonly RetainerListNodeIds Retainer7 = new(7, 41006);
            public static readonly RetainerListNodeIds Retainer8 = new(8, 41007);
            public static readonly RetainerListNodeIds Retainer9 = new(9, 41008);
            public static readonly RetainerListNodeIds Retainer10 = new(10, 41009);
            public static readonly RetainerListNodeIds Retainer11 = new(11, 41010);
            public static readonly RetainerListNodeIds Retainer12 = new(12, 41011);
            public static readonly RetainerListNodeIds Unknown1 = new(13, 3);
            public static readonly RetainerListNodeIds Unknown2 = new(14, 2);

            public uint ChildIndex { get; }
            public uint Id { get; }

            protected RetainerListNodeIds(uint index, uint id) {
                ChildIndex = index;
                Id = id;
            }

            public static RetainerListNodeIds GetNthRetainer(uint n) {
                if (n == 0) throw new ArgumentException("Retainer indices start at 1.");

                var type = typeof(RetainerListNodeIds);
                return type
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(p => p.Name == $"Retainer{n}")
                    ?.GetValue(null) as RetainerListNodeIds;
            }
        }

        private class RetainerListRowNodeIds {
            public static readonly RetainerListRowNodeIds Collider = new(0, 15);
            public static readonly RetainerListRowNodeIds BackgroundLowlight = new(1, 14);
            public static readonly RetainerListRowNodeIds BackgroundHighlight = new(2, 13);
            public static readonly RetainerListRowNodeIds Res = new(3, 2);
            public static readonly RetainerListRowNodeIds VentureStatus = new(4, 12);
            public static readonly RetainerListRowNodeIds MarketBoardStatus = new(5, 11);
            public static readonly RetainerListRowNodeIds LocationIcon = new(6, 10);
            public static readonly RetainerListRowNodeIds GilAmount = new(7, 9);
            public static readonly RetainerListRowNodeIds GilIcon = new(8, 8);
            public static readonly RetainerListRowNodeIds InventoryStatus = new(9, 7);
            public static readonly RetainerListRowNodeIds InventoryIcon = new(10, 6);
            public static readonly RetainerListRowNodeIds ClassJobName = new(11, 5);
            public static readonly RetainerListRowNodeIds ClassJobIcon = new(12, 4);
            public static readonly RetainerListRowNodeIds Name = new(13, 3);

            public const int FirstFreeNodeId = 16;

            public uint ChildIndex { get; }
            public uint Id { get; }

            protected RetainerListRowNodeIds(uint index, uint id) {
                ChildIndex = index;
                Id = id;
            }
        }
        #endregion
    }
}