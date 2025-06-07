using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser
{
    /// <summary>
    /// 词法分析器
    /// 将DSL源代码转换为词法单元序列
    /// </summary>
    public class Lexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;
        private readonly List<LexerError> _errors;

        /// <summary>
        /// 当前字符
        /// </summary>
        private char Current => _position >= _source.Length ? '\0' : _source[_position];

        /// <summary>
        /// 下一个字符
        /// </summary>
        private char Peek => _position + 1 >= _source.Length ? '\0' : _source[_position + 1];

        /// <summary>
        /// 是否到达文件末尾
        /// </summary>
        private bool IsAtEnd => _position >= _source.Length;

        /// <summary>
        /// 词法分析错误列表
        /// </summary>
        public IReadOnlyList<LexerError> Errors => _errors;

        public Lexer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _position = 0;
            _line = 1;
            _column = 1;
            _errors = new List<LexerError>();
        }

        /// <summary>
        /// 词法分析，返回所有词法单元
        /// </summary>
        /// <returns>词法单元列表</returns>
        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            
            while (!IsAtEnd)
            {
                var token = NextToken();
                if (token != null)
                {
                    tokens.Add(token);
                }
            }

            // 添加EOF标记
            tokens.Add(new Token(TokenType.EOF, string.Empty, GetCurrentPosition()));
            
            return tokens;
        }

        /// <summary>
        /// 获取下一个词法单元
        /// </summary>
        /// <returns>词法单元，如果跳过则返回null</returns>
        public Token? NextToken()
        {
            SkipWhitespace();

            if (IsAtEnd)
            {
                return null;
            }

            var startPosition = GetCurrentPosition();
            var c = Current;

            // 处理换行符
            if (c == '\n')
            {
                Advance();
                return new Token(TokenType.Newline, "\n", startPosition);
            }

            // 处理注释
            if (c == '/' && Peek == '/')
            {
                return ReadLineComment(startPosition);
            }

            if (c == '/' && Peek == '*')
            {
                return ReadBlockComment(startPosition);
            }

            // 处理字符串字面量
            if (c == '"' || c == '\'')
            {
                return ReadString(startPosition, c);
            }

            // 处理数字字面量
            if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek)))
            {
                return ReadNumber(startPosition);
            }

            // 处理标识符和关键字
            if (char.IsLetter(c) || c == '_')
            {
                return ReadIdentifier(startPosition);
            }

            // 处理操作符
            if (Operators.IsOperatorStart(c))
            {
                return ReadOperator(startPosition);
            }

            // 处理分隔符
            var delimiter = ReadDelimiter(startPosition);
            if (delimiter != null)
            {
                return delimiter;
            }

            // 未知字符
            AddError($"Unexpected character '{c}'", startPosition);
            Advance();
            return new Token(TokenType.Invalid, c.ToString(), startPosition);
        }

        /// <summary>
        /// 跳过空白字符
        /// </summary>
        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current) && Current != '\n')
            {
                Advance();
            }
        }

        /// <summary>
        /// 读取行注释
        /// </summary>
        private Token ReadLineComment(TokenPosition startPosition)
        {
            var value = new StringBuilder();
            
            // 跳过 //
            Advance();
            Advance();

            while (!IsAtEnd && Current != '\n')
            {
                value.Append(Current);
                Advance();
            }

            return new Token(TokenType.Comment, value.ToString(), startPosition);
        }

        /// <summary>
        /// 读取块注释
        /// </summary>
        private Token ReadBlockComment(TokenPosition startPosition)
        {
            var value = new StringBuilder();
            
            // 跳过 /*
            Advance();
            Advance();

            while (!IsAtEnd)
            {
                if (Current == '*' && Peek == '/')
                {
                    // 跳过 */
                    Advance();
                    Advance();
                    break;
                }

                value.Append(Current);
                Advance();
            }

            return new Token(TokenType.Comment, value.ToString(), startPosition);
        }

        /// <summary>
        /// 读取字符串字面量
        /// </summary>
        private Token ReadString(TokenPosition startPosition, char quote)
        {
            var value = new StringBuilder();
            
            // 跳过开始引号
            Advance();

            while (!IsAtEnd && Current != quote)
            {
                if (Current == '\\')
                {
                    // 处理转义字符
                    Advance();
                    if (!IsAtEnd)
                    {
                        var escaped = Current switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\\' => '\\',
                            '\'' => '\'',
                            '"' => '"',
                            _ => Current
                        };
                        value.Append(escaped);
                        Advance();
                    }
                }
                else
                {
                    value.Append(Current);
                    Advance();
                }
            }

            if (IsAtEnd)
            {
                AddError("Unterminated string literal", startPosition);
                return new Token(TokenType.Invalid, value.ToString(), startPosition);
            }

            // 跳过结束引号
            Advance();

            var token = new Token(TokenType.String, value.ToString(), startPosition)
            {
                DataType = typeof(string),
                ParsedValue = value.ToString()
            };

            return token;
        }

        /// <summary>
        /// 读取数字字面量
        /// </summary>
        private Token ReadNumber(TokenPosition startPosition)
        {
            var value = new StringBuilder();
            bool hasDecimalPoint = false;

            while (!IsAtEnd && (char.IsDigit(Current) || Current == '.'))
            {
                if (Current == '.')
                {
                    if (hasDecimalPoint)
                    {
                        break; // 第二个小数点，停止
                    }
                    hasDecimalPoint = true;
                }

                value.Append(Current);
                Advance();
            }

            var numberString = value.ToString();
            var token = new Token(TokenType.Number, numberString, startPosition);

            // 解析数值
            if (hasDecimalPoint)
            {
                if (double.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    token.DataType = typeof(double);
                    token.ParsedValue = doubleValue;
                }
                else
                {
                    AddError($"Invalid number format: {numberString}", startPosition);
                    token.Type = TokenType.Invalid;
                }
            }
            else
            {
                if (int.TryParse(numberString, out var intValue))
                {
                    token.DataType = typeof(int);
                    token.ParsedValue = intValue;
                }
                else if (long.TryParse(numberString, out var longValue))
                {
                    token.DataType = typeof(long);
                    token.ParsedValue = longValue;
                }
                else
                {
                    AddError($"Invalid number format: {numberString}", startPosition);
                    token.Type = TokenType.Invalid;
                }
            }

            return token;
        }

        /// <summary>
        /// 读取标识符或关键字
        /// </summary>
        private Token ReadIdentifier(TokenPosition startPosition)
        {
            var value = new StringBuilder();

            while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
            {
                value.Append(Current);
                Advance();
            }

            var identifier = value.ToString();
            
            // 检查是否为关键字
            var keywordType = Keywords.GetKeywordType(identifier);
            if (keywordType.HasValue)
            {
                var token = new Token(keywordType.Value, identifier, startPosition);
                
                // 处理布尔值
                if (keywordType == TokenType.Boolean)
                {
                    token.DataType = typeof(bool);
                    token.ParsedValue = bool.Parse(identifier);
                }
                else if (keywordType == TokenType.Null)
                {
                    token.DataType = typeof(object);
                    token.ParsedValue = null;
                }

                return token;
            }

            return new Token(TokenType.Identifier, identifier, startPosition);
        }

        /// <summary>
        /// 读取操作符
        /// </summary>
        private Token ReadOperator(TokenPosition startPosition)
        {
            var value = new StringBuilder();
            value.Append(Current);
            Advance();

            // 尝试读取多字符操作符
            if (!IsAtEnd)
            {
                var twoCharOp = value.ToString() + Current;
                if (Operators.GetOperatorType(twoCharOp).HasValue)
                {
                    value.Append(Current);
                    Advance();
                }
            }

            var operatorString = value.ToString();
            var operatorType = Operators.GetOperatorType(operatorString);

            if (operatorType.HasValue)
            {
                return new Token(operatorType.Value, operatorString, startPosition);
            }

            AddError($"Unknown operator: {operatorString}", startPosition);
            return new Token(TokenType.Invalid, operatorString, startPosition);
        }

        /// <summary>
        /// 读取分隔符
        /// </summary>
        private Token? ReadDelimiter(TokenPosition startPosition)
        {
            var tokenType = Current switch
            {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                '[' => TokenType.LeftBracket,
                ']' => TokenType.RightBracket,
                '{' => TokenType.LeftBrace,
                '}' => TokenType.RightBrace,
                ',' => TokenType.Comma,
                ';' => TokenType.Semicolon,
                ':' => TokenType.Colon,
                '.' => TokenType.Dot,
                _ => (TokenType?)null
            };

            if (tokenType.HasValue)
            {
                var value = Current.ToString();
                Advance();
                return new Token(tokenType.Value, value, startPosition);
            }

            return null;
        }

        /// <summary>
        /// 前进一个字符
        /// </summary>
        private void Advance()
        {
            if (!IsAtEnd)
            {
                if (Current == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }

        /// <summary>
        /// 获取当前位置信息
        /// </summary>
        private TokenPosition GetCurrentPosition()
        {
            return new TokenPosition(_line, _column, _position, 1);
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        private void AddError(string message, TokenPosition position)
        {
            _errors.Add(new LexerError(message, position));
        }
    }

    /// <summary>
    /// 词法分析错误
    /// </summary>
    public class LexerError
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 错误位置
        /// </summary>
        public TokenPosition Position { get; }

        public LexerError(string message, TokenPosition position)
        {
            Message = message;
            Position = position;
        }

        public override string ToString()
        {
            return $"Lexer Error at {Position}: {Message}";
        }
    }
}
