using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ODE;
using PaintUtils;
using static DSDCore.DomNode;

namespace DSDCore
{
    public static class ExpressionPrinter
    {
        public static void PrintExpression(ExpressionNode node, int indent = 0)
        {
            if (node == null) return;
            string indentStr = new string(' ', indent * 2);
            switch (node)
            {
                case BinaryExpression bin:
                    Console.WriteLine($"{indentStr}BinaryExpression");
                    PrintExpression(bin.Left, indent + 1);
                    Console.WriteLine($"{indentStr}  [{bin.Operator}]");
                    PrintExpression(bin.Right, indent + 1);
                    break;

                case ListExpression list:
                    Console.WriteLine($"{indentStr}ListExpression [");
                    foreach (var item in list.Values)
                    {
                        PrintExpression(item, indent + 1);
                    }
                    Console.WriteLine($"{indentStr}]");
                    break;

                case Parameters param:
                    Console.WriteLine($"{indentStr}Parameters:");
                    PrintExpression(param.Value, indent + 1);
                    PrintExpression(param.parameters, indent + 1);
                    break;

                case IntegerNode intNode:
                    Console.WriteLine($"{indentStr}IntegerNode: {intNode.Value}");
                    break;

                case FloatNode floatNode:
                    Console.WriteLine($"{indentStr}FloatNode: {floatNode.Value}");
                    break;

                case StringNode strNode:
                    Console.WriteLine($"{indentStr}StringNode: \"{strNode.Value}\"");
                    break;

                case NameNode nameNode:
                    Console.WriteLine($"{indentStr}NameNode: {nameNode.Name}");
                    break;

                case FuncNode funcNode:
                    Console.WriteLine($"{indentStr}FuncNode: {funcNode.Name}");
                    Console.WriteLine($"总共有{funcNode.Arguments.Count}个参数");
                    Console.WriteLine($"{indentStr}Arguments:");
                    foreach (var arg in funcNode.Arguments)
                    {
                        PrintExpression(arg, indent + 1);
                    }
                    Console.WriteLine("");
                    break;

                case KeywordNode kwNode:
                    Console.WriteLine($"{indentStr}KeywordNode: {kwNode.Value}");
                    break;

                case UnitValueNode unitNode:
                    Console.WriteLine($"{indentStr}UnitValueNode: {unitNode.Value} {unitNode.units}");
                    break;
                case DomNode domNode:
                    //Console.WriteLine("\nDomNode:\n");
                    List<char> chars1 = new List<char>();
                    chars1.Add(' ');
                    if (domNode.seq != null)
                    {
                        chars1 = ((string)(domNode.seq.GetValue())).ToList();
                    }
                    else
                    {
                        chars1 = ((string)(domNode.Name.GetValue())).ToList();
                        if (domNode.Type == DomType.ToeHold)
                        {
                            chars1.Add('^');
                        }
                        else if (domNode.Type == DomType.Rev)
                        {
                            chars1.Add('*');
                        }
                        else if (domNode.Type == DomType.ToeHoldRev)
                        {
                            chars1.Add('^');
                            chars1.Add('*');
                        }
                    }
                    chars1.Add(' ');
                    Console.Write(new string(chars1.ToArray()));
                    break;
                case SeqNode seqNode:
                    //Console.WriteLine("SeqNode");
                    if (seqNode.Value is List<SeqNode>)
                    {
                        foreach (var item in (List<SeqNode>)seqNode.Value)
                        {
                            PrintExpression(item, indent + 1);
                        }
                    }
                    else if (seqNode.Value is DomNode)
                    {
                        PrintExpression((DomNode)seqNode.Value, indent);
                    }
                    else if (seqNode.Value is TetherNode)
                    {
                        PrintExpression((TetherNode)seqNode.Value, indent);
                    }
                    break;
                case StrandNode strandNode:
                    //Console.WriteLine($"{indentStr}StrandNode:");
                    if (strandNode.Type == StrandNode.StrandType.duplex)
                    {
                        //Console.WriteLine($"{indentStr}双链:");
                        Console.Write("[");
                    }
                    else if (strandNode.Type == StrandNode.StrandType.upper)
                    {
                        //Console.WriteLine($"{indentStr}上链:");
                        Console.Write("<");
                    }
                    else if (strandNode.Type == StrandNode.StrandType.lower)
                    {
                        //Console.WriteLine($"{indentStr}下链:");
                        Console.Write("{");
                    }
                    PrintExpression(strandNode.seq, indent + 1);
                    if (strandNode.Type == StrandNode.StrandType.duplex)
                    {
                        Console.Write("]");
                    }
                    else if (strandNode.Type == StrandNode.StrandType.upper)
                    {
                        Console.Write(">");
                    }
                    else if (strandNode.Type == StrandNode.StrandType.lower)
                    {
                        Console.Write("}");
                    }
                    //Console.Write("\n");
                    break;
                case ComplexNode complexNode://完善这个输出，下一步进行测试，然后争取今晚完成所有的语法解析
                    Console.WriteLine($"{indentStr}ComplexNode:，有{complexNode.Values.Count}个元素");
                    foreach (var item in complexNode.Values)
                    {
                        PrintExpression(item, indent + 1);
                    }
                    Console.Write("\n");
                    break;
                case LinkerNode linkerNode:
                    //Console.WriteLine($"{indentStr}LinkerNode:");
                    if (linkerNode.Type == LinkerNode.LinkerType.lower)
                    {
                        Console.Write(":");
                    }
                    else Console.Write("::");
                    break;
                default:
                    Console.WriteLine($"{indentStr}并非表达式: {node.GetType().Name}");
                    break;
            }
        }
    }
    public static class ASTPrinter
    {
        public static void PrintAst(AstNode node, int indent = 0)
        {
            if (node == null) return;
            string indentStr = new string(' ', indent * 2);
            switch (node)
            {
                case ProgramNode program:
                    Console.WriteLine($"{indentStr}ProgramNode");
                    foreach (var item in program.statements)
                    {
                        PrintAst(item, indent + 1);
                    }
                    break;
                case DirectiveNode directive:
                    Console.WriteLine($"{indentStr}DirectiveNode: {directive.Name}");
                    PrintAst(directive.Value, indent + 1);
                    break;
                case ProcessList list:
                    foreach (var item in list.processes)
                    {
                        PrintAst(item, indent + 1);
                    }
                    break;
                case Species species:
                    Console.WriteLine($"{indentStr}Species:");
                    if (species.Value1 != null)
                        PrintAst(species.Value1, indent + 1);
                    PrintAst(species.Name, indent + 1);
                    if (species.Value2 != null)
                        PrintAst(species.Value2, indent + 1);
                    Console.WriteLine("\n---------------------------");
                    break;
                case ProcessNode pocess:
                    Console.WriteLine($"{indentStr}PocessNode: ");
                    PrintAst(pocess.Value, indent + 1);
                    break;
                case DeclareNode declare:
                    Console.WriteLine($"\n{indentStr}DeclareNode:");
                    PrintAst(declare.Name, indent + 1);
                    PrintAst(declare.Value, indent + 1);
                    break;
                case ExpressionNode:
                    ExpressionPrinter.PrintExpression((ExpressionNode)node, indent);
                    break;
            }
        }
    }


