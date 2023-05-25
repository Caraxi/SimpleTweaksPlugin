using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

// Original:
//      https://git.anna.lgbt/ascclemens/XivCommon/src/branch/main/XivCommon/Functions/FriendList/FriendList.cs


namespace SimpleTweaksPlugin.Utility; 

/// <summary>
/// The class containing friend list functionality
/// </summary>
public static class FriendList {
    // Updated: 5.58-HF1
    private const int InfoOffset = 0x28;
    private const int LengthOffset = 0x10;
    private const int ListOffset = 0x98;

    /// <summary>
    /// <para>
    /// A live list of the currently-logged-in player's friends.
    /// </para>
    /// <para>
    /// The list is empty if not logged in.
    /// </para>
    /// </summary>
    public static unsafe IList<FriendListEntry> List {
        get {
            var friendListAgent = (IntPtr) Framework.Instance()
                        ->GetUiModule()
                    ->GetAgentModule()
                ->GetAgentByInternalId(AgentId.SocialFriendList);
            if (friendListAgent == IntPtr.Zero) {
                return Array.Empty<FriendListEntry>();
            }

            var info = *(IntPtr*) (friendListAgent + InfoOffset);
            if (info == IntPtr.Zero) {
                return Array.Empty<FriendListEntry>();
            }

            var length = *(ushort*) (info + LengthOffset);
            if (length == 0) {
                return Array.Empty<FriendListEntry>();
            }

            var list = *(IntPtr*) (info + ListOffset);
            if (list == IntPtr.Zero) {
                return Array.Empty<FriendListEntry>();
            }

            var entries = new List<FriendListEntry>(length);
            for (var i = 0; i < length; i++) {
                var entry = *(FriendListEntry*) (list + i * FriendListEntry.Size);
                entries.Add(entry);
            }

            return entries;
        }
    }

    static FriendList() {
    }
        
        
    /// <summary>
    /// An entry in a player's friend list.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct FriendListEntry {
        internal const int Size = 104;
        
        /// <summary>
        /// The content ID of the friend.
        /// </summary>
        [FieldOffset(0x00)]
        public readonly ulong ContentId;
        
        /// <summary>
        /// The current world of the friend.
        /// </summary>
        [FieldOffset(0x1E)]
        public readonly ushort CurrentWorld;
        
        /// <summary>
        /// The home world of the friend.
        /// </summary>
        [FieldOffset(0x20)]
        public readonly ushort HomeWorld;
        
        /// <summary>
        /// The job the friend is currently on.
        /// </summary>
        [FieldOffset(0x29)]
        public readonly byte Job;
        
        /// <summary>
        /// The friend's raw SeString name. See <see cref="Name"/>.
        /// </summary>
        [FieldOffset(0x2A)]
        public fixed byte RawName[32];
        
        /// <summary>
        /// The friend's raw SeString free company tag. See <see cref="FreeCompany"/>.
        /// </summary>
        [FieldOffset(0x4A)]
        public fixed byte RawFreeCompany[5];
        
        /// <summary>
        /// The friend's name.
        /// </summary>
        public SeString Name {
            get {
                fixed (byte* ptr = this.RawName) {
                    return MemoryHelper.ReadSeStringNullTerminated((IntPtr) ptr);
                }
            }
        }
        
        /// <summary>
        /// The friend's free company tag.
        /// </summary>
        public SeString FreeCompany {
            get {
                fixed (byte* ptr = this.RawFreeCompany) {
                    return MemoryHelper.ReadSeStringNullTerminated((IntPtr) ptr);
                }
            }
        }
    }
        
}