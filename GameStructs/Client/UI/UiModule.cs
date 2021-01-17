using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SimpleTweaksPlugin.GameStructs.Client.UI.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs.Client.UI.VTable;

namespace SimpleTweaksPlugin.GameStructs.Client.UI {
    
    public unsafe class UiModule : StructWrapper<UiModuleStruct> {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void* GetModuleDelegate(UiModuleStruct* @this);
        
        public static implicit operator UiModuleStruct*(UiModule module) => module.Data;
        public static explicit operator ulong(UiModule module) => (ulong) module.Data;
        public static explicit operator UiModule(UiModuleStruct* @struct) => new() { Data = @struct };
        public static explicit operator UiModule(void* ptr) => new() { Data = (UiModuleStruct*) ptr};
        

        private readonly Dictionary<Type, IStructWrapper> structWrappers = new();
        
        private T GetModuleSingleton<T>(IntPtr getterAddr) where T : IStructWrapper, new() {
            if (structWrappers.ContainsKey(typeof(T))) {
                if (structWrappers[typeof(T)].IsValid) {
                    return (T) structWrappers[typeof(T)];
                }
            }
            var getter = Marshal.GetDelegateForFunctionPointer<GetModuleDelegate>(getterAddr);
            var module = getter(this);
            if (module == null) return default;
            var wrapper = new T();
            wrapper.SetData(module);
            structWrappers.Add(typeof(T), wrapper);
            return wrapper;
        }
        
        public RaptureHotbarModule RaptureHotbarModule => GetModuleSingleton<RaptureHotbarModule>(Data->vtbl->GetRaptureHotbarModule);
        public RaptureGearsetModule RaptureGearsetModule => GetModuleSingleton<RaptureGearsetModule>(Data->vtbl->GetRaptureGearsetModule);
        public ItemOrderModule ItemOrderModule => GetModuleSingleton<ItemOrderModule>(Data->vtbl->GetItemOrderModule);
        public ItemFinderModule ItemFinderModule => GetModuleSingleton<ItemFinderModule>(Data->vtbl->GetItemFinderModule);
    }
    
    
    [StructLayout(LayoutKind.Explicit, Size = 0xDE750)]
    public unsafe struct UiModuleStruct {
        [FieldOffset(0x00000)] public UiModuleVTable* vtbl;
    }
}
