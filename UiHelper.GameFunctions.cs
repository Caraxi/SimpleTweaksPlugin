
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin
{
    public unsafe partial class UiHelper
    {
        
        private delegate void AtkTextNodeSetText(AtkTextNode* textNode, void* a2);
        private static AtkTextNodeSetText atkTextNodeSetText;


        public static bool Ready = false;
        public static void Setup(SigScanner scanner) {
            atkTextNodeSetText = Marshal.GetDelegateForFunctionPointer<AtkTextNodeSetText>(scanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC"));

            Ready = true;
        }
    }
}
