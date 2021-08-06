using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SimpleTweaksPlugin.Helper {
    public class Util {
        public static byte[] Decompress(byte[] input)
        {
            using (var source = new MemoryStream(input))
            {
                byte[] lengthBytes = new byte[4];
                source.Read(lengthBytes, 0, 4);

                var length = BitConverter.ToInt32(lengthBytes, 0);
                using (var decompressionStream = new GZipStream(source,
                    CompressionMode.Decompress))
                {
                    var result = new byte[length];
                    decompressionStream.Read(result, 0, length);
                    return result;
                }
            }
        }

        public static string DecompressString(byte[] input) {
            return Encoding.UTF8.GetString(Decompress(input));
        }

        public static byte[] Compress(byte[] input)
        {
            using (var result = new MemoryStream())
            {
                var lengthBytes = BitConverter.GetBytes(input.Length);
                result.Write(lengthBytes, 0, 4);

                using (var compressionStream = new GZipStream(result,
                    CompressionMode.Compress))
                {
                    compressionStream.Write(input, 0, input.Length);
                    compressionStream.Flush();

                }
                return result.ToArray();
            }
        }

        public static byte[] Compress(string input) {
            return Compress(Encoding.UTF8.GetBytes(input));
        }
        
        public static string Base64Encode(byte[] bytes) {
            return System.Convert.ToBase64String(bytes);
        }

        public static byte[] Base64Decode(string b64String) {
            return System.Convert.FromBase64String(b64String);
        }
    }
}
