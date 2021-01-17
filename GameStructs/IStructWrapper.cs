namespace SimpleTweaksPlugin.GameStructs {

    public interface IStructWrapper {
        public bool IsValid { get; }

        public unsafe void SetData(void* data);
    }
    
    public abstract unsafe class StructWrapper<T> : IStructWrapper where T : unmanaged {

        private ulong vtbl;
        
        private T* _data;
        public T* Data {
            get => _data;
            set {
                _data = value;
                this.vtbl = *(ulong*) _data;
            }
        }

        public virtual bool IsValid => this.vtbl == *(ulong*) Data;
        
        public void SetData(void* data) => this.Data = (T*) data;
    }
}
