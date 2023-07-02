using System;
using System.IO;
using Dalamud.Game.Text.SeStringHandling;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace SimpleTweaksPlugin.ExtraPayloads;


public abstract class CustomPayload : Payload {
    public RawPayload AsRaw() {
        return new RawPayload(Encode());
    }
}

public abstract class AbstractColorPayload : CustomPayload {
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    
    protected override byte[] EncodeImpl() {
        return new byte[] { START_BYTE, ChunkType, 0x05, 0xF6, Red, Green, Blue, END_BYTE };
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        
    }
    public override PayloadType Type => PayloadType.Unknown;

    public abstract byte ChunkType { get; }

}

public abstract class AbstractColorEndPayload : CustomPayload {
    protected override byte[] EncodeImpl() {
        return new byte[] { START_BYTE, ChunkType, 0x02, 0xEC, END_BYTE };
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        
    }
    public override PayloadType Type => PayloadType.Unknown;

    public abstract byte ChunkType { get; }
}


public class ColorPayload : AbstractColorPayload {
    public override byte ChunkType => 0x13;

    public ColorPayload(Vector3 color) {
        Red = Math.Max((byte) 1, (byte) (color.X * 255f));
        Green = Math.Max((byte) 1, (byte) (color.Y * 255f));;
        Blue = Math.Max((byte) 1, (byte) (color.Z * 255f));;
    }
    
    public ColorPayload(byte r, byte g, byte b) {
        Red = r;
        Green = g;
        Blue = b;
    }
}

public class ColorEndPayload : AbstractColorEndPayload {
    public override byte ChunkType => 0x13;
}



public class GlowPayload : AbstractColorPayload {
    public override byte ChunkType => 0x14;
    
    public GlowPayload(Vector3 color) {
        Red = Math.Max((byte) 1, (byte) (color.X * 255f));
        Green = Math.Max((byte) 1, (byte) (color.Y * 255f));;
        Blue = Math.Max((byte) 1, (byte) (color.Z * 255f));;
    }
    
    public GlowPayload(byte r, byte g, byte b) {
        Red = r;
        Green = g;
        Blue = b;
    }
}

public class GlowEndPayload : AbstractColorEndPayload {
    public override byte ChunkType => 0x14;
}
  
