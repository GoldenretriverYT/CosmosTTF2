using Cosmos.Core;
using Cosmos.Core.Memory;
using Cosmos.System.Graphics;
using IL2CPU.API.Attribs;
using MyvarEdit.TrueType;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Sys = Cosmos.System;

namespace CosmosTTF2.TestKernel {
    public class Kernel : Sys.Kernel {
        [ManifestResourceStream(ResourceName = "CosmosTTF2.TestKernel.Resources.SEGOEUI.TTF")]
        static byte[] segoeUi;
        TrueTypeFontFile font;
        Canvas cv;

        Random rng = new();

        protected override void BeforeRun() {
            TrueTypeFontFile.ManualFree = (object obj) => {
                GCImplementation.Free(obj);
            };

            TrueTypeFontFile.dbg = (str) => { Console.WriteLine(str); };


            Console.WriteLine("Cosmos booted successfully.");
            font = new TrueTypeFontFile();
            font.Load(segoeUi, () => {
                Heap.Collect();
            });
            
            Heap.Collect();
            
            cv = FullScreenCanvas.GetFullScreenCanvas(new Mode(1280, 720, ColorDepth.ColorDepth32));
            Console.WriteLine("got canvas");
        }

        protected override void Run() {
            mDebugger.Send("yo");
            mDebugger.Send("UnitsPerEM on draw: " + font.Header.UnitsPerEm);
            Cosmos.System.Graphics.Bitmap bmp = TTFManager.DrawString(font, "Hello!", 48, Color.White);
            mDebugger.Send("yo done");

            mDebugger.Send("canvas now");
            cv.Clear(Color.Black);
            cv.DrawImageAlpha(bmp, 50, 50);
            cv.Display();
            mDebugger.Send("canvas done");
        }
    }
}
