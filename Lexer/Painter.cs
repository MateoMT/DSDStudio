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
        private const double DomainWidth = 50.0;
        private const double DuplexYOffset = 25.0;
        private const double StrandBaseY = 10.0;
        private const int ConnectorGap = 5;

        private enum Lane
        {
            Upper,
            Lower
        }

        public static SvgGenerator GetSvg(List<Line> lines)
        {
            double width = 0, height = 0;
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
                    svg.AddText(line.text, line.startx + line.dx, line.starty + line.dy,
                        line.endx + line.dx, line.endy + line.dy, line.color, "Arial", 12);
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

        public static List<Line> Normal(List<Line> list)
        {
            var lines = new List<Line>();
            if (list == null || list.Count == 0)
                return lines;

            double minx = double.PositiveInfinity;
            double miny = double.PositiveInfinity;
            foreach (var line in list)
            {
                if (line.text != null && line.text.Equals("circle"))
                {
                    minx = Math.Min(minx, line.startx - line.radius);
                    miny = Math.Min(miny, line.starty - line.radius);
                }
                else if (line.text != null)
                {
                    minx = Math.Min(minx, Math.Min(line.startx, line.endx));
                    minx = Math.Min(minx, Math.Min(line.startx + line.dx, line.endx + line.dx));
                    miny = Math.Min(miny, Math.Min(line.starty, line.endy));
                    miny = Math.Min(miny, Math.Min(line.starty + line.dy, line.endy + line.dy));
                }
                else
                {
                    minx = Math.Min(minx, Math.Min(line.startx, line.endx));
                    miny = Math.Min(miny, Math.Min(line.starty, line.endy));
                }
            }

            double dx = -minx;
            double dy = -miny + 10;
            foreach (var line in list)
            {
                lines.Add(line.move(dx, dy));
            }
            return lines;
        }

        public static List<Line> PrintBaseComplex(BaseComplex complex)
        {
            if (complex is StrandNode strand) return PrintStrand(strand);
            if (complex is ComplexNode complexNode) return PrintComplex2(complexNode);
            return new List<Line>();
        }

        public static List<Line> PrintComplex(ComplexNode complex)
        {
            // Kept for API compatibility.  The previous PrintComplex implementation
            // contained a second, partially divergent layout algorithm.  Route both
            // entry points through the same lane-based layout to avoid inconsistent
            // behaviour between callers.
            return PrintComplex2(complex);
        }

        public static List<Line> PrintComplex2(ComplexNode complex)
        {
            var lines = new List<Line>();
            var nodes = Utils.getComplexNode2s(complex);
            if (nodes.Count == 0)
                return lines;

            double cursorX = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                Lane? incoming = ToLane(node.linker);
                Lane? outgoing = i + 1 < nodes.Count ? ToLane(nodes[i + 1].linker) : null;

                // 1) Left side.  Only bend an overhang when the opposite lane is the
                // incoming connector, because otherwise the overhang occupies a lane
                // already used by the previous duplex.
                if (incoming == null)
                {
                    double leftWidth = Math.Max(StrandWidth(node.lefttop), StrandWidth(node.leftbottom));
                    if (node.lefttop != null)
                        AddRightAligned(lines, node.lefttop, cursorX + leftWidth, Lane.Upper, 0, false);
                    if (node.leftbottom != null)
                        AddRightAligned(lines, node.leftbottom, cursorX + leftWidth, Lane.Lower, 0, true);
                    cursorX += leftWidth;
                }
                else if (incoming == Lane.Upper)
                {
                    if (node.lefttop != null)
                    {
                        AddLeftAligned(lines, node.lefttop, cursorX, Lane.Upper, 0, false, ConnectorGap);
                        cursorX += StrandWidth(node.lefttop) + ConnectorGap;
                    }
                    if (node.leftbottom != null)
                    {
                        double attachX = cursorX + (node.lefttop == null ? ConnectorGap : 0);
                        AddRightAligned(lines, node.leftbottom, attachX, Lane.Lower, 2, false);
                    }
                }
                else // incoming == Lane.Lower
                {
                    if (node.leftbottom != null)
                    {
                        AddLeftAligned(lines, node.leftbottom, cursorX, Lane.Lower, 0, false, ConnectorGap);
                        cursorX += StrandWidth(node.leftbottom) + ConnectorGap;
                    }
                    if (node.lefttop != null)
                    {
                        // Fixes [a^ b]:<b>[c]{d^*}: the lower lane is continuous,
                        // so the left upper overhang must bend upward instead of
                        // being drawn over the previous duplex upper lane.
                        double attachX = cursorX + (node.leftbottom == null ? ConnectorGap : 0);
                        AddRightAligned(lines, node.lefttop, attachX, Lane.Upper, 4, false);
                    }
                }

                // 2) Duplex core.
                var upper = node.middle.getUpper();
                var lower = upper.GetRevComp();
                int upperIncomingGap = incoming == Lane.Upper && node.lefttop == null ? ConnectorGap : 0;
                int lowerIncomingGap = incoming == Lane.Lower && node.leftbottom == null ? ConnectorGap : 0;

                bool upperArrow = node.righttop == null && outgoing != Lane.Upper;
                bool lowerArrow = node.leftbottom == null && incoming != Lane.Lower;

                AddLeftAligned(lines, upper, cursorX + lowerIncomingGap, Lane.Upper, 0, upperArrow, upperIncomingGap);
                AddLeftAligned(lines, lower, cursorX + upperIncomingGap, Lane.Lower, 0, lowerArrow, lowerIncomingGap);
                cursorX += StrandWidth(node.middle) + Math.Max(upperIncomingGap, lowerIncomingGap);

                // 3) Right side.  This implements the "non-essential no-bend" policy:
                // free right overhangs reserve horizontal space and remain straight.
                if (node.righttop != null)
                {
                    bool arrow = outgoing != Lane.Upper;
                    AddLeftAligned(lines, node.righttop, cursorX, Lane.Upper, 0, arrow);
                    cursorX += StrandWidth(node.righttop);
                }
                if (node.rightbottom != null)
                {
                    AddLeftAligned(lines, node.rightbottom, cursorX, Lane.Lower, 0, false);
                    cursorX += StrandWidth(node.rightbottom);
                }
            }

            return lines;
        }

        private static Lane? ToLane(LinkerNode linker)
        {
            if (linker == null)
                return null;
            return linker.Type == LinkerNode.LinkerType.upper ? Lane.Upper : Lane.Lower;
        }

        private static double LaneOffset(Lane lane)
        {
            return lane == Lane.Upper ? 0 : DuplexYOffset;
        }

        private static double StrandWidth(StrandNode strand)
        {
            return strand == null ? 0 : strand.GetLength() * DomainWidth;
        }

        private static void AddLeftAligned(List<Line> dst, StrandNode strand, double x, Lane lane,
            int rotate = 0, bool arrow = true, int extension = 0)
        {
            if (strand == null)
                return;
            dst.AddRange(MoveTo(PrintStrand(strand, rotate, arrow, extension), x, LaneOffset(lane)));
        }

        private static void AddRightAligned(List<Line> dst, StrandNode strand, double rightX, Lane lane,
            int rotate = 0, bool arrow = true)
        {
            if (strand == null)
                return;
            dst.AddRange(MoveTo(PrintStrand(strand, rotate, arrow), rightX - StrandWidth(strand), LaneOffset(lane)));
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

        public static List<Line> PrintStrand(StrandNode strand, int rotate = 0, bool arrow = true, int ddh = 0)
        {
            if (strand.Type == StrandNode.StrandType.duplex)
            {
                StrandNode upperStrand = strand.getUpper();
                StrandNode lowerStrand = upperStrand.GetRevComp();
                var upper = PrintStrand(upperStrand, rotate);
                var lower = MoveTo(PrintStrand(lowerStrand, rotate), 0, DuplexYOffset);
                return upper.Concat(lower).ToList();
            }

            string beginColor;
            string endColor;
            double endX;
            var lines = BuildStraightStrand(strand, arrow, ddh, out beginColor, out endColor, out endX);
            ApplyRotation(lines, rotate, endX, beginColor, endColor);
            return lines;
        }

        private static List<Line> BuildStraightStrand(StrandNode strand, bool arrow, int extension,
            out string beginColor, out string endColor, out double endX)
        {
            string lastColor = "gray";
            beginColor = "gray";
            endColor = "gray";
            bool hasColor = false;
            var lines = new List<Line>();
            double x = 0;
            double y = StrandBaseY;
            double textDy = strand.Type == StrandNode.StrandType.upper ? -8 : 13;

            foreach (var domNode in EnumerateDomains(strand))
            {
                lastColor = ResolveColor(domNode, lastColor);
                if (!hasColor)
                {
                    beginColor = lastColor;
                    hasColor = true;
                }
                endColor = lastColor;

                string text = FormatDomainText(domNode);
                if (text == null)
                    lines.Add(new Line(x, y, x + DomainWidth, y, lastColor));
                else
                    lines.Add(new Line(x, y, x + DomainWidth, y, lastColor, text, 0, textDy));

                x += DomainWidth;
            }

            if (lines.Count == 0)
            {
                endX = 0;
                return lines;
            }

            if (extension != 0)
            {
                Line last = lines[lines.Count - 1];
                lines[lines.Count - 1] = new Line(last.startx, last.starty, last.endx + extension, last.endy,
                    last.color, last.text, last.dx, last.dy);
                x += extension;
            }

            endX = x;
            if (arrow)
            {
                if (strand.Type == StrandNode.StrandType.upper)
                {
                    lines.Add(new Line(endX, y, endX - 15, y - 10, endColor));
                    lines.Add(new Line(endX, y, 0.5, endColor));
                }
                else if (strand.Type == StrandNode.StrandType.lower)
                {
                    lines.Add(new Line(0, y, 15, y + 10, beginColor));
                    lines.Add(new Line(0, y, 0.5, beginColor));
                }
            }

            return lines;
        }

        private static IEnumerable<DomNode> EnumerateDomains(StrandNode strand)
        {
            if (strand.seq.Value is List<SeqNode> seqNodes)
            {
                foreach (var seqNode in seqNodes)
                {
                    if (seqNode.Value is DomNode domNode)
                        yield return domNode;
                }
            }
            else if (strand.seq.Value is DomNode domNode)
            {
                yield return domNode;
            }
        }

        private static string ResolveColor(DomNode domNode, string fallback)
        {
            if (domNode.colour != null)
                return (string)domNode.colour.GetValue();
            return "gray";
        }

        private static string FormatDomainText(DomNode domNode)
        {
            if (domNode.Name == null)
                return null;

            string text = domNode.Name.GetValue() as string;
            if (domNode.Type == DomType.ToeHold)
                return text + "^";
            if (domNode.Type == DomType.Rev)
                return text + "*";
            if (domNode.Type == DomType.ToeHoldRev)
                return text + "^*";
            return text;
        }

        private static void ApplyRotation(List<Line> lines, int rotate, double endX, string beginColor, string endColor)
        {
            if (rotate == 0 || lines.Count == 0)
                return;

            double angle;
            double pivotX;
            string pivotColor;
            if (rotate == 1)
            {
                angle = -60;
                pivotX = 0;
                pivotColor = beginColor;
            }
            else if (rotate == 2)
            {
                angle = -60;
                pivotX = endX;
                pivotColor = endColor;
            }
            else if (rotate == 3)
            {
                angle = 60;
                pivotX = 0;
                pivotColor = beginColor;
            }
            else if (rotate == 4)
            {
                angle = 60;
                pivotX = endX;
                pivotColor = endColor;
            }
            else
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = lines[i].Rotate(angle, pivotX, StrandBaseY);
            }
            lines.Add(new Line(pivotX, StrandBaseY, 0.5, pivotColor));
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

