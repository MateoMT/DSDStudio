using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODE;
using PaintUtils;

namespace DSDCore
{
    //public 
    public class DSDCore
    {
        public string CodeContent { get; set; }
        private Interpreter Interpreter { get; set; }
        public DSDCore(string codeContent)
        {
            Interpreter = new Interpreter(codeContent);
            Interpreter.Run();

        }
        public Errors GetErrors()
        {
            return Interpreter.getErrors();
        }
        public List<double[]> Solve()
        {
            return Interpreter.solve();
        }
        public (List<double>, List<double[]>) SolveWithTime()
        {
            return Interpreter.solve3();
        }
        public List<SvgGenerator> GetSvgs()//将所有的反应转换成svg返回。
        {
            List < SvgGenerator > svgs = new List<SvgGenerator>();
            foreach (var reaction in Interpreter.getReactions())
            {
                var svg = ComplexPrinter.GetSvg(ReactionPrinter.PrintReaction3(reaction));
                svgs.Add(svg);
            }
            return svgs;
        }
        public ODEsys GetODEsys()
        {
            return Interpreter.getODEsys();
        }
    }
}
