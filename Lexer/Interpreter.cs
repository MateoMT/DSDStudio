
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using DSDCore;
using ILGPU.IR.Values;
using static System.Runtime.InteropServices.JavaScript.JSType;
using PaintUtils;
using System.Diagnostics;
using ODE;

namespace DSDCore
{
    internal class Interpreter//用于解释执行
    {
        private ProgramNode root;
        private ReactionContext reactionContext = new();//存储反应
        private SystemSetting systemSetting = new();//存储系统设置
        private VariableContext variableContext = new();//存储变量和函数
        private SpeciesContext speciesContext = new();//存储出现过的物种
        private ProcessContext processContext = new();//num, id，存储在反应体系中的物种
        private Dictionary<int, List<matchInfo>> ComplexMatch = new();//存储复合物的匹配信息
        private Errors errors = new();
        private Results results = new();
        private string code;
        private List<StrandNode> thisStrands = new List<StrandNode>();
        private List<ComplexNode> thisComplexs = new List<ComplexNode>();
        private Dictionary<int, List<BaseComplex>> ComUnlink = new();
        //private Dictionary<tmpss,tmpss> hasDones = new ();
        private List<int> initial_species = new();
        private Dictionary<int, List<BaseComplex>> rights = new();
        private ODEsys odes;
        private bool hasInit = false;
        private bool hasCRN = false;
        private List<reaction3> Reactions = new();
        HashSet<string> usedColors = new HashSet<string>();
        List<string> colorss = new()//各种svg支持的颜色
        {
            "red", "blue", "green", "purple", "orange",
            "teal", "magenta", "brown", "navy", "olive",
            "maroon", "cyan", "gold", "indigo", "coral",
             "crimson", "forestgreen", "royalblue", "darkviolet", "darkorange",
            "deepskyblue", "limegreen", "mediumvioletred", "dodgerblue", "firebrick",
            "darkturquoise", "goldenrod", "darkmagenta", "steelblue", "chocolate",
            "seagreen", "tomato", "slateblue", "darkgoldenrod", "darkcyan",
            "hotpink", "yellowgreen", "midnightblue", "darkslategray", "cadetblue",
            "darkkhaki", "mediumorchid", "sienna", "mediumturquoise", "saddlebrown",
            "mediumseagreen", "indianred", "mediumslateblue", "olivedrab", "palevioletred",
            "peru", "darkslateblue", "lightseagreen", "darkred", "mediumaquamarine",
            "darkolivegreen", "salmon", "springgreen", "slategray", "darkgreen"
        };