    public static class ComplexPrinter
    {
        public static SvgGenerator GetSvg(List<Line> lines)
        {
            double width = 0, height = 0;
            //lines = Normal(lines);
            foreach (var line in lines)
            {
                if (line.text != null && line.text.Equals("circle"))
                {
                    width = Math.Max(width, line.startx + line.radius);
                    height = Math.Max(height, line.starty + line.radius);
                }
                else
                {
                    width = Math.Max(width, Math.Max(line.startx, line.endx));
                    height = Math.Max(height, Math.Max(line.starty, line.endy));
                }
            }
            SvgGenerator svg = new SvgGenerator(width + 50, height + 50);
            foreach (var line in lines)
            {
                if (line.text != null && line.text.Equals("circle"))
                {
                    svg.AddCircle(line.startx, line.starty, line.radius, line.color);
                    continue;
                }
                svg.AddLine(line.startx, line.starty, line.endx, line.endy, line.color);
                if (line.text != null)
                {
                    svg.AddText(line.text, line.startx + line.dx, line.starty + line.dy, line.endx + line.dx, line.endy + line.dy, line.color, "Arial", 12);//不一定是水平的
                }
            }
            return svg;
        }
        public static List<Line> MoveTo(List<Line> list, double x, double y)
        {
            var lines = new List<Line>();
            foreach (var line in list)
            {
                lines.Add(line.move(x, y));
            }
            return lines;
        }
        public static List<Line> Normal(List<Line> list)//平移回正常的位置
        {
            var lines = new List<Line>();
            double dx = 0, dy = 0;
            double minx = 998244353, miny = 998244353;
            foreach (var line in list)
            {
                if (line.text != null && line.text.Equals("circle"))
                {
                    minx = Math.Min(minx, line.startx - line.radius);
                    miny = Math.Min(miny, line.starty - line.radius);
                }
                else if (line.text != null)
                {
                    minx = Math.Min(minx, Math.Min(line.startx , line.endx));
                    minx = Math.Min(minx, Math.Min(line.startx + line.dx, line.endx + line.dx));
                    miny = Math.Min(miny, Math.Min(line.starty , line.endy ));
                    miny = Math.Min(miny, Math.Min(line.starty + line.dy, line.endy + line.dy));
                }
                else
                {
                    minx = Math.Min(minx, Math.Min(line.startx, line.endx));
                    miny = Math.Min(miny, Math.Min(line.starty, line.endy));
                }
            }
            minx *= -1;
            miny *= -1;
            miny += 10;//确保上面的标签正常显示，如果是双链的话，上链的初始y就是20；初始x都是0
            foreach (var line in list)
            {
                lines.Add(line.move(minx, miny));
            }
            return lines;
        }
        public static List<Line> PrintBaseComplex(BaseComplex complex)
        {
            if (complex is StrandNode) return PrintStrand((StrandNode)complex);
            else if (complex is ComplexNode) return PrintComplex2((ComplexNode)complex);

            return new List<Line>();
        }

