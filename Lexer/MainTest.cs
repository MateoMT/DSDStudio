using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using HarfBuzzSharp;
using ODE;
using Spire.Xls;

namespace DSDCore
{
    internal class MainTest
    {

        public static void test1()
        {
            string code5 = @"
def Input1() = [b* ]:[tx^* ]<a* >::[a* ]
( 10 Input1()
)";
            Interpreter interpreter = new Interpreter(code5);
            interpreter.Run();
        }
        public static void test2()
        {
            string code7 = @"
directive parameters [k=0.003;u=0.1]
dom a = {bind=k;unbind=u;colour=""red""}
dom x = {bind=k;unbind=u;colour=""green""}
dom to = {bind=k;unbind=u;colour=""blue""}
dom e = {bind=k;unbind=u;colour=""pink""}
dom y = {bind=k;unbind=u;colour=""brown""}
dom q = {bind=k;unbind=u;colour=""black""}
dom d = {bind=k;unbind=u;colour=""#87CEEB""}
dom j = {bind=k;unbind=u;colour=""#FFD700""}
def Input1() =  <a>[x to^ q]<y>{x}:{z}<q>[e* to^]<d>{j}
( 10 Input1()
)";//用于测试复杂复合物绘制
            Interpreter interpreter = new Interpreter(code7);
            interpreter.Run(true);
        }
        public static void test3()
        {
            string code5 = @"directive parameters [k=0.003;u=0.1]
dom b = {bind=k;unbind=u;colour=""red""}
dom tx = {bind=k;unbind=u;colour=""green""}
dom a = {bind=k;unbind=u;colour=""blue""}
def Input1() = <tx^* a >
def Input2() = {a tx^}
( 10 Input1()
| 10 Input2()
)";
            Interpreter interpreter = new Interpreter(code5);
            interpreter.Run();
        }
        public static string GetCpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        return item["Name"]?.ToString() ?? "Unknown CPU";
                    }
                }
            }
            catch { }
            return "Unknown CPU";
        }

        public static string GetGpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController"))
                {
                    foreach (var item in searcher.Get())
                    {
                        return item["Name"]?.ToString() ?? "Unknown GPU";
                    }
                }
            }
            catch { }
            return "Unknown GPU";
        }
        public static void test4()//测试右向分支迁移
        {
            string code9 = @"def Input1() = [tb^ b ]:<b >[tx^ ]:[x to^ ]
def Input2() = <fl^* x>
( 10 Input1()
| 0 Input2()
)";
            Interpreter interpreter = new Interpreter(code9);

            interpreter.Run();
        }
        public static void test5()//进一步测试解链反应 <tb^ b>+{tb^*}[b tx^]:[x to^]<->[tb^ b]{tx^}:[x to^]
        {
            string code8 = @"directive parameters [k=0.003;u=0.1]
directive simulator deterministic
dom a = {bind=k;unbind=u;colour=""red""}
dom tx = {bind=k;unbind=u;colour=""green""}
dom to = {bind=k;unbind=u;colour=""blue""}
def Input3() = [a]<tx^>::[b]
def Input2() = {a* tx^*}
( 10 Input3()|
10 Input2()
)";
            Interpreter interpreter = new Interpreter(code8);
            interpreter.Run();
        }
        public static void test6()//Join
        {
            string code6 = @"directive simulation {initial=0; final=1000000; points=1000; plots=[<a^ b>; <c d^>; <b c>]}
directive simulator deterministic
directive parameters [k=0.003;u=0.1]
def Input1() = <tb^ b>
def Input2() = <tx^ x>
def Output() = <x to^>
def Join() = {tb^*}[b tx^]:[x to^]
def Reporter() = <fl^>[x]{to^*}
def Signal() = <fl^ x>
( 10 Input1()
| 10 Input2()
| 0 Output()
| 100 Join()
| 100 Reporter()
| 0 Signal()
)";
            Interpreter interpreter = new Interpreter(code6);
            interpreter.Run();
            var (a,b) = interpreter.solve3();
            Console.WriteLine(b.Count);
            Console.WriteLine(a.Count);
            //foreach (var item in a)
            //{
            //    Console.Write(item + " ");
            //}
            //foreach (var item in b)
            //{
            //    foreach (var item2 in item)
            //    {
            //        Console.Write(item2.ToString("F4") + " ");
            //    }
            //    Console.WriteLine();
            //}
        }
        public static void test7()//测试and directive simulation {initial=0; final=1000000; points=1000; plots=[<1^ 2>; <3 4^>; <2 3>]}
        {
            string code5 = @"
directive simulator deterministic
def N1 = 1.0
def N2 = 1.0
def N = 10.0
def Input1() = <a^ b>
def Input2() = <c d^>
def Output() = <b c>
def AND() = {a^*}[b c]{d^*}
( 1 * Input1()
| 1 * Input2()
| 10 * AND()
| 0.0 * Output())";
            Interpreter interpreter = new Interpreter(code5);
            interpreter.Run();
        }
        //directive simulation {initial=0; final=1000000; points=1000; plots=[<a^ b>; <c d^>; <b c>]}
        //directive simulation {
        //final=600;
        //plots=[Input1();Input2();Output();Signal()];
        //}

        //directive simulation {initial=0; final=500000; points=1000; plots=[sum([<_ tp^ p>]); sum([<_ tq^ q>]); sum([<_ tr^ r>]); sum([<c tB^ B>]); <c>[tB^ B]{tp^}:[p tq^]:[q ta^]:[a tq^]<q>:[b tq^]<q>:[c tB^]<B>]}
        //directive simulation {initial=0; final=36000; points=10000; plots=[<_ _ _ y1_1>; <_ _ _ y1_0>; <_ _ _ y2_1>; <_ _ _ y2_0>]}
        
        public static void test8()//测试完善前面的功能
        {
            string code = @"directive simulator deterministic
def N1 = 1.0
def N2 = 1.0
def N = 10.0
def Input1() = <a^ b>
def Input2() = <c d^>
def Output() = <b c>
def AND() = {a^*}[b c]{d^*}
( N1 * Input1()
| 1 * Input2()
| 10 * AND()
| 0.0 * Output())";
            Interpreter interpreter = new Interpreter(code);
            interpreter.Run();
            List<reaction3> reactions = interpreter.getReactions();

            for (int i = 0; i < reactions.Count; i++)
            {
                var svg = ComplexPrinter.GetSvg(ReactionPrinter.PrintReaction3(reactions[i]));
                svg.Save($"test_{i}.svg");
                Console.WriteLine(reactions[i]);
            }

        }
        public static void testoscillators()
        {
            string code = @"directive simulation {initial=0; final=500000; points=1000; plots=[sum([<_ tp^ p>]); sum([<_ tq^ q>]); sum([<_ tr^ r>]); sum([<c tB^ B>]); <c>[tB^ B]{tp^}:[p tq^]:[q ta^]:[a tq^]<q>:[b tq^]<q>:[c tB^]<B>]}
directive simulator deterministic
def bind = 0.0003
def unbind = 0.1126
def Buff = 100.0

def BJ2x2(M, N, tx, x, ty, y, tz, z, tw, w) =
( M * {tB^*}[B tx^]:[x ty^]:[y ta^]:[a tz^]<z>:[b tw^]<w>:[c tB^]<B>
| M * <ta^ a tz^ b tw^ c tB^>
| M * [B]{tx^*}
| M * [x]:[ty^ d]:[td^ y]{ta^*}
| M * <d td^>
| M * {td^*}[y]
| M * {ty^*}[d]
| N * <c tB^ B>)

( BJ2x2(Buff,1.0,tp,p,tq,q,tq,q,tq,q)
| BJ2x2(Buff,1.0,tq,q,tr,r,tr,r,tr,r)
| BJ2x2(Buff,1.0,tr,r,tp,p,tp,p,tp,p)
| 3.0 * <hp tp^ p>
| 2.0 * <hq tq^ q>
| 2.0 * <hr tr^ r>)";
            Interpreter interpreter = new Interpreter(code);
            interpreter.Run();
        }
        public static void testPlots()
        {
            string code = @"directive simulation {initial=0; final=500000; points=1000; plots=[sum([<_ tp^ p>]); sum([<_ tq^ q>]); sum([<_ tr^ r>]); sum([<c tB^ B>]); <c>[tB^ B]{tp^}:[p tq^]:[q ta^]:[a tq^]<q>:[b tq^]<q>:[c tB^]<B>]}";
            string code2 = @"directive simulation {initial=0; final=1000000; points=1000; plots=[<a^ b>; <c d^>; <b c>]}";
            string code3 = @"directive simulation {
        final=600;
        plots=[Input1();Input2();Output();Signal()];
        }";
            Interpreter interpreter = new Interpreter(code);
            interpreter.Run();

        }
        
        public static void testCPU(ODEsys odes,double final,Dictionary<int,double>init)
        {
            
            Solver.Solver.solve3(odes,init,0,final);

        }
        public static void testGPU(ODEsys odes, double final, Dictionary<int, double> init)
        {
            Solver.Solver.solve4(odes, init, 0, final);
        }
        public static (long cpuTime, long gpuTime) testCPU_GPU(int count)
        {
            ODEsys tests = ODEsys.GenerateLargeTestSystem(count, count * 4);
            double finalTime = 5;
            Dictionary<int, double> initD = new Dictionary<int, double>();
            int? seedValue = null;
            Random random = seedValue.HasValue ? new Random(seedValue.Value) : new Random();
            for (int i = 0; i < count; i++)
            {
                double value = random.NextDouble();
                initD.Add(i, value);
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            testCPU(tests, finalTime, initD);
            stopwatch.Stop();
            long cpuTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            testGPU(tests, finalTime, initD);
            stopwatch.Stop();
            long gpuTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"CPU Time: {cpuTime} ms, GPU Time: {gpuTime} ms for count {count}");
            return (cpuTime, gpuTime);
        }
        public static void testCG(int index)
        {
            var results = new List<(int count, long cpuTime, long gpuTime)>();
            int MaxCount = 10000;
            for (int i = 4; i <= MaxCount; i *= 2)
            {
                var (cpuTime, gpuTime) = testCPU_GPU(i);
                results.Add((i, cpuTime, gpuTime));
            }
            Workbook workbook = new Workbook();
            Worksheet worksheet = workbook.Worksheets[0];

            worksheet.Range["A1"].Value = "物质数目";
            worksheet.Range["B1"].Value = "CPU时间(ms)";
            worksheet.Range["C1"].Value = "GPU时间(ms)";
            Console.WriteLine("物质数目\tCPU时间(ms)\tGPU时间(ms)");
            foreach (var result in results)
            {
                Console.WriteLine($"{result.count}\t{result.cpuTime}\t{result.gpuTime}");
                worksheet.Range[$"A{results.IndexOf(result) + 2}"].Value2 = result.count;
                worksheet.Range[$"B{results.IndexOf(result) + 2}"].Value2 = result.cpuTime;
                worksheet.Range[$"C{results.IndexOf(result) + 2}"].Value2 = result.gpuTime;
            }
            worksheet.AllocatedRange.AutoFitColumns();
            workbook.SaveToFile($"CPU和GPU求解ODE对比{index}.xlsx", ExcelVersion.Version2016);
        }
        public static void Main()
        {
            string cpuName = GetCpuName();
            string gpuName = GetGpuName();
            Console.WriteLine($"CPU: {cpuName}");
            Console.WriteLine($"GPU: {gpuName}");
            for (int i = 0; i < 3; i++)
            {
                testCG(i);
            }
            Console.WriteLine("测试完成，结果已保存到Excel文件中。");
            Console.ReadLine();
            //rightpaint();
        }
        public static void rightpaint()
        {
            //ComplexNode2 node2 = new();
            //node2.middle = new StrandNode() { seq = new SeqNode() { Value = new DomNode() { Name = new NameNode("a^")} },Type = StrandNode.StrandType.duplex };
            ComplexNode complexNode = new ComplexNode();
            complexNode.Values.Add(new StrandNode() { seq = new SeqNode() { Value = new DomNode() { Name = new NameNode("a"),Type = DomNode.DomType.ToeHold,colour = new NameNode("red") } }, Type = StrandNode.StrandType.duplex });
            complexNode.Values.Add(new StrandNode() { seq = new SeqNode() { Value = new DomNode() { Name = new NameNode("b") } }, Type = StrandNode.StrandType.upper });
            complexNode.Values.Add(new LinkerNode() { Type = LinkerNode.LinkerType.lower});
            SeqNode seq1 = new SeqNode();
            seq1.Value = new List<SeqNode>();
            (seq1.Value as List<SeqNode>).Add(new SeqNode() { Value = new DomNode() { Name = new NameNode("b") } });
            (seq1.Value as List<SeqNode>).Add(new SeqNode() {Value = new DomNode() { Name = new NameNode("c"), Type = DomNode.DomType.ToeHold, colour = new NameNode("blue") } });
            complexNode.Values.Add(new StrandNode() { seq = seq1, Type = StrandNode.StrandType.duplex });
            ComplexNode complex2 = new ComplexNode();
            SeqNode seq2 = new SeqNode();
            seq2.Value = new List<SeqNode>();
            (seq2.Value as List<SeqNode>).Add(new SeqNode() { Value = new DomNode() { Name = new NameNode("a"), Type = DomNode.DomType.ToeHold, colour = new NameNode("red") } });
            (seq2.Value as List<SeqNode>).Add(new SeqNode() { Value = new DomNode() { Name = new NameNode("b") } });
            complex2.Values.Add(new StrandNode() { seq = seq2, Type = StrandNode.StrandType.duplex });
            complex2.Values.Add(new LinkerNode() { Type = LinkerNode.LinkerType.lower });
            complex2.Values.Add(new StrandNode() { seq = new SeqNode() { Value = new DomNode() { Name = new NameNode("b") } }, Type = StrandNode.StrandType.upper });
            complex2.Values.Add(new StrandNode() { seq = new SeqNode() { Value = new DomNode() { Name = new NameNode("c"), Type = DomNode.DomType.ToeHold, colour = new NameNode("blue") } }, Type = StrandNode.StrandType.duplex });
            reaction3 reaction = new reaction3();
            reaction.reactant.Add(new term() { num=1,complex = complexNode});
            reaction.product.Add(new term() { num = 1, complex = complex2 });
            reaction.rate1=0.0003;
            var aaa = ReactionPrinter.PrintReaction3(reaction);
            var svg = ComplexPrinter.GetSvg(ReactionPrinter.PrintReaction3(reaction));
            svg.Save("右向分支迁移.svg");
        }
    }
}

