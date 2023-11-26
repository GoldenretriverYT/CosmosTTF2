using IL2CPU.API.Attribs;
using MyvarEdit.TrueType;
using System;
using System.Collections.Generic;
using System.Text;
using Sys = Cosmos.System;

namespace CosmosTTF2.TestKernel {
    public class Kernel : Sys.Kernel {
        [ManifestResourceStream(ResourceName = "CosmosTTF2.TestKernel.Resources.SEGOEUI.TTF")]
        static byte[] segoeUi;
        TrueTypeFontFile font;

        protected override void BeforeRun() {
            Console.WriteLine("Cosmos booted successfully. Type a line of text to get it echoed back.");
            font = new TrueTypeFontFile();
            font.Load(segoeUi);
        }

        protected override void Run() {
            Console.Write("Input: ");
            var input = Console.ReadLine();
            Console.Write("Text typed: ");
            Console.WriteLine(input);
        }
    }
}
