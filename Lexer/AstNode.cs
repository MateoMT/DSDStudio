using DSDCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static DSDCore.DomNode;

namespace DSDCore
{
    public abstract class AstNode
    {
        public int Line { get; set; }//可以在后面解释器中用来报错
        public int Column { get; set; }
    }
    public abstract class StatementNode : AstNode { }
    // 程序根节点
    public class ProgramNode : AstNode
    {
        public List<StatementNode> statements = new();
    }
    // 指令语句
    public class DirectiveNode : StatementNode
    {
        public Keyword Name { get; set; }
        public ExpressionNode Value { get; set; }
    }
    public class DeclareNode : StatementNode
    {
        public ExpressionNode Name { get; set; }////这里的Name可以是一个变量，也可以是一个函数，或者是一个物种
        public AstNode Value { get; set; }
    }

    public class ProcessNode : StatementNode
    {
        public AstNode Value { get; set; }
    }
    public class ProcessList : ProcessNode
    {
        public List<ProcessNode> processes = new();
    }
    public class Species : ProcessNode//用于表示一般的S
    {
        public ValueNode Value1 { get; set; }//有可能是直接量，也有可能是变量
        public ValueNode Value2 { get; set; }//加入的时间
        public ExpressionNode Name { get; set; }//是一个函数，或者是一个物种
    }

