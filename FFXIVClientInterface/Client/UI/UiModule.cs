using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientInterface.Client.UI.Agent;
using FFXIVClientInterface.Client.UI.Misc;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI {
    
    public unsafe class UiModule : StructWrapper<UiModuleStruct>, IDisposable {
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
        public RaptureMacroModule RaptureMacroModule => GetModuleSingleton<RaptureMacroModule>(Data->vtbl->GetRaptureMacroModule);
        public ItemOrderModule ItemOrderModule => GetModuleSingleton<ItemOrderModule>(Data->vtbl->GetItemOrderModule);
        public ItemFinderModule ItemFinderModule => GetModuleSingleton<ItemFinderModule>(Data->vtbl->GetItemFinderModule);
        public RaptureShellModule RaptureShellModule => GetModuleSingleton<RaptureShellModule>(Data->vtbl->GetRaptureShellModule);
        public AgentModule AgentModule => GetModuleSingleton<AgentModule>(Data->vtbl->GetAgentModule);
        
        
        public override void Dispose() {
            foreach (var m in structWrappers) {
                m.Value.Dispose();
            }
        }
    }
    
    
    [StructLayout(LayoutKind.Explicit, Size = 0xDE750)]
    public unsafe struct UiModuleStruct {
        [FieldOffset(0x00000)] public VirtualTable.UiModule* vtbl;
    }
}
