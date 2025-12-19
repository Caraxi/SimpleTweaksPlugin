using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SimpleTweaksPlugin.TweakSystem;
using Task = System.Threading.Tasks.Task;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Remove Client Limit")]
[TweakDescription("Allows opening more than the standard two FFXIV clients.")]
[TweakReleaseVersion("1.9.5.0")]
public class RemoveClientLimit : Tweak {
    public override bool Experimental => true;

    public static int MutexCount() {
        var c = 0;
        for (var i = 0; i < 2; i++) {
            var mutexName = $"Global\\6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game{i:00}";
            if (Mutex.TryOpenExisting(mutexName, out _)) {
                c++;
            }
        }

        return c;
    }

    protected override void Enable() {
        Task.Run(() => {
            if (MutexCount() > 0) {
                foreach (var hi in HandleUtil.GetHandles().Where(t => t.Type == HandleUtil.HandleType.Mutant)) {
                    if (!string.IsNullOrEmpty(hi.Name) && (hi.Name.EndsWith("6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game00") || hi.Name.EndsWith("6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game01"))) {
                        SimpleLog.Log($"Close Mutex: {hi.Name}");
                        hi.Close();
                    }
                }
            }
        });
    }

    private static class HandleUtil {
        public enum HandleType {
            Other,
            Mutant,
        }

        public class HandleInfo {
            public int ProcessId { get; }
            public ushort Handle { get; }
            public int GrantedAccess { get; }
            public byte RawType { get; }

            public HandleInfo(int processId, ushort handle, int grantedAccess, byte rawType) {
                ProcessId = processId;
                Handle = handle;
                GrantedAccess = grantedAccess;
                RawType = rawType;
            }

            public void Close() {
                NativeMethods.CloseHandle(Handle);
            }

            private static readonly Dictionary<byte, string> RawTypeMap = new();

            private string? name, typeStr;
            private HandleType? type;

            public string? Name {
                get {
                    if (name == null) InitTypeAndName();
                    return name;
                }
            }

            public HandleType? Type {
                get {
                    if (typeStr == null) InitType();
                    return type;
                }
            }

            private void InitType() {
                if (RawTypeMap.TryGetValue(RawType, out var value)) {
                    typeStr = value;
                    type = HandleTypeFromString(typeStr);
                } else
                    InitTypeAndName();
            }

            private bool typeAndNameAttempted;

            private void InitTypeAndName() {
                if (typeAndNameAttempted)
                    return;
                typeAndNameAttempted = true;

                IntPtr sourceProcessHandle = IntPtr.Zero;
                IntPtr handleDuplicate = IntPtr.Zero;
                try {
                    sourceProcessHandle = NativeMethods.OpenProcess(0x40 /* dup_handle */, true, ProcessId);

                    // To read info about a handle owned by another process we must duplicate it into ours
                    // For simplicity, current process handles will also get duplicated; remember that process handles cannot be compared for equality
                    if (!NativeMethods.DuplicateHandle(sourceProcessHandle, (IntPtr)Handle, NativeMethods.GetCurrentProcess(), out handleDuplicate, 0, false, 2 /* same_access */))
                        return;

                    // Query the object type
                    if (RawTypeMap.TryGetValue(RawType, out var value))
                        typeStr = value;
                    else {
                        int length;
                        NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectTypeInformation, IntPtr.Zero, 0, out length);
                        IntPtr ptr = IntPtr.Zero;
                        try {
                            ptr = Marshal.AllocHGlobal(length);
                            if (NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectTypeInformation, ptr, length, out length) != NtStatus.StatusSuccess)
                                return;
                            typeStr = Marshal.PtrToStringUni((IntPtr)((long)ptr + 0x58 + 2 * IntPtr.Size));
                            RawTypeMap[RawType] = typeStr;
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }

                    type = HandleTypeFromString(typeStr ?? throw new Exception("Invalid Type String"));

                    // Query the object name
                    if (typeStr != null && GrantedAccess != 0x0012019f && GrantedAccess != 0x00120189 && GrantedAccess != 0x120089) // don't query some objects that could get stuck
                    {
                        int length;
                        NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectNameInformation, IntPtr.Zero, 0, out length);
                        IntPtr ptr = IntPtr.Zero;
                        try {
                            ptr = Marshal.AllocHGlobal(length);
                            if (NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectNameInformation, ptr, length, out length) != NtStatus.StatusSuccess)
                                return;
                            name = Marshal.PtrToStringUni((IntPtr)((long)ptr + 2 * IntPtr.Size));
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                } finally {
                    NativeMethods.CloseHandle(sourceProcessHandle);
                    if (handleDuplicate != IntPtr.Zero)
                        NativeMethods.CloseHandle(handleDuplicate);
                }
            }

