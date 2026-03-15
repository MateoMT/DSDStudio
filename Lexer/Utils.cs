using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace DSDCore
{
    internal class Utils
    {
        public static void SaveCSV(string filePath, Dictionary<int, string> substanceNames, List<double[]> results,double t0,double tfinal,int n)
        {
            double h = (tfinal - t0) / n;
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // 表头
                writer.WriteLine("Time," + string.Join(",", substanceNames.Values));

                // 数据行
                for (int i = 0; i < results.Count; i++)
                {
                    double time = t0 + i * h;
                    string line = time.ToString("F2") + "," + string.Join(",", results[i].Select(v => v.ToString("F4")));
                    writer.WriteLine(line);
                }
            }

            Console.WriteLine($"结果已保存至 {filePath}");
        }
        public static List<ComplexNode2> getComplexNode2s(ComplexNode complex)
        {
            List<ComplexNode2> complexNode2s = new();
            for (int i = 0; i < complex.Values.Count; i++)
            {
                if (complex.Values[i] is StrandNode strand && strand.Type == StrandNode.StrandType.duplex)
                {
                    ComplexNode2 complexNode2 = new();
                    complexNode2.middle = strand.DeepCopy() as StrandNode;
                    for (int j = 1; j <= 3 && i - j >= 0; j++)
                    {
                        if (complex.Values[i - j] is StrandNode strand1 && strand1.Type == StrandNode.StrandType.upper)
                        {
                            complexNode2.lefttop = strand1.DeepCopy() as StrandNode;
                        }
                        else if (complex.Values[i - j] is StrandNode strand2 && strand2.Type == StrandNode.StrandType.lower)
                        {
                            complexNode2.leftbottom = strand2.DeepCopy() as StrandNode;
                        }
                        else if (complex.Values[i - j] is LinkerNode linker)
                        {
                            complexNode2.linker = linker.DeepCopy() as LinkerNode;
                            break;
                        }
                        else break;
                    }
                    for (int j = 1; j < 3 && i + j < complex.Values.Count; j++)
                    {
                        if (complex.Values[i + j] is StrandNode strand3 && strand3.Type == StrandNode.StrandType.upper)
                        {
                            complexNode2.righttop = strand3.DeepCopy() as StrandNode;
                        }
                        else if (complex.Values[i + j] is StrandNode strand4 && strand4.Type == StrandNode.StrandType.lower)
                        {
                            complexNode2.rightbottom = strand4.DeepCopy() as StrandNode;
                        }
                        else break;
                    }
                    if (complexNode2.middle != null)
                    {
                        complexNode2s.Add(complexNode2);
                    }
                }
            }
            return complexNode2s;
        }
        public static BaseComplex GetComplexNode(List<ComplexNode2> list)
        {
            ComplexNode complex = new();
            foreach (var item in list)
            {
                if (item.linker != null)
                {
                    complex.Values.Add(item.linker.DeepCopy());
                }
                if (item.lefttop != null)
                {
                    complex.Values.Add(item.lefttop.DeepCopy());
                }
                if (item.leftbottom != null)
                {
                    complex.Values.Add(item.leftbottom.DeepCopy());
                }
                if (item.middle != null)
                {
                    complex.Values.Add(item.middle.DeepCopy());
                }
                if (item.righttop != null)
                {
                    complex.Values.Add(item.righttop.DeepCopy());
                }
                if (item.rightbottom != null)
                {
                    complex.Values.Add(item.rightbottom.DeepCopy());
                }
            }
            if (complex.Values.Count == 1)
                return complex.Values[0];
            return complex;
        }
        public static BaseComplex getBaseComplex1(ComplexNode complex)
        {
            if (complex.Values.Count == 1)
            {
                return complex.Values[0];
            }
            else
            {
                return complex;
            }
        }
        public static BaseComplex getBaseComplex(BaseComplex complex)
        {

            if (complex is ComplexNode complex1)
            {
                var complex2 = getBaseComplex1(complex1);
                if (complex2 is StrandNode)
                    return complex2;

                ComplexNode ans = complex1.DeepCopy() as ComplexNode;
                ans.Values.Clear();
                foreach (var item in complex1.Values)
                {
                    ans.Values.Add(getBaseComplex(item));
                }
                ASTPrinter.PrintAst(ans);
                return ans;
            }
            else if (complex is LinkerNode linker)
            {
                return linker;
            }
            else if (complex is StrandNode strand)
            {
                StrandNode strand1 = strand.DeepCopy() as StrandNode;
                SeqNode seq = new SeqNode();
                if (strand.seq.Value is List<SeqNode> list && list.Count == 1)
                {
                    strand1.seq = list[0];
                }
                else
                    strand1.seq = strand.seq;
                return strand1;
            }
            else
            {
                throw new Exception("不支持的类型");
            }
        }
    }

    public static class SystemInfo
    {
        public static void PrintCpuAndMemoryInfo()
        {
            // 获取CPU信息
            using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
            {
                foreach (var item in searcher.Get())
                {
                    Console.Write($"CPU: {item["Name"]} ");
                    Console.Write($"Cores: {item["NumberOfCores"]} ");
                    Console.Write($"LogicalProcessors: {item["NumberOfLogicalProcessors"]} ");
                    Console.Write($"MaxClockSpeed: {item["MaxClockSpeed"]} MHz");
                }
            }

            // 获取内存信息
            using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
            {
                foreach (var item in searcher.Get())
                {
                    ulong totalMemory = (ulong)item["TotalPhysicalMemory"];
                    Console.WriteLine($"MemorySize: {totalMemory} bytes");
                }
            }
        }
    }
    public class Errors
    {
        public class _error
        {
            public int line;
            public int column;
            public string message;
        }
        private List<_error> errors = new();
        public List<_error> ErrorsList => errors;
        public bool Success { get; set; }

        public void AddError(int line, int column, string message)
        {
            _error error = new _error();
            error.line = line;
            error.column = column;
            error.message = message;
            errors.Add(error);
        }
        //private void PrintError(Errors error)
        //{
        //    Console.WriteLine("**************************************************************");
        //    Console.WriteLine($"在  {error.line}行,{error.column}列处有问题： {error.message}");
        //    Console.WriteLine("**************************************************************");
        //}
        public void PrintError()
        {
            Console.WriteLine($"共有{errors.Count}个问题：");
            foreach (var error in errors)
            {
                Console.WriteLine($"在  {error.line}行,{error.column}列处有问题： {error.message}");
            }
        }
    }
    public class point
    {
        public double time { get; set; }//x
        public double value { get; set; }//y
        public point(double time, double value)
        {
            this.time = time;
            this.value = value;
        }
    }
    public readonly struct matchInfo
    {
        public int up { get; }
        public int low { get; }
        public int length { get; }
        public matchInfo(int up, int low, int length)
        {
            this.up = up;
            this.low = low;
            this.length = length;
        }
        public void Print()
        {
            Console.WriteLine($"up:{up},low:{low},length:{length}");
        }
    }
    public readonly struct GuidPair : IEquatable<GuidPair>
    {
        public Guid First { get; }
        public Guid Second { get; }

        public GuidPair(Guid guid1, Guid guid2)
        {
            if (guid1.CompareTo(guid2) <= 0)
            {
                First = guid1;
                Second = guid2;
            }
            else
            {
                First = guid2;
                Second = guid1;
            }
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + First.GetHashCode();
                hash = hash * 23 + Second.GetHashCode();
                return hash;
            }
        }
        public override bool Equals(object obj)
        {
            return obj is GuidPair other && Equals(other);
        }
        public bool Equals(GuidPair other) => First == other.First && Second == other.Second;

        public static bool operator ==(GuidPair left, GuidPair right) => left.Equals(right);
        public static bool operator !=(GuidPair left, GuidPair right) => !left.Equals(right);
    }
    public readonly struct Line
    {
        public double startx { get; }
        public double starty { get; }
        public double endx { get; }
        public double endy { get; }
        public string color { get; }
        public string text { get; }
        public double dx { get; }//text相较线段的x偏移量
        public double dy { get; }//text相较线段的y偏移量
        public double radius { get; }//如果text为"circle"//则说明这是个圆，start是圆心，radius是半径

        public Line(double startx, double starty, double endx, double endy, string color)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = endx;
            this.endy = endy;
            this.color = color;
        }
        public Line(double startx, double starty, double endx, double endy, string color, string text)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = endx;
            this.endy = endy;
            this.color = color;
            this.text = text;
        }
        public Line(double startx, double starty, double endx, double endy, string color, string text, double dx, double dy)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = endx;
            this.endy = endy;
            this.color = color;
            this.text = text;
            this.dx = dx;
            this.dy = dy;
        }
        public Line(double startx, double starty, double endx, double endy, double r, string color, string text, double dx, double dy)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = endx;
            this.endy = endy;
            this.color = color;
            this.text = text;
            this.dx = dx;
            this.dy = dy;
            this.radius = r;
        }
        //声明圆
        public Line(double startx, double starty, double radius, string color)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = startx;
            this.endy = starty;
            this.color = color;
            this.radius = radius;
            text = "circle";
        }

        public Line(double startx, double starty, double endx, double endy, double radius, string color, string text)
        {
            this.startx = startx;
            this.starty = starty;
            this.endx = endx;
            this.endy = endy;
            this.color = color;
            this.text = text;
            this.radius = radius;
        }
        public Line move(double dxx, double dyy)
        {
            return new Line(startx + dxx, starty + dyy, endx + dxx, endy + dyy, radius, color, text, this.dx, this.dy);
        }
        public Line Rotate(double angle, double x, double y)//绕(x,y)顺时针旋转angle度
        {
            double radian = angle * Math.PI / 180;
            double newStartX = (double)((startx - x) * Math.Cos(radian) - (starty - y) * Math.Sin(radian) + x);
            double newStartY = (double)((startx - x) * Math.Sin(radian) + (starty - y) * Math.Cos(radian) + y);
            double newEndX = (double)((endx - x) * Math.Cos(radian) - (endy - y) * Math.Sin(radian) + x);
            double newEndY = (double)((endx - x) * Math.Sin(radian) + (endy - y) * Math.Cos(radian) + y);
            double newDx = -(double)((dx) * Math.Cos(radian) + (dy) * Math.Sin(radian));
            double newDy = (double)((dx) * Math.Sin(radian) + (dy) * Math.Cos(radian));
#if DEBUG
            Console.WriteLine($"..........................................");
            Console.WriteLine($"angle:{angle},x:{x},y:{y}");
            Console.WriteLine($"startx:{startx},starty:{starty},endx:{endx},endy:{endy}");
            Console.WriteLine($"newStartX:{newStartX},newStartY:{newStartY},newEndX:{newEndX},newEndY:{newEndY}");
            Console.WriteLine($"newDx:{newDx},newDy:{newDy}");
            if (text == "circle")
            {
                Console.WriteLine($"⚪radius:{radius}");
            }
            Console.WriteLine($"..........................................");
#endif
            return new Line(newStartX, newStartY, newEndX, newEndY, radius, color, text, newDx, newDy);
        }
        public Line Rotate(double angle)//绕起点旋转
        {
            return Rotate(angle, startx, starty);
        }
        public override string ToString()
        {
            return ($"startx:{startx},starty:{starty},endx:{endx},endy:{endy},color:{color},text:{text}");
        }
        public Line changeStart(double x, double y)//面对<a>[b t^]<c>:[e]<f u* v>::{x}[y*]<z q*>{w}时，e那里下链需要延长5-10的像素
        {
            return new Line(x, y, endx, endy, radius, color, text, dx, dy);
        }
        public Line Normal()//避免存在负值，然后最小的y变成10，最小的x变成0；
        {
            double dx = 0, dy = 0;
            if (text == "circle")
            {
                if (startx - radius < 0)
                {
                    dx = radius - startx;//x平移到0
                }
                if (starty - radius < 0)
                {
                    dy = radius - starty + 10;//y平移到10
                }
            }
            else
            {
                double mx = Math.Min(startx, endx);
                double my = Math.Min(starty, endy);
                dx = -mx;//x平移到0
                dy = -my + 10;//y平移到10
            }

            return new Line(startx + dx, starty + dy, endx + dx, endy + dy, radius, color, text, dx, dy);
        }
    }
    public class Timeutil
    {
        public static string GetNow()
        {
            DateTime now = DateTime.Now;
            //string time = now.ToString("yyyy-MM-dd HH:mm:ss");
            string time = now.ToString("yyyyMMdd_HHmmss");
            return time;
        }
    }
}
