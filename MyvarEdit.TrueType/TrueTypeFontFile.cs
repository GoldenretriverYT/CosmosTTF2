using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MyvarEdit.TrueType.Internals;

namespace MyvarEdit.TrueType
{
    public class TrueTypeFontFile
    {
        static Random rng = new();
        
        public TrueTypeHeader Header { get; set; }
        public HorizontalHeaderTable HorizontalHeaderTable { get; set; }
        public VerticalHeaderTable VerticalHeaderTable { get; set; }
        public List<longHorMetric> longHorMetrics { get; set; } = new List<longHorMetric>();
        public MaxP MaxP { get; set; }
        private Dictionary<int, int> _cMapIndexes = new Dictionary<int, int>();
        public Dictionary<int, Glyf> Glyfs { get; set; } = new Dictionary<int, Glyf>();
        
        public byte UniqueId = 0;
        private static byte uniqueIdCounter = 0;

        public static Action<object> ManualFree = (object obj) => { };
        public static Action<string> dbg = (string str) => { };

        public TrueTypeFontFile() {
            if(uniqueIdCounter == byte.MaxValue) {
                throw new Exception("Font limit of 256 fonts reached. Please make sure to reuse fonts.");
            }

            UniqueId = uniqueIdCounter++;
        }

        public void Load(string file, Action gc)
        {
            Load(File.OpenRead(file), gc);
        }

        public void Load(byte[] bytes, Action gc)
        {
            using (var mem = new MemoryStream(bytes))
            {
                Load(mem, gc);
            }
        }

        public unsafe void Load(Stream stream, Action gc)
        {
            //NOTE: we do not want to store the stream in the class because i want to close the stream once the data is read
            //dbg("load 1");
            var off = ReadOffsetTableStruct(stream);
            //dbg("load 2");

            var glyfOffsets = new List<int>();
            var glyfOffset = 0;
            //dbg("load 3");
            TableEntry glyfOffsetTe = new TableEntry();

            //dbg("load 4");
            for (int i = 0; i < off.NumTables; i++)
            {
                //dbg("load 5");
                var te = ReadTableEntryStruct(stream);
                //dbg("load 6");
                var id = te.ToString();
                //dbg("load 7");
                var oldPos = stream.Position;
                //dbg("load 8");
                switch (id)
                {
                    case "head":
                        //dbg("load 8");
                        stream.Position = te.Offset;
                        //dbg("load 9");
                        dbg("[Pre ] Version: " + Header.Version);
                        dbg("[Pre ] FontRevision: " + Header.FontRevision);
                        dbg("[Pre ] UnitsPerEM: " + Header.UnitsPerEm);
                        var newHeader = ReadTrueTypeHeaderStruct(stream);
                        dbg("[Temp] Version: " + newHeader.Version);
                        dbg("[Temp] FontRevision: " + newHeader.FontRevision);
                        dbg("[Temp] UnitsPerEM: " + newHeader.UnitsPerEm);
                        Header = newHeader;
                        dbg("[Post] Version: " + Header.Version);
                        dbg("[Post] FontRevision: " + Header.FontRevision);
                        dbg("[Post] UnitsPerEM: " + Header.UnitsPerEm);
                        break;
                    case "hhea":
                        //dbg("load 10");
                        stream.Position = te.Offset;
                        //dbg("load 11");
                        HorizontalHeaderTable = ReadHorizontalHeaderTableStruct(stream);
                        break;
                    case "vhea":
                        //dbg("load 12");
                        stream.Position = te.Offset;
                        //dbg("load 13");
                        VerticalHeaderTable = ReadVerticalHeaderTableStruct(stream);
                        break;
                    case "hmtx":
                        //dbg("load 14");
                        stream.Position = te.Offset;
                        //dbg("load 15");
                        for (int j = 0; j < HorizontalHeaderTable.numOfLongHorMetrics; j++)
                        {
                            //dbg("load 16");
                            longHorMetrics.Add(ReadLongHorMetricStruct(stream));
                        }

                        break;
                    case "maxp":
                        //dbg("load 17");
                        stream.Position = te.Offset;
                        //dbg("load 18");
                        MaxP = ReadMaxPStruct(stream);

                        break;
                    case "cmap":
                        //dbg("load 19");
                        stream.Position = te.Offset;
                        //dbg("load 20");
                        ReadCmap(stream, gc);
                        break;
                    case "loca":
                        //dbg("load 21");
                        glyfOffsetTe = te;
                        break;
                    case "glyf":
                        //dbg("load 22");
                        glyfOffset = (int) te.Offset;
                        break;
                }

                dbg("UnitsPerEM1.5 (" + i + "): " + Header.UnitsPerEm);
                
                //dbg("load 23");
                stream.Position = oldPos;
            }

            //dbg("load 24");
            for (int charCode = 0; charCode < 255; charCode++)
            {
                //dbg("load 25");
                var maped = _cMapIndexes[charCode];
                //dbg("load 26");
                stream.Position = glyfOffset + GetGlyphOffset(glyfOffsetTe, stream, maped);
                //dbg("load 27 " + charCode + " (byte)" + (byte)charCode);
                Glyfs.Add(charCode, ReadGlyph(stream, (byte) charCode, dbg));
            }

            dbg("UnitsPerEM2: " + Header.UnitsPerEm);
            //dbg("load 28");
            foreach (var (charcode, glyf) in Glyfs.ToArray())
            {
                //dbg("load 29");
                if (glyf.Components.Count != 0)
                {
                    //dbg("load 30");
                    foreach (var component in glyf.Components)
                    {
                        stream.Position = glyfOffset + GetGlyphOffset(glyfOffsetTe, stream, component.GlyphIndex);
                        var g = ReadGlyph(stream, (byte) charcode, dbg);

                        if ((component.Flags & ComponentFlags.UseMyMetrics) == ComponentFlags.UseMyMetrics)
                        {
                            glyf.Xmax = g.Xmax;
                            glyf.Xmin = g.Xmin;
                            glyf.Ymax = g.Ymax;
                            glyf.Ymin = g.Ymin;
                        }
                    }
                }
            }

            dbg("UnitsPerEM3: " + Header.UnitsPerEm);
        }

