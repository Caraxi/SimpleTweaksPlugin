using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace SimpleTweaksPlugin.Helper {
    public abstract class ValueParser : Attribute {

        public class HexValue : ValueParser {
            public override string GetString(Type type, object obj, MemberInfo member, ulong parentAddr) {
                return $"{obj:X}";
            }
        }

        public class FixedString : ValueParser {
            public override unsafe string GetString(Type type, object obj, MemberInfo member, ulong parentAddr) {
                var fixedBuffer = (FixedBufferAttribute) member.GetCustomAttribute(typeof(FixedBufferAttribute));
                if (fixedBuffer == null || fixedBuffer.ElementType != typeof(byte)) {
                    return $"[Not a fixed byte buffer] {obj}";
                }

                var fieldOffset = (FieldOffsetAttribute) member.GetCustomAttribute(typeof(FieldOffsetAttribute));
                if (fieldOffset == null) {
                    return $"[No FieldOffset] {obj}";
                }

                var addr = (byte*) (parentAddr + (ulong)fieldOffset.Value);
                var str = Marshal.PtrToStringAnsi(new IntPtr(addr), fixedBuffer.Length);
                return $"{str}";
            }
        }

        public abstract string GetString(Type type, object obj, MemberInfo member, ulong parentAddr);

        public virtual void ImGuiPrint(Type type, object value, MemberInfo member, ulong parentAddr) {
            ImGui.Text($"[{this.GetType().Name}]");
            ImGui.SameLine();
            ImGui.Text(this.GetString(type, value, member, parentAddr));
        }
    }
}
