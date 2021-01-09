using System;
using System.Runtime.InteropServices;
using Common = SimpleTweaksPlugin.Helper.Common;

namespace SimpleTweaksPlugin.GameStructs {
    public unsafe class ActionManager {
        public readonly ActionManagerStruct* Data;
    
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate Cooldown* GetActionCooldownSlotDelegate(void* @this, int cooldownGroup);

        private delegate ulong GetCooldownGroupDelegate(void* @this, int a2, uint a3);
        
        
        private GetActionCooldownSlotDelegate getActionCooldownSlot;
        private GetCooldownGroupDelegate getCooldownGroup;

        public ActionManager(IntPtr address) : this((ActionManagerStruct*) address) { }
        public ActionManager(ActionManagerStruct* @struct) {
            Data = @struct;

            getActionCooldownSlot = Marshal.GetDelegateForFunctionPointer<GetActionCooldownSlotDelegate>(
                Common.Scanner.ScanText("E8 ?? ?? ?? ?? 0F 57 FF 48 85 C0"));

            getCooldownGroup = Marshal.GetDelegateForFunctionPointer<GetCooldownGroupDelegate>(
                Common.Scanner.ScanText("E8 ?? ?? ?? ?? 8B D0 48 8B CD 8B F0"));
        }
        
        public static implicit operator ActionManagerStruct*(ActionManager @this) => @this.Data;
        public static explicit operator ulong(ActionManager @this) => (ulong)@this.Data;

        public Cooldown* GetActionCooldownSlot(int cooldownGroup) {
            return cooldownGroup < 1 ? null : getActionCooldownSlot(this, cooldownGroup - 1);
        }

        public ulong GetCooldownGroup(int type, uint id) {
            return getCooldownGroup(this, type, id);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB8)]
    public unsafe struct ActionManagerStruct {
        [FieldOffset(0x00)] public void* @base;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x14)]
    public unsafe struct Cooldown {
        [FieldOffset(0x0)] public byte IsCooldown;
        [FieldOffset(0x4)] public uint ActionID;
        [FieldOffset(0x8)] public float CooldownElapsed;
        [FieldOffset(0xC)] public float CooldownTotal;
    }
}