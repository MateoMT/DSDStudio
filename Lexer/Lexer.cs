using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace DSDCore
{
    public class Lexer
    {
        private int _line = 1;
        private int _column = 1;
        private string _source;
        private int _position = 0;
        public Lexer(string source)
        {
            _source = source;
            //Console.WriteLine(_source);
        }
        public List<Token> GetTokenList()
        {
            List<Token> tokens = new List<Token>();
            Token token;
            do
            {
                token = this.NextToken();
                tokens.Add(token);
                //Console.WriteLine($"{token.Type,-15} | {token.Value}");
            } while (token.Type != TokenType.EOF);
#if DEBUG
            Console.WriteLine($"解析完成，共有{tokens.Count}个Token");
            foreach (var t in tokens)
            {
                Console.WriteLine($"{t.Type,-15} | {t.Value}");
            }
#endif
            return tokens;
        }
        private char Current => _position >= _source.Length ? '\0' : _source[_position];
        private Dictionary<string, Keyword> _keywords = new Dictionary<string, Keyword>
    {
        { "directive", Keyword.directive },
        { "def", Keyword.def },
        { "dom", Keyword.dom },
        { "simulation", Keyword.simulation },
        { "final", Keyword.final },
        { "plots", Keyword.plots },
        { "parameters", Keyword.parameters },
        { "compilation", Keyword.compilation },
        { "infinite", Keyword.infinite },
        //{ "colour", Keyword.colour },
        //{ "bind", Keyword.bind },
        //{ "unbind", Keyword.unbind },
        { "stochastic", Keyword.stochastic },
        { "deterministic", Keyword.deterministic },
        { "scale", Keyword.scale },
        { "trajectories", Keyword.trajectories },
        { "rendering", Keyword.rendering },
        { "mode", Keyword.mode },
        { "nucleotides", Keyword.nucleotides },
        { "new", Keyword.new_ },
        { "true", Keyword.true_ },
        { "false", Keyword.false_ },
        { "adjacent", Keyword.adjacent },
        { "not", Keyword.not },
        //{ "seq", Keyword.seq },
        { "subdomains", Keyword.subdomains },
        { "sweeps", Keyword.sweeps },
        { "rates", Keyword.rates },
        { "plot_settings", Keyword.plot_settings },
        { "x_label", Keyword.x_label },
        { "y_label", Keyword.y_label },
        { "title", Keyword.title },
        { "label_font_size", Keyword.label_font_size },
        { "tick_font_size", Keyword.tick_font_size },
        { "x_tick", Keyword.x_tick },
        { "y_tick", Keyword.y_tick },
        { "initial", Keyword.initial },
        { "points", Keyword.points },
        { "prune", Keyword.prune },
        { "multicore", Keyword.multicore },
        { "sundials", Keyword.sundials },
        { "seed", Keyword.seed },
        { "step", Keyword.step },
            {"simulator",Keyword.simulator },
        { "lna", Keyword.lna },
        { "cme", Keyword.cme },
        { "pde", Keyword.pde },
        { "stiff", Keyword.stiff },
        { "spatial", Keyword.spatial },
        { "diffusibles", Keyword.diffusibles },
        { "default_diffusion", Keyword.default_diffusion },
        { "dimensions", Keyword.dimensions },
        { "random", Keyword.random },
        { "nx", Keyword.nx },
        { "dt", Keyword.dt },
        { "xmax", Keyword.xmax },
        { "boundary", Keyword.boundary },
        { "moments", Keyword.moments },
        { "order", Keyword.order },
        { "species", Keyword.species },
        { "default_variance", Keyword.default_variance },
        { "initial_mean", Keyword.initial_mean },
        { "initial_variance", Keyword.initial_variance },
        { "inference", Keyword.inference },
        { "name", Keyword.name },
        { "burnin", Keyword.burnin },
        { "samples", Keyword.samples },
        { "thin", Keyword.thin },
        { "noise_model", Keyword.noise_model },
        { "timer", Keyword.timer },
        { "partial", Keyword.partial },
        { "data", Keyword.data },
        { "units", Keyword.units },
        { "time", Keyword.time },
        { "space", Keyword.space },
        { "conceration", Keyword.conceration },
        { "default", Keyword.default_ },
        { "finite", Keyword.finite },
        { "unproductive",Keyword.unproductive },
        { "jit", Keyword.jit },
        { "leaks", Keyword.leaks },
        { "declare", Keyword.declare },
        { "polymers", Keyword.polymers },
        { "leak", Keyword.leak },
        { "tau", Keyword.tau },
        { "migrate", Keyword.migrate },
        { "lengths", Keyword.lengths },
        { "tolerance", Keyword.tolerance },
        { "toeholds", Keyword.toeholds },
        { "tether", Keyword.tether },
            {"locations",Keyword.locations },

    };
        private Dictionary<string, Operator> _operators = new Dictionary<string, Operator>
    {
        { "^", Operator.Toehold },
        { ",", Operator.Comma },
        { ":", Operator.Colon },
        { "::", Operator.TwoColon },
        { ";", Operator.Semicolon },
        { "*", Operator.Star },
        { "/", Operator.Slash },
        { "+", Operator.Plus },
        { "-", Operator.Minus },
        { "=", Operator.Equal },
        { "<", Operator.LeftStrand },
        { ">", Operator.RightStrand },
        { "[", Operator.LeftBracket },
        { "]", Operator.RightBracket },
        { "(", Operator.ParentOpen },
        { ")", Operator.ParentClose },
        { "{", Operator.LeftBrace },
        { "}", Operator.RightBrace },
        { "|", Operator.Bar },
        { "@", Operator.At },
        { "%", Operator.Mod },
            {"_",Operator.Underline }

    };
        private Dictionary<string, Units> _units = new Dictionary<string, Units>
{
    {"h", Units.h},
    {"min", Units.min},
    {"s", Units.s},
    {"ms", Units.ms},
    {"us", Units.us},
    {"ns", Units.ns},
    {"m", Units.m},
    {"mm", Units.mm},
    {"um", Units.um},
    {"nm", Units.nm},
    {"pm", Units.pm},
    {"fm", Units.fm},
    {"M", Units.M},
    {"mM", Units.mM},
    {"uM", Units.uM},
    {"nM", Units.nM},
    {"pM", Units.pM},
    {"fM", Units.fM},
    {"aM", Units.aM},
    {"zM", Units.zM},
    {"yM", Units.yM}
};
        public Token NextToken()
        {
            if (Current == '\0')
            {
                return new Token(TokenType.EOF, "EOF", _line, _column);
            }
            SkipSpace();
            SkipComment();
            if (Current == '\0')
            {
                return new Token(TokenType.EOF, "EOF", _line, _column);
            }
            if (Current == '\'' && _position + 2 < _source.Length && _source[_position + 2] == '\'')
            {
                return ReadChar();
            }
            if (Current == '\"')
            {
                return ReadString();
            }
            if (_operators.ContainsKey(Current.ToString()))
            {
                return ReadOperator();
            }
            if (char.IsDigit(Current))
            {
                return ReadNumber();
            }
            if (char.IsLetter(Current) || Current == '_')
            {
                return ReadNameAndKey();
            }
            Console.WriteLine($"并非考虑到的字符: {Current} 当前位置:{_line}行 {_column}列");
            //throw new Exception($"并非考虑到的字符: {Current} 当前位置:{_line}行 {_column}列");
            AddError($"并非考虑到的字符: {Current}");
            return new Token(TokenType.EOF, "EOF", _line, _column);

        }
        public List<string> Errors { get; } = new List<string>();
        private void AddError(string message)
        {
            Errors.Add($"Line {_line}, Column {_column}: {message}");
        }
        private void SkipSpace()
        {
            while (char.IsWhiteSpace(Current))
            {
                if (Current == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else if (Current == '\t')
                {
                    _column += 4;
                }
                else if (Current == '\r')
                {
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }
        private void SkipComment()
        {
            if (Current == '/' && _source[_position + 1] == '/')
            {
                _position += 2;
                while (Current != '\n' && Current != '\0')
                {
                    _position++;
                }
                //_line++;
                _column = 1;
                SkipSpace();
                return;
            }
            if (Current == '(' && _source[_position + 1] == '*')
            {
                _position += 2;
                int comtLevel = 1;
                while (comtLevel > 0 && _position + 1 < _source.Length)
                {
                    if (Current == '\n')
                    {
                        _line++;
                        _column = 1;
                        _position++;
                    }
                    else if (Current == '(' && _source[_position + 1] == '*')
                    {
                        comtLevel++;
                        _position += 2;
                    }
                    else if (Current == '*' && _source[_position + 1] == ')')
                    {
                        comtLevel--;
                        _position += 2;
                    }
                    else
                    {
                        _position++;
                    }

                }
                if (comtLevel > 0)
                {
                    Console.WriteLine($"并非考虑到的字符: {Current} 当前位置:{_line}行 {_column}列");
                    AddError("并非合法的注释");
                    //throw new Exception("并非合法的注释");
                }
                SkipSpace();
            }

        }
        private Token ReadOperator()
        {
            if (Current == ':')
            {
                _position++;
                if (Current == ':')
                {
                    _column++;
                    _position++;
                    return new Token(TokenType.Operator, Operator.TwoColon, _line, _column++);
                }
                return new Token(TokenType.Operator, Operator.Colon, _line, _column++);
            }
            string opertor = Current.ToString();
            _position++;
            return new Token(TokenType.Operator, _operators[opertor], _line, _column++);
        }
        private Token ReadNumber()
        {
            int start = _position;
            bool isFloat = false;
            bool isScientific = false;
            int column = _column;
            while (char.IsDigit(Current))
            {
                _position++;
                _column++;
            }
            if (Current == '.')
            {
                isFloat = true;
                _position++;
                while (char.IsDigit(Current))
                {
                    _position++;
                }
            }
            if (Current == 'e' || Current == 'E')
            {
                isScientific = true;
                _position++;
                if (Current == '+' || Current == '-')
                {
                    _position++;
                }
                if (!char.IsDigit(Current))
                {
                    Console.WriteLine($"并非考虑到的字符: {Current} 当前位置:{_line}行 {_column}列");
                    //throw new Exception("并非科学计数法");
                    AddError("并非科学计数法");
                }
                while (char.IsDigit(Current))
                {
                    _position++;
                }
            }
            if (isScientific || isFloat)
            {
                return new Token(TokenType.Float, double.Parse(_source.Substring(start, _position - start)), _line, column);
            }
            else
            {
                return new Token(TokenType.Integer, int.Parse(_source.Substring(start, _position - start)), _line, column);
            }
        }
        private Token ReadNameAndKey()
        {

            int start = _position;
            int column = _column;
            while (char.IsLetterOrDigit(Current) || Current == '_' || Current == '\'')
            {
                _position++;
                _column++;
            }
            string name = _source.Substring(start, _position - start);
            if (_keywords.ContainsKey(name))
            {
                return new Token(TokenType.Keyword, _keywords[name], _line, column);
            }
            if (_units.ContainsKey(name))
            {
                return new Token(TokenType.Units, _units[name], _line, column);
            }
            string pattern = @"^[a-zA-Z_][a-zA-Z0-9_'""]*$";
            if (!Regex.IsMatch(name, pattern))
            {
                Console.WriteLine($"name == {name}");
                Console.WriteLine($"并非考虑到的字符串: {Current} 当前位置:{_line}行 {column}列");
                //throw new Exception("并非合法的名字");
                AddError("并非合法的名字");
            }
            return new Token(TokenType.Name, name, _line, column);
        }
        private Token ReadChar()
        {
            _position++;
            _column++;
            int column = _column;
            char current = Current;
            _position+=2;
            _column+=2;
            return new Token(TokenType.Char, current, _line, column);
        }
        private Token ReadString()
        {
            int start = _position;
            char quote = Current;
            _position++;
            while (Current != quote)
            {
                if (Current == '\0')
                {
                    Console.WriteLine($"并非考虑到的字符: {Current} 当前位置:{_line}行 {_column}列");
                    //throw new Exception("并非字符串！");
                    AddError("并非字符串！");
                }
                _position++;
            }
            _position++;
            return new Token(TokenType.String, _source.Substring(start + 1, _position - start - 2), _line, _column);
        }
        public void TestCode(string code)
        {
            var lexer = new Lexer(code);
            Token token;
            do
            {
                token = lexer.NextToken();
                Console.WriteLine($"{token.ToString()}");
            } while (token.Type != TokenType.EOF);
        }
        public static void testAll()
        {
            string directoryPath = "DSDModels";
            var txtFiles = Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories);

            foreach (var file in txtFiles)
            {
                Console.WriteLine($"正在处理文件: {file}");
                string code = File.ReadAllText(file);
                var lexer = new Lexer(code);
                Token token;
                do
                {
                    token = lexer.NextToken();
                    Console.WriteLine($"{token.Type,-15} | {token.Value}");
                } while (token.Type != TokenType.EOF);
            }
        }

    }
}
