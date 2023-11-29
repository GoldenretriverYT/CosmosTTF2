using MyvarEdit.TrueType;
using System.Diagnostics;
using System.Drawing;

namespace CosmosTTF2.Rasterizer.Test {
    internal class Program {
        static void Main(string[] args) {
            TrueTypeFontFile.dbg = (str) => { Console.WriteLine(str); };
            Rasterizer.dbg = (str) => { Console.WriteLine(str); };
            var ttf = new TrueTypeFontFile();
            ttf.Load(@"C:\Windows\Fonts\segoeui.ttf", () => { });

            Stopwatch sw = new();
            sw.Start();
            /*for(var i = 0; i < 256; i++) {
                char ch = (char)i;
                Console.Write("Rendering " + ch + " (" + i + ")... ");
                var output = Rasterizer.RasterizeGlyph(ttf, ch, 48);

                if(output == null) {
                    Console.Write(" FAILED (probably glyph not found)");
                    continue;
                }

                Bitmap bmp = new Bitmap(output.w, output.h);
                for (var x = 0; x < output.w; x++) {
                    for (var y = 0; y < output.h; y++) {
                        var val = output.buffer[x + y * output.w];
                        bmp.SetPixel(x, output.h - y - 1, Color.FromArgb(val, val, val));
                    }
                }

                bmp.Save("chr" + i + ".png");

                Bitmap bmpNonDownscaled = new Bitmap(output.w2x, output.h2x);

                for (var x = 0; x < output.w2x; x++) {
                    for (var y = 0; y < output.h2x; y++) {
                        var val = output.original2xBuffer[x + y * output.w2x];
                        bmpNonDownscaled.SetPixel(x, output.h2x - y - 1, Color.FromArgb(val, val, val));
                    }
                }

                bmpNonDownscaled.Save("chr" + i + "_2x.png");
                Console.WriteLine(" SUCCESS");
            }*/

            var output = Rasterizer.RasterizeGlyph(ttf, 'e', 48);

            Bitmap bmp = new Bitmap(output.w, output.h);
            for (var x = 0; x < output.w; x++) {
                for (var y = 0; y < output.h; y++) {
                    var val = output.buffer[x + y * output.w];
                    bmp.SetPixel(x, output.h - y - 1, Color.FromArgb(val, val, val));
                }
            }

            bmp.Save("singlechardbg.png");
            sw.Stop();

            Console.WriteLine("Took " + sw.Elapsed.TotalMilliseconds + "ms");
        }
    }
}