        public int GetBaselineOffset(Glyf glyf) {
            // Use the font's ascent and descent
            int ascent = this.HorizontalHeaderTable.ascent;
            int descent = this.HorizontalHeaderTable.descent;

            // Calculate the baseline offset
            // This is a simple calculation; you might need to adjust based on your font metrics.
            int baselineOffset = ascent - glyf.Ymax;

            return baselineOffset;
        }
        private static float Bezier(float p0, float p1, float p2, float t) // Parameter 0 <= t <= 1
        {
            //B(T) = P1 + (1 - t)^2 * (P0 - P1) + t^2 (P2 - P1)

            return (float)(p1 + Math.Pow(1f - t, 2) * (p0 - p1) + Math.Pow(t, 2) * (p2 - p1));
        }

        private Glyf ReadGlyph(Stream s, byte charcode, Action<string> dbg)
        {
            var re = new Glyf();
            var gd = ReadGlyphDescriptionStruct(s);
            var topPos = s.Position;
            re.NumberOfContours = gd.numberOfContours;
            re.Xmax = gd.xMax;
            re.Xmin = gd.xMin;
            re.Ymax = gd.yMax;
            re.Ymin = gd.yMin;


            var tmpXPoints = new List<int>();
            var tmpYPoints = new List<int>();
            var lst = new List<bool>();
            var flags = new List<OutlineFlags>();
            var max = 0;

            if (gd.numberOfContours >= 0) //simple glyph
            {
                var endPtsOfContours = ReadArray<ushort>(s, gd.numberOfContours);

                for (int i = 0; i < gd.numberOfContours; i++)
                {
                    re.ContourEnds.Add(endPtsOfContours[i]);
                }

                var instructionLength = ReadArray<ushort>(s, 1)[0];
                var instructions = ReadBytes(s, instructionLength);

                max = endPtsOfContours.Max() + 1;

                //NOTE: we are most probably reading junk because im reading to meany bytes
                var flagsRes = s.Position;
                var tmpflags = ReadBytes(s, max * 2);


                var off = 0;

                for (int p = 0; p < max; p++)
                {
                    var f = (OutlineFlags) tmpflags[off++];

                    flags.Add(f);
                    lst.Add((f & OutlineFlags.OnCurve) == OutlineFlags.OnCurve);

                    if ((f & OutlineFlags.Repeat) == (OutlineFlags.Repeat))
                    {
                        var z = tmpflags[off++];
                        p += z;

                        for (int i = 0; i < z; i++)
                        {
                            flags.Add(f);
                            lst.Add((f & OutlineFlags.OnCurve) == (OutlineFlags.OnCurve));
                        }
                    }
                }


                var xoff = 0;

                void IterPoints(byte[] arr, OutlineFlags byteFlag, OutlineFlags deltaFlag, List<int> tmp)
                {
                    xoff = 0;
                    var xVal = 0;

                    for (int i = 0; i < max; i++)
                    {
                        var flag = flags[i];

                        if ((flag & byteFlag) == (byteFlag))
                        {
                            if ((flag& deltaFlag) == (deltaFlag))
                            {
                                xVal += arr[xoff++];
                            }
                            else
                            {
                                xVal -= arr[xoff++];
                            }
                        }
                        else if (!((flag & deltaFlag) == (deltaFlag)) && !((flag & byteFlag) == (byteFlag)))
                        {
                            xVal += BitConverter.ToInt16(new[] {arr[xoff++], arr[xoff++]}.Reverse().ToArray());
                        }
                        else
                        {
                        }

                        tmp.Add(xVal);
                    }
                }


                s.Position = flagsRes + off;
                var resPoint = s.Position;
                var xPoints = ReadBytes(s, max * 2);

                IterPoints(xPoints, OutlineFlags.XIsByte, OutlineFlags.XDelta, tmpXPoints);

                s.Position = flagsRes + off + xoff;
                var yPoints = ReadBytes(s, max * 2);
                IterPoints(yPoints, OutlineFlags.YIsByte, OutlineFlags.YDelta, tmpYPoints);

                GlyfPoint MidpointRounding(GlyfPoint a, GlyfPoint b)
                {
                    return new GlyfPoint(
                        (a.X + b.X) / 2f,
                        (a.Y + b.Y) / 2f
                    );
                }


                re.Points.Add(new GlyfPoint(tmpXPoints[0], tmpYPoints[0]));
                re.Curves.Add(lst[0]);
                for (int i = 1; i < max; i++)
                {
                    re.Points.Add(new GlyfPoint(tmpXPoints[i], tmpYPoints[i])
                    {
                        IsOnCurve = lst[i]
                    });
                    re.Curves.Add(lst[i]);
                }

                var points = new List<GlyfPoint>();
                for (var i = 0; i < re.Points.Count; i++)
                {
                    points.Add(re.Points[i]);
                    if (re.ContourEnds.Contains((ushort) i))
                    {
                        re.Shapes.Add(points);
                        points = new List<GlyfPoint>();
                    }
                }

                foreach (var shape in re.Shapes)
                {
                    for (var i = 1; i < shape.Count; i++)
                    {
                        var a = shape[i];
                        var b = shape[i - 1];
                        if (!a.IsOnCurve && !b.IsOnCurve)
                        {
                            var midPoint = MidpointRounding(a, b);
                            midPoint.isMidpoint = true;
                            shape.Insert(i, midPoint);
                            i++;
                        }
                    }
                }


                foreach (var shape in re.Shapes)
                {
                    var shapes = shape.ToArray();
                    shape.Clear();
                    shape.Add(shapes[0]);
                    for (var i = 1; i < shapes.Length; i++)
                    {
                        if (!shapes[i].IsOnCurve && !shapes[i].isMidpoint)
                        {
                            var res = 15f;

                            var a = i == 0 ? shapes[^1] : shapes[i - 1];
                            var b = shapes[i];
                            var c = i + 1 >= shapes.Length ? shapes[0] : shapes[i + 1];

                            for (int j = 0; j <= res; j++)
                            {
                                var t = j / res;
                                shape.Add(new GlyfPoint(
                                    Bezier(a.X, b.X, c.X, t),
                                    Bezier(a.Y, b.Y, c.Y, t))
                                {
                                    //isMidpoint = true
                                });
                            }
                        }
                        else
                        {
                            shape.Add(shapes[i]);
                        }
                    }

                    //shape.Add(shapes.Last());
                }
            }
            else
            {
                s.Position = topPos;
                var components = new List<ComponentGlyph>();
                var flag = ComponentFlags.MoreComponents;


                while ((flag & ComponentFlags.MoreComponents) == (ComponentFlags.MoreComponents))
                {
                    var fval = ReadArray<ushort>(s, 1)[0];
                    flag = (ComponentFlags) (fval);
                    var component = new ComponentGlyph();
                    component.GlyphIndex = ReadArray<ushort>(s, 1)[0];

                    component.Flags = flag;

                    if ((flag & ComponentFlags.Arg1And2AreWords) == (ComponentFlags.Arg1And2AreWords))
                    {
                        component.Argument1 = ReadArray<short>(s, 1)[0];
                        component.Argument2 = ReadArray<short>(s, 1)[0];
                    }
                    else
                    {
                        component.Argument1 = ReadBytes(s, 1)[0];
                        component.Argument2 = ReadBytes(s, 1)[0];
                    }

                    if ((flag & ComponentFlags.ArgsAreXyValues) == (ComponentFlags.ArgsAreXyValues))
                    {
                        component.E = component.Argument1;
                        component.F = component.Argument2;
                    }
                    else
                    {
                        component.DestPointIndex = component.Argument1;
                        component.SrcPointIndex = component.Argument2;
                    }

                    if ((flag & ComponentFlags.WeHaveAScale) == (ComponentFlags.WeHaveAScale))
                    {
                        component.A = ReadArray<short>(s, 1)[0] / (1 << 14);
                        component.D = component.A;
                    }
                    else if ((flag & ComponentFlags.WeHaveAnXAndYScale) == (ComponentFlags.WeHaveAnXAndYScale))
                    {
                        component.A = ReadArray<short>(s, 1)[0] / (1 << 14);
                        component.D = ReadArray<short>(s, 1)[0] / (1 << 14);
                    }
                    else if ((flag & ComponentFlags.WeHaveATwoByTwo) == (ComponentFlags.WeHaveATwoByTwo))
                    {
                        component.A = ReadArray<short>(s, 1)[0] / (1 << 14);
                        component.B = ReadArray<short>(s, 1)[0] / (1 << 14);
                        component.C = ReadArray<short>(s, 1)[0] / (1 << 14);
                        component.D = ReadArray<short>(s, 1)[0] / (1 << 14);
                    }


                    components.Add(component);
                }

                if ((flag & ComponentFlags.WeHaveInstructions) == (ComponentFlags.WeHaveInstructions))
                {
                    var off = ReadArray<ushort>(s, 1)[0];
                    s.Position += off;
                }

                re.Components.AddRange(components);
            }

            return re;
        }