            private static HandleType HandleTypeFromString(string typeStr) {
                switch (typeStr) {
                    case "Mutant": return HandleType.Mutant;
                    default: return HandleType.Other;
                }
            }
        }

        public static IEnumerable<HandleInfo> GetHandles() {
            // Attempt to retrieve the handle information
            int length = 0x10000;
            IntPtr ptr = IntPtr.Zero;
            try {
                while (true) {
                    ptr = Marshal.AllocHGlobal(length);
                    int wantedLength;
                    var result = NativeMethods.NtQuerySystemInformation(SystemInformationClass.SystemHandleInformation, ptr, length, out wantedLength);
                    if (result == NtStatus.StatusInfoLengthMismatch) {
                        length = Math.Max(length, wantedLength);
                        Marshal.FreeHGlobal(ptr);
                        ptr = IntPtr.Zero;
                    } else if (result == NtStatus.StatusSuccess)
                        break;
                    else
                        throw new Exception("Failed to retrieve system handle information.");
                }

                int handleCount = IntPtr.Size == 4 ? Marshal.ReadInt32(ptr) : (int)Marshal.ReadInt64(ptr);
                int offset = IntPtr.Size;
                int size = Marshal.SizeOf(typeof(SystemHandleEntry));
                for (int i = 0; i < handleCount; i++) {
                    unchecked {
                        var struc = (SystemHandleEntry)Marshal.PtrToStructure((IntPtr)((long)ptr + offset), typeof(SystemHandleEntry))!;
                        yield return new HandleInfo(struc.OwnerProcessId, struc.Handle, struc.GrantedAccess, struc.ObjectTypeNumber);
                        offset += size;
                    }
                }
            } finally {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemHandleEntry {
            public int OwnerProcessId;
            public byte ObjectTypeNumber;
            public byte Flags;
            public ushort Handle;
            public IntPtr Object;
            public int GrantedAccess;
        }

        private enum NtStatus {
            StatusSuccess = 0x00000000,
            StatusInfoLengthMismatch = unchecked((int)0xC0000004L)
        }

        private enum SystemInformationClass {
            SystemHandleInformation = 16,
        }

        private enum ObjectInformationClass {
            ObjectNameInformation = 1,
            ObjectTypeInformation = 2,
        }

        private static class NativeMethods {
            [DllImport("ntdll.dll")] internal static extern NtStatus NtQuerySystemInformation([In] SystemInformationClass systemInformationClass, [In] IntPtr systemInformation, [In] int systemInformationLength, [Out] out int returnLength);

            [DllImport("ntdll.dll")] internal static extern NtStatus NtQueryObject([In] IntPtr handle, [In] ObjectInformationClass objectInformationClass, [In] IntPtr objectInformation, [In] int objectInformationLength, [Out] out int returnLength);

            [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess([In] int dwDesiredAccess, [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, [In] int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle([In] IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DuplicateHandle([In] IntPtr hSourceProcessHandle, [In] IntPtr hSourceHandle, [In] IntPtr hTargetProcessHandle, [Out] out IntPtr lpTargetHandle, [In] int dwDesiredAccess, [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, [In] int dwOptions);
        }
    }
}