    // 表达式基类
    public abstract class ExpressionNode : AstNode { }
    //dom定义的节点
    public class DomNode : BaseStrandNode //可以是一个字母，也可以是具体的序列
    {
        public enum DomType
        {
            Normal,
            ToeHold,
            Rev,//互补链
            ToeHoldRev,
            WildCard,//绘图用
        }
        public bool isToe()//是否是toe hold
        {
            return Type == DomType.ToeHold || Type == DomType.ToeHoldRev;
        }
        public int location { get; set; }//仅在复合物解链中使用，用于快速定位域在原复合物元素中第几个。
        public DomType Type { get; set; } = DomType.Normal;
        public NameNode Name { get; set; }//这里牵一发动全身，先不动了
        public NameNode seq { get; set; }
        public ValueNode colour { get; set; }
        public ValueNode bind { get; set; }
        public ValueNode unbind { get; set; }
        public ListExpression subdomains { get; set; }
        public override bool Equals(object? obj)
        {
            if (obj is DomNode other)
            {
                return this.Name.Equals(other.Name) && this.Type == other.Type;//目前只考虑这一点
            }
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name?.GetHashCode() ?? 0, Type.GetHashCode());
        }
        public DomNode clear()
        {
            DomNode dom = new();
            dom.Type = this.Type;
            dom.Name = new NameNode() { Name = (string)this.Name.GetValue() };
            dom.seq = this.seq;
            dom.colour = this.colour;
            dom.bind = this.bind;
            dom.unbind = this.unbind;
            dom.subdomains = this.subdomains;
            return dom;
        }
        private string GetRev(string s)//获取反向互补序列
        {
            char[] ss = s.ToUpper().ToCharArray();
            for (int i = 0; i < ss.Length; i++)
            {
                switch (ss[i])
                {
                    case 'A': ss[i] = 'T'; break;
                    case 'T': ss[i] = 'A'; break;
                    case 'C': ss[i] = 'G'; break;
                    case 'G': ss[i] = 'C'; break;
                }
            }
            return new string(ss);
        }
        public DomNode getNormal()
        {
            DomNode dom = new();
            dom.Type = DomType.Normal;
            dom.Name = this.Name;
            dom.seq = this.seq;
            dom.colour = this.colour;
            dom.bind = this.bind;
            dom.unbind = this.unbind;
            dom.subdomains = this.subdomains;
            return dom;
        }
        public override DomNode GetRevComp() //获取反向互补序列
        {
            DomNode revComp = this.DeepCopy() as DomNode;
            if (this.seq is not null)
                revComp.seq.Name = GetRev((string)this.seq.GetValue());
            switch (this.Type)
            {
                case DomType.Normal:
                    revComp.Type = DomType.Rev;
                    break;
                case DomType.Rev:
                    revComp.Type = DomType.Normal;
                    break;
                case DomType.ToeHold:
                    revComp.Type = DomType.ToeHoldRev;
                    break;
                case DomType.ToeHoldRev:
                    revComp.Type = DomType.ToeHold;
                    break;
            }
            return revComp;
        }
        public override BaseComplex DeepCopy()
        {
            DomNode copy = new();
            copy.Type = this.Type;
            copy.Name = this.Name;
            copy.seq = this.seq;
            copy.colour = this.colour;
            copy.bind = this.bind;
            copy.unbind = this.unbind;
            copy.subdomains = this.subdomains;
            return copy;
        }
        public string getColor()
        {
            if (this.colour is not null)
            {
                return this.colour.GetValue().ToString();
            }
            else
                return "grey";
        }
        public override string ToString()
        {
            if (this.Type == DomType.Normal)
                return Name.ToString();
            if (this.Type == DomType.Rev)
                return Name.ToString()+"*";
            if(this.Type == DomType.ToeHold)
                return Name.ToString()+"^";
            return Name.ToString()+"^*";
        }
    }
    public abstract class BaseStrandNode : BaseComplex
    {
        public abstract ExpressionNode GetRevComp();
    }

    public class StrandNode : BaseStrandNode
    {
        public enum StrandType
        {
            upper,
            lower,
            duplex,
        }
        public int GetLength()//获取链长度
        {
            if (seq is not null)
                return seq.GetLength();
            else
                return 0;
        }
        public StrandType Type { get; set; }
        public SeqNode seq { get; set; }//如果是双链的话就是上链序列
        public List<bool> bps { get; set; }//双链在解链前的碱基情况，方便后续判断能不能发生反应，在进行解链时定义更新

        public void AddSome(int num, bool bp)
        {
            if (bps is null)
                bps = new List<bool>();
            for (int i = 0; i < num; i++)
            {
                bps.Add(bp);
            }
        }
        public int getBpsCount(int start)//start一般是新结合范围内的
        {
            if (bps is null)//单链就没有任何阻碍了
                return 0;
            int count = 0;
            while (start <= bps.Count)//可能是长于Toehold链的
            {
                if (bps[start]) count++;
                else if (count > 0) break;//中断就算了，不考虑错配
                start++;
            }
            return count;
        }
        public bool canRev()
        {
            if (this.Type == StrandType.duplex && this.GetLength() == 1)
            {
                return this.seq.isToehold();
            }
            return false;
        }
        public StrandNode getUpper()
        {
            StrandNode strand = new();
            strand.Type = StrandType.upper;
            if (this.Type != StrandType.lower)
                strand.seq = this.seq.normalize();
            else
                strand.seq = this.seq.Rotate().normalize();
            return strand.DeepCopy() as StrandNode;
        }
        public override StrandNode GetRevComp()
        {
            StrandNode revComp = this.DeepCopy() as StrandNode;
            revComp.Type = (this.Type == StrandType.upper ? StrandType.lower : StrandType.upper);
            revComp.seq = this.seq.GetRevComp().normalize();
            return revComp;
        }
        public StrandNode Rotate()//单纯把当前的链反过来
        {
            StrandNode strand = new();
            strand.seq = (this.seq.DeepCopy() as SeqNode).Rotate();
            if (Type == StrandType.upper)
                strand.Type = StrandType.lower;
            else if (Type == StrandType.lower)
                strand.Type = StrandType.upper;
            else if (Type == StrandType.duplex)
            {
                strand.Type = StrandType.duplex;
                strand.seq = strand.seq.GetRevComp().normalize();
            }

            return strand;
        }
        public void link(StrandNode strand)
        {
            if (strand is null)
                return;
            if (this.seq is null)
                this.seq = strand.seq;
            else
                this.seq.link(strand.seq);
        }
        public void link(SeqNode seq2)
        {
            if (seq2 is null)
                return;
            if (this.seq is null)
                this.seq = seq2;
            else
                this.seq.link(seq2);
        }
        public void link(SeqNode seq2, int loca)//保留位置信息方便回到复合物进行查询
        {
            ExpressionPrinter.PrintExpression(seq2);
            this.seq.link(seq2.addLocation(loca));
        }
        override public BaseComplex DeepCopy()
        {
            StrandNode copy = new();
            copy.Type = this.Type;
            if (seq is not null)
                copy.seq = (this.seq.DeepCopy() as SeqNode).normalize();
            return copy;
        }
        public override bool Equals(object? obj)
        {
            if (obj is StrandNode other)
            {
                if (this.Type == other.Type && this.seq.normalize().Equals(other.seq.normalize()))
                    return true;
                if (this.Type != other.Type && this.seq.normalize().Equals(other.seq.Rotate().normalize()))
                    return true;
                return false;
            }
            return false;
        }
        public string getColor()
        {
            if (this.seq is not null)
            {
                return seq.getColor();
            }
            return "grey";
        }
        public DomNode getDom(int i)
        {
            if (this.seq is not null)
            {
                if (this.seq.Value is List<SeqNode> list)
                {
                    if (i < list.Count)
                    {
                        return list[i].Value as DomNode;
                    }
                }
                else if (i == 0 && this.seq.Value is DomNode dom)
                {
                    return dom;
                }
            }
            return null;
        }
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Type.GetHashCode());
            if (seq is not null)
                hash = hash * 23 + (seq.normalize().GetHashCode());
            return hash;
        }
        public StrandNode normalize()
        {
            StrandNode strand = this.DeepCopy() as StrandNode;
            if (this.seq is not null)
                strand.seq = this.seq.normalize();
            return strand.DeepCopy() as StrandNode;
        }
        public override string ToString()
        {
            //return base.ToString();
            string str = "";
            if (this.Type == StrandType.upper)
                str += "<";
            else if (this.Type == StrandType.lower)
                str += "{";
            else if (this.Type == StrandType.duplex)
                str += "[";
            if (this.seq is not null)
                str += this.seq.ToString();
            if (this.Type == StrandType.upper)
                str += ">";
            else if (this.Type == StrandType.lower)
                str += "}";
            else if (this.Type == StrandType.duplex)
                str += "]";
            return str;
        }
    }
    public class TetherNode : BaseStrandNode //还没想好怎么写
    {
        public override TetherNode GetRevComp()
        {
            return this;
        }
        override public BaseComplex DeepCopy()
        {
            TetherNode copy = new();
            return copy;
        }
        public override bool Equals(object? obj)
        {
            return false;
        }
        public override int GetHashCode()
        {
            return 0;
        }
    }
    public class SeqNode : BaseStrandNode //dom dom* tether seqs
    {
        public object Value { get; set; }
        public SeqNode Rotate()
        {
            if (Value is DomNode)
                return this.DeepCopy() as SeqNode;
            if (Value is List<SeqNode> list)
            {
                List<SeqNode> seqs = new List<SeqNode>();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    DomNode dom = list[i].Value as DomNode;
                    seqs.Add(new SeqNode() { Value = dom.DeepCopy() });
                }
                return new SeqNode() { Value = seqs };
            }
            else
            {
                throw new ArgumentException("Value must be of type DomNode or List<SeqNode>.");
            }

        }
        public SeqNode addLocation(int loac)//添加位置信息
        {
            SeqNode seq = this.DeepCopy() as SeqNode;//避免修改原来的值
            if (seq.Value is List<SeqNode> list)
            {
                foreach (var item in list)
                {
                    if (item.Value is DomNode domNode)
                    {
                        domNode.location = loac;
                    }
                }
            }
            else if (Value is DomNode domNode1)
            {
                domNode1.location = loac;
            }
            return seq;
        }
        public int GetLength()
        {
            if (Value is List<SeqNode> list)
            {
                return list.Count;
            }
            else
                return 1;
        }
        public SeqNode getNormal()
        {
            if (Value is DomNode dom)
            {
                SeqNode seq = new();
                seq.Value = dom.getNormal();
                return seq;
            }
            else if (Value is List<SeqNode> list)
            {
                SeqNode seq = new();
                seq.Value = new List<SeqNode>();
                foreach (var item in list)
                {
                    ((List<SeqNode>)seq.Value).Add(item.getNormal());
                }
                return seq;
            }
            else
            {
                throw new ArgumentException("Value must be of type DomNode or List<SeqNode>.");
            }
        }
        public override int GetHashCode()
        {
            int hash = 17;
            if (Value is List<SeqNode> list && list.Count > 1)
            {
                foreach (var item in list)
                {
                    hash = hash * 23 + (item.GetHashCode());
                }
            }
            else if (Value is DomNode domNode1)
            {
                hash = hash * 23 + (domNode1.GetHashCode());
            }
            else if (Value is List<SeqNode> list1 && list1.Count == 1)
            {
                hash = hash * 23 + (list1[0].GetHashCode());
            }
            return hash;
        }
        public override SeqNode GetRevComp()
        {
            SeqNode revComp = this.DeepCopy() as SeqNode;
            if (Value is List<SeqNode> seqs)
            {
                revComp.Value = new List<SeqNode>();
                foreach (var item in seqs)
                {
                    ((List<SeqNode>)revComp.Value).Add(item.GetRevComp());
                }
            }
            else if (Value is DomNode)
            {
                revComp.Value = ((DomNode)Value).GetRevComp();
            }
            else if (Value is TetherNode)
            {
                revComp.Value = ((TetherNode)Value).GetRevComp();
            }
            return revComp;
        }
        public void link(DomNode domNode)
        {
            if (Value is List<SeqNode> list)
            {
                ((List<SeqNode>)Value).Add(new SeqNode { Value = domNode });
            }
            else if (Value is DomNode domNode1)
            {
                Value = new List<SeqNode> { new SeqNode { Value = domNode1 }, new SeqNode { Value = domNode } };
            }
            else if (Value is null)
            {
                Value = domNode.DeepCopy() as DomNode;
            }
        }
        public void link(List<SeqNode> list)
        {
            if (Value is List<SeqNode> seqs)
            {
                ((List<SeqNode>)Value).AddRange(list);
            }
            else if (Value is DomNode domNode1)
            {
                Value = new List<SeqNode>();
                ((List<SeqNode>)Value).Add(new SeqNode { Value = domNode1 });
                ((List<SeqNode>)Value).AddRange(list);
            }
        }
        public void link(SeqNode seq2)
        {
            if (seq2.Value is DomNode dom)
                this.link(dom);
            else if (seq2.Value is List<SeqNode> list)
            {
                this.link(list);
            }
        }
        public override BaseComplex DeepCopy()
        {
            SeqNode copy = new();
            if (Value is List<SeqNode> list)
            {
                copy.Value = new List<SeqNode>();
                foreach (var item in list)
                {
                    if (item is SeqNode seq && seq.Value is DomNode)
                    {
                        ((List<SeqNode>)(copy.Value)).Add((SeqNode)seq.DeepCopy());
                    }
                    else
                    {
                        throw new ArgumentException("Values must be of type SeqNode.");
                    }
                }
            }
            else if (Value is DomNode domNode1)
            {
                copy.Value = (DomNode)domNode1.DeepCopy();
            }
            return copy;
        }
        public override bool Equals(object? obj)
        {
            if (obj is SeqNode other)
            {
                if (this.Value is List<SeqNode> list1 && other.Value is List<SeqNode> list2)
                {
                    if (list1.Count != list2.Count)
                        return false;
                    for (int i = 0; i < list1.Count; i++)
                    {
                        if (!list1[i].Equals(list2[i]))
                            return false;
                    }
                    return true;
                }
                else if (this.Value is DomNode domNode1 && other.Value is DomNode domNode2)
                {
                    return domNode1.Equals(domNode2);
                }
            }
            return false;
        }
        public string getColor()
        {
            if (Value is List<SeqNode> list)
            {
                foreach (var item in list)
                {
                    if (item.Value is DomNode domNode)
                    {
                        return domNode.getColor();
                    }
                }
            }
            else if (Value is DomNode domNode1)
            {
                return domNode1.getColor();
            }
            return "";
        }
        public bool isToehold()
        {
            if (this.Value is DomNode dom && dom.isToe())
            {
                return true;
            }
            return false;
        }
        public SeqNode normalize()
        {
            SeqNode seq = new();
            if (this.Value is List<SeqNode> list && list.Count == 1)
                seq.Value = list[0].normalize();
            else
            {
                seq.Value = this.Value;
            }
            return seq;
        }
        public override string ToString()
        {
            string str = "";
            if (Value is List<SeqNode> list)
            {
                foreach (var item in list)
                {
                    str += item.ToString()+" ";
                }
            }
            else if (Value is DomNode domNode1)
            {
                str += domNode1.ToString();
            }
            return str;
        }
    }
    public abstract class BaseComplex : ExpressionNode
    {
        abstract public BaseComplex DeepCopy();//解决引用问题
        abstract public override bool Equals(object? obj);//方便结合旋转来判断是否是同一个复合物
        abstract public override int GetHashCode();//方便结合旋转来判断是否是同一个复合物

    }
    public class ComplexNode2 : BaseComplex//或许是一种更高效的物种表示方式。
    {
        public StrandNode middle;
        public StrandNode lefttop = null;
        public StrandNode leftbottom = null;
        public StrandNode righttop = null;
        public StrandNode rightbottom = null;
        public LinkerNode linker = null;//前面的连接符
        public LinkerNode linker2 = null;//后面的连接符,默认不用
        public ComplexNode2() { }
        public ComplexNode2(StrandNode middle, StrandNode lefttop, StrandNode leftbottom, StrandNode righttop, StrandNode rightbottom, LinkerNode linker)
        {
            this.middle = middle;
            this.lefttop = lefttop;
            this.leftbottom = leftbottom;
            this.righttop = righttop;
            this.rightbottom = rightbottom;
            this.linker = linker;
        }
        public ComplexNode2(StrandNode middle, StrandNode lefttop, StrandNode leftbottom, StrandNode righttop, StrandNode rightbottom)
        {
            this.middle = middle;
            this.lefttop = lefttop;
            this.leftbottom = leftbottom;
            this.righttop = righttop;
            this.rightbottom = rightbottom;
        }
        public StrandNode getUpper()
        {
            StrandNode strand = new();
            strand.Type = StrandNode.StrandType.upper;
            if (this.lefttop is not null)
                strand.link(this.lefttop.DeepCopy() as StrandNode);
            if (this.middle is not null)
                strand.link(this.middle.DeepCopy() as StrandNode);
            if (this.righttop is not null)
                strand.link(this.righttop.DeepCopy() as StrandNode);
            return strand.DeepCopy() as StrandNode;
        }
        public StrandNode getLower()
        {
            StrandNode strand = new();
            strand.Type = StrandNode.StrandType.lower;
            if (this.leftbottom is not null)
                strand.link(this.leftbottom.DeepCopy() as StrandNode);
            if (this.middle is not null)
                strand.link(this.middle.GetRevComp());
            if (this.rightbottom is not null)
                strand.link(this.rightbottom);
            return strand.DeepCopy() as StrandNode;
        }
        public enum Linktype
        {
            lefttop,
            leftbottom,
            righttop,
            rightbottom,
        }
        public void linkwith(Linktype linktype, StrandNode strand0)
        {
            if (strand0 is null)
                return;
            StrandNode strand = strand0.DeepCopy() as StrandNode;
            switch (linktype)
            {
                case Linktype.lefttop:
                    if (lefttop is null)
                        lefttop = strand;
                    else
                        lefttop.link(strand.seq);
                    break;
                case Linktype.leftbottom:
                    if (leftbottom is null)
                        leftbottom = strand;
                    else
                        leftbottom.link(strand.seq);
                    break;
                case Linktype.righttop:
                    if (righttop is null)
                        righttop = strand;
                    else
                        righttop.link(strand.seq);
                    break;
                case Linktype.rightbottom:
                    if (rightbottom is null)
                        rightbottom = strand;
                    else
                        rightbottom.link(strand.seq);
                    break;
            }
        }
        override public BaseComplex DeepCopy()
        {
            ComplexNode2 copy = new();
            copy.middle = this.middle.DeepCopy() as StrandNode;
            if (this.lefttop is not null)
                copy.lefttop = this.lefttop.DeepCopy() as StrandNode;
            if (this.leftbottom is not null)
                copy.leftbottom = this.leftbottom.DeepCopy() as StrandNode;
            if (this.righttop is not null)
                copy.righttop = this.righttop.DeepCopy() as StrandNode;
            if (this.rightbottom is not null)
                copy.rightbottom = this.rightbottom.DeepCopy() as StrandNode;
            if (this.linker is not null)
                copy.linker2 = this.linker.DeepCopy() as LinkerNode;
            return copy;
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj is not ComplexNode2 other) return false;
            if (this.middle != other.middle) return false;
            if (this.lefttop != other.lefttop) return false;
            if (this.leftbottom != other.leftbottom) return false;
            if (this.righttop != other.righttop) return false;
            if (this.rightbottom != other.rightbottom) return false;
            if (this.linker != other.linker) return false;
            return true;
        }
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (middle?.GetHashCode() ?? 0);
            hash = hash * 23 + (lefttop?.GetHashCode() ?? 0);
            hash = hash * 23 + (leftbottom?.GetHashCode() ?? 0);
            hash = hash * 23 + (righttop?.GetHashCode() ?? 0);
            hash = hash * 23 + (rightbottom?.GetHashCode() ?? 0);
            hash = hash * 23 + (linker?.GetHashCode() ?? 0);
            return hash;
        }
        public ComplexNode2 Rotate()
        {
            ComplexNode2 copy = new();
            copy.middle = this.middle.Rotate();
            if (this.lefttop is not null)
                copy.rightbottom = this.lefttop.Rotate();
            if (this.leftbottom is not null)
                copy.righttop = this.leftbottom.Rotate();
            if (this.righttop is not null)
                copy.leftbottom = this.righttop.Rotate();
            if (this.rightbottom is not null)
                copy.lefttop = this.rightbottom.Rotate();
            if (this.linker is not null)
                copy.linker = this.linker.Rotate();
            return copy;
        }
        public override string ToString()
        {
            TextWriter originalOut = Console.Out;
            string Complex = "";
            using (StringWriter stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);
                if (this.linker is not null)
                    ASTPrinter.PrintAst(this.linker);
                if (this.lefttop is not null)
                    ASTPrinter.PrintAst(this.lefttop);
                if (this.leftbottom is not null)
                    ASTPrinter.PrintAst(this.leftbottom);
                if (this.middle is not null)
                    ASTPrinter.PrintAst(this.middle);
                if (this.righttop is not null)
                    ASTPrinter.PrintAst(this.righttop);
                if (this.rightbottom is not null)
                    ASTPrinter.PrintAst(this.rightbottom);
                Complex = stringWriter.ToString();
                Console.SetOut(originalOut);
            }
            return Complex;
        }
    }
    public class ComplexNode : BaseComplex
    {
        public List<BaseComplex> Values { get; } = new();//最左面要么是left，要么是segment，最右面要么是right，要么是segment，两两之间有linker
        override public BaseComplex DeepCopy()
        {
            ComplexNode copy = new();
            foreach (var item in Values)
            {
                if (item is BaseComplex baseComplex)
                {
                    copy.Values.Add(baseComplex.DeepCopy());
                }
                else
                {
                    throw new ArgumentException("Values must be of type BaseComplex.");
                }
            }
            return copy;
        }
        public override bool Equals(object? obj)
        {
            if (obj is not ComplexNode other)
                return false;
            var list1 = this.Values;
            var list2 = (obj as ComplexNode)?.Values;
            if (list1 == null || list2 == null)
                return false;
            if (list1.Count != list2.Count)
                return false;
            for (int i = 0; i < list1.Count; i++)
            {
                if (!list1[i].Equals(list2[i]))
                    return false;
            }
            return true;
        }

        public ComplexNode Rotate()
        {
            ComplexNode copy = new();
            for (int i = Values.Count - 1; i >= 0; i--)
            {
                if (Values[i] is LinkerNode link)
                {
                    copy.Values.Add(link.Rotate());
                }
                else if (Values[i] is StrandNode strand)
                    copy.Values.Add(strand.Rotate());
                else
                {
                    throw new ArgumentException("Values must be of type BaseComplex.");
                }
            }
            return copy;
        }
        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var item in Values)
            {
                hash = hash * 23 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
        public Tuple<List<StrandNode>, List<StrandNode>> getStrands()//替代checkComplex，或者可以换成一个struct，携带链的相关信息方便后续操作
        {
            List<StrandNode> uppers = new();
            List<StrandNode> lowers = new();

            return new Tuple<List<StrandNode>, List<StrandNode>>(uppers, lowers);
        }
        public override string ToString()
        {
            string str = "";
            foreach (var item in Values)
            {
                if (item is LinkerNode link)
                    str += link.ToString();
                else if (item is StrandNode strand)
                    str += strand.ToString();
                else
                    throw new ArgumentException("Values must be of type BaseComplex.");
            }
            return str;
        }
    }
    public class LinkerNode : BaseComplex
    {
        public enum LinkerType
        {
            upper,//::
            lower,//:
        }
        public LinkerType Type { get; set; }
        public bool isSame(StrandNode strand)
        {
            if (strand.Type == StrandNode.StrandType.upper && Type == LinkerType.upper)
                return true;
            else if (strand.Type == StrandNode.StrandType.lower && Type == LinkerType.lower)
                return true;
            else
                return false;
        }
        public override BaseComplex DeepCopy()
        {
            LinkerNode copy = new();
            copy.Type = this.Type;
            return copy;
        }
        public LinkerNode Rotate()
        {
            LinkerNode copy = new();
            if (Type == LinkerType.upper)
                copy.Type = LinkerType.lower;
            else if (Type == LinkerType.lower)
                copy.Type = LinkerType.upper;
            return copy;
        }
        public override bool Equals(object? obj)
        {
            if (obj is LinkerNode other)
            {
                return this.Type == other.Type;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
        public override string ToString()
        {
            if (Type == LinkerType.upper)
                return "::";
            else
                return ":";
        }
    }

    public class BinaryExpression : ExpressionNode
    {
        public Operator Operator { get; set; }
        public ExpressionNode Left { get; set; }
        public ExpressionNode Right { get; set; }
    }
    // 列表基类
    public abstract class ListNode : ExpressionNode { }// []
    public class ListExpression : ListNode
    {
        public List<ExpressionNode> Values { get; } = new();
    }
    // 参数基类
    public abstract class ParameterNode : ExpressionNode { }//{}
    public class Parameters : ParameterNode
    {
        public ExpressionNode Value { get; set; }
        public ExpressionNode parameters { get; } = null;
        public Parameters(ExpressionNode list)
        {
            parameters = list;
        }
    }
    // 值基类
    public abstract class ValueNode : ExpressionNode
    {
        public abstract object GetValue();
        abstract public override bool Equals(object? obj);
        abstract public override int GetHashCode();
        public virtual List<NameNode> Arguments => null;//函数参数仅有此
    }
    public class IntegerNode : NameNode//这样暂时来解决整数当作变量名的情况，出问题再改
    {
        public IntegerNode() { }
        public IntegerNode(int value)
        {
            Value = value;
            this.Name = value.ToString();
        }
        public int Value { get; set; }
        public string GetName() => Name;
        public override object GetValue() => Value;
        public override bool Equals(object? obj)
        {
            if (obj is IntegerNode other)
            {
                return Value == other.Value;
            }
            else if (obj is int intValue)
            {
                return Value == intValue;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public override string ToString()
        {
            return Value.ToString();
        }

    }
    public class StringNode : ValueNode
    {
        public string Value { get; set; }
        public override object GetValue() => Value;
        public override bool Equals(object? obj)
        {
            if (obj is StringNode other)
            {
                return Value == other.Value;
            }
            else if (obj is string strValue)
            {
                return Value == strValue;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
    public class NameNode : ValueNode
    {
        public string Name { get; set; }
        public NameNode()
        {
        }
        public NameNode(string name)
        {
            Name = name;
        }
        public override bool Equals(object? obj)
        {
            if (obj is NameNode other)
            {
                return Name.Equals(other.Name);
            }
            else if (obj is string strValue)
            {
                return Name.Equals(strValue);
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public override string ToString()
        {
            return Name;
        }
        public override object GetValue() => Name;
    }
    public class FloatNode : ValueNode
    {
        public double Value { get; set; }
        public override object GetValue() => Value;
        public override bool Equals(object? obj)
        {
            if (obj is FloatNode other)
            {
                return Value == other.Value;
            }
            else if (obj is double doubleValue)
            {
                return Value == doubleValue;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
    public class FuncNode : ValueNode
    {
        public string Name { get; set; }
        public override object GetValue() => Name;
        public override List<NameNode> Arguments { get; } = new();
        public AstNode Expressions { get; set; }//这个是用来存储函数体的，函数体可能是一个复合物，也可能是一个Process，先考虑复合物的情况
        public FuncNode() { }
        public FuncNode(string name)
        {
            Name = name;
        }
        public override bool Equals(object? obj)
        {
            if (obj is FuncNode other)
            {
                return Name.Equals(other.Name);
            }
            else if (obj is string strValue)
            {
                return Name.Equals(strValue);
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public FuncNode(List<ExpressionNode> arguments)
        {
            foreach (var arg in arguments)
            {
                if (arg is NameNode nameNode)
                    Arguments.Add(nameNode);
                else
                    throw new ArgumentException("All arguments must be of type NameNode.");
            }
        }
    }
    public class KeywordNode : ValueNode
    {
        public Keyword Value { get; set; }
        public override object GetValue() => Value;
        public override bool Equals(object? obj)
        {
            if (obj is KeywordNode other)
            {
                return Value == other.Value;
            }
            else if (obj is Keyword keywordValue)
            {
                return Value == keywordValue;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
    public class UnitValueNode : ValueNode
    {
        public double Value { get; set; }
        public override object GetValue() => Value;
        public Units units { get; set; }
        public override bool Equals(object? obj)
        {
            if (obj is UnitValueNode other)
            {
                return Value == other.Value && units == other.units;
            }
            else if (obj is double doubleValue)
            {
                return Value == doubleValue;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Value, units);
        }
    }
}

