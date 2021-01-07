using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Plugin;
using ImGuiScene;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Helper {
    public class IconManager : IDisposable {

        private readonly DalamudPluginInterface pluginInterface;
        private bool disposed;
        private readonly Dictionary<ushort, TextureWrap> iconTextures = new Dictionary<ushort, TextureWrap>();
        private readonly Dictionary<uint, ushort> actionCustomIcons = new Dictionary<uint, ushort>() {
            
        };

        public IconManager(DalamudPluginInterface pluginInterface) {
            this.pluginInterface = pluginInterface;
        }

        public void Dispose() {
            disposed = true;
            var c = 0;
            PluginLog.Log("Disposing icon textures");
            foreach (var texture in iconTextures.Values.Where(texture => texture != null)) {
                c++;
                texture.Dispose();
            }

            PluginLog.Log($"Disposed {c} icon textures.");
            iconTextures.Clear();
        }
        
        private void LoadIconTexture(ushort iconId) {
            Task.Run(() => {
                try {
                    var iconTex = pluginInterface.Data.GetIcon(iconId);

                    var tex = pluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);

                    if (tex.ImGuiHandle != IntPtr.Zero) {
                        this.iconTextures[iconId] = tex;
                    } else {
                        tex.Dispose();
                    }
                } catch (Exception ex) {
                    PluginLog.LogError($"Failed loading texture for icon {iconId} - {ex.Message}");
                }
            });
        }

        public TextureWrap GetActionIcon(Action action) {
            return GetIconTexture(actionCustomIcons.ContainsKey(action.RowId) ? actionCustomIcons[action.RowId] : action.Icon);
        }

        public ushort GetActionIconId(Action action) {
            return actionCustomIcons.ContainsKey(action.RowId) ? actionCustomIcons[action.RowId] : action.Icon;
        }

        public TextureWrap GetIconTexture(ushort iconId) {
            if (this.disposed) return null;
            if (this.iconTextures.ContainsKey(iconId)) return this.iconTextures[iconId];
            this.iconTextures.Add(iconId, null);
            LoadIconTexture(iconId);
            return this.iconTextures[iconId];
        }
    }
}
