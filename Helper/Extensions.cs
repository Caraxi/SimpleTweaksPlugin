using System;
namespace SimpleTweaksPlugin.Helper {
    public static class Extensions {
        public static string GetDescription(this Enum @enum) {
            var eType = @enum.GetType();
            var member = eType.GetMember(@enum.ToString());
            if ((member.Length <= 0)) return @enum.ToString();
            var attribs = member[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            return attribs.Length > 0 ? ((System.ComponentModel.DescriptionAttribute)attribs[0]).Description : @enum.ToString();
        }
    }
}
