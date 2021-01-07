using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SimpleTweaksPlugin.GameStructs.Client.UI.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs.Client.UI.VTable;

namespace SimpleTweaksPlugin.GameStructs.Client.UI {

    public unsafe class UiModule {
        
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate RaptureHotbarModuleStruct* GetRaptureHotbarModuleDelegate(UiModuleStruct* uiModule);
        private GetRaptureHotbarModuleDelegate getRaptureHotbarModule;
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate RaptureGearsetModuleStruct* GetRaptureGearsetModuleDelegate(UiModuleStruct* uiModule);
        private GetRaptureGearsetModuleDelegate getRaptureGearsetModule;
        
        public UiModuleStruct* Data { get; }
        private readonly ulong vtblAddr;
        public UiModule(UiModuleStruct* data) {
            Data = data;
            vtblAddr = (ulong) data;
            
            getRaptureHotbarModule = Marshal.GetDelegateForFunctionPointer<GetRaptureHotbarModuleDelegate>(Data->vtbl->GetRaptureHotbarModule);
            getRaptureGearsetModule = Marshal.GetDelegateForFunctionPointer<GetRaptureGearsetModuleDelegate>(Data->vtbl->GetRaptureGearsetModule);
        }
        public bool IsValid => vtblAddr == (ulong) Data->vtbl;

        public static implicit operator UiModuleStruct*(UiModule uiModule) => uiModule.Data;
        public static explicit operator UiModule(UiModuleStruct* uiModuleStruct) => new UiModule(uiModuleStruct);
        
        
        private RaptureHotbarModule raptureHotbarModule;
        public RaptureHotbarModule RaptureHotbarModule {
            get {
                if (raptureHotbarModule == null || !raptureHotbarModule.IsValid) {
                    var raptureHotbarModuleStruct = getRaptureHotbarModule(this);
                    if (raptureHotbarModuleStruct != null) this.raptureHotbarModule = new RaptureHotbarModule(raptureHotbarModuleStruct);
                }
                return raptureHotbarModule;
            }
        }
        
        private RaptureGearsetModule raptureGearsetModule;
        public RaptureGearsetModule RaptureGearsetModule {
            get {
                if (raptureGearsetModule == null || !raptureGearsetModule.IsValid) {
                    var raptureGearsetModuleStruct = getRaptureGearsetModule(this);
                    if (raptureGearsetModuleStruct != null) this.raptureGearsetModule = new RaptureGearsetModule(raptureGearsetModuleStruct);
                }
                return raptureGearsetModule;
            }
        }
        
        
    }
    
    
    [StructLayout(LayoutKind.Explicit, Size = 0xDE750)]
    public unsafe struct UiModuleStruct {
        [FieldOffset(0x00000)] public UiModuleVTable* vtbl;
    }
}