        private int GetGlyphOffset(TableEntry te, Stream s, int index)
        {
            if (Header.IndexToLocFormat == 1)
            {
                s.Position = (int) te.Offset + index * 4;
                return (int) ReadArray<uint>(s, 1)[0];
            }

            s.Position = (int) te.Offset + index * 2;
            return (int) ReadArray<ushort>(s, 1)[0] * 2;
        }

        private void ReadCmap(Stream s, Action gc)
        {
            //dbg("cmap 1");
            var startPos = s.Position;
            //dbg("cmap 2");
            var idx = ReadCmapIndexStruct(s);
            //dbg("cmap 3");
            var subtablesStart = s.Position;
            for (int i = 0; i < idx.NumberSubtables; i++)
            {
                //dbg("cmap 4");
                s.Position = subtablesStart + (i * 8);
                //dbg("cmap 5");
                var encoding = ReadCmapEncodingStruct(s);


                //dbg("cmap 6");
                s.Position = startPos + encoding.offset;
                //dbg("cmap 7");
                var old = s.Position;
                //dbg("cmap 8");
                var cmap = ReadCmapStruct(s);

                //dbg("cmap 9");
                if (encoding.platformID == 0 && cmap.format == 4)
                {
                    //dbg("cmap 10");
                    var range = cmap.searchRange;
                    //dbg("cmap 11");
                    var segcount = cmap.segCountX2 / 2;

                    //dbg("cmap 12");
                    var endCode = ReadArray<ushort>(s, segcount);
                    //dbg("cmap 13");
                    s.Position += 2;
                    //dbg("cmap 14");
                    var startCode = ReadArray<ushort>(s, segcount);
                    //dbg("cmap 15");
                    var idDelta = ReadArray<ushort>(s, segcount);
                    //dbg("cmap 16");
                    var idRangeOffsetptr = s.Position;
                    //dbg("cmap 17 segcount: " + segcount + " range: " + range + " readlen: " + segcount * 8 * range);
                    var idRangeOffset = ReadArray<ushort>(s, segcount * 8 * range, false, gc);

                    //dbg("cmap 18");
                    var startOfIndexArray = s.Position;

                    //@Hack should not do this but just to test for now
                    //dbg("cmap 19");
                    for (int charCode = 0; charCode < 255; charCode++)
                    {
                        //dbg("cmap 20");
                        var found = false;
                        //dbg("cmap 21");
                        for (int segIdx = 0; segIdx < segcount; segIdx++)
                        {
                            if (endCode[segIdx] >= charCode && startCode[segIdx] <= charCode)
                            {
                                if (idRangeOffset[segIdx] != 0)
                                {
                                    var z =
                                        idRangeOffset[
                                            segIdx + idRangeOffset[segIdx] / 2 + (charCode - startCode[segIdx])];

                                    var delta = (short) idDelta[segIdx];
                                    _cMapIndexes.Add(charCode, (short) (z) + delta);
                                }
                                else
                                {
                                    _cMapIndexes.Add(charCode, (short) idDelta[segIdx] + charCode);
                                }

                                found = true;
                            }
                        }

                        if (!found)
                        {
                            _cMapIndexes.Add(charCode, 0);
                        }
                    }

                    return;
                }
                else
                {
                    Console.WriteLine($"Only Cmap format  4 is Implemented, you tried using: {cmap.format}");
                }
            }
        }

