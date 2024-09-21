using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.AtkArray.NumberArrays;


public interface INumberArrayStruct;

public abstract unsafe class NumberArray<T>(int numberArrayIndex, string name) : NumberArray(numberArrayIndex, name) where T : unmanaged, INumberArrayStruct {
    public T* Data => (T*)BaseData->IntArray;
}

public abstract unsafe class NumberArray(int numberArrayIndex, string name = null) {

    public int NumberArrayIndex { get; } = numberArrayIndex;
    public string Name { get; } = name ?? "Number Array";


    protected NumberArrayData* BaseData => RaptureAtkModule.Instance()->GetNumberArrayData(NumberArrayIndex);

    public bool TryGetValue(uint index, out int value) {
        value = 0;
        var d = BaseData;
        if (d == null) return false;
        if (index >= d->Size) return false;

        value = d->IntArray[index];
        return true;
    }
    
    public bool TryGetValue(uint index, out uint value) {
        value = 0;
        var d = BaseData;
        if (d == null) return false;
        if (index >= d->Size) return false;

        value = (uint)d->IntArray[index];
        return true;
    }
    
    

    public static T? Get<T>() where T : NumberArray => ArrayHelper.GetNumberArray<T>();
}
