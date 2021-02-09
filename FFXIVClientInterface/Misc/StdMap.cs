using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FFXIVClientInterface.Misc
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct StdMap<T1, T2> where T1 : unmanaged, IComparable where T2 : unmanaged
    {
        public StdMap<T1, T2>* Left;
        public StdMap<T1, T2>* Parent;
        public StdMap<T1, T2>* Right;
        public bool IsBlack;
        public bool IsInitNode;
        public ushort Unknown1;
        public uint Unknown2;
        public T1 Key;
        public T2 Value;
        
        // public T2 this[T1 index]
        // {
        //     get
        //     {
        //         if (!IsInitNode)
        //             if (Key.Equals(index))
        //                 return Value;
        //         if (Parent->Key.Equals(index))
        //             return Parent->Value;
        //         return Parent->Key.CompareTo(index) < 0 ? (*Right)[index] : (*Left)[index];
        //     }
        // }

        // Cursed
        public Dictionary<T1, T2> ToDictionary()
        {
            if (!IsInitNode)
                throw new InvalidOperationException("Cannot obtain a dictionary from a child node.");
            
            var ret = new Dictionary<T1, T2>();
            
            // TODO: Remove this nonsense when the left/parent/right ambiguity is solved
            var visited = new HashSet<ulong>();
            var thisAddr = ThisAddr();
            visited.Add(thisAddr);
            
            // The initial node does not have key/value
            Left->ObtainChildren(thisAddr, ref visited, ref ret);
            Parent->ObtainChildren(thisAddr, ref visited, ref ret);
            Right->ObtainChildren(thisAddr, ref visited, ref ret);

            return ret;
        }

        private ulong ThisAddr()
        {
            fixed (void* ptr = &this)
                return (ulong) ptr;
        }

        private void ObtainChildren(ulong parentAddr, ref HashSet<ulong> visited, ref Dictionary<T1, T2> results)
        {
            var thisAddr = ThisAddr();
            if (visited.Contains(thisAddr))
                return;
            results[Key] = Value;
            visited.Add(thisAddr);

            if ((ulong) Left != parentAddr)
                Left->ObtainChildren(thisAddr, ref visited, ref results);
            if ((ulong) Right != parentAddr)
                Right->ObtainChildren(thisAddr, ref visited, ref results);
        }
    }
}