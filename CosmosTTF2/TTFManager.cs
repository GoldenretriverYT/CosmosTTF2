using Cosmos.System.Graphics;
using MyvarEdit.TrueType;
using System.Drawing;

namespace CosmosTTF2 {
    public static class TTFManager {
        public static Dictionary<ulong, (byte[] buffer, int w, int h)> RenderedGlyfCache = new();

        /// <summary>
        /// Calculates a glyf cache key.
        /// Consists of:
        /// 0-48 bits: color (RGB, A is ignored)
        /// 48-56 bits: font unique id (TrueTypeFontFile.UniqueId)
        /// 56-64 bits: codepoint (we currently only support codepoints 0-255)
        /// </summary>
        /// <param name="font"></param>
        /// <param name="codepoint"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static ulong CalculateGlyfKey(TrueTypeFontFile font, byte codepoint, Color color) {
            var key = (ulong)color.ToArgb();
            key |= (ulong)font.UniqueId << 48;
            key |= (ulong)codepoint << 56;
            return key;
        }

        public static Cosmos.System.Graphics.Bitmap DrawString(TrueTypeFontFile font, string str, int heightPx, Color color) {
            int totalWidth = 0;
            int highestPoint = 0;
            List<(byte[] buffer, int w, int h)> glyfs = new();

            foreach(var cp in str) {
                byte actualCp = 0;

                if (cp < 0 || cp > 255)
                    actualCp = (byte)'?';
                else actualCp = (byte)cp;

                UInt64 key = CalculateGlyfKey(font, actualCp, color);

                if(RenderedGlyfCache.TryGetValue(key, out var glyf)) {
                    totalWidth += glyf.w;
                    if (glyf.h > highestPoint) highestPoint = glyf.h;
                    continue;
                }
            }

            Cosmos.System.Graphics.Bitmap bmp = new((uint)totalWidth, (uint)highestPoint, ColorDepth.ColorDepth32);
            foreach(var glyf in glyfs) {
                
            }
        }
    }
}