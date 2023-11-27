using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Sys = Cosmos.System;

namespace CosmosReproTest {
    public class Kernel : Sys.Kernel {
        protected override void BeforeRun() {
            Console.WriteLine("Cosmos booted successfully. Type a line of text to get it echoed back.");
        }

        protected override void Run() {
            short yippe = -24;
            int yippe32 = (int)yippe;

            Console.WriteLine("Yippe: " + yippe + "; " + yippe32);

            Struct1 strct = new Struct1() {
                X = -24,
                Y = -24
            };

            Foo(strct);
            Console.ReadLine();
        }

        private void Foo(Struct1 strct) {
            int strctX32 = (int)strct.X;

            Console.WriteLine("StructX: " + strct.X + "; " + strctX32);
        }

        struct Struct1 {
            public short X;
            public short Y;
        }
    }
}
