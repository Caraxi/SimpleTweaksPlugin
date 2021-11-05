using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Helper {
    internal unsafe class Common {

        // Common Delegates
        public delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);

        private delegate IntPtr GetGameAllocator();

        private static GameAlloc _gameAlloc;
        private static GetGameAllocator _getGameAllocator;

        private delegate InventoryContainer* GetInventoryContainer(IntPtr inventoryManager, InventoryType inventoryType);
        private delegate InventoryItem* GetContainerSlot(InventoryContainer* inventoryContainer, int slotId);

        private static GetInventoryContainer _getInventoryContainer;
        private static GetContainerSlot _getContainerSlot;

        public static IntPtr InventoryManagerAddress;

        public static IntPtr PlayerStaticAddress { get; private set; }

        private static IntPtr LastCommandAddress;
        
        public static Utf8String* LastCommand { get; private set; }

        public static SigScanner Scanner => Service.SigScanner;

        public Common() {
            var gameAllocPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23");
            var getGameAllocatorPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08");

            InventoryManagerAddress = Scanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
            var getInventoryContainerPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
            var getContainerSlotPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");

            PlayerStaticAddress = Scanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
            LastCommandAddress = Scanner.GetStaticAddressFromSig("4C 8D 05 ?? ?? ?? ?? 41 B1 01 49 8B D4 E8 ?? ?? ?? ?? 83 EB 06");
            LastCommand = (Utf8String*) (LastCommandAddress);

            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(gameAllocPtr);
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(getGameAllocatorPtr);

            _getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(getInventoryContainerPtr);
            _getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(getContainerSlotPtr);
        }

        public static UIModule* UIModule => Framework.Instance()->GetUiModule();

        public static AtkUnitBase* GetUnitBase(string name, int index = 1) {
            return (AtkUnitBase*) Service.GameGui.GetAddonByName(name, index);
        }

        public static T* GetUnitBase<T>(string name = null, int index = 1) where T : unmanaged {
            if (string.IsNullOrEmpty(name)) {
                var attr = (Addon) typeof(T).GetCustomAttribute(typeof(Addon));
                if (attr != null) {
                    name = attr.AddonIdentifiers.FirstOrDefault();
                }
            }

            if (string.IsNullOrEmpty(name)) return null;
            
            return (T*) Service.GameGui.GetAddonByName(name, index);
        }

        public static InventoryContainer* GetContainer(InventoryType inventoryType) {
            if (InventoryManagerAddress == IntPtr.Zero) return null;
            return _getInventoryContainer(InventoryManagerAddress, inventoryType);
        }

        public static InventoryItem* GetContainerItem(InventoryContainer* container, int slot) {
            if (container == null) return null;
            return _getContainerSlot(container, slot);
        }

        public static InventoryItem* GetInventoryItem(InventoryType inventoryType, int slotId) {
            if (InventoryManagerAddress == IntPtr.Zero) return null;
            var container = _getInventoryContainer(InventoryManagerAddress, inventoryType);
            return container == null ? null : _getContainerSlot(container, slotId);
        }

        public static IntPtr Alloc(ulong size) {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }
        
        public void WriteSeString(byte** startPtr, IntPtr alloc, SeString seString) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*)alloc) return;
            WriteSeString((byte*)alloc, seString);
            *startPtr = (byte*)alloc;
        }

        public SeString ReadSeString(byte** startPtr) {
            if (startPtr == null) return null;
            var start = *(startPtr);
            if (start == null) return null;
            return ReadSeString(start);
        }

        public SeString ReadSeString(byte* ptr) {
            var offset = 0;
            while (true) {
                var b = *(ptr + offset);
                if (b == 0) {
                    break;
                }
                offset += 1;
            }
            var bytes = new byte[offset];
            Marshal.Copy(new IntPtr(ptr), bytes, 0, offset);
            return SeString.Parse(bytes);
        }

        public void WriteSeString(byte* dst, SeString s) {
            var bytes = s.Encode();
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            *(dst + bytes.Length) = 0;
        }

        public SeString ReadSeString(Utf8String xivString) {
            var len = (int) (xivString.BufUsed > int.MaxValue ? int.MaxValue : xivString.BufUsed);
            var bytes = new byte[len];
            Marshal.Copy(new IntPtr(xivString.StringPtr), bytes, 0, len);
            return SeString.Parse(bytes);
        }

        public void WriteSeString(Utf8String xivString, SeString s) {
            var bytes = s.Encode();
            int i;
            xivString.BufUsed = 0;
            for (i = 0; i < bytes.Length && i < xivString.BufSize - 1; i++) {
                *(xivString.StringPtr + i) = bytes[i];
                xivString.BufUsed++;
            }
            *(xivString.StringPtr + i) = 0;
        }

        public enum GameOptionKind : uint {
            GamePadMode        = 0x089, // [bool] Character Config -> Mouse Mode / GamePad Mode
            LegacyMovement     = 0x08A, // [bool] Character Config -> Control Settings -> General -> Standard Type / Legacy Type
            DisplayItemHelp    = 0x130, // [bool] Character Config -> UI Settings -> General -> Display Item Help
            DisplayActionHelp  = 0x136, // [bool] Character Config -> UI Settings -> General -> Display Action Help

            ClockDisplayType   = 0x153, // [enum/byte] 0 = Default, 1 = 24H, 2 = 12H 
            ClockTypeEorzea    = 0x155, // [bool]
            ClockTypeLocal     = 0x156, // [bool]
            ClockTypeServer    = 0x157, // [bool]
        }


        public T GetGameOption<T>(GameOptionKind opt) {
            var optionBase = (byte**)(Service.Framework.Address.BaseAddress + 0x2B28);
            return Marshal.PtrToStructure<T>(new IntPtr(*optionBase + 0xAAE0 + (16 * (uint)opt)));
        }

        public static HookWrapper<T> Hook<T>(string signature, T detour, int addressOffset = 0) where T : Delegate {
            var addr = Scanner.ScanText(signature);
            var h = new Hook<T>(addr + addressOffset, detour);
            var wh = new HookWrapper<T>(h);
            HookList.Add(wh);
            return wh;
        }

        public static HookWrapper<T> Hook<T>(void* address, T detour) where T : Delegate {
            var h = new Hook<T>(new IntPtr(address), detour);
            var wh = new HookWrapper<T>(h);
            HookList.Add(wh);
            return wh;
        }

        public static List<IHookWrapper> HookList = new();

        public static void OpenBrowser(string url) {
            Process.Start(new ProcessStartInfo {FileName = url, UseShellExecute = true});
        }

        public static void GenerateCallback(AtkUnitBase* unitBase, params object[] values) {
            var atkValues = (AtkValue*) Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
            if (atkValues == null) return;
            try {
                for (var i = 0; i < values.Length; i++) {
                    var v = values[i];
                    switch (v) {
                        case uint uintValue:
                            atkValues[i].Type = ValueType.UInt;
                            atkValues[i].UInt = uintValue;
                            break;
                        case int intValue:
                            atkValues[i].Type = ValueType.Int;
                            atkValues[i].Int = intValue;
                            break;
                        case float floatValue:
                            atkValues[i].Type = ValueType.Float;
                            atkValues[i].Float = floatValue;
                            break;
                        case bool boolValue:
                            atkValues[i].Type = ValueType.Bool;
                            atkValues[i].Byte = (byte) (boolValue ? 1 : 0);
                            break;
                        case string stringValue: {
                            atkValues[i].Type = ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                        default:
                            throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                    }
                }

                unitBase->FireCallback(values.Length, atkValues);
            } finally {
                for (var i = 0; i < values.Length; i++) {
                    if (atkValues[i].Type == ValueType.String) {
                        Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                    }
                }
                Marshal.FreeHGlobal(new IntPtr(atkValues));
            }
        }

    }
}
