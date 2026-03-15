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
        public Dictionary<int, string> names2 { set; get; }
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
                    system.equations[id].AddTerm(new ODEterm(-k * rate1, new Dictionary<int, int>(factors1)));
                    // 加上逆向反应速率项
                    system.equations[id].AddTerm(new ODEterm(k * rate2, new Dictionary<int, int>(factors2)));
                }
                //更新产物
                foreach (var product in reaction.product)
                {
                    int id = product.Key;
                    int k = product.Value;
                    //加上正向反应速率项
                    system.equations[id].AddTerm(new ODEterm(k * rate1, new Dictionary<int, int>(factors1)));
                    // 减去逆向反应速率项
                    system.equations[id].AddTerm(new ODEterm(-k * rate2, new Dictionary<int, int>(factors2)));
                }
            }
            return system;
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
        public static ODEsys GenerateLargeTestSystem(int substanceCount = 1000, int reactionCount = 2000,
                                                    int maxReactants = 3, int maxProducts = 3, int? seedValue = null)
        {
            Random random = seedValue.HasValue ? new Random(seedValue.Value) : new Random();

            // 创建物质名称字典
            Dictionary<int, string> names = new Dictionary<int, string>();
            for (int i = 0; i < substanceCount; i++)
            {
                names[i] = $"S{i}";
            }

            // 创建反应列表
            List<reaction4> reactions = new List<reaction4>();

            for (int i = 0; i < reactionCount; i++)
            {
                reaction4 reaction = new reaction4
                {
                    rate1 = Math.Round(random.NextDouble() * 10, 4),  // 正向反应速率 0-10
                    rate2 = random.NextDouble() < 0.7 ? Math.Round(random.NextDouble() * 5, 4) : 0  // 70%概率有逆反应
                };

                // 添加随机反应物
                int reactantCount = random.Next(1, maxReactants + 1);
                for (int j = 0; j < reactantCount; j++)
                {
                    int substance = random.Next(substanceCount);
                    if (!reaction.reactant.ContainsKey(substance))
                    {
                        reaction.reactant[substance] = random.Next(1, 4);  // 系数在1-3之间
                    }
                }

                // 添加随机产物
                int productCount = random.Next(1, maxProducts + 1);
                for (int j = 0; j < productCount; j++)
                {
                    int substance = random.Next(substanceCount);
                    // 不要让同一个物质同时做反应物和产物
                    if (!reaction.product.ContainsKey(substance) && !reaction.reactant.ContainsKey(substance))
                    {
                        reaction.product[substance] = random.Next(1, 4);  // 系数在1-3之间
                    }
                }

                // 确保至少有一个反应物和一个产物
                if (reaction.reactant.Count > 0 && reaction.product.Count > 0)
                {
                    reactions.Add(reaction);
                }
            }
            return CreateFromR4(reactions, names);
        }
    }
    

    public class GPU_ODE_Data
    {
        public double[] TermCoefficients;       // 所有项的系数
        public int[] TermFactorsStart;          // 每个项的因子起始索引
        public int[] TermFactorsLength;         // 每个项的因子数量
        public int[] FactorSubstanceIds;        // 因子对应的物质ID
        public int[] FactorExponents;           // 因子对应的指数
        public int[] EquationTermStart;         // 每个物质的项起始索引
        public int[] EquationTermCount;         // 每个物质的项数量
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
