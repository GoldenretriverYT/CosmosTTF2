using Cosmos.Debug.Kernel;
using Cosmos.System.Graphics;
using CosmosTTF2.Rasterizer;
using MyvarEdit.TrueType;
using System.Drawing;

namespace CosmosTTF2 {
    public struct PostprocessedRenderedGlyph {
        public int[] buffer; // Store ARGB values as integers
        public int w;
        public int h;
        public int advanceWidth;
        public int baselineOffset;
    }

    public static class TTFManager {
        public static bool Debug { get; set; } = true;
        
        private static Debugger debugger = new("TTF");
        private static Dictionary<ulong, PostprocessedRenderedGlyph> renderedGlyfCache = new();

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
            int lineHeight = ascent - descent;
            List<PostprocessedRenderedGlyph> glyfs = new List<PostprocessedRenderedGlyph>();

            foreach (var cp in str) {
                byte actualCp = (cp < 0 || cp > 255) ? (byte)'?' : (byte)cp;

                UInt64 key = CalculateGlyfKey(font, actualCp, color);
                if (!renderedGlyfCache.TryGetValue(key, out var coloredGlyph)) {
                    var glyf = Rasterizer.Rasterizer.RasterizeGlyph(font, (char)actualCp, heightPx);

                    coloredGlyph = new PostprocessedRenderedGlyph {
                        buffer = new int[glyf.w * glyf.h],
                        w = glyf.w,
                        h = glyf.h,
                        advanceWidth = glyf.advanceWidth,
                        baselineOffset = glyf.baselineOffset
                    };

                    for (int y = 0; y < glyf.h; y++) {
                        debugger.Send(glyf.buffer[(y * glyf.w)..((y + 1) * glyf.w)].Select(x => x.ToString()).Aggregate((x, y) => x + " " + y));
                    }

                    for (int x = 0; x < glyf.w; x++) {
                        for (int y = 0; y < glyf.h; y++) {
                            int idx = x + y * glyf.w;
                            byte val = glyf.buffer[idx];
                            coloredGlyph.buffer[idx] = (color.A << 24) | ((color.R * val / 255) << 16) | ((color.G * val / 255) << 8) | (color.B * val / 255);
                        }
                    }

                    renderedGlyfCache[key] = coloredGlyph;
                }

                if (Debug) {
                    debugger.Send("Processed glyf " + (char)actualCp + " (" + actualCp + ") Key: " + key + " AdvanceWidth: " + coloredGlyph.advanceWidth + " BaselineOffset: " + coloredGlyph.baselineOffset);
                }

                totalWidth += coloredGlyph.advanceWidth;
                glyfs.Add(coloredGlyph);
            }

            Cosmos.System.Graphics.Bitmap bmp = new((uint)totalWidth, (uint)lineHeight, ColorDepth.ColorDepth32);
            if (Debug) debugger.Send("TotalWidth: " + totalWidth + " LineHeight: " + lineHeight + " Ascent: " + ascent);
            int xOff = 0;
            foreach (var glyf in glyfs) {
                int yOff = ascent - glyf.baselineOffset;

                for (int y = 0; y < glyf.h; y++) {
                    int sourceIndex = y * glyf.w;
                    int destIndex = (y + yOff) * totalWidth + xOff;

                    Buffer.BlockCopy(glyf.buffer, sourceIndex * sizeof(int), bmp.RawData, destIndex * sizeof(int), glyf.w * sizeof(int));
                }
                xOff += glyf.advanceWidth;
            }

            return bmp;
        }

    }
}