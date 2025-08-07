using System;
using System.IO;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace SimpleTweaksPlugin.Utility;

public class GraphicFont {
    public static GraphicFont FontIcons { get; } = new();

    public string Identifier = "gftd";
    public string Version = "0100";
    public GraphicFontIcon[] Icons = [];

    private static uint _padIconMode;
    private static readonly string[] TexturePath = ["common/font/fonticon_xinput.tex", "common/font/fonticon_ps3.tex", "common/font/fonticon_ps4.tex", "common/font/fonticon_ps5.tex", "common/font/fonticon_lys.tex"];

    public enum IconStyle {
        Xbox360,
        PS3,
        PS4,
        PS5,
        XboxSeries
    }

    static GraphicFont() {
        var gfd = Service.Data.GetFile("common/font/gfdata.gfd");
        if (gfd != null) {
            FontIcons = new GraphicFont(gfd.Data);
        }
    }

    public class GraphicFontIcon {
        public ushort ID;
        public Vector2 Position;
        public Vector2 Size;

        public byte[] Remainder;

        public GraphicFontIcon(BinaryReader reader) {
            ID = reader.ReadUInt16();
            Position = new Vector2(reader.ReadUInt16(), reader.ReadUInt16());
            Size = new Vector2(reader.ReadUInt16(), reader.ReadUInt16());
            Remainder = reader.ReadBytes(6);
        }

        public bool IsValid() {
            return Size is { X: >= 1, Y: >= 1 };
        }

        public void DrawScaled(Vector2 scale, bool highQuality = true, IconStyle? style = null) {
            Draw(Size * (highQuality ? 2 : 1) * scale, highQuality, style);
        }

        public void Draw(bool highQuality, IconStyle? style = null) => Draw(null, highQuality, style);

        public void Draw(Vector2? drawSize = null, bool highQuality = true, IconStyle? style = null) {
            if (!IsValid()) return;
            var scale = new Vector2(highQuality ? 2 : 1);
            var size = Size * scale;
            var textureWrap = Service.TextureProvider.GetFromGame(TexturePath[(byte?)style ?? _padIconMode])
                .GetWrapOrEmpty();

            var textureSize = new Vector2(textureWrap.Width, textureWrap.Height);
            var basePosition = new Vector2(0, highQuality ? 1 / 3f : 0);

            var uv0 = basePosition + (Position * scale) / textureSize;
            var uv1 = uv0 + size / textureSize;

            ImGui.Image(textureWrap.Handle, drawSize ?? size, uv0, uv1);
        }
    }

    public GraphicFont() { }

    public GraphicFont(byte[] data) {
        if (data.Length < 16) {
            throw new Exception("Missing Header");
        }

        using var mStream = new MemoryStream(data);
        using var reader = new BinaryReader(mStream);

        Identifier = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Identifier != "gftd") {
            throw new Exception("Not a GFTD");
        }

        Version = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Version != "0100") {
            throw new Exception($"GFTD Version {Version} not supported.");
        }

        Icons = new GraphicFontIcon[reader.ReadInt32()];
        reader.ReadUInt32();

        if (data.Length != 16 * (Icons.Length + 1)) {
            throw new Exception("incorrect data size");
        }

        for (var i = 0; i < Icons.Length; i++) {
            Icons[i] = new GraphicFontIcon(reader);
        }
    }
}