        Dictionary<int,string> colors = new();
        public List<reaction3>  getReactions()
        {
            return [.. Reactions];
        }
        public bool isInit()
        {
            return hasInit;
        }
        public bool isCRN()
        {
            return hasCRN;
        }
        public Errors getErrors()
        {
            return errors;
        }
        public ODEsys getODEsys()
        {
            return odes;
        }
        readonly struct re
        {
            public readonly int id1;
            public readonly int id2;
            public re(int a, int b)
            {
                id1 = a;
                id2 = b;
            }
        }
        private Dictionary<re, bool> hasDone = new();
        public Interpreter()
        {
        }
        public Interpreter(string code)
        {
            this.code = code;
        }
        //这里没有判断是否是相同的名字不同物种的
        private void addColor(BaseComplex complex)//给复合物根据变量中的dom颜色上色，在绘制之前要调用。顺便把绑定率什么的都放进去吧
        {
            switch (complex)
            {
                case DomNode dom:
                    string name = dom.Name.GetValue() as string;
#if DEBUG
                    Console.WriteLine($"当前域的名字为{name}");

#endif
                    if (variableContext.variables.ContainsKey(name))
                    {
                        if (variableContext.variables[name] is DomNode dom1 && dom1.isToe())
                        {
                            dom.colour = dom1.colour;
                            dom.seq = dom1.seq;
                            dom.bind = dom1.bind;
                            dom.unbind = dom1.unbind;
#if DEBUG
                            Console.WriteLine($"当前域的颜色为{dom.colour.ToString()}");
#endif
                        }
                        else
                        {
                            errors.AddError(dom.Line, dom.Column, "域变量不是域");
                        }
                    }
                    else
                    {

                    }
                    break;
                case SeqNode seq:
                    if (seq.Value is DomNode)
                    {
                        addColor(seq.Value as DomNode);
                    }
                    else if (seq.Value is List<SeqNode> seqs)
                    {
                        foreach (var item in seqs)
                            addColor(item);
                    }
                    break;
                case StrandNode strand:
                    if (strand.seq is not null)
                    {
                        addColor(strand.seq);
                    }
                    break;
                case ComplexNode complexNode:
                    foreach (var item in complexNode.Values)
                    {
                        if (item is not LinkerNode)
                            addColor(item);
                    }
                    break;
            }

        }
        private int AddNewSpecies(BaseComplex species0)//检测是否合法并加入进去，合法或者有的话则返回id，否则返回-1
        {
            Console.WriteLine($"\n添加新物种,{species0.GetType()}");
            ExpressionPrinter.PrintExpression(species0);
            var species = Utils.getBaseComplex(species0);
            ASTPrinter.PrintAst(species);
            switch (species)
            {
                case StrandNode s://链也想不到还有什么不合法的地方
                    //这里需要把dom的属性赋进去
                    if (speciesContext.species_exist.ContainsKey(s))
                    {
                        return speciesContext.species_exist[s];
                    }
                    if (speciesContext.species_exist.ContainsKey(s.Rotate()))
                    {
                        return speciesContext.species_exist[s.Rotate()];
                    }
                    speciesContext.species_id.Add(s);
                    speciesContext.species_exist.Add(s, speciesContext.species_id.Count - 1);
                    if (s.Type == StrandNode.StrandType.lower)
                    {
                        var guid = Guid.NewGuid();
                        reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.DeepCopy() as StrandNode, guid = guid });//后面还需要增加一个彻底反过来的，就是当前链作为上链(里面的域是反的)
                        reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.Rotate().DeepCopy() as StrandNode, isRotated = true, guid = guid });
                    }
                    else if (s.Type == StrandNode.StrandType.upper)
                    {
                        var guid = Guid.NewGuid();
                        reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s, guid = guid });
                        reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.Rotate().DeepCopy() as StrandNode, isRotated = true, guid = guid });
                    }
                    else
                    {
                        var guid = Guid.NewGuid();
                        reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = s.seq, Type = StrandNode.StrandType.upper }, guid = guid });
                        reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = s.seq.Rotate(), Type = StrandNode.StrandType.lower }, isRotated = true, guid = guid });
                        var guid2 = Guid.NewGuid();
                        reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = ((SeqNode)s.seq.DeepCopy()).GetRevComp(), Type = StrandNode.StrandType.lower }, guid = guid2 });
                        reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = ((SeqNode)s.seq.DeepCopy()).GetRevComp().Rotate(), Type = StrandNode.StrandType.upper }, isRotated = true, guid = guid2 });
                    }
                    return speciesContext.species_id.Count - 1;
                case DomNode d://似乎想不到dom会有什么不合法的地方
                    if (speciesContext.species_exist.ContainsKey(d))
                    {
                        return speciesContext.species_exist[d];
                    }
                    speciesContext.species_id.Add(d);
                    return speciesContext.species_id.Count - 1;
                case ComplexNode c:
                    //c = Normalize(c);//左右分支迁移不变
                    var cc = rightMigration(c);
                    if(cc.Count==1)
                        c = Normalize(c);
                    if (speciesContext.species_exist.ContainsKey(c))
                    {
                        return speciesContext.species_exist[c];
                    }
                    if (speciesContext.species_exist.ContainsKey(c.Rotate()))
                    {
                        return speciesContext.species_exist[c.Rotate()];
                    }
                    if (checkComplex(c))
                    {
                        return speciesContext.species_exist[c];
                    }
                    Console.WriteLine($"!!!!!!!!!!!!!!!!!!!复合物不合法");
                    ASTPrinter.PrintAst(c);
                    return -1;//不合法
            }
            return 0;
        }
        private bool checkComplex(ComplexNode complex0, bool add = true)//回头看，向后看
        {
            ComplexNode complex = (ComplexNode)complex0.DeepCopy();
            if (speciesContext.species_exist.ContainsKey(complex)) return true;
            StrandNode lastupper = null, lastlower = null;//用来同之前的链相连接
            List<StrandNode> upper = [], lower = [];
            int i = 0, lastlinker = -1;
            List<BaseComplex> list = (complex.Values);
            while (i < list.Count)
            {
                while (i < list.Count && !(list[i] is StrandNode && ((StrandNode)(list[i])).Type == StrandNode.StrandType.duplex)) i++;//找到第一个双链
                if (i == list.Count) break;//没有找到双链，直接返回
                if (i - lastlinker - 1 > 2)
                {
                    ////一个[]前面最多一个上链一个下链
                    return false;
                }
                //往前看

                if (lastlinker > -1)
                {
                    LinkerNode linker = list[lastlinker] as LinkerNode;
                    if (linker.Type == LinkerNode.LinkerType.lower)//说明之前的上链终结了
                    {
                        upper.Add(lastupper);
                        lastupper = null;
                    }
                    else if (linker.Type == LinkerNode.LinkerType.upper)
                    {
                        lower.Add(lastlower);
                        lastlower = null;
                    }
                }
                int up = 0, low = 0;
                for (int j = lastlinker + 1; j < i; j++)
                {
                    if (list[j] is StrandNode)
                    {
                        StrandNode node = list[j].DeepCopy() as StrandNode;
                        if (node.Type == StrandNode.StrandType.upper)
                        {
                            if (lastupper is null)
                            {
                                lastupper = new StrandNode() { Type = StrandNode.StrandType.upper, seq = node.seq.addLocation(j).DeepCopy() as SeqNode };//如果没有上链，就新建一个

                            }
                            else lastupper.link(node.seq, j);//有上链就连接
                            lastupper.AddSome(node.GetLength(), false);//这些没有碱基
                            up++;
                        }
                        else if (node.Type == StrandNode.StrandType.lower)
                        {
                            if (lastlower is null) lastlower = new StrandNode() { Type = StrandNode.StrandType.lower, seq = node.seq.addLocation(j).DeepCopy() as SeqNode };
                            else lastlower.link(node.seq, j);
                            lastlower.AddSome(node.GetLength(), false);
                            low++;
                        }
                        else
                        {
                            //errors.AddError("复合物中出现了奇怪的元素");
                            return false;
                        }
                    }
                    else
                    {
                        //errors.AddError("复合物中出现了奇怪的元素");
                        return false;
                    }
                }
                if (up > 1 || low > 1)
                {
                    //前面最多一个上链一个下链
                    return false;

                }
                //处理[]
                StrandNode strand = list[i].DeepCopy() as StrandNode;
                if (lastupper is null) lastupper = new StrandNode() { Type = StrandNode.StrandType.upper, seq = strand.seq.addLocation(i) };//如果没有上链，就新建一个
                else lastupper.link(strand.seq, i);//有上链就连接
                if (lastlower is null) lastlower = new StrandNode() { Type = StrandNode.StrandType.lower, seq = strand.seq.GetRevComp().addLocation(i) };
                else lastlower.link(strand.seq.GetRevComp(), i);
                //设置链的位情况
                lastupper.AddSome(strand.GetLength(), true);
                lastlower.AddSome(strand.GetLength(), true);
                //往后看
                up = 0; low = 0;
                for (int j = 1; i + j < list.Count && j <= 3; j++)//往后最多看3个字符
                {
                    if (list[j + i] is StrandNode)
                    {
                        StrandNode node = list[i + j].DeepCopy() as StrandNode;
                        if (node.Type == StrandNode.StrandType.upper)
                        {
                            lastupper.link(node.seq, j);
                            up++;
                            lastupper.AddSome(node.GetLength(), false);
                        }
                        else if (node.Type == StrandNode.StrandType.lower)
                        {
                            lastlower.link(node.seq, j);
                            low++;
                            lastlower.AddSome(node.GetLength(), false);
                        }
                        else//可以排除出现的多链
                        {
                            //errors.AddError("复合物中出现了奇怪的元素");
                            return false;
                        }
                    }
                    else if (list[j + i] is LinkerNode)
                    {
                        lastlinker = i + j;
                        i = lastlinker;//跳过连接符
                        break;
                    }
                }
                if (up > 1 || low > 1)//似乎已经可以排除后面3个全是链的情况
                {
                    //前面最多一个上链一个下链
                    return false;
                }
                i++;//就算没有找到下一个连接符，往后+1也能跳过当前的[]
            }
            if (add == false)//可以不往当前库里添加。
            {
                return true;
            }
            //这里处理最后剩下的链们
            //complex0 = Normalize(complex0);
            speciesContext.species_id.Add(complex0);
            speciesContext.species_exist.Add(complex0, speciesContext.species_id.Count - 1);
            //Console.WriteLine($"当前物种id为:{speciesContext.species_exist[complex0]}");
            speciesContext.Printer();
            if(speciesContext.species_exist[complex0]==7)
            {
                Console.WriteLine($"当前物种id为:{speciesContext.species_exist[complex0]}");
            }
            ASTPrinter.PrintAst(complex0);
            if (lastlower is not null)
            {
                var guid = Guid.NewGuid();
                reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastlower, guid = guid });
                reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastlower.Rotate(), isRotated = true, guid = guid });
            }
            if (lastupper is not null)
            {
                var guid = Guid.NewGuid();
                reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastupper, guid = guid });
                reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastupper.Rotate(), isRotated = true, guid = guid });
            }
            foreach (var item in upper)
            {
                var guid = Guid.NewGuid();
                reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item, guid = guid });
                reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item.Rotate(), isRotated = true, guid = guid });
            }
            foreach (var item in lower)
            {
                var guid = Guid.NewGuid();
                reactionContext.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item, guid = guid });
                reactionContext.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item.Rotate(), isRotated = true, guid = guid });
            }
            return true;
        }
        private ReactionContext getUperandLower(int id)
        {
            BaseComplex species = speciesContext.species_id[id].DeepCopy();
            ReactionContext context = new();
            if (species is StrandNode s)
            {
                if (s.Type == StrandNode.StrandType.lower)
                {
                    var guid = Guid.NewGuid();
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.DeepCopy() as StrandNode, guid = guid });//后面还需要增加一个彻底反过来的，就是当前链作为上链(里面的域是反的)
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.Rotate().DeepCopy() as StrandNode, isRotated = true, guid = guid });
                }
                else if (s.Type == StrandNode.StrandType.upper)
                {
                    var guid = Guid.NewGuid();
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s, guid = guid });
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = s.Rotate().DeepCopy() as StrandNode, isRotated = true, guid = guid });
                }
                else
                {
                    var guid = Guid.NewGuid();
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = s.seq, Type = StrandNode.StrandType.upper }, guid = guid });
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = s.seq.Rotate(), Type = StrandNode.StrandType.lower }, isRotated = true, guid = guid });
                    var guid2 = Guid.NewGuid();
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = ((SeqNode)s.seq.DeepCopy()).GetRevComp(), Type = StrandNode.StrandType.lower }, guid = guid2 });
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[s], strand = new StrandNode() { seq = ((SeqNode)s.seq.DeepCopy()).GetRevComp().Rotate(), Type = StrandNode.StrandType.upper }, isRotated = true, guid = guid2 });
                }
            }
            else if (species is ComplexNode complex)
            {
                ComplexNode complex0 = (ComplexNode)complex.DeepCopy();
                StrandNode lastupper = null, lastlower = null;//用来同之前的链相连接
                List<StrandNode> upper = [], lower = [];
                int i = 0, lastlinker = -1;
                List<BaseComplex> list = (complex.Values);
                while (i < list.Count)
                {
                    while (i < list.Count && !(list[i] is StrandNode && ((StrandNode)(list[i])).Type == StrandNode.StrandType.duplex)) i++;//找到第一个双链
                    if (i == list.Count) break;//没有找到双链，直接返回
                    if (i - lastlinker - 1 > 2)
                    {
                        ////一个[]前面最多一个上链一个下链
                        return null;
                    }
                    //往前看

                    if (lastlinker > -1)
                    {
                        LinkerNode linker = list[lastlinker] as LinkerNode;
                        if (linker.Type == LinkerNode.LinkerType.lower)//说明之前的上链终结了
                        {
                            upper.Add(lastupper);
                            lastupper = null;
                        }
                        else if (linker.Type == LinkerNode.LinkerType.upper)
                        {
                            lower.Add(lastlower);
                            lastlower = null;
                        }
                    }
                    int up = 0, low = 0;
                    for (int j = lastlinker + 1; j < i; j++)
                    {
                        if (list[j] is StrandNode)
                        {
                            StrandNode node = list[j].DeepCopy() as StrandNode;
                            if (node.Type == StrandNode.StrandType.upper)
                            {
                                if (lastupper is null)
                                {
                                    lastupper = new StrandNode() { Type = StrandNode.StrandType.upper, seq = node.seq.addLocation(j).DeepCopy() as SeqNode };//如果没有上链，就新建一个

                                }
                                else lastupper.link(node.seq, j);//有上链就连接
                                lastupper.AddSome(node.GetLength(), false);//这些没有碱基
                                up++;
                            }
                            else if (node.Type == StrandNode.StrandType.lower)
                            {
                                if (lastlower is null) lastlower = new StrandNode() { Type = StrandNode.StrandType.lower, seq = node.seq.addLocation(j).DeepCopy() as SeqNode };
                                else lastlower.link(node.seq, j);
                                lastlower.AddSome(node.GetLength(), false);
                                low++;
                            }
                            else
                            {
                                //errors.AddError("复合物中出现了奇怪的元素");
                                return null;
                            }
                        }
                        else
                        {
                            //errors.AddError("复合物中出现了奇怪的元素");
                            return null;
                        }
                    }
                    if (up > 1 || low > 1)
                    {
                        //前面最多一个上链一个下链
                        return null;

                    }
                    //处理[]
                    StrandNode strand = list[i].DeepCopy() as StrandNode;
                    if (lastupper is null) lastupper = new StrandNode() { Type = StrandNode.StrandType.upper, seq = strand.seq.addLocation(i) };//如果没有上链，就新建一个
                    else lastupper.link(strand.seq, i);//有上链就连接
                    if (lastlower is null) lastlower = new StrandNode() { Type = StrandNode.StrandType.lower, seq = strand.seq.GetRevComp().addLocation(i) };
                    else lastlower.link(strand.seq.GetRevComp(), i);
                    //设置链的位情况
                    lastupper.AddSome(strand.GetLength(), true);
                    lastlower.AddSome(strand.GetLength(), true);
                    //往后看
                    up = 0; low = 0;
                    //#if DEBUG
                    //                foreach (var item in list)
                    //                {
                    //                    Console.WriteLine(item.GetType());
                    //                }
                    //#endif
                    for (int j = 1; i + j < list.Count && j <= 3; j++)//往后最多看3个字符
                    {
                        if (list[j + i] is StrandNode)
                        {
                            StrandNode node = list[i + j].DeepCopy() as StrandNode;
                            if (node.Type == StrandNode.StrandType.upper)
                            {
                                lastupper.link(node.seq, j);
                                up++;
                                lastupper.AddSome(node.GetLength(), false);
                            }
                            else if (node.Type == StrandNode.StrandType.lower)
                            {
                                lastlower.link(node.seq, j);
                                low++;
                                lastlower.AddSome(node.GetLength(), false);
                            }
                            else//可以排除出现的多链
                            {
                                //errors.AddError("复合物中出现了奇怪的元素");
                                return null;
                            }
                        }
                        else if (list[j + i] is LinkerNode)
                        {
                            lastlinker = i + j;
                            i = lastlinker;//跳过连接符
                            break;
                        }
                    }
                    if (up > 1 || low > 1)//似乎已经可以排除后面3个全是链的情况
                    {
                        //前面最多一个上链一个下链
                        return null;
                    }
                    i++;//就算没有找到下一个连接符，往后+1也能跳过当前的[]
                }
                //这里处理最后剩下的链们
                if (lastlower is not null)
                {
                    var guid = Guid.NewGuid();
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastlower, guid = guid });
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastlower.Rotate(), isRotated = true, guid = guid });
                }
                if (lastupper is not null)
                {
                    var guid = Guid.NewGuid();
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastupper, guid = guid });
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = lastupper.Rotate(), isRotated = true, guid = guid });
                }
                foreach (var item in upper)
                {
                    var guid = Guid.NewGuid();
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item, guid = guid });
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item.Rotate(), isRotated = true, guid = guid });
                }
                foreach (var item in lower)
                {
                    var guid = Guid.NewGuid();
                    context.lowers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item, guid = guid });
                    context.uppers.Add(new ReactionStrand() { id = speciesContext.species_exist[complex0], strand = item.Rotate(), isRotated = true, guid = guid });
                }
            }
            return context;
        }
        private bool addVariable(BinaryExpression node)//用来添加等式 函数体和数值型变量
        {
            if (node.Operator != Operator.Equal)
            {
                //errors.AddError("等式左右两边没有等号");
                return false;
            }
            if (node.Left is not NameNode)
            {
                //errors.AddError("等式左边不是标识符");
                return false;
            }
            NameNode name = node.Left as NameNode;
            if (variableContext.variables.ContainsKey(name.Name))
            {
                //errors.AddError("重复定义");
                return false;
            }
            variableContext.id.Add(name.Name);
            variableContext.variables.Add(name.Name, node.Right);
            return true;
        }
        private bool SettingInit(StatementNode statement)//初始化设置
        {
            DirectiveNode directive = statement as DirectiveNode;
            switch (directive.Name)
            {
                case Keyword.simulation:
                    if (directive.Value is not ListExpression)
                    {
                        //errors.AddError("simulaton");
                        return false;
                    }
                    ListExpression list = directive.Value as ListExpression;
                    foreach (BinaryExpression binary in list.Values)
                    {
                        if (binary.Operator is not Operator.Equal || binary.Left is not KeywordNode)
                        {
                            //errors.AddError("simulaton");
                            return false;
                        }
                        KeywordNode keyword = binary.Left as KeywordNode;
                        switch (keyword.GetValue())
                        {
                            case Keyword.initial:
                                if (binary.Right is IntegerNode || binary.Right is FloatNode)
                                {
                                    ValueNode value = binary.Right as ValueNode;
                                    systemSetting.InitialTime = Convert.ToDouble(value.GetValue());
                                }
                                else return false;
                                break;
                            case Keyword.final:
                                if (binary.Right is IntegerNode || binary.Right is FloatNode)
                                {
                                    ValueNode value = binary.Right as ValueNode;
                                    systemSetting.EndTime = (int)value.GetValue();
                                }
                                else return false;
                                break;
                            case Keyword.points:
                                if (binary.Right is IntegerNode)
                                {
                                    IntegerNode value = binary.Right as IntegerNode;
                                    systemSetting.PlotPoints = value.Value;
                                }
                                else return false;
                                break;
                            case Keyword.prune://是否剪枝

                                break;
                            case Keyword.multicore://是否多核
                                break;
                            case Keyword.plots:
                                if (binary.Right is ListExpression)
                                {
                                    systemSetting.plots = (binary.Right as ListExpression).Values;
                                    //foreach (var value in values.Values)
                                    //{
                                    //    systemSetting.plots.Add(value);
                                    //    //if (value is BaseComplex)//两种情况，物种，或者是sum(通配符)，所以先实现对其他部分的解析再进行此处解析吧
                                    //    //{
                                    //    //    BaseComplex node = value as BaseComplex;
                                    //    //    //systemSetting.PlotObjects.Add(node.Value);
                                    //    //}
                                    //    //else if (value is FuncNode)
                                    //    //{

                                    //    //}
                                    //    //else return false;

                                    //}
                                }
                                else return false;
                                break;
                        }
                    }
                    break;
                case Keyword.parameters:
                    if (directive.Value is not ListExpression)
                    {
                        //errors.AddError("simulaton");
                        return false;
                    }
                    foreach (var item in (directive.Value as ListExpression).Values)
                    {
                        if (item is not BinaryExpression)
                        {
                            //errors.AddError("参数应该是一个等式");
                            return false;
                        }
                        if (!addVariable(item as BinaryExpression))
                        {
                            return false;
                        }
                    }
                    break;
                case Keyword.simulator:
                    break;
                case Keyword.tau:
                    if (directive.Value is not (FloatNode or IntegerNode))
                    {
                        //errors.AddError("simulaton");
                        return false;
                    }
                    ValueNode val = directive.Value as ValueNode;
                    systemSetting.Tau = (double)val.GetValue();
                    break;
                case Keyword.lengths:
                    if (directive.Value is not ListExpression)
                    {
                        //errors.AddError("simulaton");
                        return false;
                    }
                    ListExpression lens = directive.Value as ListExpression;
                    try
                    {
                        systemSetting.ToeholdLength = (int)((IntegerNode)lens.Values[0]).Value;
                        systemSetting.RecognitionLength = (int)((IntegerNode)lens.Values[1]).Value;
                    }
                    catch
                    {
                        //需要两个整数
                        return false;
                    }
                    break;
                case Keyword.unproductive:
                    systemSetting.EnableUnproductive = true;
                    break;
            }
            return true;
        }
        private bool CollectVariables(StatementNode statement)//收集变量 def和dom以及new产生的
        {
            DeclareNode declare = statement as DeclareNode;
            ASTPrinter.PrintAst(declare);
            var value = declare.Value;
            if (declare.Name is FuncNode)
            {
                FuncNode func = declare.Name as FuncNode;
                string name_ = func.Name;
                func.Expressions = value;//保存函数体
                if (variableContext.variables.ContainsKey(name_))
                {
                    errors.AddError(func.Line, func.Column, $"{name_}已经存在定义");
                    return false;
                }
                variableContext.id.Add(name_);
                variableContext.variables.Add(name_, func);

                return true;
            }
            NameNode name = declare.Name as NameNode;//不是函数的话就是普通变量

            switch (value)
            {
                case DomNode dom:
                    if (variableContext.variables.ContainsKey(name.Name))
                    {
                        errors.AddError(dom.Line, dom.Column, "重复定义");
                        return false;
                    }
                    if (dom.colour != null)
                    {
                        colors.Add(variableContext.id.Count, dom.getColor());
                    }
                    variableContext.id.Add(name.Name);
                    variableContext.variables.Add(name.Name, dom);
                    break;
                case ValueNode val:
                    if (variableContext.variables.ContainsKey(name.Name))
                    {
                        errors.AddError(val.Line, val.Column, "重复定义");
                        return false;
                    }
                    variableContext.id.Add(name.Name);
                    variableContext.variables.Add(name.Name, val);
                    break;
            }
            return true;
        }
        private bool CollectSpecies(StatementNode statement)//这里就是查找process里的了
        {
            ProcessNode proces = statement as ProcessNode;
            ASTPrinter.PrintAst(proces);
            process pro = new();

            Console.WriteLine($"process.val的类型为{proces.GetType()}");
            if (proces is Species)
            {
                Species species = proces as Species;
                proces = new ProcessNode() { Value = species };
            }
            switch (proces.Value)
            {
                case ProcessList list://可能是列表
                    foreach (var item in list.processes)
                    {
                        if (!CollectSpecies(item))//递归调用
                        {
                            return false;
                        }
                    }
                    return true;
                case Species species://对于单个的DNA复合物或者是函数调用 val1 数目 val2 加入的时间 name
                    ValueNode value1 = species.Value1;
                    if (value1 is not (NameNode or FloatNode))//int包含在name里
                    {
                        errors.AddError(value1.Line, value1.Column, "这里需要一个数值");
                        return false;
                    }
                    if (value1 is FloatNode)
                    {
                        pro.num = (double)(((FloatNode)value1).GetValue());
                    }
                    else if (value1 is NameNode)
                    {
                        if (value1 is IntegerNode)
                        {
                            pro.num = (int)(((IntegerNode)value1).GetValue());
                        }
                        else
                        {
                            NameNode name = value1 as NameNode;
                            if (!variableContext.variables.ContainsKey((string)(name.GetValue())))
                            {
                                errors.AddError(value1.Line, value1.Column, $"变量 {(string)(name.GetValue())} 不存在");
                                return false;
                            }
                            if (variableContext.variables[(string)(name.GetValue())] is not (IntegerNode or FloatNode))
                            {
                                errors.AddError(value1.Line, value1.Column, $"变量{(string)(name.GetValue())}需要是整数或浮点数");
                                return false;
                            }
                            if (variableContext.variables[(string)(name.GetValue())] is FloatNode)
                                pro.num = (double)((FloatNode)variableContext.variables[(string)(name.GetValue())]).GetValue();
                            else pro.num = (int)((IntegerNode)value1).GetValue();
                        }
                    }//完成数值的添加，如果是0的话直接不用往List放吧
                    Console.WriteLine($"物种的数量为{pro.num}，时间为{pro.time}，位置为{species.Line},{species.Column}，值为{species.Value1}");
                    if (pro.num == 0) return true;
                    Console.WriteLine($"物种的数量为{pro.num}，时间为{pro.time}，位置为{species.Line},{species.Column}，值为{species.Value1}");

                    //下面是时间的添加
                    if (species.Value2 is null) pro.time = 0;
                    else
                    {
                        ValueNode value2 = species.Value2;
                        if (value2 is not ((NameNode or FloatNode)))
                        {
                            errors.AddError(value2.Line, value2.Column, "这里需要一个数值");
                            return false;
                        }
                        if (value2 is FloatNode)
                        {
                            pro.time = (int)(((FloatNode)value2).GetValue());
                        }
                        else if (value2 is NameNode)
                        {
                            if (value2 is IntegerNode)
                            {
                                pro.time = (int)(((IntegerNode)value2).GetValue());
                            }
                            else
                            {
                                NameNode name = value2 as NameNode;
                                if (!variableContext.variables.ContainsKey((string)(name.GetValue())))
                                {
                                    errors.AddError(value2.Line, value2.Column, $"变量 {(string)(name.GetValue())} 不存在");
                                    return false;
                                }
                                if (variableContext.variables[(string)(name.GetValue())] is not (IntegerNode or FloatNode))
                                {
                                    errors.AddError(value2.Line, value2.Column, $"变量{(string)(name.GetValue())}需要是整数");
                                    return false;
                                }
                                if (variableContext.variables[(string)(name.GetValue())] is FloatNode)
                                    pro.time = (int)((FloatNode)value2).GetValue();
                                else pro.time = (int)((IntegerNode)value2).GetValue();
                            }
                        }
                    }
                    //下面是物种的添加，需要对函数进行调用/替换，单纯的复合物直接加入
                    switch (species.Name)
                    {
                        case FuncNode func://需要调用函数来生成物种
                            Console.WriteLine($"函数的名字为{func.Name}，参数为{func.Arguments.Count}");
                            string name = func.Name;
                            if (!variableContext.variables.ContainsKey(name))
                            {
                                errors.AddError(func.Line, func.Column, $"函数 {name} 不存在");
                                return false;
                            }
                            //查看此处参数和存储的函数的参数是否对的上
                            FuncNode func1 = variableContext.variables[name] as FuncNode;
                            if (func1.Arguments.Count != func.Arguments.Count)
                            {
                                errors.AddError(func.Line, func.Column, $"函数 {name} 参数数量不匹配");
                                return false;
                            }

                            //接下来找到目标函数的函数体,
                            /*
                             函数需要实现以下类型
                            def reporter(N, (iL, i, iR), Fluor) = ( N * {t^*}[iL^ i iR^]<Fluor>)
                            函数参数里面有数字和域的混合
                            def seesawOR2I2O(I1, I2, K1, K2) =
                            (( gateL((20.0 * N),I1,I2)
                            | thresholdL((6.0 * N),I1,I2)
                            | gateL((10.0 * N),I2,K1)
                            | gateL((10.0 * N),I2,K2)
                            | signal((40.0 * N),I2,F)))
                            函数里面是一个复杂的ProcessList，里面也包含了其它函数
                             * 
                             * 
                             */

                            //此处对函数体进行解析
                            Console.WriteLine("开始call");
                            int id1 = AddNewSpecies(FuncCall(func1.Arguments, func.Arguments, func1.Expressions));
                            if (id1 == -1)
                            {
                                errors.AddError(func.Line, func.Column, "物种定义不合法");
                                return false;
                            }
                            pro.id = id1;
                            break;

                        case BaseComplex complex:
                            int id = AddNewSpecies(complex);
                            if (id == -1)
                            {
                                errors.AddError(species.Line, species.Column, "物种定义不合法");
                                return false;
                            }
                            pro.id = id;
                            break;
                    }

                    break;
                case BaseComplex complex://直接加入物种
                    int id2 = AddNewSpecies(complex);
                    pro.num = 1;//默认数量为1
                    if (id2 == -1)
                    {
                        errors.AddError(complex.Line, complex.Column, "物种定义不合法");
                        return false;
                    }
                    pro.id = id2;
                    break;
                default://忘了这个是干啥的了
                    break;
            }
            Console.WriteLine($"物种{pro.id}的数量为{pro.num}，时间为{pro.time}");
            processContext.processes.Add(pro);
            processContext.process_init.Add(pro.id, pro.num);
            return true;
        }
        //private SeqNode LinkWithFunc()
        //实参只可能是NameNode，返回最后生成的物种
        private BaseComplex FuncCall(List<NameNode> funcA, List<NameNode> actualA, AstNode expressions)//完成函数调用,得能完成函数套函数的递归操作
        {
            Console.WriteLine($"开始函数调用,{funcA.Count},{actualA.Count}");
            if (expressions is null)
            {
                errors.AddError(0, 0, "函数体为空！");
                return null;
            }
            if (funcA is not null && actualA is not null && funcA.Count != actualA.Count)
            {
                errors.AddError(expressions.Line, expressions.Column, "参数数量不匹配");
                return null;
            }
            if ((funcA is null && actualA is null) || funcA.Count == 0)//无参函数，直接返回
            {
                ASTPrinter.PrintAst(expressions);
                return expressions as BaseComplex;
            }
            Dictionary<NameNode, NameNode> dict = new();//建立替换表
            for (int i = 0; i < funcA.Count; i++)
            {
                dict.Add(funcA[i], actualA[i]);
            }
            switch (expressions)//目前假设函数体就是一个复合物，不会函数嵌套函数。实际上还有可能是一个ProcessList
            {

                case StrandNode strand_://是一个简单链
                    SeqNode seq = strand_.seq;
                    if (seq is null)
                    {
                        errors.AddError(expressions.Line, expressions.Column, "链的序列为空");
                        return null;
                    }

                    if (seq.Value is List<SeqNode>)//是一堆域，需要对每个域进行替换
                    {
                        List<SeqNode> list = seq.Value as List<SeqNode>;//原序列
                        List<SeqNode> list1 = new();//替换后的序列，可以通过add来实现扩增
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i].Value is not DomNode)
                            {
                                errors.AddError(list[i].Line, list[i].Column, "链里面应该是域");
                                return null;
                            }
                            DomNode dom = list[i].Value as DomNode;
                            NameNode name = dom.Name as NameNode;
                            if (dict.ContainsKey(name))
                            {
                                list1.Add(new SeqNode() { Value = new DomNode() { Name = dict[name] } });//参数没有函数了似乎
                            }
                            else
                            {
                                list1.Add(new SeqNode() { Value = dom });
                            }



                        }
                        strand_.seq.Value = list1;
                        return strand_;
                    }
                    else if (seq.Value is not (NameNode or DomNode))
                    {
                        errors.AddError(seq.Line, seq.Column, "链的序列不合法");
                        return null;

                    }

                    break;
                case ComplexNode complex://是一个复合物
                    if (!checkComplex(complex))//首先需要一个合法的复合物
                    {
                        errors.AddError(complex.Line, complex.Column, "复合物定义不合法");
                        return null;
                    }
                    ComplexNode complex1 = new ComplexNode();
                    for (int i = 0; i < complex.Values.Count; i++)
                    {
                        if (complex.Values[i] is LinkerNode)
                        {
                            complex1.Values.Add(complex.Values[i]);
                            continue;
                        }
                        if (complex.Values[i] is StrandNode)
                        {
                            StrandNode strand = complex.Values[i] as StrandNode;
                            complex1.Values.Add(FuncCall(funcA, actualA, strand));
                        }

                    }
                    return complex1;
                case ProcessNode process:

                    break;
            }

            return null;
        }

        public readonly struct IntPair
        {
            public int Start { get; }
            public int Length { get; }

            public IntPair(int start, int length)
            {
                Start = start;
                Length = length;
            }
            public override bool Equals([NotNullWhen(true)] object? obj)
            {
                if (obj is IntPair pair)
                {
                    return Start == pair.Start && Length == pair.Length;
                }
                return false;
            }
            public override int GetHashCode()
            {
                return HashCode.Combine(Start, Length);
            }
        }
        public readonly struct strandInfos
        {
            public Dictionary<IntPair, StrandNode> substrands { get; }
            public Dictionary<DomNode, List<int>> doms { get; }
            public strandInfos(Dictionary<IntPair, StrandNode> substrands, Dictionary<DomNode, List<int>> doms)
            {
                this.substrands = substrands;
                this.doms = doms;
            }
        }
        private strandInfos getSubStrand(StrandNode strand)//获取子链们,按照起点长度获取
        {

            Dictionary<IntPair, StrandNode> list = new();
            Dictionary<DomNode, List<int>> doms = new Dictionary<DomNode, List<int>>();
            strandInfos infos = new();
            if (strand.seq.Value is DomNode dom111)
            {
                list.Add(new IntPair(0, 1), strand);
                if (dom111.isToe())
                {
                    doms.Add(dom111.clear(), new List<int>() { 0 });
                }
                return new strandInfos(list, doms);
            }
            if (strand.seq.Value is not List<SeqNode>)
            {
                errors.AddError(strand.Line, strand.Column, "需要列表域构成的链");
                return infos;
            }
            List<SeqNode> seqs = strand.seq.Value as List<SeqNode>;
            Console.WriteLine($"链的长度为{seqs.Count}");
            for (int j = 0; j < seqs.Count; j++)//不同起点
            {
                if (seqs[j].Value is DomNode)
                {
                    DomNode dom = (seqs[j].Value as DomNode).clear();
                    if (dom.isToe())
                    {
                        ExpressionPrinter.PrintExpression(dom);
                        Console.WriteLine($"当前的hashcode={dom.GetHashCode()}");
                        Console.WriteLine($"域为,位置为{j}");
                        if (doms.ContainsKey(dom))
                        {
                            doms[dom].Add(j);
                        }
                        else
                        {
                            doms.Add(dom, new List<int>() { j });
                        }
                    }
                    //else
                    //    continue;
                }
                for (int i = 1; i + j - 1 < seqs.Count; i++)//不同长度
                {
                    List<SeqNode> sub = new();
                    for (int k = 0; k < i && k + j < seqs.Count; k++)
                    {
                        sub.Add(seqs[k + j]);
                    }
                    StrandNode strand1;
                    if (sub.Count > 1)
                        strand1 = new StrandNode() { seq = new SeqNode() { Value = sub } };
                    else if (sub.Count == 1)
                        strand1 = new StrandNode() { seq = sub[0] };
                    else
                        continue;
                    //list.Add(strand1);
#if DEBUG
                    Console.WriteLine($"起点为{j},长度为{i}");
                    ExpressionPrinter.PrintExpression(strand1);
                    Console.WriteLine();
                    Console.WriteLine(strand1.seq.Value.GetType());
#endif
                    list.Add(new IntPair(j, i), strand1);
                }
            }
#if DEBUG
            Console.WriteLine($"list的大小为{list.Count}");
            foreach (var item in list)
            {
                Console.WriteLine($"起点为{item.Key.Start},长度为{item.Key.Length}");
                ExpressionPrinter.PrintExpression(item.Value);
                //Console.WriteLine();
            }
#endif
            return new strandInfos(list, doms);
        }
        private ComplexNode Normalize(ComplexNode complex)//把[x to^ q]<y>{x}:{z}<q>[e* to^]的合并成[x to^ q]<y>{xz}:<q>[e* to^]
                                                          //默认能进行右向分支迁移的完成右向分支迁移。可是如果释放下了单链怎么办。先不考虑了。
        {
            var node2 = Utils.getComplexNode2s(complex);
            for (int i = 1; i < node2.Count; i++)
            {
                Console.WriteLine($"i=={i},{node2[i]}");
                if (node2[i].linker is LinkerNode linker)
                {
                    if (linker.Type == LinkerNode.LinkerType.lower && node2[i].leftbottom != null)
                    {
                        node2[i - 1].linkwith(ComplexNode2.Linktype.rightbottom, node2[i].leftbottom);
                        node2[i].leftbottom = null;
                    }
                    else if (linker.Type == LinkerNode.LinkerType.upper && node2[i].lefttop != null)
                    {
                        node2[i - 1].linkwith(ComplexNode2.Linktype.righttop, node2[i].lefttop);
                        node2[i].lefttop = null;
                    }
                }
            }
            var complexx = Utils.GetComplexNode(node2) as ComplexNode;
            var ans = rightMigration(complexx);
            if (ans is null)
            {
                errors.AddError(complex.Line, complex.Column, "复合物定义不合法");
                return null;
            }
            if (ans.Count > 1)
            {
                //errors.AddError(complex.Line, complex.Column, "复合物定义不合法");
                Console.WriteLine($"复合物定义不合法，复合物的数量为{ans.Count}");
                //return null;
            }
            return ans[0] as ComplexNode;
        }
        private bool isSame(BaseComplex a, BaseComplex b)//判断两个复合物是不是一个
        {

            return false;
        }
        private bool checkMatch(StrandNode s1, StrandNode s2)//从0开始
        {
#if DEBUG
            Console.WriteLine($"s1的长度为{s1.GetLength()}");
            Console.WriteLine($"s2的长度为{s2.GetLength()}");
            Console.WriteLine("-----开始测试-----");
#endif
            bool flag = true;
            if (s1.seq.Value is DomNode dom111 && s2.seq.Value is DomNode dom222 && dom111.GetRevComp().Equals(dom222))
                return true;
            if (s1.seq.Value is not List<SeqNode> || s2.seq.Value is not List<SeqNode>)
            {
                errors.AddError(s1.Line, s1.Column, "链应该是个序列");
                return false;
            }
            List<SeqNode> seqs1 = s1.seq.Value as List<SeqNode>;
            List<SeqNode> seqs2 = s2.seq.Value as List<SeqNode>;
            if (seqs1.Count != seqs2.Count)
            {
                errors.AddError(s1.Line, s1.Column, "链的长度不一致");
                return false;
            }
            for (int i = 0; i < seqs1.Count; i++)
            {
#if DEBUG
                Console.WriteLine($"\n现在是第{i}个元素\n");
                ExpressionPrinter.PrintExpression(seqs1[i]);
                ExpressionPrinter.PrintExpression(seqs2[i]);
                Console.WriteLine($"1的类型{seqs1[i].GetType()},2的类型{seqs2[i].GetType()}");

#endif
                if (seqs1[i].Value is DomNode && seqs2[i].Value is DomNode)
                {
                    DomNode dom1 = (seqs1[i].Value as DomNode).DeepCopy() as DomNode;
                    DomNode dom2 = (seqs2[i].Value as DomNode).DeepCopy() as DomNode;

                    if (!dom1.GetRevComp().Equals(dom2))
                    {
                        Console.WriteLine($"域不匹配，位置为   {i}");
                        flag = false;
                        break;
                    }
                }
                else
                {
                    flag = false;
                    break;
                }
            }
#if DEBUG
            //if(!flag)
            //Console.WriteLine($"匹配失败，位置为{seqs1[0].Line},{seqs1[0].Column}");
#endif
            return flag;
        }
        private List<matchInfo> MatchToehold(StrandNode strand1, StrandNode strand2, strandInfos strand1s)//第一个是上链，第二个是下链
        {
            Console.WriteLine($"开始获取doms2");
            strandInfos strand2s = getSubStrand(strand2);
            Dictionary<IntPair, StrandNode> upsub = strand1s.substrands;
            Dictionary<IntPair, StrandNode> lowsub = strand2s.substrands;
            Dictionary<DomNode, List<int>> doms1 = strand1s.doms;
            Dictionary<DomNode, List<int>> doms2 = strand2s.doms;

            List<matchInfo> matches = new();//匹配的结果
            if (doms1 != null)
                foreach (var toehold1 in doms1)//从上链往下链匹配
                {
                    DomNode dom = toehold1.Key;
                    DomNode domRev = (dom.DeepCopy() as DomNode).GetRevComp().clear();
                    ASTPrinter.PrintAst(domRev);
                    Console.WriteLine($"domRev.Hash == {(uint)domRev.GetHashCode()}");
                    if (doms2 == null) break;
                    foreach (var item in doms2)
                    {
                        ASTPrinter.PrintAst(item.Key);
                        Console.WriteLine($"item.Key.Hash == {(uint)item.Key.GetHashCode()}");
                        Console.WriteLine($"是否相等？{domRev.Equals(item.Key)}");
                        Console.WriteLine($"是否包含？{doms2.ContainsKey(domRev)}");
                        IEqualityComparer<DomNode>? comparer = EqualityComparer<DomNode>.Default;
                        uint hashCode = (uint)comparer.GetHashCode(domRev);
                        Console.WriteLine($"hashCode == {hashCode}");
                        Console.WriteLine($"item.Key.Hash == {(uint)comparer.GetHashCode(item.Key)}");
                    }
                    //ASTPrinter.PrintAst(doms2);

                    Console.WriteLine($"是否包含？{doms2.ContainsKey(domRev)}");
                    if (!doms2.ContainsKey(domRev))//找下链有没有互补的
                    {
                        continue;
                    }
                    List<int> list1 = toehold1.Value;//该域出现的位置
                    List<int> list2 = doms2[domRev];//互补出现的位置

                    Dictionary<IntPair, StrandNode> s1 = new(), s2 = new();
                    //获得该域为起点或终点的子链
                    foreach (int start in list1)
                    {
                        for (int l = 1; l + start - 1 < strand1.GetLength(); l++)//此处包含了一个toe的情况
                        {
                            s1[new IntPair(start, l)] = upsub[new IntPair(start, l)];
                        }
                        for (int l = 2; start - l + 1 >= 0; l++)
                        {
                            s1[new IntPair(start - l + 1, l)] = upsub[new IntPair(start - l + 1, l)];
                        }
                    }
                    foreach (int start in list2)
                    {
                        for (int l = 1; l + start - 1 < strand2.GetLength(); l++)//此处包含了一个toe的情况
                        {
                            s2[new IntPair(start, l)] = lowsub[new IntPair(start, l)];
                        }
                        for (int l = 2; start - l + 1 >= 0; l++)
                        {
                            s2[new IntPair(start - l + 1, l)] = lowsub[new IntPair(start - l + 1, l)];
                        }
                    }
                    foreach (var item in s1)//对s1和s2中的子链进行匹配
                    {
                        IntPair pair = item.Key;
                        StrandNode strand11 = item.Value;
                        foreach (var item1 in s2)
                        {
                            IntPair pair1 = item1.Key;
                            StrandNode strand22 = item1.Value;
                            if (pair.Length != pair1.Length)
                            {
                                continue;
                            }
                            Console.WriteLine("strand1:");
                            ASTPrinter.PrintAst(strand11);
                            Console.WriteLine("strand2:");
                            ASTPrinter.PrintAst(strand22);
                            Console.WriteLine($"两者的seq类型{strand11.seq.Value.GetType()}，{strand22.seq.Value.GetType()}");
                            if (checkMatch(strand11, strand22))
                            {
                                matches.Add(new matchInfo(pair.Start, pair1.Start, pair.Length));
                            }
                        }

                    }
                }
            // 处理作为起点的情况，相同up和low的项保留最长的
            var processedStart = matches
                .GroupBy(m => new { m.up, m.low })
                .Select(g => g.OrderByDescending(m => m.length).First())
                .ToList();

            // 处理作为终点的情况，相同终点的项保留up最小的
            var processedEnd = processedStart
                .GroupBy(m => new { UpEnd = m.up + m.length, LowEnd = m.low + m.length })
                .Select(g => g.OrderBy(m => m.up).ThenBy(m => m.low).First())
                .ToList();

            return processedEnd;
        }
        private List<matchInfo> MatchToehold(StrandNode strand1, StrandNode strand2)//匹配互补前缀
        {
            strandInfos strand1s = getSubStrand(strand1);
            strandInfos strand2s = getSubStrand(strand2);
            Dictionary<IntPair, StrandNode> upsub = strand1s.substrands;
            Dictionary<IntPair, StrandNode> lowsub = strand2s.substrands;
            Dictionary<DomNode, List<int>> doms1 = strand1s.doms;
            Dictionary<DomNode, List<int>> doms2 = strand2s.doms;
#if DEBUG
            Console.WriteLine("--------------------------------");
            Console.WriteLine($"链1的长度为{strand1.GetLength()}");
            Console.WriteLine($"链2的长度为{strand2.GetLength()}");
            foreach (var item in doms1)
            {
                ExpressionPrinter.PrintExpression(item.Key);
                Console.WriteLine($"链1的域位置为{item.Value[0]}");
            }
            foreach (var item in doms2)
            {
                ExpressionPrinter.PrintExpression(item.Key);
                Console.WriteLine($"链2的域位置为{item.Value[0]}");
            }
            Console.WriteLine("--------------------------------");
            foreach (var item in upsub)
            {
                IntPair pair = item.Key;
                StrandNode strand = item.Value;
                Console.WriteLine($"\n起点为{pair.Start},长度为{pair.Length}");
                ExpressionPrinter.PrintExpression(strand);
            }
            Console.WriteLine("\n++++++++++++++++++++++++++++++++++++");
            foreach (var item in lowsub)
            {
                IntPair pair = item.Key;
                StrandNode strand = item.Value;
                Console.WriteLine($"\n起点为{pair.Start},长度为{pair.Length}");
                ExpressionPrinter.PrintExpression(strand);
            }
            Console.WriteLine("\n++++++++++++++++++++++++++++++++++++");
#endif
            List<matchInfo> matches = new();//匹配的结果
            foreach (var toehold1 in doms1)//从上链往下链匹配
            {
                DomNode dom = toehold1.Key;
                DomNode domRev = (dom.DeepCopy() as DomNode).GetRevComp().clear();
#if DEBUG
                Console.WriteLine($"是否包含？{doms2.ContainsKey(domRev)}");
                //Console.WriteLine($"{}")
                //foreach (var item in doms2)
                //{
                //    DomNode dom1d = item.Key;
                //    ExpressionPrinter.PrintExpression(dom1d);
                //    Console.WriteLine($"{dom1d == domRev}");
                //}
#endif
                if (!doms2.ContainsKey(domRev))//找下链有没有互补的
                {
                    continue;
                }
                List<int> list1 = toehold1.Value;//该域出现的位置
                List<int> list2 = doms2[domRev];//互补出现的位置

                Dictionary<IntPair, StrandNode> s1 = new(), s2 = new();
                //获得该域为起点或终点的子链
                foreach (int start in list1)
                {
                    for (int l = 1; l + start - 1 < strand1.GetLength(); l++)//此处包含了一个toe的情况
                    {
                        s1[new IntPair(start, l)] = upsub[new IntPair(start, l)];
                    }
                    for (int l = 2; start - l + 1 >= 0; l++)
                    {
                        s1[new IntPair(start - l + 1, l)] = upsub[new IntPair(start - l + 1, l)];
                    }
                }
#if DEBUG
                Console.WriteLine("\n11111111111111111111111111111111111");
                foreach (var item in s1)
                {
                    IntPair pair = item.Key;
                    StrandNode strand11 = item.Value;
                    Console.WriteLine($"\n起点为{pair.Start},长度为{pair.Length}");
                    ExpressionPrinter.PrintExpression(strand11);
                }
                Console.WriteLine("\n11111111111111111111111111111111111");
#endif
                foreach (int start in list2)
                {
                    for (int l = 1; l + start - 1 < strand2.GetLength(); l++)//此处包含了一个toe的情况
                    {
                        s2[new IntPair(start, l)] = lowsub[new IntPair(start, l)];
                    }
                    for (int l = 2; start - l + 1 >= 0; l++)
                    {
                        s2[new IntPair(start - l + 1, l)] = lowsub[new IntPair(start - l + 1, l)];
                    }
                }
#if DEBUG
                Console.WriteLine("\n22222222222222222222222222222");
                foreach (var item in s2)
                {
                    IntPair pair = item.Key;
                    StrandNode strand11 = item.Value;
                    Console.WriteLine($"\n起点为{pair.Start},长度为{pair.Length}");
                    ExpressionPrinter.PrintExpression(strand11);
                }
                Console.WriteLine("\n22222222222222222222222222222");
#endif
                foreach (var item in s1)//对s1和s2中的子链进行匹配
                {
                    IntPair pair = item.Key;
                    StrandNode strand11 = item.Value;
#if DEBUG
                    Console.WriteLine("\n0000000000000000000000000000");
                    Console.WriteLine($"当前的上链为:");
                    ExpressionPrinter.PrintExpression(strand11);
#endif
                    foreach (var item1 in s2)
                    {
#if DEBUG
                        Console.WriteLine($"\n当前的下链为:");
                        ExpressionPrinter.PrintExpression(item1.Value);
                        Console.WriteLine("\nllllllllllllllllllllllllllllllll");
#endif
                        IntPair pair1 = item1.Key;
                        StrandNode strand22 = item1.Value;
                        if (pair.Length != pair1.Length)
                        {
                            continue;
                        }
                        if (checkMatch(strand11, strand22))
                        {
#if DEBUG
                            Console.WriteLine($"匹配成功，长度为{pair.Length}");
                            ExpressionPrinter.PrintExpression(strand11);
                            Console.WriteLine("\n=====================================\n");
#endif
                            matches.Add(new matchInfo(pair.Start, pair1.Start, pair.Length));
                        }
                    }

                }
            }
            // 处理作为起点的情况，相同up和low的项保留最长的
            var processedStart = matches
                .GroupBy(m => new { m.up, m.low })
                .Select(g => g.OrderByDescending(m => m.length).First())
                .ToList();

            // 处理作为终点的情况，相同终点的项保留up最小的
            var processedEnd = processedStart
                .GroupBy(m => new { UpEnd = m.up + m.length, LowEnd = m.low + m.length })
                .Select(g => g.OrderBy(m => m.up).ThenBy(m => m.low).First())
                .ToList();

            return processedEnd;
        }
        //1是正反应，2是可逆反应，0是不反应
        //private int CanItHappen(int id1,int id2,matchInfo info)//比较原复合物此处附近的总碱基对和新形成碱基对数目判断反应能否发生
        //{
        //    //这里是假设id1，id2分别为自由链来看看行不行
        //    StrandNode strand1 = speciesContext.species_id[id1] as StrandNode;
        //    StrandNode strand2 = speciesContext.species_id[id2] as StrandNode;
        //    int cnt1 = strand1.getBpsCount(info.up),cnt2 = strand2.getBpsCount(info.low);
        //    if(info.length>cnt1&& cnt2>cnt1)
        //        return 1;
        //    return false;
        //}
        private ComplexNode Stickss(StrandNode strand1, StrandNode strand2, matchInfo info)//把两个链粘在一起，假设第一个是上链，第二个是下链
        {
            StrandNode s1 = strand1.DeepCopy() as StrandNode;
            StrandNode s2 = strand2.DeepCopy() as StrandNode;

            int length = info.length, up = info.up, low = info.low;
            if (length + up > s1.GetLength() || length + low > s2.GetLength())
            {
                errors.AddError(s1.Line, s1.Column, "位点+长度比链都长了，😥");
                return null;
            }
            //可能是一个契合的双链
            if (length == s1.GetLength() && length == s2.GetLength())
            {
                var tmpstrand = new StrandNode() { Type = StrandNode.StrandType.duplex, seq = (s1.seq.DeepCopy() as SeqNode) };
                var newcomplex = new ComplexNode();
                newcomplex.Values.Add(tmpstrand);
                return newcomplex;
            }
            //可能是复合物
            List<SeqNode> upper = new List<SeqNode>();
            if (s1.seq.Value is DomNode)
                upper.Add(s1.seq);
            else upper = s1.seq.Value as List<SeqNode>;
            List<SeqNode> lower = new List<SeqNode>();
            if (s2.seq.Value is DomNode)
                lower.Add(s2.seq);
            else lower = s2.seq.Value as List<SeqNode>;
            ComplexNode complex = new ComplexNode();
            StrandNode upperleft = new StrandNode();
            StrandNode upperright = new StrandNode();
            StrandNode lowerleft = new StrandNode();
            StrandNode lowerright = new StrandNode();
            StrandNode middle = new StrandNode();
#if DEBUG
            foreach (var item in upper)
            {
                Console.WriteLine("\n上链");
                ExpressionPrinter.PrintExpression(item);
            }
            Console.WriteLine("\ndebug");
#endif
            for (int i = up; i < up + length; i++)
                middle.link(upper[i]);
            middle.Type = StrandNode.StrandType.duplex;
            //现在拼装整个完整的复合物
            if (up > 0)
            {
                for (int i = 0; i < up; i++)
                    upperleft.link(upper[i]);
                upperleft.Type = StrandNode.StrandType.upper;
                complex.Values.Add(upperleft);
            }
            if (low > 0)
            {
                for (int i = 0; i < low; i++)
                    lowerleft.link(lower[i]);
                lowerleft.Type = StrandNode.StrandType.lower;
                complex.Values.Add(lowerleft);
            }
            complex.Values.Add(middle);
            if (up + length < s1.GetLength())
            {
                for (int i = up + length; i < s1.GetLength(); i++)
                    upperright.link(upper[i]);
                upperright.Type = StrandNode.StrandType.upper;
                complex.Values.Add(upperright);
            }
            if (low + length < s2.GetLength())
            {
                for (int i = low + length; i < s2.GetLength(); i++)
                    lowerright.link(lower[i]);

                lowerright.Type = StrandNode.StrandType.lower;
                complex.Values.Add(lowerright);
            }
            return complex;
        }
        //连接两个复合物
        private ComplexNode LinkComplexWithComplex(ComplexNode complex1, ComplexNode complex2, LinkerNode.LinkerType linker)
        {
            if (!checkComplex(complex1, false) || !checkComplex(complex2, false))
            {
#if DEBUG
                Console.WriteLine("复合物不合法");
#endif
                return null;
            }
            ASTPrinter.PrintAst(complex1);
            ASTPrinter.PrintAst(complex2);
            ComplexNode complex = new ComplexNode();
            //先找到第一个的最后一个双链
            int lastduplex = complex1.Values.Count - 1;
            List<BaseComplex> list = complex1.Values;
            while (lastduplex >= 0)
            {
                if (list[lastduplex] is StrandNode strand && strand.Type == StrandNode.StrandType.duplex)
                {
                    break;
                }
                lastduplex--;
            }
            if (lastduplex == -1)//没有双链
            {
                return complex2.DeepCopy() as ComplexNode;
            }
            for (int i = 0; i <= lastduplex; i++)//这里改成了=号，解决前面链双链忘记复制的问题
            {
                complex.Values.Add(list[i]);
            }
            //下面需要判断两者连接了。
            //上连接
            //情况考虑[x to^]<x>:[x to^]->[x to^ x]:<x>[to^]这俩其实等价-》这个交给向右分支迁移吧。。
            //[x to^]<x>::<q>[m to^] -> [x to^ x]<x q>::[m to^]
            //
            //找第二个复合物的第一个双链
            int firstduplex = 0;
            while (firstduplex < complex2.Values.Count)
            {
                if (complex2.Values[firstduplex] is StrandNode strand && strand.Type == StrandNode.StrandType.duplex)
                {
                    break;
                }
                firstduplex++;
            }
            StrandNode lastup = null, lastlow = null, nextup = null, nextlow = null;
            for (int i = lastduplex + 1; i < list.Count; i++)//最多也就两次，如果再多就有问题了
            {
                if (list[i] is StrandNode strand)
                {
                    if (strand.Type == StrandNode.StrandType.upper && lastup == null)
                        lastup = strand.DeepCopy() as StrandNode;
                    else if (strand.Type == StrandNode.StrandType.lower && lastlow == null)
                        lastlow = strand.DeepCopy() as StrandNode;
                    else
                    {
#if DEBUG
                        Console.WriteLine($"第一个复合物的{i}不是单链");
#endif
                        return null;
                    }
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"第一个复合物的{i}不是单链");
#endif
                    return null;
                }
            }
            for (int i = 0; i < firstduplex; i++)
            {
                if (complex2.Values[i] is StrandNode strand)
                {
                    if (nextup == null && strand.Type == StrandNode.StrandType.upper)
                        nextup = strand.DeepCopy() as StrandNode;
                    else if (nextlow == null && strand.Type == StrandNode.StrandType.lower)
                        nextlow = strand.DeepCopy() as StrandNode;
                    else
                    {
#if DEBUG
                        Console.WriteLine($"第二个复合物的{i}不是单链");
#endif
                        return null;
                    }
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"第二个复合物的{i}不是单链");
#endif
                    return null;
                }
            }
            if (linker == LinkerNode.LinkerType.lower)//把后一个low的连接到前一个里
            {
                if (lastlow == null)
                {
                    lastlow = nextlow; nextlow = null;
                }
                else if (nextlow != null)
                {
                    lastlow.link(nextlow.seq.DeepCopy() as SeqNode);
                    nextlow = null;
                }
            }
            else if (linker == LinkerNode.LinkerType.upper)//把后一个up的连接到前一个里
            {
                if (lastup == null)
                {
                    lastup = nextup; nextup = null;
                }
                else if (nextup != null)
                {
                    lastup.link(nextup.seq.DeepCopy() as SeqNode);
                    nextup = null;
                }
            }
            if (lastup != null)
                complex.Values.Add(lastup.DeepCopy() as StrandNode);
            if (lastlow != null)
                complex.Values.Add(lastlow.DeepCopy() as StrandNode);
            complex.Values.Add(new LinkerNode() { Type = linker });
            if (nextup != null)
                complex.Values.Add(nextup.DeepCopy() as StrandNode);
            if (nextlow != null)
                complex.Values.Add(nextlow.DeepCopy() as StrandNode);

            for (int i = firstduplex; i < complex2.Values.Count; i++)
                complex.Values.Add(complex2.Values[i].DeepCopy());
            Console.WriteLine("连接完成");
            ASTPrinter.PrintAst(complex);
            return complex;
        }

        //这里考虑的是把Stickss以及Sticksc的产物再进行粘合,外面可以固定住一个然后测试另一个，那么可能得考虑深度优先算法来对一个链完成所有的搜索了，假设的是第二个链会与第一个的上链的位点进行结合，从左往右
        private ComplexNode Sticksc(ComplexNode complex, StrandNode strand, matchInfo info)
        {
            ComplexNode node = new ComplexNode();
            //先复制过去左边不会变的
            ComplexNode complexNode = complex.DeepCopy() as ComplexNode;
            int i = 0, j = 0;
            for (; j < complexNode.Values.Count; j++)
            {
                if (!(complexNode.Values[j] is LinkerNode))
                {
                    var strandtmp = complexNode.Values[j] as StrandNode;
                    if (strandtmp is not null)
                    {
                        if (i + strandtmp.GetLength() >= info.up)//说明结合位点在这个区域
                            break;
                    }
                }
                node.Values.Add(complexNode.Values[j]);
            }
            //然后因为是确保从左往右，所以剩下的肯定是一个单链
            StrandNode strand1 = new StrandNode();
            strand1.Type = StrandNode.StrandType.upper;
            while (j < complexNode.Values.Count)//复制过去
            {
                //strand1.link();
                if (complexNode.Values[j] is StrandNode strandtmp)
                {
                    strand1.link(strandtmp.seq.DeepCopy() as SeqNode);
                }
                j++;
            }
            matchInfo info1 = new matchInfo(info.up - i, 0, info.length);
            ComplexNode strand2 = Stickss(strand1, strand, info1) as ComplexNode;
            if (strand2 is null)
            {
                return null;
            }
            return LinkComplexWithComplex(node, strand2, LinkerNode.LinkerType.upper);
        }
        //private ComplexNode Sticksc(ComplexNode complex, StrandNode strand, matchInfo info,int strandnum)//粘合链和复合物，可以迭代调用
        //{
        //    ComplexNode node = new ComplexNode();
        //    ComplexNode complexNode = complex.DeepCopy() as ComplexNode;
        //    for(int i=0;i<strandnum-1;i++)//因为是新连接一个链，所以不会改变之前的连接符
        //    {
        //        node.Values.Add(complexNode.Values[i]);
        //    }


        //    return null;
        //}
        //这个确实不好设计。-_-_这种的就不知道该怎么解决了。
        //这里假设复合物连接位点是下链，info是指复合物下链和需要连接的链的匹配信息
        //默认是从左往右连接
        //到底是怎么和怎么连接让另一个函数去完成吧，这个就单纯根据info完成下链和复合物的连接

        //private ComplexNode Sticksc(ComplexNode complex,StrandNode strand, matchInfo info)//粘合链和复合物，可以迭代调用
        //{
        //    //确认下链是复合物，设定上复合物由Stickss生成

        //    return null;
        //}
        //将复合物向右进行分支迁移
        private List<BaseComplex> rightMigration(ComplexNode complex0)//会把完全替换的替换下来，这里不生成可逆反应
        {
            Console.WriteLine("开始右迁移");
            List<BaseComplex> complexes = new List<BaseComplex>();
            ComplexNode complex = complex0.DeepCopy() as ComplexNode;
            //int cid = AddNewSpecies(complex);
            //if (rights.ContainsKey(cid))
            //    return rights[cid];
            //ASTPrinter.PrintAst(complex);
            //if(complex.Values.Count == 7)
            //{
            //    Console.WriteLine("有问题");
            //}
            ComplexNode tmpcomplex = new ComplexNode();
            for (int i = 0; i < complex.Values.Count; i++)
            {

                // [xxxx]<a>:[a xxxxxx]->[xxxx a]:<a>[xxxxxx]这种样式,或[xxxxxx]{a*}::[a xxxxxxxxxx]->[xxxxx a]::{a}[xxxxxxxxxxx]
                //Console.WriteLine($"tmpcomplexvc == {tmpcomplex.Values.Count > 0}");
                //if ((tmpcomplex.Values.Count > 0))
                //    ASTPrinter.PrintAst(tmpcomplex.Values[tmpcomplex.Values.Count - 1]);
                //Console.WriteLine($"tmpcomplexvc == {tmpcomplex.Values.Count > 0}");
                //Console.WriteLine($"\ni == {i}");

                //ASTPrinter.PrintAst(complex.Values[i]);
                //Console.WriteLine($"\ni+1 == {i + 1}");
                //if (i + 1 < complex.Values.Count)
                //{
                //    ASTPrinter.PrintAst(complex.Values[i + 1]);
                //    if(complex.Values[i + 1] is LinkerNode linker11&& complex.Values[i] is StrandNode strand333)
                //    {
                //        Console.WriteLine(linker11.isSame(strand333));
                //    }
                //}
                //Console.WriteLine($"\ni+1 == {i + 2}");
                //if(i + 2 < complex.Values.Count)
                //ASTPrinter.PrintAst(complex.Values[i + 2]);
                // 显式声明所有临时变量并初始化为 null
                StrandNode temp_strand2 = null;
                StrandNode temp_strand = null;
                StrandNode temp_strand1 = null;
                LinkerNode temp_linker = null;

                // 条件拆解及调试输出
                bool cond1 = tmpcomplex.Values.Count > 0;
                Console.WriteLine($"Condition 1 (tmpcomplex.Values.Count > 0): {cond1}");

                bool cond2 = cond1 &&
                             tmpcomplex.Values[tmpcomplex.Values.Count - 1] is StrandNode s2 &&
                             (temp_strand2 = s2).Type == StrandNode.StrandType.duplex;
                Console.WriteLine($"Condition 2 (Last is duplex StrandNode): {cond2}");

                bool cond3 = cond2 &&
                             complex.Values[i] is StrandNode s &&
                             (temp_strand = s).Type != StrandNode.StrandType.duplex;
                Console.WriteLine($"Condition 3 (Current is non-duplex StrandNode): {cond3}");

                bool cond4 = cond3 && i + 2 < complex.Values.Count;
                Console.WriteLine($"Condition 4 (i + 2 < complex.Values.Count): {cond4}");

                bool cond5 = cond4 &&
                             complex.Values[i + 2] is StrandNode s1 &&
                             (temp_strand1 = s1).Type == StrandNode.StrandType.duplex;
                Console.WriteLine($"Condition 5 (Next+2 is duplex StrandNode): {cond5}");

                bool cond6 = cond5 &&
                             complex.Values[i + 1] is LinkerNode l &&
                             ((temp_linker = l) is not null);  // 修复点：确保此表达式返回 bool
                Console.WriteLine($"Condition 6 (Middle is LinkerNode): {cond6}");

                bool cond7 = cond6 && !temp_linker.isSame(temp_strand);
                Console.WriteLine($"Condition 7 (Linker.isSame(strand)): {cond7}");
                //下面进不去，有问题 --已修复
                if ((tmpcomplex.Values.Count > 0) && tmpcomplex.Values[tmpcomplex.Values.Count - 1] is StrandNode strand2 && strand2.Type == StrandNode.StrandType.duplex && complex.Values[i] is StrandNode strand && strand.Type != StrandNode.StrandType.duplex && i + 2 < complex.Values.Count && complex.Values[i + 2] is StrandNode strand1 && strand1.Type == StrandNode.StrandType.duplex && complex.Values[i + 1] is LinkerNode linker && !linker.isSame(strand))
                {
                    //补完后面的上链，遇到上连接双链停止
                    if (strand.Type == StrandNode.StrandType.upper)
                    {
                        StrandNode common = new StrandNode();
                        int j = 0;
                        for (; j < Math.Min(strand.GetLength(), strand1.GetLength()); j++)//看看能匹配哪些，有可能是0个
                        {
                            if (strand.getDom(j).Equals(strand1.getDom(j)))
                            {
                                common.link(new SeqNode() { Value = strand.getDom(j) });
                            }
                            else break;
                        }
                        if (common.seq != null)
                            strand2.link(common.seq.DeepCopy() as SeqNode);//扩展前面的双链
                        StrandNode tmp1 = new StrandNode();//剩下的单链
                        tmp1.Type = StrandNode.StrandType.upper;
                        for (int k = j; k < strand.GetLength(); k++)
                        {
                            tmp1.link(new SeqNode() { Value = strand.getDom(k) });
                        }
                        if (tmp1.seq != null)
                            tmpcomplex.Values.Add(tmp1);//把剩下的上链添加上去
                        if (j < strand1.GetLength())//说明没有把后面双链的上链完全替换下来
                        {
                            tmpcomplex.Values.Add(new LinkerNode() { Type = LinkerNode.LinkerType.lower });//添加原本的连接符
                            common.Type = StrandNode.StrandType.upper;
                            if (common.seq != null)
                                tmpcomplex.Values.Add(common);
                            StrandNode tmp2 = new StrandNode();//剩下的双链部分
                            tmp2.Type = StrandNode.StrandType.duplex;
                            for (int k = j; k < strand1.GetLength(); k++)
                            {
                                tmp2.link(new SeqNode() { Value = strand1.getDom(k) });
                            }
                            tmpcomplex.Values.Add(tmp2);
                            i = i + 2;
                        }
                        else //说明原先双链的上链被完全取代
                        {
                            StrandNode upper = (strand1.DeepCopy() as StrandNode).getUpper();
                            StrandNode lower = new StrandNode();
                            lower.Type = StrandNode.StrandType.lower;
                            LinkerNode linker1 = null;
                            int i2 = i + 3;//从被替换的双链后面开始
                            for (; i2 < complex.Values.Count; i2++)//扩展之前双链右边从属的上链和下链
                            {
                                if (complex.Values[i2] is LinkerNode)
                                {
                                    linker1 = complex.Values[i2] as LinkerNode;
                                    break;
                                }
                                else if (complex.Values[i2] is StrandNode strand3)
                                {
                                    if (strand3.Type == StrandNode.StrandType.upper)
                                    {
                                        upper.link(strand3.seq.DeepCopy() as SeqNode);
                                    }
                                    else if (strand3.Type == StrandNode.StrandType.lower)
                                    {
                                        lower.link(strand3.seq.DeepCopy() as SeqNode);
                                    }
                                }
                            }
                            if (lower.seq != null)
                            {
                                tmpcomplex.Values.Add(lower);
                            }
                            if (linker1 == null)//说明已经到了最后
                            {
                                complexes.Add(tmpcomplex);
                                complexes.Add(new StrandNode() { Type = StrandNode.StrandType.upper, seq = upper.seq });
                                return complexes;
                            }
                            else//说明后面还有双链
                            {
                                i2++;//跳过连接符
                                if (linker1.Type == LinkerNode.LinkerType.upper)//上连接，形成新的复合物
                                {
                                    complexes.Add(tmpcomplex);
                                    tmpcomplex = new ComplexNode();
                                    for (; i2 < complex.Values.Count; i2++)//二续
                                    {
                                        if (complex.Values[i2] is StrandNode strand3)
                                        {
                                            if (strand3.Type == StrandNode.StrandType.upper)
                                            {
                                                upper.link(strand3.seq.DeepCopy() as SeqNode);//续接上链
                                            }
                                            else if (strand3.Type == StrandNode.StrandType.lower)
                                            {
                                                tmpcomplex.Values.Add(strand3);
                                            }
                                            else//这里是遇到了双链
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    i = i2 - 1;
                                    tmpcomplex.Values.Add(new StrandNode() { Type = StrandNode.StrandType.upper, seq = upper.seq });
                                }
                                else//下连接，上链成为独立单链，下链继续
                                {
                                    complexes.Add(upper);
                                    if (lower != null && lower.seq != null)
                                        tmpcomplex.Values.Add(new StrandNode() { Type = StrandNode.StrandType.lower, seq = lower.seq });
                                    tmpcomplex.Values.Add(linker1);
                                    i = i2 - 1;
                                }

                            }
                        }

                    }//下链的右迁移
                    else//说明strand是下链
                    {
                        StrandNode common = new StrandNode();
                        common.Type = StrandNode.StrandType.lower;
                        int j = 0;
                        for (; j < Math.Min(strand.GetLength(), strand1.GetLength()); j++)//看看能匹配哪些，有可能是0个
                        {
                            if (strand.getDom(j).Equals(strand1.getDom(j).GetRevComp()))//和互补的相等
                            {
                                common.link(new SeqNode() { Value = strand.getDom(j).GetRevComp() });//但是连接还是按照上链
                            }
                            else break;
                        }
                        if(common.seq!=null)
                        strand2.link(common.seq.DeepCopy() as SeqNode);//扩展前面的双链
                        StrandNode tmp1 = new StrandNode();//前者剩下的单链
                        tmp1.Type = StrandNode.StrandType.lower;
                        for (int k = j; k < strand.GetLength(); k++)
                        {
                            tmp1.link(new SeqNode() { Value = strand.getDom(k) });
                        }
                        if (tmp1.seq != null)
                            tmpcomplex.Values.Add(tmp1);//把剩下的下链添加上去
                        if (j < strand1.GetLength())//说明没有把后面双链的下链完全替换下来
                        {
                            tmpcomplex.Values.Add(new LinkerNode() { Type = LinkerNode.LinkerType.upper });//添加原本的连接符
                            common.Type = StrandNode.StrandType.lower;
                            if (common.seq != null)
                                tmpcomplex.Values.Add(new StrandNode() { Type = StrandNode.StrandType.lower,seq = common.seq.GetRevComp()});//strand的剩余下链
                            StrandNode tmp2 = new StrandNode();//剩下的双链部分
                            tmp2.Type = StrandNode.StrandType.duplex;
                            for (int k = j; k < strand1.GetLength(); k++)
                            {
                                tmp2.link(new SeqNode() { Value = strand1.getDom(k) });
                            }
                            tmpcomplex.Values.Add(tmp2);
                            i = i + 2;
                        }
                        else //说明原先双链的下链被完全取代
                        {
                            StrandNode upper = new StrandNode();
                            StrandNode lower = (strand1.DeepCopy() as StrandNode).getUpper().GetRevComp();
                            upper.Type = StrandNode.StrandType.upper;
                            LinkerNode linker1 = null;
                            int i2 = i + 3;//从被替换的双链后面开始
                            for (; i2 < complex.Values.Count; i2++)//扩展之前双链右边从属的上链和下链
                            {
                                if (complex.Values[i2] is LinkerNode)
                                {
                                    linker1 = complex.Values[i2] as LinkerNode;
                                    break;
                                }
                                else if (complex.Values[i2] is StrandNode strand3)
                                {
                                    if (strand3.Type == StrandNode.StrandType.upper)
                                    {
                                        upper.link(strand3.seq.DeepCopy() as SeqNode);
                                    }
                                    else if (strand3.Type == StrandNode.StrandType.lower)
                                    {
                                        lower.link(strand3.seq.DeepCopy() as SeqNode);
                                    }
                                }
                            }
                            if (upper.seq != null)
                            {
                                tmpcomplex.Values.Add(upper);
                            }
                            if (linker1 == null)//说明已经到了最后
                            {
                                complexes.Add(tmpcomplex);
                                complexes.Add(new StrandNode() { Type = StrandNode.StrandType.lower, seq = lower.seq });
                                return complexes;
                            }
                            else//说明后面还有双链
                            {
                                i2++;//跳过连接符
                                if (linker1.Type == LinkerNode.LinkerType.lower)//下连接，形成新的复合物
                                {
                                    complexes.Add(tmpcomplex);
                                    tmpcomplex = new ComplexNode();
                                    for (; i2 < complex.Values.Count; i2++)//二续
                                    {
                                        if (complex.Values[i2] is StrandNode strand3)
                                        {
                                            if (strand3.Type == StrandNode.StrandType.lower)
                                            {
                                                lower.link(strand3.seq.DeepCopy() as SeqNode);//续接上链
                                            }
                                            else if (strand3.Type == StrandNode.StrandType.upper)
                                            {
                                                tmpcomplex.Values.Add(strand3);
                                            }
                                            else//这里是遇到了双链
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    i = i2 - 1;
                                    tmpcomplex.Values.Add(new StrandNode() { Type = StrandNode.StrandType.lower, seq = lower.seq });
                                }
                                else//上连接，下链成为独立单链，上链继续
                                {
                                    complexes.Add(lower);
                                    if (upper != null && upper.seq != null)
                                        tmpcomplex.Values.Add(new StrandNode() { Type = StrandNode.StrandType.upper, seq = upper.seq });
                                    tmpcomplex.Values.Add(linker1);
                                    i = i2 - 1;
                                }

                            }
                        }
                    }

                }
                else
                {
                    tmpcomplex.Values.Add(complex.Values[i]);
                }
            }
            if (tmpcomplex.Values.Count > 0)
            {
                complexes.Add(tmpcomplex);
            }
            Console.WriteLine("右迁移完成:");
            foreach (var item in complexes)
            {
                Console.WriteLine(item);
            }
            return complexes;
        }
        private List<BaseComplex> leftMigration(ComplexNode complex0)
        {
            List<BaseComplex> complexes = new List<BaseComplex>();
            foreach (var item in rightMigration(complex0.Rotate()))
            {
                if (item is ComplexNode complex)
                {
                    complexes.Add(complex.Rotate());
                }
                else if (item is StrandNode strand)
                {
                    complexes.Add(strand.Rotate());
                }
            }
            return complexes;
        }
        private List<BaseComplex> tryUnlink(ComplexNode complex0)//对于仅剩一个toehold连接的双链进行解链
        {
            //int cid = AddNewSpecies(complex0);
            //if (ComUnlink.ContainsKey(cid))
            //{
            //    return ComUnlink[cid];
            //}
            List<BaseComplex> complexes = new List<BaseComplex>();
            var complex = complex0.DeepCopy() as ComplexNode;
            ComplexNode tmpcomplex = new ComplexNode();
            var complex2 = Utils.getComplexNode2s(complex);
            List<ComplexNode2> tmplex2 = new List<ComplexNode2>();
            List<List<ComplexNode2>> complexes2 = new List<List<ComplexNode2>>();
            StrandNode up = null, low = null;
            Console.WriteLine("开始解链");
            ASTPrinter.PrintAst(complex0);
            Console.WriteLine("---------------");
            for (int i = 0; i < complex2.Count; i++)
            {
                ComplexNode2 current = complex2[i];
                Console.WriteLine($"i=={i}.{current}");
                if (up != null && current.linker is LinkerNode linker1 && linker1.Type == LinkerNode.LinkerType.upper)//上一次的上链作为新的开始了
                {
                    ASTPrinter.PrintAst(Utils.GetComplexNode(tmplex2));
                    if (tmplex2.Count > 0)
                    {
                        complexes2.Add(tmplex2);
                        tmplex2 = new List<ComplexNode2>();
                    }
                    ComplexNode2 tmplow = new ComplexNode2();
                    tmplow.righttop = up;
                    up = null;
                    tmplex2.Add(tmplow);
                    ASTPrinter.PrintAst(Utils.GetComplexNode(tmplex2));
                }
                else if (low != null && current.linker is LinkerNode linker2 && linker2.Type == LinkerNode.LinkerType.lower)
                {
                    if (tmplex2.Count > 0)
                    {
                        complexes2.Add(tmplex2);
                        tmplex2 = new List<ComplexNode2>();
                    }
                    ComplexNode2 tmplow = new ComplexNode2();
                    tmplow.rightbottom = low;
                    low = null;
                    tmplex2.Add(tmplow);
                }
                if (current.middle is StrandNode middle && middle.Type == StrandNode.StrandType.duplex && middle.GetLength() == 1 && middle.getDom(0).isToe())//可以解链
                {
                    Console.WriteLine($"复合物是{current}");
                    if (current.linker is LinkerNode linker)
                    {
                        if (tmplex2.Count - 1 < 0)
                            tmplex2.Add(new ComplexNode2());
                        if (linker.Type == LinkerNode.LinkerType.upper)//需要得到上链连到前面，并把下链作为新的开始
                        {
                            tmplex2[tmplex2.Count - 1].linkwith(ComplexNode2.Linktype.righttop, current.getUpper());
                            up = null;
                            low = current.getLower();
                        }
                        else if (linker.Type == LinkerNode.LinkerType.lower)//摘出当前的上链
                        {
                            tmplex2[tmplex2.Count - 1].linkwith(ComplexNode2.Linktype.rightbottom, current.getLower());
                            low = null;
                            up = current.getUpper();
                        }
                        //complexes2.Add(tmplex2);这里添加哪个是需要看后面的
                        //tmplex2 = new List<ComplexNode2>();
                    }
                    else//说明是开头，那么分成的两个链需要依据下一个来连接上还是直接解离。
                    {
                        Console.WriteLine($"复合物是{current}");
                        up = current.getUpper();
                        Console.WriteLine($"复合物是{current}");
                        low = current.getLower();
                        Console.WriteLine($"复合物是{current}");
                        ASTPrinter.PrintAst(up);
                        Console.WriteLine("---------------");
                        ASTPrinter.PrintAst(low);
                    }

                }
                else//当前链不能解链
                {
                    if (current.linker is LinkerNode linker)//把前面分出来的单链放进去
                    {
                        if (linker.Type == LinkerNode.LinkerType.upper && low != null)//说明前面生成的下链要放进去了
                        {
                            ComplexNode2 tmplow = new ComplexNode2();
                            tmplow.leftbottom = low.DeepCopy() as StrandNode;
                            low = null;
                            complexes2.Add(new List<ComplexNode2>() { tmplow });
                            //continue;//这个连接符相当于废掉了
                            if (tmplex2.Count == 0)
                                current.linker = null;
                            else if (tmplex2.Count == 1 && tmplex2[0].middle == null)
                            {
                                var front = tmplex2[0];
                                tmplex2.Remove(front);
                                var lt = current.lefttop;
                                front.righttop.link(lt);
                                Console.WriteLine($"front == {front}");
                                current.lefttop = front.righttop;
                                current.linker = null;
                            }
                        }
                        else if (linker.Type == LinkerNode.LinkerType.lower && up != null)
                        {
                            ComplexNode2 tmplow = new ComplexNode2();
                            tmplow.leftbottom = up.DeepCopy() as StrandNode;
                            up = null;
                            complexes2.Add(new List<ComplexNode2>() { tmplow });
                            if (tmplex2.Count == 0)
                                current.linker = null;
                            else if (tmplex2.Count == 1 && tmplex2[0].middle == null)
                            {
                                var front = tmplex2[0];
                                var lb = current.leftbottom;
                                tmplex2.Remove(front);
                                front.rightbottom.link(lb);
                                Console.WriteLine($"front == {front}");
                                current.leftbottom = front.rightbottom;
                                current.linker = null;
                            }
                        }
                        else//还有其他情况吗？
                        {
                            //if(linker.Type == LinkerNode.LinkerType.lower)
                            //{

                            //}
                        }
                    }
                    //else//没有连接符的话前面不可能有解链的结果
                    tmplex2.Add(current);
                }
            }
            Console.WriteLine("---------------");
            Console.WriteLine(tmplex2.Count);
            Console.WriteLine(complexes2.Count);
            if (tmplex2.Count > 0)
            {
                complexes2.Add(tmplex2);
            }
            if (up != null)
            {
                ComplexNode2 tmplow = new ComplexNode2();
                tmplow.leftbottom = up;
                List<ComplexNode2> tmplex22 = new List<ComplexNode2>();
                tmplex22.Add(tmplow);
                complexes2.Add(tmplex22);
            }
            if (low != null)
            {
                ComplexNode2 tmplow = new ComplexNode2();
                tmplow.leftbottom = low;
                List<ComplexNode2> tmplex22 = new List<ComplexNode2>();
                tmplex22.Add(tmplow);
                complexes2.Add(tmplex22);
            }
            foreach (var item in complexes2)
            {
                complexes.Add(Utils.GetComplexNode(item));
            }
            //ComUnlink.Add(cid, complexes);
            foreach(var item in complexes)
                Console.WriteLine(item);
            return complexes;
        }
        private void deleteUseless()//删除无效反应
        {

        }

        private void getStickss(List<ComplexNode> list)//递归获取两单链之间的结合-待完成
        {

        }
        //把连接符两端要连接的都移到左边去
        private ComplexNode moveSameTypeStrand(ComplexNode node)//[a]<v>::<u>[a]->[a]<v u>::[a]  [a]{v}:{u}[a]->[a]{v u}:[a]
        {
            ComplexNode complex = new ComplexNode();
            int lastduplex = -1, i = 0;
            while (i < node.Values.Count)
            {
                while (i < node.Values.Count && (node.Values[i] is not StrandNode || (node.Values[i] is StrandNode strand && strand.Type != StrandNode.StrandType.duplex)))
                {
                    complex.Values.Add(node.Values[i]);
                    i++;
                }
                complex.Values.Add(node.Values[i]);
                LinkerNode linker = null;
                i++;
                int j = 0;
                StrandNode upper = new StrandNode(), lower = new StrandNode();
                for (; j < 5 && i + j < complex.Values.Count; j++)
                {
                    if (complex.Values[i + j] is LinkerNode)
                    {
                        linker = complex.Values[i + j] as LinkerNode;
                        break;
                    }
                    else if (complex.Values[i + j] is StrandNode strand && strand.Type != StrandNode.StrandType.duplex)
                    {
                        if (strand.Type == StrandNode.StrandType.upper)
                        {
                            upper.link(strand.seq.DeepCopy() as SeqNode);
                        }
                        else if (strand.Type == StrandNode.StrandType.lower)
                        {
                            lower.link(strand.seq.DeepCopy() as SeqNode);
                        }
                    }
                }
                if (linker != null)
                {
                    if (linker.Type == LinkerNode.LinkerType.upper)
                    {
                        if (lower.seq != null)
                            complex.Values.Add(lower);
                    }
                    else if (linker.Type == LinkerNode.LinkerType.lower)
                    {
                        if (upper.seq != null)
                            complex.Values.Add(upper);
                    }
                    j++;
                    for (; j < 5 && i + j < complex.Values.Count; j++)
                    {
                        if (complex.Values[i + j] is StrandNode strand)
                        {
                            if (strand.Type == StrandNode.StrandType.duplex)
                            {
                                break;
                            }
                            if (strand.Type == StrandNode.StrandType.upper)
                            {
                                if (linker.Type == LinkerNode.LinkerType.lower)
                                {
                                    upper = strand;
                                }
                                else
                                {
                                    upper.link(strand.seq.DeepCopy() as SeqNode);
                                }
                            }
                            else if (strand.Type == StrandNode.StrandType.lower)
                            {
                                if (linker.Type == LinkerNode.LinkerType.upper)
                                {
                                    lower = strand;
                                }
                                else
                                {
                                    lower.link(strand.seq.DeepCopy() as SeqNode);
                                }
                            }

                        }
                    }
                    if (linker.Type == LinkerNode.LinkerType.upper)
                    {
                        if (upper.seq != null)
                            complex.Values.Add(upper);
                        complex.Values.Add(linker);
                        if (lower.seq != null)
                            complex.Values.Add(lower);
                    }
                    else if (linker.Type == LinkerNode.LinkerType.lower)
                    {
                        if (lower.seq != null)
                            complex.Values.Add(lower);
                        complex.Values.Add(linker);
                        if (upper.seq != null)
                            complex.Values.Add(upper);
                    }
                }
                else
                {
                    if (lower.seq != null)
                        complex.Values.Add(lower);
                    if (upper.seq != null)
                        complex.Values.Add(upper);
                }
                i += j;
            }
            return complex;
        }

        private void Stick_Recursion(List<stickInfo> complexes, strandInfos strand1s, int start, ComplexNode complex0, ComplexNode complex1, StrandNode strand, int num)//简化反应
        {
            Console.WriteLine("\n开始Stick_Recursion\n");
            ComplexNode node = complex1.DeepCopy() as ComplexNode;
            Console.WriteLine("node:\n");
            ASTPrinter.PrintAst(node);
            Console.WriteLine("complex0:\n");
            ASTPrinter.PrintAst(complex0);
            for (int i = start; i < complex0.Values.Count; i++)
            {
                //保证是 []<i>[]这种情况的[]{i}[]或是<>[],[]{}
                if ((i - 1 < 0 || (complex0.Values[i - 1] is StrandNode duplex1 && duplex1.Type == StrandNode.StrandType.duplex)) && (complex0.Values[i] is StrandNode strand1 && strand1.Type != StrandNode.StrandType.duplex) && ((i + 1 >= complex0.Values.Count) || ((complex0.Values[i + 1] is StrandNode duplex2 && duplex2.Type == StrandNode.StrandType.duplex) || (i + 2 < complex0.Values.Count && complex0.Values[i + 1] is LinkerNode linker && complex0.Values[i + 2] is StrandNode duplex3 && duplex3.Type == StrandNode.StrandType.duplex))))
                {
                    Console.WriteLine("strand::");
                    ASTPrinter.PrintAst(strand);
                    Console.WriteLine("strand1::");
                    ASTPrinter.PrintAst(strand1);//当前的是strand1
                    var strand11 = strand1.DeepCopy() as StrandNode;
                    bool st1rotate = false;
                    if (strand11.Type != StrandNode.StrandType.lower)
                    {
                        strand11 = strand11.Rotate();
                        st1rotate = true;
                    }
                    Console.WriteLine("strand11::");
                    ASTPrinter.PrintAst(strand11);

                    var matchinfo = MatchToehold(strand, strand11, strand1s);
                    if (matchinfo == null || matchinfo.Count == 0)
                    {
                        node.Values.Add(complex0.Values[i]);
                        continue;
                    }
                    LinkerNode.LinkerType linker1 = LinkerNode.LinkerType.lower;
                    if (st1rotate)
                    {
                        linker1 = LinkerNode.LinkerType.upper;
                    }
                    foreach (var item in matchinfo)//这里后面可以替换成对粘接后的复合物进行遍历，而非对信息遍历在此粘合
                    {
                        //这里需要确认第一个是上链，第二个是下链

                        ComplexNode complex2 = Stickss(strand, strand11, item) as ComplexNode;
                        if (st1rotate)
                        {
                            complex2 = complex2.Rotate();
                        }
                        Console.WriteLine("complex2::\n");
                        ASTPrinter.PrintAst(complex2);
                        Console.WriteLine("complex0::\n");
                        ASTPrinter.PrintAst(complex0);
                        Console.WriteLine("node::\n");
                        ASTPrinter.PrintAst(node);
                        var nextcomplex = complex2.DeepCopy() as ComplexNode;

                        if (node.Values.Count != 0)
                            nextcomplex = LinkComplexWithComplex(node, complex2, linker1);
                        bool flag = false, flag2 = false;
                        for (int j = 0; j < 5 && i + j < complex0.Values.Count; j++)
                        {
                            Console.WriteLine($"{complex0.Values[i + j]}");
                            ASTPrinter.PrintAst(complex0.Values[i + j]);
                            if (complex0.Values[i + j] is StrandNode strand2 && strand2.Type == StrandNode.StrandType.duplex)
                            {
                                flag = true;
                                break;
                            }
                            if (!flag && complex0.Values[i + j] is LinkerNode)
                            {
                                flag2 = true;
                            }
                        }
                        if (flag && !flag2)//后面还有双链的话
                            nextcomplex.Values.Add(new LinkerNode() { Type = linker1 });//往后面连接上
                        Stick_Recursion(complexes, strand1s, i + 1, complex0, nextcomplex, strand, num + 1);
                    }
                    return;
                }
                else
                {
                    node.Values.Add(complex0.Values[i]);
                }
                Console.WriteLine("node::\n");
                ASTPrinter.PrintAst(node);
            }
            if(num!=0)
                complexes.Add(new stickInfo() { num = num, complex = node });
        }

        //调用前需要确保单链是上链
        private void Stick_Recursion(List<ComplexNode> complexes, strandInfos strand1s, int start, ComplexNode complex0, ComplexNode complex1, StrandNode strand)//简化反应
        {
            ComplexNode node = complex1.DeepCopy() as ComplexNode;
            Console.WriteLine("node:\n");
            ASTPrinter.PrintAst(node);
            for (int i = start; i < complex0.Values.Count; i++)
            {
                //保证是 []<i>[]这种情况的[]{i}[]或是<>[],[]{}
                if ((i - 1 < 0 || (complex0.Values[i - 1] is StrandNode duplex1 && duplex1.Type == StrandNode.StrandType.duplex)) && (complex0.Values[i] is StrandNode strand1 && strand1.Type != StrandNode.StrandType.duplex) && ((i + 1 >= complex0.Values.Count) || ((complex0.Values[i + 1] is StrandNode duplex2 && duplex2.Type == StrandNode.StrandType.duplex) || (i + 2 < complex0.Values.Count && complex0.Values[i + 1] is LinkerNode linker && complex0.Values[i + 2] is StrandNode duplex3 && duplex3.Type == StrandNode.StrandType.duplex))))
                {
                    Console.WriteLine("strand::");
                    ASTPrinter.PrintAst(strand);
                    Console.WriteLine("strand1::");
                    ASTPrinter.PrintAst(strand1);//当前的是strand1
                    var strand11 = strand1.DeepCopy() as StrandNode;
                    bool st1rotate = false;
                    if (strand11.Type != StrandNode.StrandType.lower)
                    {
                        strand11 = strand11.Rotate();
                        st1rotate = true;
                    }
                    var matchinfo = MatchToehold(strand, strand11, strand1s);
                    if (matchinfo == null || matchinfo.Count == 0)
                    {
                        node.Values.Add(complex0.Values[i]);
                        continue;
                    }
                    LinkerNode.LinkerType linker1 = LinkerNode.LinkerType.lower;
                    if (st1rotate)
                    {
                        linker1 = LinkerNode.LinkerType.upper;
                    }
                    foreach (var item in matchinfo)//这里后面可以替换成对粘接后的复合物进行遍历，而非对信息遍历在此粘合
                    {
                        //这里需要确认第一个是上链，第二个是下链

                        ComplexNode complex2 = Stickss(strand, strand11, item) as ComplexNode;
                        if (st1rotate)
                        {
                            complex2 = complex2.Rotate();
                        }
                        Console.WriteLine("complex2::\n");
                        ASTPrinter.PrintAst(complex2);
                        Console.WriteLine("complex0::\n");
                        ASTPrinter.PrintAst(complex0);
                        Console.WriteLine("anode::\n");
                        ASTPrinter.PrintAst(node);
                        var nextcomplex = complex2.DeepCopy() as ComplexNode;

                        if (node.Values.Count != 0)
                            nextcomplex = LinkComplexWithComplex(node, complex2, linker1);
                        Console.WriteLine("nextcomplex");
                        ASTPrinter.PrintAst(nextcomplex);
                        bool flag = false;
                        for (int j = 0; j < 5; j++)
                        {
                            if (complex0.Values[i + j] is StrandNode strand2 && strand2.Type == StrandNode.StrandType.duplex)
                            {
                                flag = true;
                                break;
                            }
                        }
                        if (flag)//后面还有双链的话
                            nextcomplex.Values.Add(new LinkerNode() { Type = linker1 });//往后面连接上
                        Stick_Recursion(complexes, strand1s, i + 1, complex0, nextcomplex, strand);
                    }
                    return;
                }
                else
                {
                    node.Values.Add(complex0.Values[i]);
                }
                Console.WriteLine("bnode::\n");
                ASTPrinter.PrintAst(node);
            }
            complexes.Add(node);
        }
        private List<reaction> getRevFromComplex(ComplexNode complex0)
        {
            List<reaction> reactions = new List<reaction>();
            reaction _reaction = new reaction();
            var complex = complex0.DeepCopy() as ComplexNode;
            int lastduplex = -1;
            ComplexNode tmp = new ComplexNode();

            for (int i = 0; i < complex.Values.Count; i++)
            {
                while (i < complex.Values.Count && (complex.Values[i] is not StrandNode || (complex.Values[i] is StrandNode strand && strand.Type != StrandNode.StrandType.duplex)))
                {
                    i++;
                }
                if (i >= complex.Values.Count) break;
                //找到双链，判断其是否只有一个toehold
                StrandNode strand1 = complex.Values[i] as StrandNode;
                if (strand1.canRev())//说明结合部分只有一个可以解链的
                {
                    bool up = false;//是不是上连接？其实看看完成后还有那条链剩下就好了
                    StrandNode upper = new StrandNode(), lower = new StrandNode();
                    //往前看
                    for (int j = lastduplex + 1; j < i; j++)
                    {
                        if (complex.Values[j] is LinkerNode linker)
                        {
                            if (linker.Type == LinkerNode.LinkerType.upper)
                                up = true;
                        }
                        else if (complex.Values[j] is StrandNode strand)
                        {
                            if (strand.Type == StrandNode.StrandType.upper)
                            {
                                upper.link(strand.seq);
                            }
                            else if (strand.Type == StrandNode.StrandType.lower)
                            {
                                lower.link(strand.seq);
                            }
                        }
                    }
                    //_reaction.product.Add
                    //处理中间
                    StrandNode strand2 = (complex.Values[i] as StrandNode).getUpper();
                    upper.link(strand2.DeepCopy() as SeqNode);
                    lower.link(strand2.GetRevComp().DeepCopy() as SeqNode);
                    //往后看
                    for (int j = i + 1; j < complex.Values.Count; j++)
                    {
                        if (complex.Values[j] is LinkerNode linker)
                        {
                            if (linker.Type == LinkerNode.LinkerType.upper)
                                up = true;
                        }
                        else if (complex.Values[j] is StrandNode strand)
                        {
                            if (strand.Type == StrandNode.StrandType.upper)
                            {
                                upper.link(strand.seq);
                            }
                            else if (strand.Type == StrandNode.StrandType.lower)
                            {
                                lower.link(strand.seq);
                            }
                        }
                    }
                }
                else
                {
                    for (int j = lastduplex + 1; j <= i; j++)
                    {
                        tmp.Values.Add(complex.Values[j]);
                    }
                    lastduplex = i;
                }
            }


            return null;
        }
        private void allocolor(DomNode domm)
        {
            int i = 0;
            if(variableContext.id.Contains(domm.Name.ToString()))
            {
                //i = variableContext.id.Find(dom.Name.ToString());
                i = variableContext.id.IndexOf(domm.Name.ToString());
                //return;//如果在里面就假设有颜色了吧
                if(i == -1)
                {
                    Console.WriteLine($"没有找到域 {domm.Name.ToString()}");
                    return;
                }
            }
            else
            {
                var ne = new BinaryExpression();
                ne.Left = new NameNode(domm.Name.ToString());
                ne.Right = domm;
                ne.Operator = Operator.Equal;
                addVariable(ne);
                i = variableContext.id.Count - 1;
            }
            if (variableContext.variables[variableContext.id[i]] is DomNode dom)
            {
                if (dom.isToe() && dom.colour == null)
                {
                    // 找一个未使用的颜色
                    string colorToAssign = null;
                    foreach (string color in colorss)
                    {
                        if (!usedColors.Contains(color))
                        {
                            colorToAssign = color;
                            break;
                        }
                    }

                    if (colorToAssign == null)
                    {
                        colorToAssign = colorss[usedColors.Count % colorss.Count];
                    }

                    StringNode colorNode = new StringNode() { Value = colorToAssign };
                    dom.colour = colorNode;
                    usedColors.Add(colorToAssign);
                    colors.Add(variableContext.id.Count + usedColors.Count, colorToAssign);

                    Console.WriteLine($"为域 {variableContext.id[i]} 分配颜色: {colorToAssign}");
                }
            }
        }
        private void addColors()
        {
            foreach (var color in colors.Values)
            {
                usedColors.Add(color);
            }
            for (int i = 0;i<speciesContext.species_id.Count;i++)
            {
                if (speciesContext.species_id[i] != null)
                {
                    if (speciesContext.species_id[i] is StrandNode strand)
                    {
                        if (strand.seq.Value is DomNode dom)
                            allocolor(dom);
                        else if (strand.seq.Value is List<SeqNode> seqs)
                        {
                            foreach (var item in seqs)
                            {
                                if (item.Value is DomNode dom1)
                                    allocolor(dom1);
                            }
                        }
                        
                    }
                    else if (speciesContext.species_id[i] is ComplexNode complex)
                    {
                        foreach(var item in complex.Values)
                        {
                            if(item is StrandNode strandd)
                            {
                                if (strandd.seq.Value is DomNode dom)
                                    allocolor(dom);
                                else if (strandd.seq.Value is List<SeqNode> seqs)
                                {
                                    foreach (var item1 in seqs)
                                    {
                                        if (item1.Value is DomNode dom1)
                                            allocolor(dom1);
                                    }
                                }
                            }
                            
                        }
                    }
                }
                
            }
        }
        private bool InitPlots()//三种情况 直接是一个普通物种，2是带通配符的物种单独显示，3是sum(_)把符合条件的加进来。或者一开始是先检查是否符合条件，最后整理数据的时候干脆直接用字符串匹配了。
        {
            var plots = systemSetting.plots;
            if (plots == null || plots.Count == 0)
            {
                Console.WriteLine("没有设置绘图参数"); 
                return true;
            }
            foreach(var item in plots)
            {
                if(item is FuncNode func)
                {
                    Console.WriteLine("函数名称："+func.Name);
                    Console.WriteLine("参数数目："+func.Arguments.Count);
                    if(func.Arguments.Count != 0)
                    {
                        Console.WriteLine("参数：");
                        foreach (var arg in func.Arguments)
                        {
                            Console.Write(arg + " ");
                        }
                        Console.WriteLine();
                    }
                    
                    if(func.Expressions!=null && func.Expressions is BaseComplex complex)
                    {
                        Console.WriteLine("函数体为："+complex);
                        if(complex is ComplexNode node&&!checkComplex(node,false))
                        {
                            errors.AddError(func.Line,func.Column,"复合物不符合要求");
                            return false;
                        }

                    }

                }
                else if(item is ComplexNode complex)
                {
                    Console.WriteLine("复合物：" + complex);
                    if (complex is ComplexNode node && !checkComplex(node, false))
                    {
                        errors.AddError(complex.Line, complex.Column, "复合物不符合要求");
                        return false;
                    }
                }
            }
            return true;
        }
        public bool Init()//完整的初始化
        {
            root = new Parser(new Lexer(code).GetTokenList()).Parse();//或许能把错误统一收集下？
            if (root == null)
            {
                errors.PrintError();
                return false;
            }
            Console.WriteLine("解析完成");
            ASTPrinter.PrintAst(root);
            Console.WriteLine("打印完成");
            bool flag = true;
            foreach (var statement in root.statements)
            {
                if (statement is DirectiveNode)
                {
                    Console.WriteLine("SettingInit");
                    flag &= SettingInit(statement);
                }
                else if (statement is DeclareNode)
                {
                    Console.WriteLine("CollectVariables");
                    bool ans = CollectVariables(statement);
                    flag &= ans;
                }
                else if (statement is ProcessNode)
                {
                    Console.WriteLine("CollectSpecies");
                    bool ans = CollectSpecies(statement);
                    flag &= ans;
                }
            }
            flag &= InitPlots();
            errors.Success = flag;
            if (!flag)
            {
                errors.PrintError();
                return false;
            }
            addColors();
            for (int i = 0; i < speciesContext.species_id.Count; i++)
            {
                initial_species.Add(i);
                addColor(speciesContext.species_id[i]);
                Console.WriteLine($"species_id:{i}");
            }
            hasInit = true;
            
            return true;
        }

#if DEBUG
        public struct info_tmp
        {
            public int id1, id2;
            public matchInfo info;
            public void Print()
            {
                Console.WriteLine($"id1:{id1},id2:{id2}");
                info.Print();
            }
        };
        private void testMatch(bool skip = false)
        {
            foreach (var item in speciesContext.species_id)
            {
                //Console.WriteLine("----id == {}");
                addColor(item);
                var thing = ComplexPrinter.PrintBaseComplex(item);
                if (thing.Count > 0)
                {
                    ComplexPrinter.GetSvg(ComplexPrinter.Normal(thing)).Save($"{Timeutil.GetNow()}.svg");
                }
            }
            if (skip)
                return;
            Dictionary<GuidPair, bool> rexist = new Dictionary<GuidPair, bool>();
            List<info_tmp> matchess = new List<info_tmp>();
            for (int i = 0; i < reactionContext.uppers.Count; i++)
            {
                for (int j = 0; j < reactionContext.lowers.Count; j++)
                {
                    if (reactionContext.uppers[i].id == reactionContext.lowers[j].id)//需要避免来自同一个复合物
                    {
                        continue;
                    }
                    var tmpstrand = speciesContext.species_id[reactionContext.uppers[i].id] as StrandNode;
                    addColor(tmpstrand);
                    Console.WriteLine($"&&&&&&&&&&&&&&&&&&&&&&&&&");
                    ASTPrinter.PrintAst(tmpstrand);
                    var tmpline = ComplexPrinter.PrintStrand(tmpstrand, 2);
                    Console.WriteLine($"&&&&&&&&&&&&&&&&&&&&&&&&&");
                    ComplexPrinter.GetSvg(tmpline).Save($"test{i}_{j}.svg");
                    if (rexist.ContainsKey(new GuidPair(reactionContext.uppers[i].guid, reactionContext.lowers[j].guid))) continue;//说明之前已经发生过了
                    rexist[new GuidPair(reactionContext.uppers[i].guid, reactionContext.lowers[j].guid)] = true;
                    Console.WriteLine($"开始匹配{reactionContext.uppers[i].id},{reactionContext.lowers[j].id}");
                    StrandNode strand1 = reactionContext.uppers[i].strand;
                    StrandNode strand2 = reactionContext.lowers[j].strand;

                    List<matchInfo> matches = MatchToehold(strand1, strand2);
                    Console.WriteLine($"匹配的数量为{matches.Count}");
                    foreach (var item in matches)
                    {
                        matchess.Add(new info_tmp() { id1 = reactionContext.uppers[i].id, id2 = reactionContext.lowers[j].id, info = item });
                        item.Print();
                        Console.WriteLine("\n💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕");
                        Console.WriteLine($"匹配成功，长度为{item.length},此时拼装的产物为");
                        ExpressionPrinter.PrintExpression(Stickss(strand1, strand2, item));
                        Console.WriteLine("\n💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕💕");
                        addColor(strand1);
                        addColor(strand2);
                        ComplexPrinter.GetSvg(ComplexPrinter.Normal(ComplexPrinter.PrintStrand(strand1))).Save($"strand1{Timeutil.GetNow()}.svg");
                        ComplexPrinter.GetSvg(ComplexPrinter.Normal(ComplexPrinter.PrintStrand(strand2))).Save($"strand2{Timeutil.GetNow()}.svg");
                        ComplexNode strand = Stickss(strand1, strand2, item) as ComplexNode;
                        addColor(strand);
                        ASTPrinter.PrintAst(strand);
                        ComplexPrinter.GetSvg(ComplexPrinter.Normal(ComplexPrinter.PrintBaseComplex(strand))).Save($"complex{Timeutil.GetNow()}.svg");

                    }
                }
            }

            variableContext.Printer();
            speciesContext.Printer();
            reactionContext.Printer();
            Console.WriteLine("\n************************************\n");

            foreach (var item in matchess)
            {
                item.Print();
            }
        }
#endif
        private void Printer()//输出
        {
            systemSetting.Printer();
            variableContext.Printer();
            speciesContext.Printer();

            processContext.Printer();
            reactionContext.Printer();
        }
        struct reaction2
        {
            public double rate1;
            public double rate2;
            public List<BaseComplex> reactant;
            public List<BaseComplex> product;
        }
        struct tmpreaction
        {
            public ComplexNode complex;
            public StrandNode strand;
            public List<double> nums;
            public List<ComplexNode> complexs;
        }
        struct stickInfo
        {
            public int num;
            public ComplexNode complex;
        }

        private List<term> getFromListCom(List<BaseComplex> bases)
        {
            List<term> terms = new List<term>();
            Dictionary<BaseComplex, int> dict;
            dict = new Dictionary<BaseComplex, int>();
            foreach (var item in bases)
            {

                if (dict.ContainsKey(item))
                {
                    dict[item]++;
                }
                else
                {
                    dict[item] = 1;
                }
            }
            foreach (var item in dict)
            {
                term term = new term();
                term.complex = item.Key;
                term.num = item.Value;
                terms.Add(term);
            }
            return terms;
        }

        private process getProcessFromTerm(term term)
        {
            process process = new process();
            process.num = term.num;
            process.id = AddNewSpecies(term.complex);
            Console.WriteLine($"添加的物种：{process.id}");
            ASTPrinter.PrintAst(term.complex);
            return process;
        }
        private Dictionary<int, int> getDiiFrom3(List<term> terms)
        {
            Dictionary<int, int> dict = new Dictionary<int, int>();
            foreach (var item in terms)
            {
                process process = getProcessFromTerm(item);
                if (!dict.ContainsKey(process.id))
                {
                    dict.Add(process.id, Math.Max((int)process.num, (int)process.num));
                }
                else
                {
                    dict[process.id] += Math.Max((int)process.num, (int)process.num);
                }
            }
            return dict;
        }
        private reaction4 getReactionFrom3(reaction3 reaction3)
        {
            reaction4 reaction = new reaction4();
            reaction.rate1 = reaction3.rate1;
            reaction.rate2 = reaction3.rate2;
            reaction.reactant = getDiiFrom3(reaction3.reactant);
            reaction.product = getDiiFrom3(reaction3.product);

            Console.WriteLine($"\n转换后的反应：\n{reaction}\n");
            return reaction;
        }
        struct rp
        {
            public Dictionary<int, int> reactant { get; set; }
            public Dictionary<int, int> product { get; set; }
            public override string ToString()
            {
                string ans = "";
                foreach (var item in reactant)
                {
                    ans += $"{item.Key}*{item.Value} ";
                }
                ans += "-> ";
                foreach (var item in product)
                {
                    ans += $"{item.Key}*{item.Value} ";
                }
                return ans;
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    void AddDictionaryToHash(Dictionary<int, int> dict)
                    {
                        foreach (var kvp in dict)
                        {
                            hash = hash * 23 + kvp.Key.GetHashCode();
                            hash = hash * 23 + kvp.Value.GetHashCode();
                        }
                    }
                    if (reactant != null)
                    {
                        AddDictionaryToHash(reactant);
                    }
                    hash = hash * 23 + 31;

                    if (product != null)
                    {
                        AddDictionaryToHash(product);
                    }
                    return hash;
                }
            }
            public override bool Equals(object obj)
            {
                if (obj is not rp other)
                    return false;
                if (reactant.Count != other.reactant.Count || product.Count != other.product.Count)
                    return false;
                foreach (var item in reactant)
                {
                    if (!other.reactant.ContainsKey(item.Key) || other.reactant[item.Key] != item.Value)
                        return false;
                }
                foreach (var item in product)
                {
                    if (!other.product.ContainsKey(item.Key) || other.product[item.Key] != item.Value)
                        return false;
                }
                return true;
            }
        }
        struct tmpss
        {
            public Dictionary<int, int> val;
            public override string ToString()
            {
                string ans = "";
                foreach (var item in val)
                {
                    ans += $"{item.Key}*{item.Value} ";
                }
                return ans;
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var kvp in val)
                    {
                        hash = hash * 23 + kvp.Key.GetHashCode();
                        hash = hash * 23 + kvp.Value.GetHashCode();
                    }
                    return hash;
                }
            }
            public override bool Equals(object obj)
            {
                if (obj is not tmpss other)
                    return false;
                if (val.Count != other.val.Count)
                    return false;
                foreach (var item in val)
                {
                    if (!other.val.ContainsKey(item.Key) || other.val[item.Key] != item.Value)
                        return false;
                }
                return true;
            }
        }
        private List<reaction4> sthRec(List<reaction4> reactions)
        {
            List<reaction4> reactions2 = new List<reaction4>();
            Console.WriteLine(initial_species.Count);
            foreach (var reaction in reactions)
            {
                bool can = true;
                foreach (var item in reaction.reactant)
                {
                    if (!initial_species.Contains(item.Key))
                    {
                        can = false;
                        break;
                    }
                }
                if (!can) continue;
                foreach (var item in reaction.product)
                {
                    if (!initial_species.Contains(item.Key))
                    {
                        initial_species.Add(item.Key);
                    }
                }
                Console.WriteLine($"反应2：{reaction}");
                reactions2.Add(reaction);
            }
            return reactions2;
        }
        private List<reaction4> NormalizeReactions(List<reaction3> reactions)//消除无效反应、重复反应，合并a+b->c c->e+f这种为a+b->e+f
        {
            Console.WriteLine("原来的反应");
            List<reaction3> reactions32 = new List<reaction3>();
            foreach (var item in reactions)
            {
                Console.WriteLine(item);
                Console.WriteLine("\n===================================");
                if (item.vaidate())
                {
                    reactions32.Add(item);
                }
            }
            Console.WriteLine("开始规范化反应");
            List<reaction4> reactionss = new List<reaction4>();
            foreach (var item in reactions32)
            {
                reactionss.Add(getReactionFrom3(item));
            }
            foreach (reaction4 item in reactionss)
            {
                Console.WriteLine(item);
                Console.WriteLine("\n++++++++++++++++++++++++++++++++++++++");
            }
            List<reaction4> reactions2 = new List<reaction4>();

            Dictionary<rp, bool> rev = new();

            foreach (var reaction in reactionss)//去除无效反应。
            {
                foreach (var item in reaction.reactant)
                {
                    if (reaction.product.ContainsKey(item.Key))
                    {
                        int left = reaction.product[item.Key] - item.Value;
                        if (left < 0)
                        {
                            reaction.product.Remove(item.Key);
                            reaction.reactant[item.Key] = -left;
                        }
                        else if (left == 0)
                        {
                            reaction.product.Remove(item.Key);
                            reaction.reactant.Remove(item.Key);
                        }
                        else
                        {
                            reaction.product[item.Key] = left;
                            reaction.reactant.Remove(item.Key);
                        }
                    }
                }

            }
            List<tmpss> unRev = new();

            foreach (var reaction in reactionss)
            {
                if (reaction.reactant.Count == 0 || reaction.product.Count == 0)
                {
                    continue;
                }
                if (reaction.rate2 == 0)
                {
                    Console.WriteLine($"不可逆反应：{reaction}，rate2为0");
                    unRev.Add(new tmpss() { val = reaction.reactant });
                }
                reactions2.Add(reaction);
            }
            var reactions3 = new List<reaction4>();
            List<rp> rptest = new List<rp>();
            foreach (var reaction in reactions2)//这里可能有问题，因为重复的可逆反应出现次序可逆不一定一样，，
            {
                reaction.reactant = reaction.reactant.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                reaction.product = reaction.product.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                //去除重复的可逆反应 A<->B 和B<->A实际上是一样的
                Console.WriteLine($"反应：{reaction}");
                if (reaction.rate2 > 0 && unRev.Contains(new tmpss() { val = reaction.reactant }))
                {
                    continue;
                }
                if (rev.ContainsKey(new rp() { reactant = reaction.reactant, product = reaction.product }) || rev.ContainsKey(new rp() { product = reaction.reactant, reactant = reaction.product }))
                {
                    continue;
                }
                else
                {
                    reactions3.Add(reaction);
                    rev.Add(new rp() { reactant = reaction.reactant, product = reaction.product }, true);
                }
            }

            foreach (var reaction in reactions3)
            {
                Console.WriteLine($"反应3：{reaction}");
            }

            //去除不可达反应
            int cnt = 0;

            var reactions4 = new List<reaction4>();
            while (cnt != initial_species.Count)
            {
                cnt = initial_species.Count;
                reactions4 = sthRec(reactions3);
            }
            //去除中间反应 A+B{u}<->{k}C C{u}<->{k}D+E 变成A+B{u}<->{k}D+E,这里看后面的常微分方程好不好生成。如果不太好生成，可可能得默认最大两反应物了。
            //换言之就是把一生多而且具有相同反应类型的替换掉。
            Dictionary<int, Dictionary<int, int>> revaa = new();//用于存储单个的
            foreach (var reaction in reactions4)
            {
                Console.WriteLine($"反应4：{reaction}");
                if (reaction.reactant.Count == 1)//这种情况下必然是可逆反应吧
                {
                    revaa.Add(reaction.reactant.First().Key, reaction.product);
                }
            }
            var reactions5 = new List<reaction4>();
            foreach (var reaction in reactions4)
            {
                if (reaction.reactant.Count == 1 && revaa.ContainsKey(reaction.reactant.First().Key))
                {
                    continue;
                }
                if (reaction.product.Count == 1 && revaa.ContainsKey(reaction.product.First().Key))
                {
                    reaction.product = revaa[reaction.product.First().Key];
                }
                reactions5.Add(reaction);
            }
            return reactions5;
        }
        private reaction3 newReaction3(double rate1, double rate2, List<term> reactant, List<term> product)
        {
            reaction3 reaction = new reaction3();
            reaction.reactant = reactant;
            reaction.product = product;
            reaction.rate1 = rate1;
            reaction.rate2 = rate2;
            if (product.Count == 0)
            {
                //throw new Exception("产物为空");
            }
            foreach (var item in product)
            {
                int count = speciesContext.species_id.Count;
                int zz = AddNewSpecies(item.complex);
                if (zz != -1 && count != speciesContext.species_id.Count)//说明刚刚添加了一个新的东西
                {
                    Console.WriteLine($"添加的物种：{zz}");
                    ASTPrinter.PrintAst(item.complex);

                    if (item.complex is ComplexNode complex)
                    {
                        var complex1 = Utils.getBaseComplex(complex);
                        if (complex1 is ComplexNode complex2 && !thisComplexs.Contains(complex2))
                            thisComplexs.Add(Normalize(complex));
                        else if (complex1 is StrandNode strand && !thisStrands.Contains(strand) && strand.Type != StrandNode.StrandType.duplex)
                        {
                            thisStrands.Add(strand);
                        }
                    }
                    else if (item.complex is StrandNode strand && !thisStrands.Contains(strand) && strand.Type != StrandNode.StrandType.duplex)
                    {
                        thisStrands.Add(strand);
                    }
                }

            }
            for (int i = 0; i < reaction.reactant.Count; i++)
            {
                if (reaction.reactant[i].complex is ComplexNode complex)
                {
                    var complex1 = Utils.getBaseComplex(complex);
                    if (complex1 is ComplexNode complex2 && !thisComplexs.Contains(complex2))
                        reaction.reactant[i].complex = (Normalize(complex));
                    else if (complex1 is StrandNode strand && !thisStrands.Contains(strand) && strand.Type != StrandNode.StrandType.duplex)
                    {
                        reaction.reactant[i].complex = (strand);
                    }
                }
            }
            for (int i = 0; i < reaction.product.Count; i++)
            {
                if (reaction.product[i].complex is ComplexNode complex)
                {
                    var complex1 = Utils.getBaseComplex(complex);
                    if (complex1 is ComplexNode complex2 && !thisComplexs.Contains(complex2))
                        reaction.product[i].complex = (Normalize(complex));
                    else if (complex1 is StrandNode strand && !thisStrands.Contains(strand) && strand.Type != StrandNode.StrandType.duplex)
                    {
                        reaction.product[i].complex = (strand);
                    }
                }
            }
            return reaction;
        }

        private bool canitHappen(List<BaseComplex> list)
        {
            return true;
            foreach (var ll in list)
            {
                int id = AddNewSpecies(ll);
                if (id == -1 || !initial_species.Contains(id))
                {
                    return false;
                }
            }
            return true;
        }

        private void rightleft(ComplexNode complex1, List<reaction3> reactions)
        {
            var right_list = rightMigration(complex1);//对复合物进行右向分支迁移，这一步会生成不可逆反应。
            bool hasStrand = false;//
            ComplexNode complexx = null;
            foreach (var lll in right_list)
            {
                if (lll is StrandNode)
                {
                    hasStrand = true;
                }
                else if (lll is ComplexNode node)
                {
                    complexx = node;
                }

            }
            if (hasStrand)
            {
                reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complex1 } }, getFromListCom(right_list)));//添加得到的可逆反应。
            }

            //对剩下的复合物(可能是生成单链的后剩下的，也可能是唯一的复合物，总之只剩一个)
            var unlink1 = tryUnlink(complexx);//尝试解链
            bool same = false;
            foreach (var item in unlink1)
            {
                if (item is ComplexNode node1 && (node1.Equals(complex1.Rotate()) || node1.Equals(complex1)))//解链后得到与反应物相同的复合物
                {
                    same = true;
                    break;
                }
            }
            if (!same)//粘上去的链右分支迁移后再解链不会得到原来的链，说明该发生了新的可逆反应
            {
                reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complexx } }, getFromListCom(unlink1)));//添加得到的可逆反应。
            }
            //return false;
        }
        private void leftright(ComplexNode complex1, List<reaction3> reactions)
        {
            var left_list = leftMigration(complex1);//对复合物进行左向分支迁移，这一步会生成不可逆反应。
            bool hasStrand = false;//
            ComplexNode complexx = null;
            foreach (var lll in left_list)
            {
                if (lll is StrandNode)
                {
                    hasStrand = true;
                }
                else if (lll is ComplexNode node)
                {
                    complexx = node;
                }
            }
            if (hasStrand)
            {
                reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complex1 } }, getFromListCom(left_list)));//添加得到的不可逆反应。
            }
            var unlink1 = tryUnlink(complexx);//尝试解链
            bool same = false;
            foreach (var item in unlink1)
            {
                if (item is ComplexNode node1 && (Normalize(node1).Equals(complex1) || node1.Equals(complex1)))//解链后得到与反应物相同的复合物
                {
                    same = true;
                    break;
                }
            }
            if (!same)
            {
                reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complexx } }, getFromListCom(unlink1)));//添加得到的可逆反应。
            }
        }

        private void leftright(int num, StrandNode strand, ComplexNode complex, ComplexNode complex1, List<reaction3> reactions)
        {
            var left_list = leftMigration(complex1);//对复合物进行左向分支迁移，这一步会生成不可逆反应。
            bool hasStrand = false;//
            ComplexNode complexx = null;
            foreach (var lll in left_list)
            {
                if (lll is StrandNode stranddd)
                {
                    hasStrand = true;
                }
                else if (lll is ComplexNode node)
                {
                    complexx = node;
                }
                ASTPrinter.PrintAst(lll);
            }
            if (hasStrand)
            {
                reactions.Add(newReaction3(systemSetting.Binding, 0, new List<term>() { new term() { num = 1, complex = complex }, new term() { num = num, complex = strand } }, getFromListCom(left_list)));//添加得到的不可逆反应。
            }
            var unlink1 = tryUnlink(complexx);//尝试解链
            bool same = false;
            //bool sameunllink = false;
            foreach (var item in unlink1)
            {
                ASTPrinter.PrintAst(item);
                if (item is ComplexNode node1 && ((node1).Equals(complex1) || node1.Equals(complex)))//解链后得到与反应物相同的复合物
                {
                    same = true;
                    break;
                }
                else if (item is StrandNode strand2 && (strand2.Equals(strand.Rotate()) || strand2.Equals(strand)))//相同的单链，这个先不考虑特殊情况 
                {
                    same = true;
                    break;
                }
            }
            if (!same)
            {
                if(hasStrand)
                    reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complexx } }, getFromListCom(unlink1)));//添加得到的可逆反应。
                else 
                    reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complex }, new term() { num = num, complex = strand } }, getFromListCom(unlink1)));//添加得到的可逆反应。
            }

        }
        private void rightleft(int num, StrandNode strand, ComplexNode complex, ComplexNode complex1, List<reaction3> reactions)
        {
            var right_list = rightMigration(complex1);//对复合物进行右向分支迁移，这一步会生成不可逆反应。
            bool hasStrand = false;//
            ComplexNode complexx = null;
            foreach (var lll in right_list)
            {
                if (lll is StrandNode stranddd)
                {
                    hasStrand = true;
                }
                else if (lll is ComplexNode node)
                {
                    complexx = node;
                }
                ASTPrinter.PrintAst(lll);
            }
            if (hasStrand)
            {
                reactions.Add(newReaction3(systemSetting.Binding, 0, new List<term>() { new term() { num = 1, complex = complex }, new term() { num = num, complex = strand } }, getFromListCom(right_list)));//添加得到的可逆反应。}
            }
            var unlink1 = tryUnlink(complexx);//尝试解链
            bool same = false;
            Console.WriteLine("解链的结果为：");
            foreach (var item in unlink1)
            {
                ASTPrinter.PrintAst(item);
                if (item is ComplexNode node1 && (Normalize(node1).Equals(complex1) || node1.Equals(complex)))//解链后得到与反应物相同的复合物
                {
                    same = true;
                    break;
                }
                else if (item is StrandNode strand2 && (strand2.Equals(strand.Rotate()) || strand2.Equals(strand)))//相同的单链，这个先不考虑特殊情况 
                {
                    same = true;
                    break;
                }
            }
            if (!same)
            {
                if(hasStrand)
                    reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complexx } }, getFromListCom(unlink1)));//添加得到的可逆反应。
                else
                    reactions.Add(newReaction3(systemSetting.Binding, systemSetting.Unbing, new List<term>() { new term() { num = 1, complex = complex }, new term() { num = num, complex = strand } }, getFromListCom(unlink1)));//添加得到的可逆反应。
            }
        }
        private List<reaction3> GenReactions()
        {
            //获得其中的链和复合物
            List<reaction3> reactions = new List<reaction3>();
            Dictionary<int, bool> comUnlink = new();
            for (int i = 0; i < speciesContext.species_id.Count; i++)
            {
                var species = speciesContext.species_id[i];
                if (species is StrandNode strand && !thisStrands.Contains(strand) && strand.Type != StrandNode.StrandType.duplex)
                {
                    thisStrands.Add(strand);
                }
                else if (species is ComplexNode complex)
                {
                    ASTPrinter.PrintAst(complex);
                    ASTPrinter.PrintAst(Normalize(complex));
                    int cid = AddNewSpecies(Normalize(complex));
                    if (comUnlink.ContainsKey(cid)) continue;
                    comUnlink.Add(cid, true);
                    var unLink = tryUnlink(Normalize(complex));
                    foreach (var item in unLink)
                    {
                        if (item is StrandNode strand2 && !thisStrands.Contains(strand2) && strand2.Type != StrandNode.StrandType.duplex)
                        {
                            thisStrands.Add(strand2);
                        }
                        else if (item is ComplexNode complex2)
                        {
                            var complex3 = Utils.getBaseComplex(complex2);
                            if (complex3 is ComplexNode complex4 && !thisComplexs.Contains(complex4))
                                thisComplexs.Add(Normalize(complex4));
                            else if (complex3 is StrandNode strand3 && !thisStrands.Contains(strand3) && strand3.Type != StrandNode.StrandType.duplex)
                                thisStrands.Add(strand3);
                        }
                    }

                }
            }


            for (int si = 0; si < thisStrands.Count; si++)
            {
                var strand = thisStrands[si];
                int sid = AddNewSpecies(strand);
                var strandd = strand.DeepCopy() as StrandNode;
                bool rotate = false;
                if (strandd.Type == StrandNode.StrandType.lower)
                {
                    strandd = strandd.Rotate();
                    rotate = true;
                }
                strandInfos strand1 = getSubStrand(strandd);
                for (int cmi = 0; cmi < thisComplexs.Count; cmi++)
                {
                    //复合物自己分支迁移和解链测试
                    var complex = thisComplexs[cmi];
                    int cid = AddNewSpecies(complex);
                    Console.WriteLine($"\n****************************\n反应物为：{sid}+{cid}\n****************************\n");

                    if (hasDone.ContainsKey(new re(sid, cid)))
                    {
                        continue;
                    }
                    hasDone.Add(new re(sid, cid), true);
                    if (!comUnlink.ContainsKey(cid))
                    {
                        leftright(complex, reactions);
                        rightleft(complex, reactions);
                        comUnlink.Add(cid, true);
                    }
                    //复合物和链反应
                    ASTPrinter.PrintAst(complex);
                    ASTPrinter.PrintAst(strand);

                    var complexx = complex.DeepCopy() as ComplexNode;
                    if (rotate)
                    {
                        complexx = complexx.Rotate();
                        
                    }
                    if (rightMigration(complexx).Count == 0)
                    {
                        complexx = Normalize(complexx);
                    }
                    for (int jz = 0;jz<2;jz++)
                    {
                        if(jz==1)
                        {
                            if(leftMigration(complexx).Count == 1)
                            {
                                complexx = leftMigration(complexx)[0] as ComplexNode;
                            }
                        }

                        List<stickInfo> coms = new List<stickInfo>();

                        Console.WriteLine("黏贴链信息:");
                        Stick_Recursion(coms, strand1, 0, complexx, new ComplexNode(), strandd, 0);//这里是单纯地黏上
                        Console.WriteLine("双链是：");
                        ASTPrinter.PrintAst(complexx);
                        Console.WriteLine("单链是：");
                        ASTPrinter.PrintAst(strandd);
                        foreach (var item in coms)
                        {
                            Console.WriteLine($"黏贴的结果为");
                            ASTPrinter.PrintAst(item.complex);
                            Console.WriteLine("\n===================================");
                        }
                        //得到复合物和x个链生成的复合物
                        for (int i = 0; i < coms.Count; i++)
                        {
                            stickInfo com = coms[i];
                            if (com.complex is ComplexNode complex1)//这里假设的是一个中间复合物，然后假设对于此的左右分支迁移不会产生矛盾的地方
                            {
                                if(jz==0)
                                    leftright(com.num, strandd, complex, (complex1), reactions);
                                if(jz==1)
                                    rightleft(com.num, strandd, complex, (complex1), reactions);
                            }

                        }
                    }
                    
                    Console.WriteLine("\n反应::");
                    foreach(var item in reactions)
                    {
                        Console.WriteLine(item);
                    }

                }
                for (int sj = si + 1; sj < thisStrands.Count; sj++)//链和链反应
                {
                    var strand11 = thisStrands[sj];
                    if (strandd.Equals(strand11)) continue;
                    var strand2 = strand11.DeepCopy() as StrandNode;
                    if (strand2.Type == StrandNode.StrandType.upper)
                    {
                        strand2 = strand2.Rotate();
                        //rotate = true;
                    }
                    var matchinfo = MatchToehold(strandd, strand2, strand1);
                    foreach (var item in matchinfo)
                    {
                        ComplexNode complex = Stickss(strandd, strand2, item) as ComplexNode;
                        if (rotate)
                        {
                            complex = complex.Rotate();
                        }
                        if (canitHappen(new List<BaseComplex>() { strandd, strand2 }))
                            reactions.Add(newReaction3(systemSetting.Binding*2, 0, new List<term>() { new term() { num = 1, complex = strandd }, new term() { num = 1, complex = strand2 } }, getFromListCom(new List<BaseComplex>() { complex })));//添加得到的不可逆反应。
                    }
                }
            }
            return reactions;
        }
        private void testIdSame(int id1, int id2)
        {
            Console.WriteLine($"id1 == {id1},id2 == {id2}");
            if (speciesContext.species_id.Count <= id1)
            {
                Console.WriteLine($"id1不存在");
                return;
            }
            if (speciesContext.species_id.Count <= id2)
            {
                Console.WriteLine($"id2不存在");
                return;
            }
            var species1 = speciesContext.species_id[id1];
            var species2 = speciesContext.species_id[id2];
            Console.WriteLine($"id1.hash == :{species1.GetHashCode()}");
            ASTPrinter.PrintAst(species1);
            Console.WriteLine($"id2.hash == :{species2.GetHashCode()}");
            ASTPrinter.PrintAst(species2);

            if (species1.Equals(species2))
            {
                Console.WriteLine($"id1和id2是同一个物种");
            }
            else
            {
                Console.WriteLine($"id1和id2不是同一个物种");
            }
        }
        private void printListReactions(List<reaction3> rr)
        {
            foreach (var item in NormalizeReactions(rr))
            {
                Console.WriteLine("-----------------------------------");
                Console.WriteLine($"\n反应物为");
                foreach (var item1 in item.reactant)
                {
                    Console.WriteLine($"\n复合物id为{item1.Key}\n复合物数目为:{item1.Value}");
                    ASTPrinter.PrintAst(speciesContext.species_id[item1.Key]);
                }
                Console.WriteLine($"\n反应速率为：{item.rate1}，{item.rate2}");
                Console.WriteLine("\n生成物为");
                foreach (var item1 in item.product)
                {
                    Console.WriteLine($"\n复合物id为{item1.Key}\n复合物数目为:{item1.Value}");
                    ASTPrinter.PrintAst(speciesContext.species_id[item1.Key]);
                }
                Console.WriteLine("\n----------------------------------");

            }
        }
        private reaction3 getFromReaction4(reaction4 reaction)
        {
            reaction3 reaction3 = new reaction3();
            reaction3.rate1 = reaction.rate1;
            reaction3.rate2 = reaction.rate2;
            foreach (var item in reaction.reactant)
            {
                term term = new term();
                term.num = item.Value;
                term.complex = speciesContext.species_id[item.Key];
                if(term.complex is ComplexNode complex)
                {
                    term.complex = Normalize(complex);
                }
                reaction3.reactant.Add(term);
            }
            foreach (var item in reaction.product)
            {
                term term = new term();
                term.num = item.Value;
                term.complex = speciesContext.species_id[item.Key];
                if (term.complex is ComplexNode complex)
                {
                    term.complex = Normalize(complex);
                }
                reaction3.product.Add(term);
            }
            return reaction3;
        }
        public void GenCRN()
        {
            if(!hasInit)
                Init();
            List<reaction3> rr = new List<reaction3>();
            int cnt = thisComplexs.Count + thisStrands.Count;
            while (true)
            {
                var tmp = GenReactions();
                rr.AddRange(tmp);
                if (thisComplexs.Count + thisStrands.Count == cnt)
                {
                    break;
                }
                cnt = thisComplexs.Count + thisStrands.Count;
            }
            printListReactions(rr);

            Console.WriteLine();
            List<reaction4> anss = NormalizeReactions(rr);
            rr.Clear();
            foreach (var item in anss)
            {
                rr.Add(getFromReaction4(item));
            }
            this.Reactions = rr;
            Dictionary<int, string> names = new();
            Dictionary<int, string> names2 = new();
            foreach (var id in initial_species)
            {
                names.Add(id, $"sp_{id}");
                names2.Add(id, speciesContext.species_id[id].ToString());
            }
            this.odes = ODE.ODEsys.CreateFromR4(anss, names);
            this.odes.names2 = names2;

            hasCRN = true;
        }
        private (bool,string) CheckPlots(int id)
        {
            bool success = true;
            string name = "";
            var specie = speciesContext.species_id[id];
            if(specie is ComplexNode)
                specie = Normalize(specie as ComplexNode);
            foreach (var item in systemSetting.plots)
            {
                switch(item)
                {
                    case FuncNode func:
                        
                        break;
                    case StrandNode strand:

                        break;
                    case ComplexNode complex:

                        break;

                }
            }
            return (success, name);
        }
        private List<double[]> setPlots(List<double[]> data,ODEsys odes)//根据plots参数来选择数据
        {
            List<double[]> data2 = new List<double[]>();
            List<int> speices = new List<int>();
            Dictionary<string, List<int>> dict = new();
            foreach (var item in odes.equations)
            {
               speices.Add(item.Key);
            }

            return data;
        }
        public (List<double>, List<double[]>) solve4()
        {
            Dictionary<int, double> y0 = new();
            foreach (var item in this.odes.equations)
            {
                if (processContext.process_init.ContainsKey(item.Key))
                {
                    y0.Add(item.Key, processContext.process_init[item.Key]);
                }
            }

            double t0 = systemSetting.InitialTime, tfinal = systemSetting.EndTime;
            int nn = systemSetting.PlotPoints;
            double dt = (tfinal - t0) / nn;
            Console.WriteLine($"t0 == {t0},tfinal == {tfinal},nn == {nn},dt == {dt}");
            return Solver.Solver.solve2(this.odes, y0, t0, tfinal, dt);
        }
        public (List<double> ,List<double[]> ) solve3()
        {
            Dictionary<int, double> y0 = new();
            foreach (var item in this.odes.equations)
            {
                if (processContext.process_init.ContainsKey(item.Key))
                {
                    y0.Add(item.Key, processContext.process_init[item.Key]);
                }
            }

            double t0 = systemSetting.InitialTime, tfinal = systemSetting.EndTime;
            int nn = systemSetting.PlotPoints;
            double dt = (tfinal - t0) / nn;
            Console.WriteLine($"t0 == {t0},tfinal == {tfinal},nn == {nn},dt == {dt}");
            int num = this.odes.equations.Count;
            if(num<256)
                return Solver.Solver.solve3(this.odes, y0, t0, tfinal, dt);
            else
                return Solver.Solver.solve4(this.odes, y0, t0, tfinal, dt);
        }
        public (List<int>,List<double[]>) solve2()
        {
            Dictionary<int, double> y0 = new();
            foreach (var item in this.odes.equations)
            {
                if (processContext.process_init.ContainsKey(item.Key))
                {
                    y0.Add(item.Key, processContext.process_init[item.Key]);
                }
            }

            double t0 = systemSetting.InitialTime, tfinal = systemSetting.EndTime;
            int nn = systemSetting.PlotPoints;
            double dt = (tfinal - t0) / nn;
            Console.WriteLine($"t0 == {t0},tfinal == {tfinal},nn == {nn},dt == {dt}");
            List<int> time = new List<int>();
            for (int i = 0; i <= tfinal; i++)
            {
                time.Add(i);
            }
            var data = Solver.Solver.solve(this.odes, y0, t0, tfinal, dt);
            return (time, data);
        }
        public List<double[]> solve()//求解
        {
            Dictionary<int, double> y0 = new();
            foreach (var item in this.odes.equations)
            {
                if (processContext.process_init.ContainsKey(item.Key))
                {
                    y0.Add(item.Key, processContext.process_init[item.Key]);
                }
            }
            
            double t0 = systemSetting.InitialTime, tfinal = systemSetting.EndTime;
            int nn = systemSetting.PlotPoints;
            double dt = (tfinal - t0) / nn;
            var data = Solver.Solver.solve(this.odes, y0, t0, tfinal, dt);
            return data;
        }
        public void TestGen1()//测试之前的
        {
            List<reaction3> rr = new List<reaction3>();
            int cnt = thisComplexs.Count + thisStrands.Count;
            while (true)
            {
                var tmp = GenReactions();
                //printListReactions(tmp);
                rr.AddRange(tmp);
                Console.WriteLine($"当前反应数量为{rr.Count}");
                Console.WriteLine($"cnt == {cnt},sum == {thisComplexs.Count} + {thisStrands.Count} == {thisComplexs.Count + thisStrands.Count}");
                foreach (var item in speciesContext.species_exist)
                {
                    Console.WriteLine($"id == {item.Value}");
                    ASTPrinter.PrintAst(item.Key);
                    Console.WriteLine("\noooooooooooo");
                }


                if (thisComplexs.Count + thisStrands.Count == cnt)
                {
                    break;
                }
                cnt = thisComplexs.Count + thisStrands.Count;

            }
            printListReactions(rr);

            Console.WriteLine();
            List<reaction4> anss = NormalizeReactions(rr);
            //Console.WriteLine("规范化后的反应");
            Dictionary<int,string>names = new();
            //for(int i=0;i<20;i++)names.Add(i, $"sp_{i}");
            foreach(var id in initial_species)
            {
                names.Add(id, $"sp_{id}");
            }
            foreach(var item in anss)
            {
                string ss = "";
                foreach (var item1 in item.reactant)
                {
                    ss += $"{item1.Value}*{names[item1.Key]} + ";
                }
                ss = ss.Substring(0, ss.Length - 3);
                string arrow = "";
                string ra1 = item.rate1.ToString("F4");
                string ra2 = item.rate2.ToString("F4");
                if (item.rate2 == 0)
                    arrow = $"({ra1})->";
                else
                    arrow = $"({ra1})<->({ra2})";
                ss += arrow;
                foreach (var item1 in item.product)
                {
                    ss += $"{item1.Value}*{names[item1.Key]} + ";
                }
                ss = ss.Substring(0, ss.Length - 3);
                Console.WriteLine(ss);
            }
            var odes = ODE.ODEsys.CreateFromR4(anss, names);
            Console.WriteLine($"ODEs:\n\\begin{{align}}\n{odes.ToLatex()}\\end{{align}}");
            
            List<double> y0 = new List<double>();
            foreach (var item in odes.equations)
            {
                //Console.WriteLine($"方程为：{item.Key}");
                if(processContext.process_init.ContainsKey(item.Key))
                {
                    y0.Add(processContext.process_init[item.Key]);
                }
                else
                {
                    y0.Add(0);
                }
            }
            int n = y0.Count;
            double[] yy = new double[n];
            for (int i = 0; i < y0.Count; i++)
            {
                yy[i] = y0[i];
            }
            double t0 = 0, tfinal = 10;
            int nn = 1000;
            // 开始计时
            Stopwatch stopwatch = Stopwatch.StartNew();
            var ans = Solver.Solver.RK4(odes.GetRightHandSide(), yy, t0,tfinal,nn);
            stopwatch.Stop();
            Console.WriteLine($"RK4 求解耗时: {stopwatch.ElapsedMilliseconds} 毫秒");
            Utils.SaveCSV("test.csv", names, ans, t0,tfinal, nn);
            Chart.SaveLineChart("test.png", ans, t0,1000,names);

        }
        //public static ODE.ODEsys genReactions()
        //{
        //    List<reaction4> reactions = new List<reaction4>
        //{
        //    // A -> B
        //    new reaction4
        //    {
        //        rate1 = 0.1,
        //        reactant = new Dictionary<int, int> { { 0, 1 } },
        //        product = new Dictionary<int, int> { { 1, 1 } }
        //    },

        //    // B + C -> C + D
        //    new reaction4
        //    {
        //        rate1 = 0.2,
        //        rate2 = 0.05,
        //        reactant = new Dictionary<int, int> { { 1, 1 }, { 2, 1 } },
        //        product = new Dictionary<int, int> { { 2, 1 }, { 3, 1 } }
        //    },

        //    // D -> C + E
        //    new reaction4
        //    {
        //        rate1 = 0.3,
        //        reactant = new Dictionary<int, int> { { 3, 1 } },
        //        product = new Dictionary<int, int> { { 2, 1 }, { 4, 1 } }
        //    },

        //    // C + E -> A + F
        //    new reaction4
        //    {
        //        rate1 = 0.4,
        //        reactant = new Dictionary<int, int> { { 2, 1 }, { 4, 1 } },
        //        product = new Dictionary<int, int> { { 0, 1 }, { 5, 1 } }
        //    },

        //    // F -> G
        //    new reaction4
        //    {
        //        rate1 = 0.5,
        //        reactant = new Dictionary<int, int> { { 5, 1 } },
        //        product = new Dictionary<int, int> { { 6, 1 } }
        //    }
        //};

        //    Dictionary<int, string> initialConcentrations = new Dictionary<int, string>
        //{
        //    { 0, "A" },  // A
        //    { 1, "B" },  // B
        //    { 2, "C" },  // C
        //    { 3, "D" },  // D
        //    { 4, "E" },  // E
        //    { 5, "F" },  // F
        //    { 6, "G" }   // G
        //};
        //    return ODE.ODEsys.CreateFromR4(reactions, initialConcentrations);
        //}
        public void Run(bool skip = false)
        {
            Init();
            GenCRN();
            //solve();        

        }
    }
}
