namespace SimpleTweaksPlugin.AtkArray.StringArrays;

public abstract class StringArray(int stringArrayIndex) {
    public int StringArrayIndex { get; } = stringArrayIndex;
    
    
    public static T? Get<T>() where T : StringArray => ArrayHelper.GetStringArray<T>();
}
