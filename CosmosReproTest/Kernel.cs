using System;
using System.Collections.Generic;
using System.Text;
using Sys = Cosmos.System;

namespace CosmosReproTest {
    public class Kernel : Sys.Kernel {
        public RandomStruct FieldOfStruct { get; set; }

        protected override void BeforeRun() {
            Console.WriteLine("Cosmos booted successfully. Type a line of text to get it echoed back.");
        }

        protected override void Run() {
            FieldOfStruct = ParseStruct(/* not actual data */);
            Console.WriteLine("FieldOfStruct.a: " + FieldOfStruct.a);
            Console.WriteLine("FieldOfStruct.b: " + FieldOfStruct.b);
            Console.WriteLine("FieldOfStruct.c: " + FieldOfStruct.c);
            Console.WriteLine("FieldOfStruct.d: " + FieldOfStruct.d);
            Console.WriteLine("FieldOfStruct.e: " + FieldOfStruct.e);
            Console.WriteLine("FieldOfStruct.f: " + FieldOfStruct.f);
            Console.ReadLine();
        }

        public RandomStruct ParseStruct(/* not actual data */) {
            var re = new RandomStruct() {
                a = 1,
                b = 2,
                c = 3,
                d = 4,
                e = 5,
                f = 6
            };

            Console.WriteLine("a: " + re.a);
            Console.WriteLine("b: " + re.b);
            Console.WriteLine("c: " + re.c);
            Console.WriteLine("d: " + re.d);
            Console.WriteLine("e: " + re.e);
            Console.WriteLine("f: " + re.f);

            return re;
        }
    }

    public struct RandomStruct {
        public UInt16 a;
        public UInt32 b;
        public UInt64 c;
        public UInt16 d;
        public UInt16 e;
        public UInt64 f;
    }
}
