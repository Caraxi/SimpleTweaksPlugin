using System;
using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class AddonEventAttribute : EventAttribute {
    public AddonEvent Event { get; }
    public string[] AddonNames { get; }

    public AddonEventAttribute(AddonEvent @event, params string[] addonNames) {
        Event = @event;
        AddonNames = addonNames;
    }
}

#region Aliases
public class AddonPreSetupAttribute : AddonEventAttribute {
    public AddonPreSetupAttribute(params string[] addonNames) : base(AddonEvent.PreSetup, addonNames) { }
}

public class AddonPostSetupAttribute : AddonEventAttribute {
    public AddonPostSetupAttribute(params string[] addonNames) : base(AddonEvent.PostSetup, addonNames) { }
}

public class AddonFinalizeAttribute : AddonEventAttribute {
    public AddonFinalizeAttribute(params string[] addonNames) : base(AddonEvent.PreFinalize, addonNames) { }
}

public class AddonPreUpdateAttribute : AddonEventAttribute {
    public AddonPreUpdateAttribute(params string[] addonNames) : base(AddonEvent.PreUpdate, addonNames) { }
}

public class AddonPostUpdateAttribute : AddonEventAttribute {
    public AddonPostUpdateAttribute(params string[] addonNames) : base(AddonEvent.PostUpdate, addonNames) { }
}

public class AddonPreRequestedUpdateAttribute : AddonEventAttribute {
    public AddonPreRequestedUpdateAttribute(params string[] addonNames) : base(AddonEvent.PreRequestedUpdate, addonNames) { }
}

public class AddonPostRequestedUpdateAttribute : AddonEventAttribute {
    public AddonPostRequestedUpdateAttribute(params string[] addonNames) : base(AddonEvent.PostRequestedUpdate, addonNames) { }
}

public class AddonPreDrawAttribute : AddonEventAttribute {
    public AddonPreDrawAttribute(params string[] addonNames) : base(AddonEvent.PreDraw, addonNames) { }
}

public class AddonPostDrawAttribute : AddonEventAttribute {
    public AddonPostDrawAttribute(params string[] addonNames) : base(AddonEvent.PostDraw, addonNames) { }
}

public class AddonPreRefreshAttribute : AddonEventAttribute {
    public AddonPreRefreshAttribute(params string[] addonNames) : base(AddonEvent.PreRefresh, addonNames) { }
}

public class AddonPostRefreshAttribute : AddonEventAttribute {
    public AddonPostRefreshAttribute(params string[] addonNames) : base(AddonEvent.PostRefresh, addonNames) { }
}

#endregion
