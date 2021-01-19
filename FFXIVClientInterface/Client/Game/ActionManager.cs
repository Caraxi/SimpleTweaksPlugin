using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Internal;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.Game {
    public unsafe class ActionManager : StructWrapper<ActionManagerStruct> {

        public class ActionManagerAddressResolver : BaseAddressResolver {
            public IntPtr BaseAddress;

            public IntPtr GetRecastGroup;
            public IntPtr GetGroupTimer;
            
            protected override void Setup64Bit(SigScanner sig) {
                BaseAddress = sig.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C");
                GetRecastGroup = sig.ScanText("E8 ?? ?? ?? ?? 8B D0 48 8B CD 8B F0");
                GetGroupTimer = sig.ScanText("E8 ?? ?? ?? ?? 0F 57 FF 48 85 C0");
            }
        }
        
        public ActionManager(ActionManagerAddressResolver address) {
            this.Data = (ActionManagerStruct*) address.BaseAddress;

            getRecastGroup = Marshal.GetDelegateForFunctionPointer<GetRecastGroupDelegate>(address.GetRecastGroup);
            getGroupTimer = Marshal.GetDelegateForFunctionPointer<GetGroupTimerDelegate>(address.GetGroupTimer);
        }
        
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate RecastTimer* GetGroupTimerDelegate(void* @this, int cooldownGroup);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate ulong GetRecastGroupDelegate(void* @this, int a2, uint a3);
        
        private readonly GetGroupTimerDelegate getGroupTimer;
        private readonly GetRecastGroupDelegate getRecastGroup;

        public RecastTimer* GetGroupRecastTime(int group) {
            return group < 1 ? null : getGroupTimer(this, group - 1);
        }

        public ulong GetRecastGroup(int type, uint id) {
            return getRecastGroup(this, type, id);
        }
        

        public static implicit operator ActionManagerStruct*(ActionManager module) => module.Data;
        public static explicit operator ulong(ActionManager module) => (ulong) module.Data;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public unsafe struct ActionManagerStruct {
        [FieldOffset(0x0)] public void* vtbl;
    }
    
    
    [StructLayout(LayoutKind.Explicit, Size = 0x14)]
    public unsafe struct RecastTimer {
        [FieldOffset(0x0)] public byte IsActive;
        [FieldOffset(0x4)] public uint ActionID;
        [FieldOffset(0x8)] public float Elapsed;
        [FieldOffset(0xC)] public float Total;
    }

}