        private unsafe byte[] ReadBytes(Stream s, int leng) {
            var re = new byte[leng];
            s.Read(re);

            return re;
        }

        private unsafe T[] ReadArray<T>(Stream s, int length, bool dontFlipBits = false, Action gc = null) {
            dbg = dbg ?? ((str) => { });
            gc = gc ?? (() => { });

            var result = new T[length];
            var elementSize = sizeof(T);
            var size = elementSize * length;
            var buffer = new byte[size];
            var readSize = s.Read(buffer, 0, size);

            if (readSize != size) {
                // Handle error
            }

            var converter = FindOverload<T>();

            fixed (byte* pBuffer = buffer) {
                for (int i = 0; i < length; i++) {
                    if (i % 2500 == 0) {
                        gc();
                    }

                    var offset = i * elementSize;
                    byte* segmentPtr = pBuffer + offset;

                    if (!dontFlipBits) {
                        // Reverse the bytes in place
                        for (int j = 0; j < elementSize / 2; j++) {
                            byte temp = segmentPtr[j];
                            segmentPtr[j] = segmentPtr[elementSize - j - 1];
                            segmentPtr[elementSize - j - 1] = temp;
                        }
                    }

                    // Create a temporary array for the converter
                    byte[] segmentArray = new byte[elementSize];
                    Marshal.Copy((IntPtr)segmentPtr, segmentArray, 0, elementSize);
                    result[i] = converter(segmentArray);

                    // Manually free the temporary array
                    ManualFree(segmentArray);
                }
            }

            // Optionally, manually free the main buffer
            ManualFree(buffer);

            return result;
        }

