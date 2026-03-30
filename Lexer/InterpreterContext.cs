using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSDCore
{
    public abstract class InterpreterContext
    {
        virtual public void Printer()
        {
            Console.WriteLine("**********************************************");
        }
    }
    public class SystemSetting : InterpreterContext
    {
        public double InitialTime { get; set; } = 0.0f;
        public double EndTime { get; set; } = 10000;
        public int PlotPoints { get; set; } = 10000;
        public List<int> PlotObjects { get; set; } = new();//初始化的时候将所以物种判别合法性并给予id
        public List<ExpressionNode> plots { get; set; } = new();
        public bool EnableMulticore { get; set; } = false;
        public bool EnableUnproductive { get; set; } = false;//是否启用无效反应
        public bool Prune { get; set; } = true;//是否事先移除不可达的反应来剪枝
        public double Leak { get; set; } = 0.0f;//泄露速率
        public bool DomainDeclaration { get; set; } = false;//是否启用域声明
        public bool RenderMode { get; set; } = false; // true来显示核苷酸
        public double Migration { get; set; } = 8000.0f;
        public double Tau { get; set; } = 0.1126f;
        public int ToeholdLength { get; set; } = 6;
        public int RecognitionLength { get; set; } = 20;
        public double Binding { get; set; } = 0.0003f;//toehold结合速率
        public double Unbing { get; set; } = 0.0003f;//toehold解离速率
        override public void Printer()
        {
            base.Printer();
            Console.WriteLine("SystemSetting:");
            Console.WriteLine("InitialTime: " + InitialTime);
            Console.WriteLine("EndTime: " + EndTime);
            Console.WriteLine("PlotPoints: " + PlotPoints);
            Console.WriteLine("EnableMulticore: " + EnableMulticore);
            Console.WriteLine("Prune: " + Prune);
            Console.WriteLine("Leak: " + Leak);
            Console.WriteLine("DomainDeclaration: " + DomainDeclaration);
            Console.WriteLine("RenderMode: " + RenderMode);
            Console.WriteLine("Migration: " + Migration);
            Console.WriteLine("Tau: " + Tau);
            Console.WriteLine("ToeholdLength: " + ToeholdLength);
            Console.WriteLine("RecognitionLength: " + RecognitionLength);
            Console.WriteLine("Binding: " + Binding);
            Console.WriteLine("Unbing: " + Unbing);
        }
    }
    public class VariableContext : InterpreterContext
    {
        public Dictionary<string, ExpressionNode> variables = new Dictionary<string, ExpressionNode>();//存储数值型变量和域变量，以及函数变量
        public List<string> id = new List<string>();//存储变量的id
        override public void Printer()
        {
            base.Printer();
            Console.WriteLine("VariableContext:");
            foreach (var item in variables)
            {
                Console.WriteLine("Variable: " + item.Key);
                ExpressionPrinter.PrintExpression(item.Value);
            }
        }
    }

    public class SpeciesContext : InterpreterContext
    {
        public Dictionary<BaseComplex, int> species_exist = new();
        public List<BaseComplex> species_id = new();
        public override void Printer()
        {
            base.Printer();
            Console.WriteLine("SpeciesContext:");
            foreach (var item in species_exist)
            {
                ExpressionPrinter.PrintExpression(item.Key);
                Console.WriteLine("ID: " + item.Value);
            }
        }
    }
    public class process
    {
        public double num { get; set; }
        public int id { get; set; }
        public int time { get; set; }

    }
    public class ProcessContext : InterpreterContext
    {
        public List<process> processes = new();
        public Dictionary<int,double> process_init= new();//存储物种id和浓度
        public override void Printer()
        {
            base.Printer();
            Console.WriteLine("ProcessContext:");
            foreach (var item in processes)
            {
                Console.WriteLine("Process: " + item.id);
                Console.WriteLine("Num: " + item.num);
                Console.WriteLine("Time: " + item.time);
            }
        }
    }
    public class reaction
    {

        public double rate1 { get; set; }
        public double rate2 { get; set; }//逆反应速率
        public List<process> reactant { get; set; } = new List<process>();
        public List<process> product { get; set; } = new();
        public override string ToString()
        {
            string reactants = "";
            string products = "";
            foreach (var reactant in reactant)
            {
                reactants += reactant.id.ToString() + " ";
            }
            foreach (var product in product)
            {
                products += product.id.ToString() + " ";
            }
            string arrow = "";
            if (rate2 == 0)
                arrow = $"({rate1})->";
            else
                arrow = $"({rate1})<->({rate2})";
            return $"{reactants} {arrow} {products}";
        }
    }

    public class ReactionContext : InterpreterContext//用来存放所有可能的上下链，假设先完全解离，似乎不需要区分上下链了？或者需要按照toehold来存储链，如果包含多个toehold呢
    {
        public List<ReactionStrand> lowers = new();
        public List<ReactionStrand> uppers = new();
        //public List<ReactionStrand> strands = new();
        public override void Printer()
        {
            base.Printer();
            Console.WriteLine("ReactionContext:");
            Console.WriteLine("\nUpperStrands:");
            foreach (var item in uppers)
            {
                Console.WriteLine("\nid ==" + item.id);
                ExpressionPrinter.PrintExpression(item.strand);
            }
            Console.WriteLine("\nLowerStrands:");
            foreach (var item in lowers)
            {
                Console.WriteLine("\nid ==" + item.id);
                ExpressionPrinter.PrintExpression(item.strand);
            }
        }
        //public List<ReactionStrand> lowers = new();
    }
    public class ReactionStrand : InterpreterContext
    {
        public int id { get; set; }//来自哪个复合物
        public StrandNode strand { get; set; }
        public bool isRotated { get; set; } = false; //是否被旋转
        public Guid guid { get; set; }//唯一标识符，但是旋转后的链共用一个guid

    }
    //public class ComplexMatchInfo
    //{
    //    public int 
    //}

    public class SpecisePoint
    {
        public List<point> points = new();
    }
    public class Results
    {
        public Dictionary<int, SpecisePoint> result = new();//物种id+物种的浓度时间
    }
    public class term
    {
        public int num;
        public BaseComplex complex;
        public override string ToString()
        {
            //TextWriter originalOut = Console.Out;
            //string Complex = "";
            //using (StringWriter stringWriter = new StringWriter())
            //{
            //    Console.SetOut(stringWriter);
            //    ASTPrinter.PrintAst(complex);
            //    Complex = stringWriter.ToString();
            //    Console.SetOut(originalOut);
            //}
            return num.ToString() + " " + complex.ToString();
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj is term other)
            {
                return num == other.num && (complex.Equals(other.complex));
            }
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(num, complex);
        }

        public term DeepCopy()
        {
            return new term { num = this.num, complex = this.complex };
        }
        public bool vaidate()
        {
            if (num < 0)
                return false;
            if (complex == null)
                return false;
            return true;
        }
    }
    public class reaction3
    {
        public List<term> reactant;
        public List<term> product;
        public double rate1;
        public double rate2;
        public reaction3()
        {
            reactant = new List<term>();
            product = new List<term>();
            rate1 = 0;
            rate2 = 0;
        }
        public reaction3(List<term> reactant, List<term> product, double rate1, double rate2)
        {
            this.reactant = reactant;
            this.product = product;
            this.rate1 = rate1;
            this.rate2 = rate2;
        }
        public reaction3(List<term> reactant, List<term> product, double rate1)
        {
            this.reactant = reactant;
            this.product = product;
            this.rate1 = rate1;
            this.rate2 = 0;
        }
        public override string ToString()
        {
            string reactants = "";
            string products = "";
            for (int i = 0; i < reactant.Count; i++)
            {
                reactants += reactant[i].ToString();
                if (i != reactant.Count - 1)
                    reactants += " + ";
            }
            for (int i = 0; i < product.Count; i++)
            {
                products += product[i].ToString();
                if (i != product.Count - 1)
                    products += " + ";
            }
            string arrow = "";
            if (rate2 == 0)
                arrow = $"({rate1.ToString("F4")})->";
            else
                arrow = $"({rate1.ToString("F4")})<->({rate2.ToString("F4")})";
            return $"{reactants} {arrow} {products}";

        }
        public bool vaidate()
        {
            if (reactant == null || product == null)
                return false;
            if (reactant.Count == 0 && product.Count == 0)
                return false;
            if (rate1 < 0 || rate2 < 0)
                return false;
            foreach (var item in reactant)
            {
                if (!item.vaidate())
                    return false;
            }
            foreach (var item in product)
            {
                if (!item.vaidate())
                    return false;
            }
            return true;
        }
    }
    public class Reaction3Comparer : IEqualityComparer<reaction3>
    {
        public bool Equals(reaction3 x, reaction3 y)
        {
            if (x == null || y == null)
                return false;

            return AreTermListsEqual(x.reactant, y.reactant) && AreTermListsEqual(x.product, y.product);
        }

        public int GetHashCode(reaction3 obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var term in obj.reactant.OrderBy(t => t.complex.ToString()))
                    hash = hash * 23 + term.GetHashCode();
                foreach (var term in obj.product.OrderBy(t => t.complex.ToString()))
                    hash = hash * 23 + term.GetHashCode();
                return hash;
            }
        }

        private bool AreTermListsEqual(List<term> list1, List<term> list2)
        {
            if (list1 == null || list2 == null || list1.Count != list2.Count)
                return false;

            var sorted1 = list1.OrderBy(t => t.GetHashCode()).ToList();
            var sorted2 = list2.OrderBy(t => t.GetHashCode()).ToList();

            for (int i = 0; i < sorted1.Count; i++)
            {
                if (!sorted1[i].Equals(sorted2[i]))
                    return false;
            }

            return true;
        }
    }
}
