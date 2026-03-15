using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace DSDCore
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private Token Current => _position < _tokens.Count ? _tokens[_position] : new Token(TokenType.EOF, "EOF", 0, 0);
        private int _position;
        public List<Errors> errors;
        private int plots = 0, simulation = 0;
        private Stack<char> stack = new Stack<char>();//用于括号匹配

        public class Errors
        {
            public int line { get; set; }
            public int column { get; set; }
            public string message { get; set; }
        }
        private void PrintErrors()
        {
            foreach (var error in errors)
            {
                //Console.WriteLine($"Error at line {error.line}, column {error.column}: {error.message}");
            }
        }
        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }
        public Parser(Lexer lexer)
        {
            _tokens = lexer.GetTokenList();
            ////Console.WriteLine($"Parser初始完成！共{_tokens.Count()}个Token！\n");
        }
        private Token Next()
        {
            var current = Current;
            if (_position < _tokens.Count)
                _position++;
            return current;
        }
        private Token Back()
        {
            if (_position > 0)
                _position--;
            return Current;
        }
        private Token _Next()
        {
            if (_position + 1 < _tokens.Count)
                return _tokens[_position + 1];
            else return new Token(TokenType.EOF, "EOF", 0, 0);
        }
        private Token _Back()
        {
            if (_position > 0)
                return _tokens[_position - 1];
            else return new Token(TokenType.EOF, "EOF", 0, 0);
        }
        private bool Match(TokenType type) => Current.Type == type;

        private bool MatchNext(TokenType type) => _Next().Type == type;
        private bool MatchEOF() => Match(TokenType.EOF);
        private bool MatchKeyword(Keyword keyword) => (Match(TokenType.Keyword) && (Keyword)Current.Value == keyword);

        private bool MatchOperator(Operator op) => (Match(TokenType.Operator) && (Operator)Current.Value == op);
        private bool MatchInt() => Match(TokenType.Integer);
        private bool MatchFloat() => Match(TokenType.Float);
        private bool MatchComma() => MatchOperator(Operator.Comma);//,
        private bool MatchColon() => MatchOperator(Operator.Colon);//:
        private bool MatchSemicolon() => MatchOperator(Operator.Semicolon);//;
        private bool MatchEqual() => MatchOperator(Operator.Equal);//=
        private bool MatchLeftBracket()
        {
            if (MatchOperator(Operator.LeftBracket))
            {
                stack.Push('[');
                Next();
                return true;
            }
            return false;
        }

        private bool MatchRightBracket()
        {
            if (MatchOperator(Operator.RightBracket))
            {
                if (stack.Count > 0 && stack.Peek() == '[')
                {
                    stack.Pop();
                    Next();
                    return true;
                }
                else
                {
                    AddError($"]括号不匹配,当前共有{stack.Count}个字符");

                    return false;
                }
            }
            return false;
        }
        private bool MeetRightBracket()
        {
            if (MatchOperator(Operator.RightBracket))
            {
                return true;
            }
            return false;
        }
        private bool MatchLeftBrace()
        {
            if (MatchOperator(Operator.LeftBrace))
            {
                stack.Push('{');
                Next();
                return true;
            }
            return false;
        }
        private bool MatchRightBrace()
        {
            if (MatchOperator(Operator.RightBrace))
            {
                if (stack.Count > 0 && stack.Peek() == '{')
                {
                    stack.Pop();
                    Next();
                    return true;
                }
                else
                {
                    AddError("}括号不匹配");
                    return false;
                }
            }
            return false;
        }
        private bool MeetRightBrace()
        {
            if (MatchOperator(Operator.RightBrace))
            {
                return true;
            }
            return false;
        }
        private bool MatchLeftStrand()
        {
            if (MatchOperator(Operator.LeftStrand))
            {
                stack.Push('<');
                Next();
                return true;
            }
            return false;
        }
        private bool MatchRightStrand()
        {
            if (MatchOperator(Operator.RightStrand))
            {
                if (stack.Count > 0 && stack.Peek() == '<')
                {
                    stack.Pop();
                    Next();
                    return true;
                }
                else
                {
                    AddError(">括号不匹配");
                    return false;
                }
            }
            return false;
        }
        private bool MeetRightStrand()
        {
            if (MatchOperator(Operator.RightStrand))
            {
                return true;
            }
            return false;
        }
        private bool MatchParentOpen()
        {
            if (MatchOperator(Operator.ParentOpen))
            {
                stack.Push('(');
                Next();
                return true;
            }
            return false;
        }
        private bool MatchParentClose()
        {
            if (MatchOperator(Operator.ParentClose))
            {
                if (stack.Count > 0 && stack.Peek() == '(')
                {
                    stack.Pop();
                    Next();
                    return true;
                }
                else
                {
                    AddError(")括号不匹配");
                    return false;
                }
            }
            return false;
        }
        private bool MeetParentOpen()
        {
            if (MatchOperator(Operator.ParentOpen))
            {
                return true;
            }
            return false;
        }
        private bool MeetParentClose()
        {
            if (MatchOperator(Operator.ParentClose))
            {
                return true;
            }
            return false;
        }
        private bool MatchTwoColon() => MatchOperator(Operator.TwoColon);//::
        private bool MatchUnderline() => MatchOperator(Operator.Underline);//_
        private bool MatchAT() => MatchOperator(Operator.At);//@
        private void OutputCurrent()
        {
#if _DEBUG
            //Console.WriteLine($"当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
#endif
        }
        private void AddError(string msg)
        {
            Errors error = new Errors();
            error.line = Current.Line;
            error.column = Current.Column;
            error.message = msg;
            errors.Add(error);
            //Console.WriteLine($"Error at line {error.line}, column {error.column}: {error.message}");

            throw new Exception($"行 {error.line}, 列 {error.column}: {error.message}");
        }
        public ProgramNode Parse()
        {
            errors = new List<Errors>();
            var program = new ProgramNode();
            ////Console.WriteLine("开始解析...\n");
            ////Console.WriteLine($"当前Token:{Current.ToString()}\n");

            ////ExpressionPrinter.PrintExpression(ParseSpecies());
            ////program.Statements.Add(ParseStrand());
            //if(MatchLeftBracket())
            //ExpressionPrinter.PrintExpression(ParseSpeciesList());
            while (Current.Type != TokenType.EOF)
            {
                program.statements.Add(ParseStatement());//现在默认会吃掉分号
                ////Console.WriteLine($"解析完成一句话 当前Token:{Current.ToString()}\n");
                if(MatchSemicolon())
                {
                    Next();
                }
            }
            PrintErrors();
            if (errors.Count > 0)
            {
                Console.WriteLine("解析失败！\n");
            }
            else
            {
                Console.WriteLine("解析成功！\n");
            }
            return program;
        }
        private StatementNode ParseStatement()
        {
            ////Console.WriteLine($"当前Token:{Current.ToString()}\n MatchKeyword == {MatchKeyword(Keyword.directive)}");
            if (MatchKeyword(Keyword.directive))
            {
                DirectiveNode directive = ParseDirective();
                return directive;
            }

            if (MatchKeyword(Keyword.dom))
            {
                Next();
                return ParseDom();
            }
            if (MatchKeyword(Keyword.new_))
            {
                Next();
                return ParseNew();
            }
            if (MatchKeyword(Keyword.def))
            {
                Next();
                return ParseDef();
            }

            return ParseProcess();//最后解析初始进程
        }
        private DirectiveNode ParseDirective()
        {
            simulation = 0;
            ////Console.WriteLine("解析指令...\n");
            Next();
            var directive = new DirectiveNode();
            if (Match(TokenType.Keyword))
            {
                directive.Name = (Keyword)Current.Value;
                switch ((Keyword)Current.Value)
                {
                    case Keyword.parameters:
                        ////Console.WriteLine("检测到parameters，正在解析参数...\n");
                        directive.Value = ParseListDirective();
                        break;
                    case Keyword.sweeps:
                        ////Console.WriteLine("检测到sweep，正在解析参数...\n");
                        directive.Value = ParseListDirective();
                        break;
                    case Keyword.plot_settings:
                        //Console.WriteLine("检测到plot_settings，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.units:
                        //Console.WriteLine("检测到units，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.deterministic:
                        //Console.WriteLine("检测到deterministic，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.rendering:
                        //Console.WriteLine("检测到rendering，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.stochastic:
                        //Console.WriteLine("检测到stochastic，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.spatial:
                        //Console.WriteLine("检测到spatial，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.simulation:
                        simulation = 1;
                        //Console.WriteLine("检测到simulation，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.inference:
                        //Console.WriteLine("检测到inference，正在解析参数...\n");
                        directive.Value = ParseParaDirective();
                        break;
                    case Keyword.locations:
                        //Console.WriteLine("检测到locations，正在解析参数...\n");
                        directive.Value = ParseListDirective();
                        break;
                    case Keyword.leak:
                        //Console.WriteLine("检测到leak，正在解析参数...\n");
                        directive.Value = ParseValueDirective();
                        break;
                    case Keyword.tau:
                        //Console.WriteLine("检测到tau，正在解析参数...\n");
                        directive.Value = ParseValueDirective();
                        break;
                    case Keyword.migrate:
                        //Console.WriteLine("检测到migrate，正在解析参数...\n");
                        directive.Value = ParseValueDirective();
                        break;
                    case Keyword.lengths:
                        //Console.WriteLine("检测到lengths，正在解析参数...\n");
                        ListExpression list1 = new ListExpression();
                        Next();
                        if (MatchInt())
                        {
                            list1.Values.Add(GetFromCurrent());
                            Next();
                            if (MatchInt())
                            {
                                list1.Values.Add(GetFromCurrent());
                                Next();
                            }
                        }
                        if (list1.Values.Count != 2)
                            AddError("lengths参数错误");
                        directive.Value = list1;
                        break;
                    case Keyword.toeholds:
                        //Console.WriteLine("检测到toeholds，正在解析参数...\n");
                        ListExpression list2 = new ListExpression();
                        Next();
                        if (MatchInt() || MatchFloat())
                        {
                            list2.Values.Add(GetFromCurrent());
                            Next();
                            if (MatchInt() || MatchFloat())
                            {
                                list2.Values.Add(GetFromCurrent());
                                Next();
                            }
                        }
                        if (list2.Values.Count != 2)
                            AddError("toeholds参数错误");
                        directive.Value = list2;
                        break;
                    case Keyword.compilation:
                        //Console.WriteLine("检测到compilation，正在解析参数...\n");
                        Next();
                        if (!Match(TokenType.Keyword))
                        {
                            AddError("compilation参数错误");
                            break;
                        }
                        directive.Value = GetFromCurrent();
                        Next();
                        break;
                    case Keyword.simulator:
                        //Console.WriteLine("检测到simulator，正在解析参数...\n");
                        Next();
                        if (!Match(TokenType.Keyword))
                        {
                            AddError("compilation参数错误");
                            break;
                        }
                        directive.Value = GetFromCurrent();
                        Next();
                        break;
                    default://没有跟东西的指令
                        directive.Value = new IntegerNode(1);
                        Next();
                        break;
                }
            }
            else
            {
                AddError("并非合法指令名称");
                return null;
            }
            //Console.WriteLine($"指令解析完成！当前Token:{Current.ToString()}\n 指令值为：");
            ExpressionPrinter.PrintExpression(directive.Value);
            return directive;
        }
        private DeclareNode ParseNew()//new tx  @ bind, unbind
        {
            DeclareNode declare = new DeclareNode();
            if (Match(TokenType.Name))
            {
                declare.Name = GetFromCurrent();
                Next();
                if (MatchAT())
                {
                    ListExpression para = new();
                    Next();
                    if (Match(TokenType.Integer) || Match(TokenType.Float) || Match(TokenType.Name))//此处可能会占用到bind,或者说会自己定义这个，所以先暂时改改keyword吧，后面可能还得细化这个keyword
                    {
                        BinaryExpression bina1 = new BinaryExpression();
                        bina1.Left = new NameNode("bind");
                        bina1.Operator = Operator.Equal;
                        bina1.Right = GetFromCurrent();
                        para.Values.Add(bina1);
                        Next();
                        if (MatchComma())
                        {
                            Next();
                            if (Match(TokenType.Integer) || Match(TokenType.Float) || Match(TokenType.Name))
                            {
                                BinaryExpression bina2 = new BinaryExpression();
                                bina2.Left = new NameNode("unbind");
                                bina2.Operator = Operator.Equal;
                                bina2.Right = GetFromCurrent();
                                para.Values.Add(bina2);
                                declare.Value = para;
                                Next();
                            }
                        }
                    }
                    else
                    {
                        AddError("@的不对！");
                        return null;
                    }
                }

            }
            else
            {
                AddError("new的不对！");
                return null;
            }

            return declare;
        }
        private DeclareNode ParseDom()
        {
            DeclareNode declare = new DeclareNode();
            ValueNode name = GetFromCurrent();
            if (name is NameNode nameNode)
            {
                declare.Name = name;
                Next();
                if (MatchEqual())
                {
                    Next();
                    if (MatchLeftBrace())
                    {
                        var paras = BraceParser();
                        DomNode dom = new DomNode();
                        dom.Name = nameNode;
                        if((string)(((NameNode)name).GetValue()) == "_")
                        {
                            dom.Type = DomNode.DomType.WildCard;
                        }
                        else dom.Type = DomNode.DomType.Normal;
                        foreach (var para in paras.Values)
                        {
                            //Console.WriteLine($"Dom参数：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
                            ExpressionPrinter.PrintExpression(para);
                            if(para is not BinaryExpression)
                            {
                                AddError("dom参数错误a");
                                return null;
                            }
                            BinaryExpression binary = (BinaryExpression)para;
                            if(binary.Left is not NameNode || binary.Right is not ValueNode)
                            {
                                //Console.WriteLine("有点小问题：");
                                ExpressionPrinter.PrintExpression(binary);
                                AddError("dom参数错误b");
                                return null;
                            }
                            NameNode nd = (NameNode)binary.Left;
                            ValueNode vd = (ValueNode)binary.Right;
                            switch (nd.GetValue())
                            {
                                case "bind":
                                    dom.bind = vd;
                                    break;
                                case "unbind":
                                    dom.unbind = vd;
                                    break;
                                case "seq":
                                    dom.seq = (NameNode)vd;
                                    break;
                                case "colour":
                                    dom.colour = vd;
                                    break;
                                default:
                                    AddError("并非dom的参数");
                                    return null;
                            }
                        }
                        declare.Value = dom;
                    }
                }
            }
            else
            {
                AddError("dom不能这样定义域！");
                return null;
            }
            return declare;
        }
        private DeclareNode ParseDef() 
        {
            DeclareNode declare = new DeclareNode();
            var name = GetFromCurrent();//有可能是FuncNode，也有可能是NameNode
            //Console.WriteLine($"Def参数：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            ////Console.BackgroundColor = //ConsoleColor.DarkGreen;
            //Console.WriteLine($"name:");
            ASTPrinter.PrintAst(name);
            declare.Name = name;
            if(!MatchOperator(Operator.Equal))
            Next();
            if(MatchOperator(Operator.Equal))
            {
                Next();
                if(name is not FuncNode)//就是name =  value这种了
                {
                    declare.Value = GetFromCurrent();
                    Next();
                    return declare;
                }
                //接下来应该是个Process了
                declare.Value = ParseProcess().Value;
            }
            return declare;
        }
        private ProcessNode ParseProcess()
        {
            ProcessNode psnd = new ProcessNode();
            psnd.Column = Current.Column;
            psnd.Line = Current.Line;
            if (MatchParentOpen())//可以匹配括号列表
            {
                ProcessList list = new ();
                while (!MatchParentClose())
                {
                    list.processes.Add(ParseProcess());
                    if(MatchParentClose())
                    {
                        psnd.Value = list;
                        return psnd;
                    }
                    if(!MatchOperator(Operator.Bar))//|
                    {
                        AddError("我的|呢？");
                        return null;
                    }
                    Next();
                }
                if(list.processes.Count==1)//如果只有一个，那么就不用list了
                {
                    psnd.Value = list.processes[0];
                }
                else
                    psnd.Value = list;
            }
            else if(Match(TokenType.Integer)|| Match(TokenType.Float)||Match(TokenType.Name))//解决 0 Output()这种，后面如果跟@ val就是说明在val时有前面的数目
            {
                Species spe = new Species();
                spe.Column = Current.Column;
                spe.Line = Current.Line;
                spe.Value1 = GetFromCurrent();
                if(spe.Value1 is not FuncNode)//因为如果是FuncNode，上个函数会吃掉右括号
                Next();
                if(MatchOperator(Operator.Bar)||MeetParentClose())//说明是Name(x,x,x)这种,这里是否会吃多了，前面没有应该设置值为1
                {
                    spe.Name = spe.Value1;
                    spe.Value1 = new IntegerNode(1);
                    return spe;
                    //ProcessNode ps = new();
                    //ps.Value = spe.Value1;
                    //return ps;
                }
                if(!Match(TokenType.Name))//虽然是个名字，但其实是个函数
                {
                    if(MatchOperator(Operator.Star))
                    {
                        Next();
                        if(!Match(TokenType.Name))//后面是复合物
                        {
                            if (StrandStart())//如果是 M * [B]{tx^*}这种的
                            {
                                spe.Name=ParseSpecies();
                            }
                            else// 就是说后面必然是个复合物
                            {
                                AddError("我物种名去哪了？");
                                return null;
                            }
                        }
                        else
                            spe.Name = (FuncNode)GetFromCurrent();
                    }
                    else if(StrandStart())//如果是 M  [B]{tx^*}
                    {
                        spe.Name = ParseSpecies();
                    }
                    else
                    {
                        AddError("我物种名去哪了？");
                        return null;
                    }
                }else 
                    spe.Name = (FuncNode)GetFromCurrent();
                if (MatchAT())
                {
                    Next();
                    if (Match(TokenType.Integer) || Match(TokenType.Float) || Match(TokenType.Name))
                    {
                        spe.Value2 = GetFromCurrent();
                        Next();
                    }
                }
                return spe;
            }
            else if(StrandStart())//直接就来一个物种表达式
            {
                psnd.Value = ParseSpecies();
            }
            //else if()
            else
            {
                AddError("这咱还真没见过");
                return null;
            }

           return psnd;
        }
        //private 
        private ExpressionNode ParseListDirective()//解析指令后面是[]的，包括parameters和sweeps
        {
            Next();
            if (!MatchLeftBracket())
            {
                AddError("缺少左方括号");
                return null;
            }
            return ParseList();
        }
        private ExpressionNode ParseParaDirective()//解析指令后面是{}的，包括bind和unbind
        {
            Next();
            if (!MatchLeftBrace())
            {
                AddError("缺少左大括号");
                return null;
            }
            return BraceParser();
        }
        private ExpressionNode ParseValueDirective() //指令后面是一个值的
        {
            Next();
            ExpressionNode node = GetFromCurrent();
            Next();
            return node;
        }
        
        private ListExpression BraceParser()//解析{}内的内容，有两种可能，{;;;;}或者是{,;,;,;},前者返回值为ListExpression，内里为表达式，后者返回值为ListExpression内里是Parameters
        {
            ListExpression list = new ListExpression();

            while ((!Match(TokenType.EOF)))
            {
                if (MatchRightBrace())//替代掉&& (!MatchRightBrace())
                {
                    break;
                }
                list.Values.Add(ParseBaseExpression());
                //Console.WriteLine($"DA括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
                ExpressionPrinter.PrintExpression(list);
                if (MatchRightBrace())
                {
                    break;
                }
                Next();
            }

            //Console.WriteLine($"已吃掉大括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            return list;
        }
        private ListExpression ParseParenthese()//会吃掉右括号
        {
            //Console.WriteLine("解析括号...\n");
            ListExpression nodes = new ListExpression();
            while (!MatchEOF())
            {
                if (MatchParentClose())//!MatchParentClose()&&
                {
                    break;
                }
                nodes.Values.Add(ParseBaseExpression());
                ////Console.WriteLine($"0括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
                if (MatchParentClose())
                {
                    break;
                }
                Next();
                ////Console.WriteLine($"括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            }
            //Console.WriteLine($"已吃掉括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            return nodes;
        }
        private ExpressionNode ParseBaseExpression()//不吃右括号
        {
            //Console.WriteLine("解析基础表达式...\n");
            //Console.WriteLine($"当前Token:{Current.ToString()}\n");
            if (Match(TokenType.Operator))
            {
                switch ((Operator)Current.Value)
                {
                    case Operator.LeftBracket:
                        MatchLeftBrace();//吃掉左括号
                        return ParseList();
                    case Operator.LeftBrace:
                        MatchLeftBrace();
                        return BraceParser();
                    case Operator.ParentOpen:
                        MatchParentOpen();
                        return ParseParenthese();
                }
            }
            BinaryExpression binaryExpression = new BinaryExpression();
            //Console.WriteLine($"即将调用获得值！,当前Token:{Current.ToString()}\n");
            binaryExpression.Left = GetFromCurrent();
            if (binaryExpression.Left is KeywordNode && (Keyword)(((KeywordNode)(binaryExpression.Left)).GetValue()) == Keyword.plots)//特别处理plots
            {
                plots++;
            }
            //Console.WriteLine($"获得值完成！二元表达式左侧：\n");
            ExpressionPrinter.PrintExpression(binaryExpression.Left);
            //Console.WriteLine($"当前Token:{Current.ToString()}\n");
            Next();
            if (Match(TokenType.Operator))
            {
                //这里有问题，为啥拿出来了？？？？
                if (MatchComma() || MatchSemicolon() || MeetParentClose() || MeetRightBrace() || MeetRightBracket()|| MeetRightStrand())//说明是一个值，遇到单个的就返回,这几个应该不存在才对|| MatchParentClose()||MatchRightBrace()||MatchRightBracket()
                {
                    //Back();

                    //Console.WriteLine($"哦吼，返回咯，{Current.ToString()}\n");
                    return binaryExpression.Left;
                }
                if (MatchParentOpen()) //匹配到了左括号(
                {
                    return ParseParenthese();
                }
                binaryExpression.Operator = (Operator)Current.Value;//=
                Next();
                if (MatchLeftBracket())
                {
                    if (plots > 0 && simulation == 1)//plots后面的参数
                    {
                        binaryExpression.Right = ParseSpeciesList();
                    }
                    else
                        binaryExpression.Right = ParseList();//这里是解析[]的了
                }
                else
                {
                    binaryExpression.Right = GetFromCurrent();//那么此时应该就是x=y这种
                    Next();
                    if (MatchParentOpen())//解析含参函数
                    {
                        ListExpression lp = ParseParenthese();
                        FuncNode func = new FuncNode(lp.Values);
                        if (binaryExpression.Right is NameNode)
                            func.Name = ((NameNode)binaryExpression.Right).GetValue().ToString();
                        binaryExpression.Right = func;
                    }
                    return binaryExpression;
                }
            }
            else
            {
                AddError("并非合法的操作符");
                return null;
            }
            return binaryExpression;
        }
        private ListExpression ParseList()
        {
            //Console.WriteLine("解析列表...\n");
            //Console.WriteLine($"当前Token:{Current.ToString()}\n");

            ListExpression list = new ListExpression();
            //因为吃掉了左括号，所以直接开始解析
            while (!Match(TokenType.EOF))
            {
                if (MatchRightBracket())//替代掉&& (!MatchRightBracket())
                {
                    break;
                }
                ExpressionNode node1 = ParseBaseExpression();
                //ExpressionPrinter.PrintExpression(node1);
                if (MatchComma())//这个是针对指令中的变量带参数的或者是[,,,,,类型的]
                {
                    Next();
                    ExpressionNode node2 = ParseBaseExpression();
                    //Console.WriteLine($"[[[[当前Token:{Current.ToString()}\n");
                    if (MatchSemicolon())//说明是指令中的变量带参数的in1 = 0, {interval=Real; distribution=Uniform(0,0); variation=Fixed};
                    {
                        Parameters parameters = new Parameters(node2);
                        parameters.Value = node1;
                        //ExpressionPrinter.PrintExpression(parameters);
                        list.Values.Add(parameters);
                        Next();//似乎没有考虑[in1 = 0, {interval=Real; distribution=Uniform(0,0); variation=Fixed}]
                        continue;
                    }
                    else if (MatchComma())
                    {
                        list.Values.Add(node1);
                        list.Values.Add(node2);
                        Next();
                        continue;
                    }
                }
                else if (MatchSemicolon())//[x=y;
                {
                    list.Values.Add(node1);
                    Next();
                    continue;
                }
                list.Values.Add(node1);
            }
            //if(stack.Peek()!=']')//吃掉]括号
            //{
            //    //Console.WriteLine($"已吃掉右方括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            return list;
            //}
            //AddError("缺少右方括号");
            //return null;
        }
        private bool MatchPlusOrMinus() => (MatchOperator(Operator.Plus) || MatchOperator(Operator.Minus));
        private bool MatchMathOperator() => (MatchOperator(Operator.Plus) || MatchOperator(Operator.Minus) || MatchOperator(Operator.Star) || MatchOperator(Operator.Slash));//+ - * /
        private ListExpression ParseSpeciesList() //要能解析物种，解析函数，解析参数，解析名字,进来之前需要吃掉左括号
        {
            ListExpression list = new ListExpression();

            while (!Match(TokenType.EOF))//最特殊的plots = [<X0> + Signal_Fork2X() - Signal_Fork2B(); <Y0> + Signal_Fork2Y() - Signal_Fork2B(); 2 * Signal_Fork2B() - Signal_Fork2X() - Signal_Fork2Y()];
            {
                if (MatchRightBracket())//替代掉&& (!MatchRightBracket())
                {
                    break;
                }
                var spe = ParseBaseSpecies();
                list.Values.Add(spe);
                if (MatchRightBracket())//替代掉&& (!MatchRightBracket())
                {
                    break;
                }
                Next();
                
            }
            //if (MatchRightBracket())//吃掉]括号
            //{
            ////Console.WriteLine($"已吃掉右方括号：当前Token:{Current.ToString()}\n 是第{_position}个Token\n");
            return list;
            //}
            //AddError("缺少右方括号");

            //return list;
        }
        private ExpressionNode ParseBaseSpecies()//用来解析plots里单独的一个物种,不吃掉分隔符
        {
            ExpressionNode strand;
            if (StrandStart())
            {

                strand = ParseSpecies();
                //Next();
                if (MatchPlusOrMinus())//解决物种相加减的情况
                {
                    BinaryExpression binaryExpression = new BinaryExpression();
                    binaryExpression.Left = strand;
                    binaryExpression.Operator = (Operator)Current.Value;
                    Next();
                    binaryExpression.Right = ParseBaseSpecies();
                    ExpressionPrinter.PrintExpression(binaryExpression);
                    return binaryExpression;
                }
                return strand;
            }
            else if (Match(TokenType.Name))//没有单纯的名字，带名字只可能是函数
            {
                FuncNode func;
                NameNode name = new NameNode();//假设物种叫
                name.Name = (string)Current.Value;
                Next();
                if (MatchParentOpen())//这里有两种可能，一种是函数(物种)，另一种就是sum([])里面带通配符的，如果不是这种的
                {
                    if (!MatchParentClose())//说明里面有东西,sum情况
                    {
                        ExpressionNode ep;
                        if (MatchLeftBracket())
                        {
                            ep = ParseSpecies();
                            if (!MatchRightBracket())
                            {
                                AddError("缺少右方括号");
                                return null;
                            }
                            MatchParentClose();
                        }
                        else
                        {
                            AddError("并非合法的物种");
                            return null;
                        }
                        if(name.Name.Equals("sum"))//里面的将会是一个带通配符的物种
                        {
                            FuncNode func1 = new FuncNode();
                            func1.Name = name.Name;
                            func1.Expressions = ep;
                            Console.WriteLine("\nsum!\n");
                            return func1;
                        }
                        List<ExpressionNode> list = new List<ExpressionNode>();
                        list.Add(ep);
                        //Console.WriteLine($"当前Token:{Current.ToString()}\nep is {ep.GetType()}");

                        func = new FuncNode(list);
                        func.Name = name.Name;

                        return func;
                    }
                    func = new FuncNode();
                    func.Name = name.Name;
                    if (MatchMathOperator())
                    {
                        BinaryExpression binaryExpression = new BinaryExpression();
                        binaryExpression.Left = func;
                        binaryExpression.Operator = (Operator)Current.Value;
                        Next();
                        binaryExpression.Right = ParseBaseSpecies();
                        ExpressionPrinter.PrintExpression(binaryExpression);
                        return binaryExpression;
                    }
                    return func;
                }
                else
                {

                    AddError("并非函数");
                    return null;
                }
            }
            else if (Match(TokenType.Integer) || Match(TokenType.Float))
            {
                BinaryExpression binaryExpression = new BinaryExpression();

                binaryExpression.Left = GetFromCurrent();
                Next();
                if (!MatchMathOperator())
                {
                    AddError("你数学运算符呢？");
                    return null;
                }
                binaryExpression.Operator = (Operator)Current.Value;
                Next();
                binaryExpression.Right = ParseBaseSpecies();
                ExpressionPrinter.PrintExpression(binaryExpression);

                return binaryExpression;
            }
            return null;
        }
        private bool CheckSpeciesSymbol()//{},[],<>,::,: _ * ^
                                         => (MatchOperator(Operator.TwoColon) || MatchOperator(Operator.Colon) || MatchOperator(Operator.LeftBracket) || MatchOperator(Operator.LeftBrace) || MatchOperator(Operator.LeftStrand) || MatchOperator(Operator.RightBrace) || MatchOperator(Operator.RightBracket) || MatchOperator(Operator.RightStrand) || MatchUnderline() || MatchOperator(Operator.Toehold) || MatchOperator(Operator.Star));

        private bool MatchLinker() => (MatchOperator(Operator.Colon) || MatchOperator(Operator.TwoColon));
        //private bool SpeciesEnd()=>(MatchOperator(Operator.ParentOpen) || MatchOperator(Operator.Comma) || MatchOperator(Operator.Semicolon) || MatchOperator(Operator.ParentClose) || Match(TokenType.EOF)||Match(TokenType.Keyword));
        private bool StrandStart() => (MatchOperator(Operator.LeftStrand) || MatchOperator(Operator.LeftBracket) || MatchOperator(Operator.LeftBrace));//由于成功后后面的解析还用到，所以这里不吃了(MatchLeftBrace() || MatchLeftBracket() || MatchLeftStrand());
        private bool InSeq() => (Match(TokenType.Name) || MatchOperator(Operator.Star) || MatchOperator(Operator.Underline) || MatchOperator(Operator.Toehold));
        private ExpressionNode ParseSpecies()//或许此时只需要把链和符号存上就行了，后面的语义分析再进行解析
        {
            //Console.WriteLine("解析物种...\n");
            //Console.WriteLine($"当前Token:{Current.ToString()}\n");
            List<StrandNode> strands = new List<StrandNode>();
            ComplexNode complex = new ComplexNode();
            complex.Column = Current.Column;
            complex.Line = Current.Line;
            while (StrandStart() || MatchLinker())//每次只有两种可能，链的开始或者是连接符 这里会不会受到影响？
            {
                //Console.WriteLine($"当前Token:{Current.ToString()}\n");
                if ((MatchOperator(Operator.Colon) || MatchOperator(Operator.TwoColon)))//如果已经存了一个，然后此时是连接符
                {
                    LinkerNode lk = new LinkerNode();
                    if (MatchOperator(Operator.Colon))
                        lk.Type = LinkerNode.LinkerType.lower;
                    else if (MatchOperator(Operator.TwoColon))
                        lk.Type = LinkerNode.LinkerType.upper;
                    complex.Values.Add(lk);
                    Next();
                }
                else if (StrandStart())
                {
                    StrandNode strand = ParseStrand();
                    strands.Add(strand);
                    complex.Values.Add(strand);
                }
            }
            if (strands.Count == 1)//如果只有一个链，那么就是一个链
                return strands[0];
            return complex;

        }
        private StrandNode ParseStrand()//遇到<{[调用的,会吃掉后面的符号，但不会吃掉分隔符,;
        {
            StrandNode strand = new StrandNode();
            strand.Column = Current.Column;
            strand.Line = Current.Line;
            if (MatchLeftBrace())//{
                strand.Type = StrandNode.StrandType.lower;
            else if (MatchLeftStrand())//<
                strand.Type = StrandNode.StrandType.upper;
            else if (MatchLeftBracket())//[
                strand.Type = StrandNode.StrandType.duplex;
            else//说明不是链
            {
                AddError("并非合法的链段");
                return null;
            }
            strand.seq = ParseSeq();
            //Console.WriteLine($"链段解析完成！当前Token:{Current.ToString()}\n");
            if ((MatchRightBrace() && strand.Type == StrandNode.StrandType.lower) || (MatchRightBracket() && strand.Type == StrandNode.StrandType.duplex) || (MatchRightStrand() && strand.Type == StrandNode.StrandType.upper))//}
            {
                return strand;
            }
            AddError("并非合法的链段");
            return null;
        }
        private SeqNode ParseSeq()
        {
            SeqNode seq = new SeqNode();
            List<SeqNode> nodes = new List<SeqNode>();//符合定义里的seq列表

            while (InSeq())//此时是Name，* _ ^
            {
                DomNode dom = new DomNode();
                if (Match(TokenType.Name))//可能是 a* ,a^
                {
                    dom.Name = GetFromCurrent() as NameNode;//这里可能会为null
                    Next();
                    if (MatchOperator(Operator.Star))
                    {
                        dom.Type = DomNode.DomType.Rev;
                        Next();
                    }
                    else if (MatchOperator(Operator.Toehold))
                    {
                        dom.Type = DomNode.DomType.ToeHold;
                        Next();
                        if (MatchOperator(Operator.Star))
                        {
                            dom.Type = DomNode.DomType.ToeHoldRev;
                            Next();
                        }
                    }

                }
                else if (MatchOperator(Operator.Underline))
                {
                    dom.Name = new NameNode("_");
                    dom.Type = DomNode.DomType.WildCard;
                    Next();
                }
                else
                {
                    AddError("并非合法的序列");
                    return null;
                }
                SeqNode seqtmp = new SeqNode();
                seqtmp.Value = dom;
                nodes.Add(seqtmp);
                ExpressionPrinter.PrintExpression(dom);
                ExpressionPrinter.PrintExpression(seqtmp);
            }
            seq.Value = nodes;
            return seq;
        }
        private ValueNode GetFromCurrent()//从当前Token得到一个新的ValueNode,除了函数以外不next
        {
            //Console.WriteLine($"获得值：当前Token：{Current.ToString()}\n");
            switch (Current.Type)
            {
                case TokenType.String:
                    var stringNode = new StringNode();
                    stringNode.Value = (string)Current.Value;
                    stringNode.Line = Current.Line;
                    stringNode.Column = Current.Column;
                    return stringNode;
                case TokenType.Name:
                    var nameNode = new NameNode();
                    nameNode.Column = Current.Column;
                    nameNode.Line = Current.Line;
                    nameNode.Name = (string)Current.Value;
                    Next();
                    if (MatchParentOpen())
                    {
                        ListExpression lp = ParseParenthese();//会吃掉右括号
                        FuncNode func = new FuncNode(lp.Values);
                        func.Name = nameNode.Name;
                        func.Line = nameNode.Line;
                        func.Column = nameNode.Column;
                        return func;
                    }
                    else
                        Back();
                    return nameNode;
                case TokenType.Integer:
                    var integerNode = new IntegerNode();
                    integerNode.Line = Current.Line;
                    integerNode.Column = Current.Column;
                    integerNode.Value = (int)Current.Value;
                    //Console.WriteLine($"下一个Token是{_Next().ToString()} 判断结果");
                    if (_Next().Type == TokenType.Units)
                    {
                        UnitValueNode uv = new UnitValueNode();
                        uv.Value = Convert.ToDouble(Current.Value);
                        Next();
                        uv.units = (Units)Current.Value;
                        uv.Column = Current.Column;
                        uv.Line = Current.Line;
                        return uv;
                    }
                    return integerNode;
                case TokenType.Float:
                    var floatNode = new FloatNode();
                    floatNode.Value = (double)Current.Value;
                    floatNode.Line = Current.Line;
                    floatNode.Column = Current.Column;
                    if (_Next().Type == TokenType.Units)
                    {
                        UnitValueNode uv = new UnitValueNode();
                        uv.Value = (double)Current.Value;
                        Next();
                        uv.units = (Units)Current.Value;
                        uv.Column = Current.Column;
                        uv.Line = Current.Line;
                        return uv;
                    }
                    return floatNode;
                case TokenType.Keyword:
                    var keywordNode = new KeywordNode();
                    keywordNode.Value = (Keyword)Current.Value;
                    keywordNode.Line = Current.Line;
                    keywordNode.Column = Current.Column;
                    return keywordNode;
                case TokenType.Operator://待解决 (括号内带算术表达式)，或者是一个算数表达式
                    if(MeetParentOpen())
                    {
                        //return ParseParenthese();
                    }
                    return null;
            }
            AddError("并非合法的Current值");
            return null;
        }
    }
}