        private static Func<byte[], T> FindOverload<T>() {
            if (typeof(T) == typeof(int)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToInt32(bytes, 0);
            } else if (typeof(T) == typeof(short)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToInt16(bytes, 0);
            } else if (typeof(T) == typeof(long)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToInt64(bytes, 0);
            } else if (typeof(T) == typeof(UInt16)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToUInt16(bytes, 0);
            } else if (typeof(T) == typeof(UInt32)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToUInt32(bytes, 0);
            } else if (typeof(T) == typeof(UInt64)) {
                return (byte[] bytes) => (T)(object)BitConverter.ToUInt64(bytes, 0);
            } else {
                throw new NotImplementedException($"No conversion implemented for {typeof(T)}");
            }
        }
        

        private ushort ReadUInt16BigEndian(BinaryReader reader) {
            var bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private uint ReadUInt32BigEndian(BinaryReader reader) {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private ulong ReadUInt64BigEndian(BinaryReader reader) {
            var bytes = reader.ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        private short ReadInt16BigEndian(BinaryReader reader) {
            var bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        
        private unsafe TrueTypeHeader ReadTrueTypeHeaderStruct(Stream stream) {
            var reader = new BinaryReader(stream);

            var re = new TrueTypeHeader {
                Version = reader.ReadUInt32BE(),
                FontRevision = reader.ReadUInt32BE(),
                CheckSumAdjustment = reader.ReadUInt32BE(),
                MagicNumber = reader.ReadUInt32BE(),
                Flags = reader.ReadUInt16BE(),
                UnitsPerEm = LogButPass(reader.ReadUInt16BE()),
                Created = reader.ReadUInt64BE(),
                Modified = reader.ReadUInt64BE(),
                Xmin = reader.ReadInt16BE(),
                Ymin = reader.ReadInt16BE(),
                Xmax = reader.ReadInt16BE(),
                Ymax = reader.ReadInt16BE(),
                MacStyle = reader.ReadUInt16BE(),
                LowestRecPPEM = reader.ReadUInt16BE(),
                FontDirectionHint = reader.ReadInt16BE(),
                IndexToLocFormat = reader.ReadInt16BE(),
                GlyphDataFormat = reader.ReadInt16BE()
            };

            dbg("[Read] Version: " + re.Version);
            dbg("[Read] FontRevision: " + re.FontRevision);
            dbg("[Read] UnitsPerEM: " + re.UnitsPerEm);
            return re;
        }

        private HorizontalHeaderTable ReadHorizontalHeaderTableStruct(Stream stream) {
            var reader = new BinaryReader(stream);

            return new HorizontalHeaderTable {
                Version = reader.ReadUInt32BE(),
                ascent = reader.ReadInt16BE(),
                descent = reader.ReadInt16BE(),
                lineGap = reader.ReadInt16BE(),
                advanceWidthMax = reader.ReadUInt16BE(),
                minLeftSideBearing = reader.ReadInt16BE(),
                minRightSideBearing = reader.ReadInt16BE(),
                xMaxExtent = reader.ReadInt16BE(),
                caretSlopeRise = reader.ReadInt16BE(),
                caretSlopeRun = reader.ReadInt16BE(),
                caretOffset = reader.ReadInt16BE(),
                reserved = reader.ReadInt16BE(),
                reserved1 = reader.ReadInt16BE(),
                reserved2 = reader.ReadInt16BE(),
                reserved4 = reader.ReadInt16BE(),
                metricDataFormat = reader.ReadInt16BE(),
                numOfLongHorMetrics = reader.ReadUInt16BE()
            };
        }

        private CmapEncoding ReadCmapEncodingStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new CmapEncoding {
                platformID = reader.ReadUInt16BE(),
                platformSpecificID = reader.ReadUInt16BE(),
                offset = reader.ReadUInt32BE()
            };
        }

        private GlyphDescription ReadGlyphDescriptionStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            var d = new GlyphDescription {
                numberOfContours = reader.ReadInt16BE(),
                xMin = reader.ReadInt16BE(),
                yMin = reader.ReadInt16BE(),
                xMax = reader.ReadInt16BE(),
                yMax = reader.ReadInt16BE()
            };

            return d;
        }

        public static T LogButPass<T>(T whatToLog) {
            Console.WriteLine(whatToLog);
            return whatToLog;
        }

        private Cmap ReadCmapStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new Cmap {
                format = reader.ReadUInt16BE(),
                length = reader.ReadUInt16BE(),
                language = reader.ReadUInt16BE(),
                segCountX2 = reader.ReadUInt16BE(),
                searchRange = reader.ReadUInt16BE(),
                entrySelector = reader.ReadUInt16BE(),
                rangeShift = reader.ReadUInt16BE()
            };
        }

        private CmapIndex ReadCmapIndexStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new CmapIndex {
                Version = reader.ReadUInt16BE(),
                NumberSubtables = reader.ReadUInt16BE()
            };
        }