        public static List<Line> PrintComplex2(ComplexNode complex)
        {
            List<Line> lines = new List<Line>();
            //Console.WriteLine("\n2当前绘制的是：" + complex.ToString());
            var complexNode2s = Utils.getComplexNode2s(complex);

            if (complexNode2s.Count == 0)
                return lines;

            double xt =0 ;
            int dh = 5;

            for (int i = 0; i < complexNode2s.Count; i++)
            {
                var node2 = complexNode2s[i];
                List<Line> segmentLines = new List<Line>();
                if (node2.linker == null)//绘制第一个,上链转4，下链转2
                {
                    List<Line> tup=null,tdown=null;
                    if(node2.lefttop!=null)
                    {
                        int rotate = 0;
                        if(node2.leftbottom != null)
                        {
                            rotate = 4;
                        }
                        tup = (PrintStrand(node2.lefttop, rotate, false));
                        xt = Math.Max(xt, node2.lefttop.GetLength() * 50);
                    }
                    if (node2.leftbottom != null)
                    {
                        int rotate = 0;
                        if (node2.lefttop != null)
                        {
                            rotate = 4;
                        }
                        tdown = (MoveTo(PrintStrand(node2.leftbottom, rotate, true),0,25));
                        xt = Math.Max(xt, node2.leftbottom.GetLength() * 50);
                    }
                    if(node2.lefttop!=null&& tup !=null)
                    {
                        int dx = 0;
                        if(node2.lefttop.GetLength()*50<xt)
                            dx = (int)(xt - node2.lefttop.GetLength() * 50);
                        segmentLines.AddRange(MoveTo(tup, dx, 0));
                    }
                    if (node2.leftbottom != null&&tdown!=null)
                    {
                        int dx = 0;
                        if (node2.leftbottom.GetLength() * 50 < xt)
                            dx = (int)(xt - node2.leftbottom.GetLength() * 50);
                        segmentLines.AddRange(MoveTo(tdown, dx, 0));
                    }
                }
                else if (node2.linker.Type == LinkerNode.LinkerType.upper)//说明是上链接，需要把当前的lefttop连上，或者是把当前的middle的上链和上一次的上链连上
                {
                    if(node2.lefttop!=null)
                    {          
                        segmentLines.AddRange(MoveTo(PrintStrand(node2.lefttop,0,false,dh),xt,0));
                        xt += node2.lefttop.GetLength() * 50+dh;
                    }
                    if (node2.leftbottom != null)
                    {
                        int dx = (int)(xt - (50 * node2.leftbottom.GetLength()));
                        if(node2.lefttop == null)
                            dx +=+dh;
                        segmentLines.AddRange(MoveTo(PrintStrand(node2.leftbottom,2,true), dx, 25));
                    }
                }
                else if(node2.linker.Type == LinkerNode.LinkerType.lower)//说明是下链接，需要把当前的leftbottom连上，或者是把当前的middle的下链和上一次的下链连上
                {
                    if(node2.leftbottom!=null)
                    {
                        segmentLines.AddRange(MoveTo(PrintStrand(node2.leftbottom,0,false,dh), xt, 25));
                        xt += node2.leftbottom.GetLength() * 50+dh;
                    }
                    if (node2.lefttop != null)
                    {
                        int dx = (int)(xt - (50 * node2.lefttop.GetLength()));
                        if (node2.leftbottom == null)
                            dx += dh;
                        segmentLines.AddRange(MoveTo(PrintStrand(node2.lefttop,4,false), dx, 0));
                    }
                }
                //lines.AddRange(MoveTo(PrintStrand(node2.middle), xt, 0));//这个还没测试
                var upst = node2.middle.getUpper();
                var lowst = upst.GetRevComp();
                bool isend1 = false, isend2 = false;
                int udh = 0, dddh = 0;
                if (node2.linker!=null&&node2.linker.Type == LinkerNode.LinkerType.upper && node2.lefttop == null)
                    udh = 5;
                if (node2.linker != null && node2.linker.Type == LinkerNode.LinkerType.lower && node2.leftbottom == null)
                    dddh = 5;
                if (node2.righttop==null&&(i+1>=complexNode2s.Count|| complexNode2s[i + 1].linker.Type != LinkerNode.LinkerType.upper))
                    isend1 = true;
                if (node2.leftbottom == null && (node2.linker==null||node2.linker.Type!= LinkerNode.LinkerType.lower))
                    isend2 = true;

                segmentLines.AddRange(MoveTo(PrintStrand(upst, 0, isend1,udh), xt+dddh, 0));
                segmentLines.AddRange(MoveTo(PrintStrand(lowst, 0, isend2,dddh), xt+udh, 25));
                xt += node2.middle.GetLength() * 50+Math.Max(dddh,udh);
                int flag = 0;
                if (i + 1 < complexNode2s.Count)//后面还有
                {
                    var next = complexNode2s[i + 1];
                    if (next.linker != null)
                    {
                        flag = next.linker.Type == LinkerNode.LinkerType.upper ? 1 : 2;
                    }
                }
                if (node2.righttop != null)
                {
                    int rotate = 0;
                    bool isend = false;
                    if (flag != 1 && flag!=0)//不用这边连接需要偏折
                    {
                        rotate = 1;
                        isend = true;
                    }
                    else
                        xt = Math.Max(xt, node2.righttop.GetLength() * 50);//用上链连接就需要更新下一个链的起始绘制点
                    segmentLines.AddRange(MoveTo(PrintStrand(node2.righttop, rotate, isend), xt, 0));

                }
                if (node2.rightbottom != null)
                {
                    int rotate = 0;
                    if (flag != 2 && flag != 0)
                    {
                        rotate = 3;
                    }
                    else
                        xt = Math.Max(xt, node2.rightbottom.GetLength() * 50);
                    segmentLines.AddRange(MoveTo(PrintStrand(node2.rightbottom, rotate, false), xt, 25));
                }
                //SvgGenerator svg = GetSvg(Normal(segmentLines));
                //svg.Save($"complex-{i}-{Timeutil.GetNow()}.svg");
                lines.AddRange(segmentLines);
            }
            //SvgGenerator svg1 = GetSvg(Normal(lines));
            //svg1.Save($"complex-{Timeutil.GetNow()}.svg");

            return (lines);
        }

