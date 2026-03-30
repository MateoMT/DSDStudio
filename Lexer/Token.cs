using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSDCore
{
    public class Token
    {
        public TokenType Type { get; private set; }
        public object Value { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public Token()
        {
            Type = TokenType.EOF;
            Value = "EOF";
            Line = 0;
            Column = 0;
        }

        public Token(TokenType type, char value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, int value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, double value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, Operator value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, Keyword value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public Token(TokenType type, Units value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
        public override string ToString()
        {
            return $"Token({Type}, {Value}, line:{Line},column: {Column})";
        }
    }
}
