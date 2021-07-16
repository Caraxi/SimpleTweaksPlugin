using System;
using System.Runtime.InteropServices;

namespace FFXIVClientInterface.VirtualTable {

    [StructLayout(LayoutKind.Sequential, Size = 0x658)]
    public unsafe struct UiModule {
        public void* vf0; // dtor
        public void* vf1;
        public void* vf2;
        public void* vf3;
        public void* vf4;
        public void* vf5;
        public void* vf6;
        public IntPtr GetRaptureAtkModule;
        public void* vf8;
        public IntPtr GetRaptureShellModule;
        public void* vf10;
        public void* vf11;
        public IntPtr GetRaptureMacroModule;
        public IntPtr GetRaptureHotbarModule;
        public IntPtr GetRaptureGearsetModule;
        public void* vf15;
        public IntPtr GetItemOrderModule;
        public IntPtr GetItemFinderModule;
        public IntPtr GetConfigModule;
        public void* vf19;
        public void* vf20;
        public void* vf21;
        public void* vf22;
        public void* vf23;
        public void* vf24;
        public void* vf25;
        public void* vf26;
        public void* vf27;
        public void* vf28;
        public void* vf29;
        public void* vf30;
        public void* vf31;
        public void* vf32;
        public void* vf33;
        public IntPtr GetAgentModule;
    }
}