        public static List<Line> PrintComplex(ComplexNode complex)//必须是验证过的，
        {
            Console.WriteLine("当前绘制的是："+complex.ToString());
            List<Line> lines = new List<Line>();
            double lastx = 0, lasty = 0, dh = 25;
            int i = 0;
            LinkerNode lastlinker = null;//两个双链之间肯定会以某种方式连接
            while (i < complex.Values.Count)
            {
                int j = i; //连接符后面的
                while (i < complex.Values.Count && !(complex.Values[i] is StrandNode strand1 && strand1.Type == StrandNode.StrandType.duplex)) i++;
                //先绘制双链，再往前看，往后看
                List<Line> duplex = new List<Line>();//或者可以最后绘制双链，因为要判断上链会不会是结尾
                bool arrowup = false, arrowdown = false, duplexup = true, duplexdown = true;
                double dstx = 0, dsty = 0;//双链起始点
                //*** 往前看 ***
                List<Line> lines1 = new List<Line>();//存放前面的
                for (int k = j; k < i; k++)//先找好长度
                {
                    if (complex.Values[k] is StrandNode strand3)
                        dstx = Math.Max((50 * strand3.GetLength()), dstx);
                }
                if (lastlinker is null)//最左边的
                {
                    for (int k = j; k < i; k++)
                    {
                        if (complex.Values[k] is StrandNode strand1)
                        {
                            if (strand1.Type == StrandNode.StrandType.upper)
                            {
                                var line = PrintStrand(strand1, 4, false);//上链的末尾的y会是10，下链y也是10//理论上说2的旋转方式的到的链的末尾会是(50*count,10)
                                line = MoveTo(line, dstx - strand1.GetLength() * 50, 0);//往右平移，两链都对齐                                          
                                //PrintList(line);
                                lines1.AddRange(line);
                                //PrintList(lines1);
                            }
                            else if (strand1.Type == StrandNode.StrandType.lower)
                            {
                                duplexdown = false;//前面有下链，所以双链的下链不需要箭头
                                var line = PrintStrand(strand1, 2, true);
                                line = MoveTo(line, dstx - strand1.GetLength() * 50, 0);//往右平移，两链都对齐
                                lines1.AddRange(MoveTo(line, 0, dh));//往下平移
                            }
                        }

                    }
                }
                else if (lastlinker.Type == LinkerNode.LinkerType.lower)
                {
                    for (int k = j; k < i; k++)
                    {
                        if (complex.Values[k] is StrandNode strand1)
                        {
                            if (strand1.Type == StrandNode.StrandType.upper)
                            {
                                var line = PrintStrand(strand1, 4, false);
                                dstx = Math.Max((50 * strand1.GetLength()), dstx);
                                lines1.AddRange(line);
                            }
                            else if (strand1.Type == StrandNode.StrandType.lower)
                            {
                                duplexdown = false;
                                var line = PrintStrand(strand1, 0, false);
                                dstx = Math.Max((50 * strand1.GetLength()), dstx);
                                line.Add(new Line(0, 10, 0.5, strand1.getColor()));
                                lines1.AddRange(MoveTo(line, 0, dh));
                            }
                        }

                    }
                }
                else if (lastlinker.Type == LinkerNode.LinkerType.upper)
                {
                    for (int k = j; k < i; k++)
                    {
                        if (complex.Values[k] is StrandNode strand1)
                        {
                            if (strand1.Type == StrandNode.StrandType.upper)
                            {
                                var line = PrintStrand(strand1, 0, false);
                                dstx = Math.Max((50 * strand1.GetLength()), dstx);
                                line.Add(new Line(0, 10, 0.5, strand1.getColor()));
                                lines1.AddRange(line);
                            }
                            else if (strand1.Type == StrandNode.StrandType.lower)
                            {
                                duplexdown = false;//前面有下链，所以双链的下链不需要箭头
                                var line = PrintStrand(strand1, 2, true);
                                dstx = Math.Max((50 * strand1.GetLength()), dstx);
                                lines1.AddRange(MoveTo(line, 0, dh));
                            }
                        }

                    }
                }
                GetSvg(ComplexPrinter.Normal(lines1)).Save($"up-{Timeutil.GetNow()}.svg");
                //*** 往后看 ***
                List<Line> lines2 = new List<Line>();//存放后面的
                double lenlas = 0;
                //先需要找到连接符，要连接的那条链是平的。
                int linkid = -1;

                for (int k = 1; i + k < complex.Values.Count && k <= 3; k++)//往后最多看3个字符
                {
                    if (complex.Values[i + k] is LinkerNode linker)
                    {
                        linkid = k + i;
                        break;
                    }
                }
#if DEBUG
                Console.WriteLine($"linkid == {linkid}");
#endif
                if (linkid == -1)//末尾了
                {
                    arrowup = true;
                    arrowdown = false;
                }
                else if (complex.Values[linkid] is LinkerNode linker)
                {
                    lastlinker = linker;//更新lastlinker
                    if (linker.Type == LinkerNode.LinkerType.lower)//连接下链说明上一个上链结束了
                    {
                        arrowup = true;
                    }
                    else if (linker.Type == LinkerNode.LinkerType.upper)//连接上链说明下链刚开始
                        arrowdown = true;
                }

                for (int k = 1; i + k < complex.Values.Count && k < 3 && k< linkid; k++)//往后最多看2个字符
                {
                    if (complex.Values[i + k] is StrandNode strand2)
                    {
                        int ifrotate = 0;

                        if (strand2.Type == StrandNode.StrandType.upper)
                        {
                            if (linkid<0 ||(complex.Values[linkid] is LinkerNode linker0 && linker0.Type != LinkerNode.LinkerType.upper))
                            {
                                ifrotate = 1;
                            }
                            else lenlas = Math.Max(lenlas, (50 * strand2.GetLength()));

                            duplexup = false;//后面有上链，所以双链的上链不需要箭头
                            var line = PrintStrand(strand2, ifrotate, arrowup);//上链的起点的y会是10，下链y也是10
                            line.Add(new Line(0, 10, 0.5, strand2.getColor()));
                            lines2.AddRange(line);
                        }
                        else if (strand2.Type == StrandNode.StrandType.lower)
                        {
                            if (linkid <0 || (complex.Values[linkid] is LinkerNode linker0 && linker0.Type != LinkerNode.LinkerType.lower))
                            {
                                ifrotate = 3;
                            }
                            else lenlas = Math.Max(lenlas, (50 * strand2.GetLength()));
                            var line = PrintStrand(strand2, ifrotate, false);//下链的起点的y会是20，上链y也是20
                            line.Add(new Line(0, 10, 0.5, strand2.getColor()));
                            //理论上说1的旋转方式的到的链的末尾会是(50*count,20)
                            lines2.AddRange(MoveTo(line, 0, dh));//往下平移
                        }
                    }

                }
#if DEBUG
                GetSvg(ComplexPrinter.Normal(lines2)).Save($"low-{Timeutil.GetNow()}.svg");
                Console.WriteLine($"dstx == {dstx},dsty == {dsty}\n lenlas == {lenlas}\nlastx == {lastx},lasty == {lasty}\n");

#endif

                if (complex.Values[i] is StrandNode strand)
                {
                    if (strand.Type == StrandNode.StrandType.duplex)
                    {
                        StrandNode up = strand.getUpper();
                        StrandNode low = up.GetRevComp();
                        var upp = PrintStrand(up, 0, duplexup);
                        upp.Add(new Line(0, 10, 0.5, up.getColor()));
                        var loww = PrintStrand(low, 0, duplexdown);
                        loww.Add(new Line(0, 10, 0.5, up.getColor()));
                        lines1.AddRange(MoveTo(upp, dstx, dsty));

                        lines1.AddRange(MoveTo(loww, dstx, dsty + 25));



                        dstx += 50 * up.GetLength();
                        lines1.AddRange(MoveTo(lines2, dstx, dsty));

                        lines.AddRange(MoveTo(lines1, lastx, lasty));

                        lastx = lastx + dstx + lenlas;//更新lastx
                    }
                }
                if (linkid != -1) i = linkid + 1;
                else break;
            }

            return lines;
        }
        public static void PrintList(List<Line> list)
        {
            foreach (var line in list)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine("-----------------------------\n");
        }
        public static point GetEnd(List<Line> lines)
        {
            double x = 0, y = 0;
            foreach (var line in lines)
            {
                if (line.endx > x)
                {
                    x = line.endx;
                    y = line.endy;
                }
            }
            return new point(x, y);
        }
        public static List<Line> PrintStrand(StrandNode strand, int rotate = 0, bool arrow = true,int ddh = 0)//不加平移了，平移好实现
        {
            if (strand.Type == StrandNode.StrandType.duplex)
            {
                List<Line> upper = new List<Line>(), lower = new List<Line>();
                StrandNode upperStrand = strand.getUpper();
                StrandNode lowerStrand = upperStrand.GetRevComp();
                upper = PrintStrand(upperStrand, rotate);
                lower = PrintStrand(lowerStrand, rotate);
                for (int i = 0; i < lower.Count; i++)
                {
                    lower[i] = lower[i].move(0, 25);//下链下移25
                }
                //return Normal(upper.Concat(lower).ToList());
                return upper.Concat(lower).ToList();//Normal交给需要的时候调用吧。
            }
            string lastColor = "gray";//默认黑色
            List<Line> lines = new List<Line>();
            double x = 0, y = 10, dh = 13;//起始点，单链的链的默认起始点是(0,10)
            if (strand.Type == StrandNode.StrandType.upper)
                dh = -8;
            string begincolor = "None";
            if (strand.seq.Value is List<SeqNode> seqNodes)
            {
                foreach (var seqNode in seqNodes)
                {
                    if (seqNode.Value is DomNode domNode)
                    {

                        if (domNode.colour != null)
                            lastColor = (string)(domNode.colour.GetValue());
                        else
                        {
                            if (lastColor == "gray")
                                lastColor = "gray";
                            else
                                lastColor = "gray";
                        }
                        if (begincolor == "None")
                        {
                            begincolor = lastColor;
                        }
                        if (domNode.Name is null)
                            lines.Add(new Line(x, y, x + 50, y, lastColor));
                        else
                        {
                            string text = domNode.Name.GetValue() as string;
                            if (domNode.Type == DomType.ToeHold)
                            {
                                text += "^";
                            }
                            else if (domNode.Type == DomType.Rev)
                            {
                                text += "*";
                            }
                            else if (domNode.Type == DomType.ToeHoldRev)
                            {
                                text += "^*";
                            }
                            lines.Add(new Line(x, y, x + 50, y, lastColor, text, 0, dh));
                        }
                        x += 50;
                    }

                }
            }
            else if (strand.seq.Value is DomNode domNode)
            {
                if (domNode.colour != null)
                    lastColor = (string)(domNode.colour.GetValue());
                else
                {
                    if (lastColor == "gray")
                        lastColor = "gray";
                    else
                        lastColor = "gray";
                }
                if (domNode.Name is null)
                    lines.Add(new Line(x, y, x + 50, y, lastColor));
                else
                {
                    string text = domNode.Name.GetValue() as string;
                    if (domNode.Type == DomType.ToeHold)
                    {
                        text += "^";
                    }
                    else if (domNode.Type == DomType.Rev)
                    {
                        text += "*";
                    }
                    else if (domNode.Type == DomType.ToeHoldRev)
                    {
                        text += "^*";
                    }
                    lines.Add(new Line(x, y, x + 50, y, lastColor, text, 0, dh));
                }
                if (begincolor == "None")
                {
                    begincolor = lastColor;
                }
                x += 50;
            }
            var tmpline = new Line(lines[lines.Count - 1].startx, lines[lines.Count - 1].starty, lines[lines.Count - 1].endx+ddh, lines[lines.Count - 1].endy, lines[lines.Count - 1].color, lines[lines.Count - 1].text, lines[lines.Count - 1].dx, lines[lines.Count - 1].dy);
            lines[lines.Count - 1]= tmpline;
            if (arrow)//绘制箭头
            {
                if (strand.Type == StrandNode.StrandType.upper)//如果是双链也先绘制一个的
                {
                    lines.Add(new Line(x, y, x - 15, y - 10, lastColor));//高为10，向上偏折
                    lines.Add(new Line(x, y, 1, lastColor));//添加一个圆使得更加美观
                }
                else if (strand.Type == StrandNode.StrandType.lower)
                {
                    lines.Add(new Line(0, 10, 15, 20, begincolor));//高为10，向下偏折
                    lines.Add(new Line(0, 10, 1, begincolor));//添加一个圆使得更加美观
                }
            }

            if (rotate == 1)
            {
                for (int i = 0; i < lines.Count; i++)
                {
#if DEBUG
                    //Console.WriteLine(lines[i]);
#endif
                    lines[i] = lines[i].Rotate(-60, 0, 10);
                    
#if DEBUG
                    //Console.WriteLine($"旋转后:{lines[i]}");
#endif
                }
                lines.Add(new Line(0, 10, 1, lastColor));//添加一个圆使得更加美观
            }
            else if (rotate == 2)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i] = lines[i].Rotate(-60, x, y);
                }
                lines.Add(new Line(x, y, 1, lastColor));//添加一个圆使得更加美观
            }
            else if(rotate == 3)
            {
                for (int i = 0; i < lines.Count; i++)
                {
#if DEBUG
                    //Console.WriteLine(lines[i]);
#endif
                    lines[i] = lines[i].Rotate(60, 0, 10);
#if DEBUG
                    //Console.WriteLine($"旋转后:{lines[i]}");
#endif
                }
                lines.Add(new Line(0, 10, 1, lastColor));//添加一个圆使得更加美观

            }
            else if(rotate == 4)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i] = lines[i].Rotate(60, x, y);
                }
                lines.Add(new Line(x, y, 1, lastColor));//添加一个圆使得更加美观

            }
            return lines;
        }
    }

    public static class ReactionPrinter
    {
        private static List<Line> PrintArrow(double x0,double y0,double x1,double y1,double x2,double y2)//箭头是往上偏折的
        {
            List<Line> lines = new List<Line>();
            lines.Add(new Line(x0, y0, x1, y1, "black"));
            lines.Add(new Line(x1, y1, x2, y2, "black"));
            lines.Add(new Line(x1, y1, 0.5, "black"));//添加一个圆使得更加美观
            return lines;

        }
        private static List<Line> PrintSignleArrow(double rate1) // 固定大小，可以到时候平移
        {
            // 起点(0,10) 终点(50,10) 箭头偏折(40,0)
            // 文字在主干线上方
            var lines = new List<Line>();
            lines.Add(new Line(0, 10, 40, 10, "black", rate1.ToString("F4"), 0, -5)); // 文字上移15个单位
            lines.AddRange(PrintArrow(0, 10, 50, 10, 40, 0));
            return lines;
        }

        private static List<Line> PrintDoubleArrow(double rate1, double rate2)
        {
            var lines = new List<Line>();

            // 上方箭头（正向）
            lines.Add(new Line(0, 10, 40, 10, "black", rate1.ToString("F4"), 0, -5));
            lines.AddRange(PrintArrow(0, 10, 50, 10, 40, 0));
            // 下方箭头（反向）
            lines.Add(new Line(10, 20,50, 20,  "black", rate2.ToString("F4"), 0, 15)); 
            lines.AddRange(PrintArrow(50, 20, 0, 20, 10, 30)); 
            return lines;
        }

        public static List<Line> PrintReaction3(reaction3 reaction)
        {
            if (!reaction.vaidate())
            {
                return new List<Line>();
            }

            List<Line> lines = new List<Line>();
            double currentX = 0;
            double currentY = 50; // 基准高度，让所有元素在同一水平线上
            double speciesSpacing = 50; // 物种之间的间距
            double reactionArrowWidth = 100; // 反应箭头的宽度

            for (int i = 0; i < reaction.reactant.Count; i++)
            {
                term reactantTerm = reaction.reactant[i];

                if (reactantTerm.num > 1)
                {
                    lines.Add(new Line(currentX, currentY, currentX + 20, currentY, "black",
                                       reactantTerm.num.ToString(), 0, -20));
                    currentX += 30; 
                }

                List<Line> complexLines = ComplexPrinter.PrintBaseComplex(reactantTerm.complex);

                double maxX = 0;
                foreach (Line line in complexLines)
                {
                    maxX = Math.Max(maxX, Math.Max(line.startx, line.endx));
                }

                lines.AddRange(ComplexPrinter.MoveTo(complexLines, currentX, currentY - 25));

                currentX += maxX + speciesSpacing;

                // 如果不是最后一个反应物，添加加号
                if (i < reaction.reactant.Count - 1)
                {
                    lines.Add(new Line(currentX - speciesSpacing / 2 , currentY - 10,
                                       currentX - speciesSpacing / 2 , currentY + 10, "black"));
                    lines.Add(new Line(currentX - speciesSpacing / 2 - 10 , currentY,
                                       currentX - speciesSpacing / 2 + 10 , currentY, "black"));
                }
            }

            // 绘制箭头
            if (reaction.rate2 == 0) 
            {
                List<Line> arrowLines = PrintSignleArrow(reaction.rate1);
                lines.AddRange(ComplexPrinter.MoveTo(arrowLines, currentX, currentY-15));
                currentX += reactionArrowWidth;
            }
            else 
            {
                List<Line> arrowLines = PrintDoubleArrow(reaction.rate1, reaction.rate2);
                lines.AddRange(ComplexPrinter.MoveTo(arrowLines, currentX, currentY -20));
                currentX += reactionArrowWidth;
            }

            for (int i = 0; i < reaction.product.Count; i++)
            {
                term productTerm = reaction.product[i];

                if (productTerm.num > 1)
                {
                    lines.Add(new Line(currentX, currentY, currentX + 20, currentY, "black",
                                       productTerm.num.ToString(), 0, -20));
                    currentX += 30; 
                }

                List<Line> complexLines = ComplexPrinter.PrintBaseComplex(productTerm.complex);

                double maxX = 0;
                foreach (Line line in complexLines)
                {
                    maxX = Math.Max(maxX, Math.Max(line.startx, line.endx));
                }

                lines.AddRange(ComplexPrinter.MoveTo(complexLines, currentX, currentY - 25));

                currentX += maxX + speciesSpacing;

                if (i < reaction.product.Count - 1)
                {
                    lines.Add(new Line(currentX - speciesSpacing / 2 , currentY - 10,
                                       currentX - speciesSpacing / 2 , currentY + 10, "black"));
                    lines.Add(new Line(currentX - speciesSpacing / 2 - 10 , currentY,
                                       currentX - speciesSpacing / 2 + 10 , currentY, "black"));
                }
            }

            return ComplexPrinter.Normal(lines);
        }


    }
}
