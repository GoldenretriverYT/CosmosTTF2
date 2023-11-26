using MyvarEdit.TrueType;
using System.Diagnostics;
using System.Drawing;

namespace CosmosTTF2.Rasterizer.Test {
    internal class Program {
        static void Main(string[] args) {
            var ttf = new TrueTypeFontFile();
            ttf.Load(@"C:\Windows\Fonts\segoeui.ttf");

            Stopwatch sw = new();
            sw.Start();
            for(var i = 0; i < 256; i++) {
                char ch = (char)i;
                Console.Write("Rendering " + ch + " (" + i + ")... ");
                try {
                    var output = Rasterizer.RasterizeGlyph(ttf, ch, 48);

                    Bitmap bmp = new Bitmap(output.w, output.h);
                    for (var x = 0; x < output.w; x++) {
                        for (var y = 0; y < output.h; y++) {
                            var val = output.buffer[x + y * output.w];
                            bmp.SetPixel(x, output.h - y - 1, Color.FromArgb(val, val, val));
                        }
                    }

                    bmp.Save("chr" + i + ".png");
                    Console.WriteLine(" SUCCESS");
                } catch(Exception ex) {
                    Console.WriteLine(" FAILURE: " + ex.Message);
                }
            }
            sw.Stop();

            Console.WriteLine("Took " + sw.Elapsed.TotalMilliseconds + "ms");
        }
    }
}