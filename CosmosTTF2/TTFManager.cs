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
        private static Dictionary<string, PostprocessedRenderedGlyph> renderedGlyfCache = new();

        public static Cosmos.System.Graphics.Bitmap DrawString(TrueTypeFontFile font, string str, int sizePt, Color color) {
            int totalWidth = 0;
            var unitsPerEm = font.Header.UnitsPerEm;

            // Convert point size to pixel size
            int pixelSize = (int)(sizePt * (96.0 / 72)); // assuming 96 DPI screen

            // Calculate scale factor
            float scale = (float)pixelSize / unitsPerEm;

            int ascent = font.HorizontalHeaderTable.ascent * pixelSize / unitsPerEm;
            int descent = font.HorizontalHeaderTable.descent * pixelSize / unitsPerEm;
            
            int lineHeight = ascent - descent;
            List<PostprocessedRenderedGlyph> glyfs = new List<PostprocessedRenderedGlyph>();

            // Lookup table for color.R * val / 255, index being the val
            byte[] rLookup = new byte[256];
            for (int i = 0; i < 256; i++) {
                rLookup[i] = (byte)(color.R * i / 255);
            }

            // Lookup table for color.G * val / 255, index being the val
            byte[] gLookup = new byte[256];
            for (int i = 0; i < 256; i++) {
                gLookup[i] = (byte)(color.G * i / 255);
            }

            // Lookup table for color.B * val / 255, index being the val
            byte[] bLookup = new byte[256];
            for (int i = 0; i < 256; i++) {
                bLookup[i] = (byte)(color.B * i / 255);
            }
            
            foreach (var cp in str) {
                byte actualCp = (cp < 0 || cp > 255) ? (byte)'?' : (byte)cp;

                string key = font.UniqueId.ToString() + actualCp + color.ToArgb() + sizePt;
                
                if (!renderedGlyfCache.TryGetValue(key, out var coloredGlyph)) {
                    var glyf = Rasterizer.Rasterizer.RasterizeGlyph(font, (char)actualCp, sizePt);

                    coloredGlyph = new PostprocessedRenderedGlyph {
                        buffer = new int[glyf.w * glyf.h],
                        w = glyf.w,
                        h = glyf.h,
                        advanceWidth = glyf.advanceWidth,
                        baselineOffset = glyf.baselineOffset
                    };

                    /*for (int y = 0; y < glyf.h; y++) {
                        debugger.Send(glyf.buffer[(y * glyf.w)..((y + 1) * glyf.w)].Select(x => x.ToString()).Aggregate((x, y) => x + " " + y));
                    }*/

                    for (int x = 0; x < glyf.w; x++) {
                        for (int y = 0; y < glyf.h; y++) {
                            int idxDest = x + (glyf.h - y - 1) * glyf.w; // Vertically flip the glyph
                            byte val = glyf.buffer[x + y * glyf.w];
                            coloredGlyph.buffer[idxDest] = (color.A << 24) | (rLookup[val] << 16) | (gLookup[val] << 8) | bLookup[val];
                        }
                    }

                    renderedGlyfCache[key] = coloredGlyph;
                }

                if (Debug) {
                    debugger.Send("Processed glyf " + (char)actualCp + " (" + actualCp + ") Key: " + key + " AdvanceWidth: " + coloredGlyph.advanceWidth + " (w: " + coloredGlyph.w + ", h: " + coloredGlyph.h + ") BaselineOffset: " + coloredGlyph.baselineOffset);
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