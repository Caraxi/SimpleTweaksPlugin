using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Libc;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin {
    public static class External {
        public static DalamudPluginInterface PluginInterface;
        public static BuddyList Buddies;
        public static ChatGui Chat;
        public static ChatHandlers ChatHandlers;
        public static ClientState ClientState;
        public static CommandManager Commands;
        public static Condition Condition;
        public static DataManager Data;
        public static FateTable Fates;
        public static FlyTextGui FlyText;
        public static Framework Framework;
        public static GameGui GameGui;
        public static GameNetwork GameNetwork;
        public static JobGauges Gauges;
        public static KeyState KeyState;
        public static LibcFunction LibcFunction;
        public static ObjectTable Objects;
        public static PartyFinderGui PartyFinderGui;
        public static PartyList Party;
        public static SeStringManager SeStringManager;
        public static SigScanner SigScanner;
        public static TargetManager Targets;
        public static ToastGui Toasts;
    }
}
