using Cosmos.Debug.Kernel;
using Cosmos.System.Graphics;
using CosmosTTF2.Rasterizer;
using MyvarEdit.TrueType;
using System.Drawing;

namespace CosmosTTF2 {
    public static class TTFManager {
        public static bool Debug { get; set; } = true;
        
        private static Debugger debugger = new("TTF");
        private static Dictionary<ulong, RenderedGlyph> renderedGlyfCache = new();

        /// <summary>
        /// Calculates a glyf cache key.
        /// Consists of:
        /// 0-32 bits: color (ARGB)
        /// 48-56 bits: font unique id (TrueTypeFontFile.UniqueId)
        /// 56-64 bits: codepoint (we currently only support codepoints 0-255)
        /// </summary>
        /// <param name="font"></param>
        /// <param name="codepoint"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static ulong CalculateGlyfKey(TrueTypeFontFile font, byte codepoint, Color color) {
            // Extract ARGB value from the color (32 bits)
            uint colorValue = (uint)color.ToArgb();

            // Ensure the UniqueId is within 8-bit range and shift it 32 bits to the left
            ulong uniqueId = ((ulong)font.UniqueId) << 32;

            // Ensure the codepoint is within 8-bit range and shift it 40 bits to the left
            ulong codePointValue = ((ulong)codepoint) << 40;

            // Combine the values to form the key
            ulong key = colorValue | uniqueId | codePointValue;

            return key;
        }


        public static Cosmos.System.Graphics.Bitmap DrawString(TrueTypeFontFile font, string str, int heightPx, Color color) {
            int totalWidth = 0;
            int ascent = font.HorizontalHeaderTable.ascent * heightPx / font.Header.UnitsPerEm;
            int descent = font.HorizontalHeaderTable.descent * heightPx / font.Header.UnitsPerEm;
            int lineHeight = ascent - descent; // This is the maximum height of a line
            List<RenderedGlyph> glyfs = new List<RenderedGlyph>();

            // Iterating through characters in the string
            foreach (var cp in str) {
                byte actualCp = (cp < 0 || cp > 255) ? (byte)'?' : (byte)cp;

                UInt64 key = CalculateGlyfKey(font, actualCp, color);
                if (!renderedGlyfCache.TryGetValue(key, out var glyf)) {
                    // Rasterize the glyph if not in cache
                    glyf = Rasterizer.Rasterizer.RasterizeGlyph(font, (char)actualCp, heightPx, (str) => debugger.Send(str));
                    renderedGlyfCache.Add(key, glyf);
                }

                if (Debug) {
                    debugger.Send("Processed glyf " + (char)actualCp + " (" + actualCp + ") Key: " + key);
                }

                totalWidth += glyf.advanceWidth; // Use advance width for spacing
                glyfs.Add(glyf);
            }

            // Create bitmap with the total width and line height
            Cosmos.System.Graphics.Bitmap bmp = new((uint)totalWidth, (uint)lineHeight, ColorDepth.ColorDepth32);
            int xOff = 0;
            foreach (var glyf in glyfs) {
                int yOff = ascent - glyf.baselineOffset; // Adjust vertical position based on baseline

                for (int y = 0; y < glyf.h; y++) {
                    int sourceIndex = y * glyf.w; // Adjust this if your glyphs are stored in a different format
                    int destIndex = (int)(((y + yOff) * bmp.Width + xOff) * 4);

                    // Copy one row of pixels
                    Buffer.BlockCopy(glyf.buffer, sourceIndex, bmp.RawData, destIndex, glyf.w * 4);
                }
                xOff += glyf.advanceWidth; // Move to the next glyph position
            }

            return bmp;
        }
    }
}