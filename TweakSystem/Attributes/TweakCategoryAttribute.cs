using System;
using System.Collections.Generic;
using System.Linq;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.TweakSystem;


public enum TweakCategory {
    Other,
    UI,
    Chat,
    Command,
    Tooltip,
    Joke,
    Disabled,
    Experimental
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public class TweakCategoryAttribute : Attribute {
    
    public static Dictionary<string, string> CategoryDescriptions { get; } = new() {
        ["Other"] = "Tweaks that haven't been given a specific category.",
        ["UI"] = "Tweaks that change or interact with the game's UI in some way.",
        ["Chat"] = "Tweaks that interact with the game's Chat.",
        ["Command"] = "Tweaks that add or adjust commands.",
        ["Tooltip"] = "Tweaks that modify the game's tooltips.",
        ["Joke"] = "Tweaks that serve no real purpose.",
        ["Disabled"] = "Tweaks that have been disabled or made obsolete by a game update or other issue.",
        ["Experimental"] = "Tweaks that have a higher than average chance of causing the game to break in some way.",
    };

    public string[] Categories;
    public TweakCategoryAttribute(params string[] categories) {
        Categories = categories;
    }

    public TweakCategoryAttribute(params TweakCategory[] categories) {
        Categories = categories.Select(c => c.GetDescription() ?? $"{c}").ToArray();
    }
}
