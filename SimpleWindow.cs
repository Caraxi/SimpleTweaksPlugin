using Dalamud.Interface.Windowing;

namespace SimpleTweaksPlugin;

public abstract class SimpleWindow : Window {
    
    private bool isCollapsed;

    protected SimpleWindow(string name) : base(name) {
        
    }
    
    public void UnCollapseOrToggle() {
        if (isCollapsed) {
            isCollapsed = false;
            Collapsed = false;
            IsOpen = true;
        } else {
            Toggle();
        }
    }

    public override void Update() {
        isCollapsed = true;
    }

    public override void Draw() {
        isCollapsed = false;
        Collapsed = null;
    }
}