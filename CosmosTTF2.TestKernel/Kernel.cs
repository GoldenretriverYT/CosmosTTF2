using Cosmos.Core;
using Cosmos.Core.Memory;
using Cosmos.System.Graphics;
using CosmosTTF2.Rasterizer;
using IL2CPU.API.Attribs;
using MyvarEdit.TrueType;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using Bitmap = Cosmos.System.Graphics.Bitmap;
using Sys = Cosmos.System;

namespace CosmosTTF2.TestKernel {
    public class Kernel : Sys.Kernel {
        [ManifestResourceStream(ResourceName = "CosmosTTF2.TestKernel.Resources.SEGOEUI.TTF")]
        static byte[] segoeUiData;

        [ManifestResourceStream(ResourceName = "CosmosTTF2.TestKernel.Resources.ARIAL.TTF")]
        static byte[] arialData;

        TrueTypeFontFile segoeUi;
        TrueTypeFontFile arial;
        
        Canvas cv;

        Random rng = new();

        protected override void BeforeRun() {
            TrueTypeFontFile.ManualFree = (object obj) => {
                GCImplementation.Free(obj);
            };

            TrueTypeFontFile.dbg = (str) => { Console.WriteLine(str); };
            //Rasterizer.Rasterizer.dbg = (str) => { mDebugger.Send(str); };


            Console.WriteLine("Loading Segoe UI...");
            segoeUi = new TrueTypeFontFile();
            segoeUi.Load(segoeUiData, () => {
                Heap.Collect();
            });

            Heap.Collect();

            Console.WriteLine("Loading Arial...");
            arial = new TrueTypeFontFile();
            arial.Load(arialData, () => {
                Heap.Collect();
            });

            Heap.Collect();
            
            cv = FullScreenCanvas.GetFullScreenCanvas(new Mode(1280, 720, ColorDepth.ColorDepth32));
            Console.WriteLine("got canvas");
        }

        protected override void Run() {
            Bitmap bmpSegoe = TTFManager.DrawString(segoeUi, "the quick brown fox jumps over the lazy dog", 24, Color.White);
            Bitmap bmpArial = TTFManager.DrawString(arial, "the quick brown fox jumps over the lazy dog", 24, Color.White);

            cv.Clear(Color.Black);
            cv.DrawImageAlpha(bmpSegoe, 45, 45);
            cv.DrawImageAlpha(bmpArial, 45, 90);
            cv.Display();
            
            Console.ReadKey();
        }
    }
}
