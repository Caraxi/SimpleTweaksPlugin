using System;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game;
using FFXIVClientInterface.Client.Game;
using FFXIVClientInterface.Client.Game.Character;
using FFXIVClientInterface.Client.UI;

namespace FFXIVClientInterface {
    public unsafe class ClientInterface : IDisposable {
        private bool ready;
        internal static DataManager DataManager;
        internal static SigScanner SigScanner;
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UiModuleStruct* GetUiModuleDelegate();
        private GetUiModuleDelegate getUiModule;
        
        private UiModule uiModule;
        public UiModule UiModule {
            get {
                if (!ready) return null;
                if (uiModule != null && uiModule.IsValid) return uiModule;
                var fetchedUiModule = getUiModule();
                if (fetchedUiModule != null) {
                    uiModule = new UiModule() { Data = fetchedUiModule };
                }
                return uiModule;
            }
        }
        
        private ActionManager actionManager;
        public ActionManager ActionManager {
            get {
                if (actionManager != null && actionManager.IsValid) return actionManager;
                var address = new ActionManager.ActionManagerAddressResolver();
                address.Setup(SigScanner);
                actionManager = new ActionManager(address);
                return actionManager;
            }
        }

        private CharacterManager characterManager;

        public CharacterManager CharacterManager {
            get {
                if (characterManager != null) return characterManager;
                var address = new CharacterManagerAddressResolver();
                address.Setup(SigScanner);
                characterManager = new CharacterManager(address) {
                    Data = (CharacterManagerStruct*) address.BaseAddress
                };
                return characterManager;
            }
        }

        public ClientInterface(SigScanner scanner, DataManager dataManager) {
            DataManager = dataManager;
            SigScanner = scanner;
            this.getUiModule = Marshal.GetDelegateForFunctionPointer<GetUiModuleDelegate>(scanner.ScanText("E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 52 40 80 88 ?? ?? ?? ?? 01 E9"));
            ready = true;
        }

        public void Dispose() {
            ready = false;
            uiModule?.Dispose();
        }
    }
}
