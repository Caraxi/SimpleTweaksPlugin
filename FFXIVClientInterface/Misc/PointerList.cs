namespace FFXIVClientInterface.Misc {
    public unsafe class PointerList<T> where T : unmanaged {
        public PointerList(ulong* address) {
            this.BaseAddress = address;
        }
        public ulong* BaseAddress { get; }

        public T* this[int i] => (T*) BaseAddress[i];
    }
}