        private longHorMetric ReadLongHorMetricStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new longHorMetric {
                advanceWidth = reader.ReadUInt16BE(),
                leftSideBearing = reader.ReadInt16BE()
            };
        }

        private VerticalHeaderTable ReadVerticalHeaderTableStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new VerticalHeaderTable {
                Version = reader.ReadUInt32BE(),
                vertTypoAscender = reader.ReadInt16BE(),
                vertTypoDescender = reader.ReadInt16BE(),
                vertTypoLineGap = reader.ReadInt16BE(),
                advanceHeightMax = reader.ReadInt16BE(),
                minTopSideBearing = reader.ReadInt16BE(),
                minBottomSideBearing = reader.ReadInt16BE(),
                yMaxExtent = reader.ReadInt16BE(),
                caretSlopeRise = reader.ReadInt16BE(),
                caretSlopeRun = reader.ReadInt16BE(),
                caretOffset = reader.ReadInt16BE(),
                reserved = reader.ReadInt16BE(),
                reserved1 = reader.ReadInt16BE(),
                reserved2 = reader.ReadInt16BE(),
                reserved4 = reader.ReadInt16BE(),
                metricDataFormat = reader.ReadInt16BE(),
                numOfLongVerMetrics = reader.ReadUInt16BE()
            };
        }

        private MaxP ReadMaxPStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new MaxP {
                Version = reader.ReadUInt32BE(),
                numGlyphs = reader.ReadUInt16BE(),
                maxPoints = reader.ReadUInt16BE(),
                maxContours = reader.ReadUInt16BE(),
                maxComponentPoints = reader.ReadUInt16BE(),
                maxComponentContours = reader.ReadUInt16BE(),
                maxZones = reader.ReadUInt16BE(),
                maxTwilightPoints = reader.ReadUInt16BE(),
                maxStorage = reader.ReadUInt16BE(),
                maxFunctionDefs = reader.ReadUInt16BE(),
                maxInstructionDefs = reader.ReadUInt16BE(),
                maxStackElements = reader.ReadUInt16BE(),
                maxSizeOfInstructions = reader.ReadUInt16BE(),
                maxComponentElements = reader.ReadUInt16BE(),
                maxComponentDepth = reader.ReadUInt16BE()
            };
        }

        private OffsetTable ReadOffsetTableStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new OffsetTable {
                ScalerType = reader.ReadUInt32BE(),
                NumTables = reader.ReadUInt16BE(),
                SearchRange = reader.ReadUInt16BE(),
                EntrySelector = reader.ReadUInt16BE(),
                RangeShift = reader.ReadUInt16BE()
            };
        }

        private TableEntry ReadTableEntryStruct(Stream stream) {
            var reader = new BinaryReader(stream);
            return new TableEntry {
                Id = reader.ReadUInt32BE(),
                CheckSum = reader.ReadUInt32BE(),
                Offset = reader.ReadUInt32BE(),
                Length = reader.ReadUInt32BE()
            };
        }
    }

    public static class BinaryReaderExtensions {
        public static ushort ReadUInt16BE(this BinaryReader reader) {
            var bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static uint ReadUInt32BE(this BinaryReader reader) {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static ulong ReadUInt64BE(this BinaryReader reader) {
            var bytes = reader.ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        public static short ReadInt16BE(this BinaryReader reader, bool dbgRawBytes = false) {
            var bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            var re = BitConverter.ToInt16(bytes, 0);
            return re;
        }

        public static int ReadInt32BE(this BinaryReader reader) {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static long ReadInt64BE(this BinaryReader reader) {
            var bytes = reader.ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }
    }

}