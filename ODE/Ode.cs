using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODE
{
    public class reaction4
    {
        public double rate1 { get; set; }
        public double rate2 { get; set; }//逆反应速率
        public Dictionary<int, int> reactant { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> product { get; set; } = new Dictionary<int, int>();
        public override string ToString()
        {
            string reactants = "";
            string products = "";
            foreach (var reactant in reactant)
            {
                reactants += reactant.Value.ToString() + "*" + reactant.Key.ToString() + " ";
            }
            foreach (var product in product)
            {
                products += product.Value.ToString() + "*" + product.Key.ToString() + " ";
            }
            string arrow = "";
            if (rate2 == 0)
                arrow = $"({rate1})->";
            else
                arrow = $"({rate1})<->({rate2})";
            return $"{reactants} {arrow} {products}";
        }
    }
    public class ODEterm//一个ODE项
    {
        public double k { set; get; }
        public Dictionary<int, int> factors { get; set; }

        public ODEterm(double k, Dictionary<int, int> factors)
        {
            this.k = k;
            this.factors = factors;
        }
        public double Calculate(Dictionary<int, double> concentrations)
        {
            double result = k;
            foreach (var factor in factors)
            {
                if (concentrations.ContainsKey(factor.Key))
                {
                    result *= Math.Pow(concentrations[factor.Key], factor.Value);
                }
                else
                {
                    return 0;
                }
            }
            return result;
        }
        public string ToLatex(Dictionary<int, string> names)
        {
            if (k == 0)
                return "";
            StringBuilder sb = new StringBuilder();

            double absk = Math.Abs(k);
            if (absk != 1.0 || factors.Count == 0)
                sb.Append(absk.ToString("F4"));

            foreach (var factor in factors)
            {
                sb.Append("[");
                sb.Append(names[factor.Key]);
                sb.Append("]");

                if (factor.Value > 1)
                    sb.Append($"^{{{factor.Value}}}");
            }

            return sb.ToString();
        }
    }
    public class ODEequation //单个ODE方程
    {
        public int id { set; get; }
        public List<ODEterm> terms { set; get; }
        public ODEequation(int id, List<ODEterm> terms)
        {
            this.id = id;
            this.terms = terms;
        }
        public void AddTerm(ODEterm term)
        {
            terms.Add(term);
        }
        public double Calculate(Dictionary<int, double> concentrations)
        {
            return terms.Sum(term => term.Calculate(concentrations));
        }
        public string ToLatex(Dictionary<int, string> names)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"\\frac{{d[{names[id]}]}}{{dt}} = ");

            bool isFirst = true;
            foreach (var term in terms)
            {
                if (term.k == 0)
                    continue;
                if (!isFirst)
                {
                    if (term.k > 0)
                        sb.Append(" + ");
                    else
                        sb.Append("-");
                }
                else
                {
                    isFirst = false;
                    if (term.k < 0)
                        sb.Append("-");

                }
                sb.Append(term.ToLatex(names));
            }
            return sb.ToString();
        }
    }
    public class ODEsys
    {
        public Dictionary<int, ODEequation> equations { set; get; }
        public Dictionary<int, string> names { set; get; }
        public Dictionary<int, string> names2 { set; get; } = new Dictionary<int, string>();
        public ODEsys(Dictionary<int, string> names)
        {
            this.names = names;
            equations = new Dictionary<int, ODEequation>();
            foreach (var name in names)
            {
                equations[name.Key] = new ODEequation(name.Key, new List<ODEterm>());
            }
        }

        public static ODEsys CreateFromR4(List<reaction4> reactions, Dictionary<int, string> names)
        {
            ODEsys system = new ODEsys(names);
            foreach (var reaction in reactions)
            {
                //Console.WriteLine(reaction.ToString());
                double rate1 = reaction.rate1;
                double rate2 = reaction.rate2;
                //反应物及其指数
                Dictionary<int, int> factors1 = new Dictionary<int, int>();
                foreach (var reactant in reaction.reactant)
                {
                    factors1[reactant.Key] = reactant.Value;
                }
                //产物及其指数
                Dictionary<int, int> factors2 = new Dictionary<int, int>();
                foreach (var product in reaction.product)
                {
                    factors2[product.Key] = product.Value;
                }
                //更新反应物
                foreach (var reactant in reaction.reactant)
                {
                    int id = reactant.Key;
                    int k = reactant.Value;
                    // 减去正向反应速率项
                    AddTermIfNonZero(system.equations[id], -k * rate1, factors1);
                    // 加上逆向反应速率项
                    AddTermIfNonZero(system.equations[id], k * rate2, factors2);
                }
                //更新产物
                foreach (var product in reaction.product)
                {
                    int id = product.Key;
                    int k = product.Value;
                    //加上正向反应速率项
                    AddTermIfNonZero(system.equations[id], k * rate1, factors1);
                    // 减去逆向反应速率项
                    AddTermIfNonZero(system.equations[id], -k * rate2, factors2);
                }
            }
            return system;
        }

        private static void AddTermIfNonZero(ODEequation equation, double coefficient, Dictionary<int, int> factors)
        {
            if (coefficient == 0)
                return;

            equation.AddTerm(new ODEterm(coefficient, new Dictionary<int, int>(factors)));
        }
        public string ToLatex()
        {
            StringBuilder sb = new StringBuilder();
            if (names2 != null)
            {
                foreach (var pair in names)
                {
                    if (names2.ContainsKey(pair.Key))
                    {
                        string escapedName = EscapeLatexChars(names2[pair.Key]);
                        sb.AppendLine($"{pair.Value} = {escapedName} \\\\");
                    }
                }
                sb.AppendLine("\\\\");
            }
            foreach (var equation in equations.Values)
            {
                sb.AppendLine(equation.ToLatex(names) + " \\\\");
            }
            return sb.ToString();
        }
        private string EscapeLatexChars(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return input
                .Replace("\\", "\\backslash ")
                .Replace("^", "^\\wedge ")
                .Replace("~", "\\sim ")
                .Replace("*", "\\ast ")
                .Replace("&", "\\&")
                .Replace("%", "\\%")
                .Replace("$", "\\$")
                .Replace("#", "\\#")
                .Replace("_", "\\_")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace(" ", "\\; ")
                ;
        }
        public Func<double, double[], double[]> GetRightHandSide()
        {
            return (t, y) =>
            {
                // 假设y的顺序与Equations中物质的顺序相同
                Dictionary<int, double> concentrations = new Dictionary<int, double>();
                int i = 0;
                foreach (int id in equations.Keys)
                {
                    concentrations[id] = y[i++];
                }

                double[] dydt = new double[y.Length];
                i = 0;
                foreach (int id in equations.Keys)
                {
                    dydt[i++] = equations[id].Calculate(concentrations);
                }

                return dydt;
            };
        }
        public static ODEsys GenerateParallelTestSystem(int substanceCount = 1000, int reactionCount = 2000,
                                                        int? seedValue = null, double maxRate = 1.0,
                                                        int groupSize = 4)
        {
            Random random = seedValue.HasValue ? new Random(seedValue.Value) : new Random();

            Dictionary<int, string> names = new Dictionary<int, string>();
            for (int i = 0; i < substanceCount; i++)
            {
                names[i] = $"S{i}";
            }

            List<reaction4> reactions = new List<reaction4>();
            if (substanceCount <= 0)
                return CreateFromR4(reactions, names);

            double minRate = Math.Min(0.001, maxRate);
            groupSize = Math.Max(1, groupSize);
            for (int i = 0; i < reactionCount; i++)
            {
                int groupStart = (i % substanceCount) / groupSize * groupSize;
                int groupLength = Math.Min(groupSize, substanceCount - groupStart);
                int typeCount = groupLength == 1 ? 2 : groupLength == 2 ? 4 : 8;
                int type = random.Next(typeCount);
                int a = PickLocalSubstance(groupStart, groupLength);
                reaction4 reaction = new reaction4
                {
                    rate1 = Rate(1.0),
                    rate2 = 0
                };

                switch (type)
                {
                    case 0:
                        // Source: empty set -> A.
                        reaction.rate1 = Rate(0.2);
                        reaction.product[a] = 1;
                        break;
                    case 1:
                        // Decay: A -> empty set.
                        reaction.reactant[a] = 1;
                        break;
                    case 2:
                        // Local first-order conversion: A -> B.
                        int b = PickDifferentLocalSubstance(groupStart, groupLength, a);
                        reaction.rate1 = Rate(0.4);
                        reaction.reactant[a] = 1;
                        reaction.product[b] = 1;
                        break;
                    case 3:
                        // Local reversible conversion: A <-> B.
                        b = PickDifferentLocalSubstance(groupStart, groupLength, a);
                        reaction.rate1 = Rate(0.3);
                        reaction.rate2 = Rate(0.3);
                        reaction.reactant[a] = 1;
                        reaction.product[b] = 1;
                        break;
                    case 4:
                        // Local association: A + B -> C.
                        PickDifferentLocalTriple(groupStart, groupLength, out a, out b, out int c);
                        reaction.rate1 = Rate(0.02);
                        reaction.reactant[a] = 1;
                        reaction.reactant[b] = 1;
                        reaction.product[c] = 1;
                        break;
                    case 5:
                        // Local dissociation: A -> B + C.
                        PickDifferentLocalTriple(groupStart, groupLength, out a, out b, out c);
                        reaction.rate1 = Rate(0.05);
                        reaction.reactant[a] = 1;
                        reaction.product[b] = 1;
                        reaction.product[c] = 1;
                        break;
                    case 6:
                        // Local catalytic conversion: A + B -> A + C.
                        PickDifferentLocalTriple(groupStart, groupLength, out a, out b, out c);
                        reaction.rate1 = Rate(0.02);
                        reaction.reactant[a] = 1;
                        reaction.reactant[b] = 1;
                        reaction.product[a] = 1;
                        reaction.product[c] = 1;
                        break;
                    default:
                        // Local reversible association: A + B <-> C.
                        PickDifferentLocalTriple(groupStart, groupLength, out a, out b, out c);
                        reaction.rate1 = Rate(0.01);
                        reaction.rate2 = Rate(0.05);
                        reaction.reactant[a] = 1;
                        reaction.reactant[b] = 1;
                        reaction.product[c] = 1;
                        break;
                }

                reactions.Add(reaction);
            }

            return CreateFromR4(reactions, names);

            double Rate(double scale)
            {
                double upper = Math.Max(minRate, maxRate * scale);
                return Math.Round(minRate + random.NextDouble() * (upper - minRate), 6);
            }

            int PickLocalSubstance(int start, int length)
            {
                return start + random.Next(length);
            }

            int PickDifferentLocalSubstance(int start, int length, int first)
            {
                int next = start + random.Next(length - 1);
                if (next >= first)
                    next++;
                return next;
            }

            void PickDifferentLocalTriple(int start, int length, out int first, out int second, out int third)
            {
                first = PickLocalSubstance(start, length);
                second = PickDifferentLocalSubstance(start, length, first);
                do
                {
                    third = PickLocalSubstance(start, length);
                } while (third == first || third == second);
            }
        }
    }
    

    public class GPU_ODE_Data
    {
        public double[] TermCoefficients = [];       // 所有项的系数
        public int[] TermFactorsStart = [];          // 每个项的因子起始索引
        public int[] TermFactorsLength = [];         // 每个项的因子数量
        public int[] FactorSubstanceIds = [];        // 因子对应的物质ID
        public int[] FactorExponents = [];           // 因子对应的指数
        public int[] EquationTermStart = [];         // 每个物质的项起始索引
        public int[] EquationTermCount = [];         // 每个物质的项数量
        public int TotalTerms;                  // 总项数
        public int SubstanceCount;              // 物质总数
        public static GPU_ODE_Data PrepareGPUData(ODEsys system)
        {
            var data = new GPU_ODE_Data();
            var substances = system.equations.Keys.ToList();
            int substanceCount = substances.Count;
            data.SubstanceCount = substanceCount;

            // 初始化列表
            List<double> termCoeffList = new List<double>();
            List<int> termFactorsStartList = new List<int>();
            List<int> termFactorsLengthList = new List<int>();
            List<int> factorSubstanceList = new List<int>();
            List<int> factorExponentList = new List<int>();
            List<int> equationTermStartList = new List<int>();
            List<int> equationTermCountList = new List<int>();

            int totalFactors = 0;
            int totalTerms = 0;

            // 遍历每个物质的方程
            for (int i = 0; i < substanceCount; i++)
            {
                int substanceId = substances[i];
                var equation = system.equations[substanceId];

                equationTermStartList.Add(totalTerms);
                equationTermCountList.Add(equation.terms.Count);

                foreach (var term in equation.terms)
                {
                    // 添加项系数
                    termCoeffList.Add(term.k);

                    // 添加因子信息
                    termFactorsStartList.Add(totalFactors);
                    termFactorsLengthList.Add(term.factors.Count);

                    foreach (var factor in term.factors)
                    {
                        factorSubstanceList.Add(factor.Key);
                        factorExponentList.Add(factor.Value);
                        totalFactors++;
                    }

                    totalTerms++;
                }
            }

            // 转换为数组
            data.TermCoefficients = termCoeffList.ToArray();
            data.TermFactorsStart = termFactorsStartList.ToArray();
            data.TermFactorsLength = termFactorsLengthList.ToArray();
            data.FactorSubstanceIds = factorSubstanceList.ToArray();
            data.FactorExponents = factorExponentList.ToArray();
            data.EquationTermStart = equationTermStartList.ToArray();
            data.EquationTermCount = equationTermCountList.ToArray();
            data.TotalTerms = totalTerms;

            return data;
        }
    }
}
