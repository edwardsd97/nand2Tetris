using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace VM
{
    public class Token
    {
        public enum Type
        {
            NONE,
            KEYWORD, SYMBOL, IDENTIFIER, INT_CONST, STRING_CONST,
            EOF
        };

        public enum Keyword
        {
            NONE,
            CLASS, METHOD, FUNCTION, CONSTRUCTOR,
            INT, BOOL, CHAR, VOID,
            VAR, STATIC, FIELD, LET,
            DO, IF, ELSE, WHILE, FOR, CONTINUE, BREAK,
            SWITCH, CASE, DEFAULT,
            RETURN, TRUE, FALSE, NULL,
            THIS
        };

        // Static data and members //
        private static bool mInitialized = false;
        private static Dictionary<string, Keyword> strToKeyword;
        private static Dictionary<Keyword, string> keywordToStr;
        private static Dictionary<Type, string> typeToStr;
        private static Dictionary<char, string> symbols;
        private static Dictionary<char, int> ops;

        protected static void InitIfNeeded()
        {
            if (Token.mInitialized)
                return;

            typeToStr = new Dictionary<Type, string>();
            typeToStr.Add(Token.Type.KEYWORD, "keyword");
            typeToStr.Add(Token.Type.SYMBOL, "symbol");
            typeToStr.Add(Token.Type.IDENTIFIER, "identifier");
            typeToStr.Add(Token.Type.INT_CONST, "integerConstant");
            typeToStr.Add(Token.Type.STRING_CONST, "stringConstant");

            strToKeyword = new Dictionary<string, Keyword>();
            strToKeyword.Add("class", Token.Keyword.CLASS);
            strToKeyword.Add("method", Token.Keyword.METHOD);
            strToKeyword.Add("function", Token.Keyword.FUNCTION);
            strToKeyword.Add("constructor", Token.Keyword.CONSTRUCTOR);
            strToKeyword.Add("int", Token.Keyword.INT);
            strToKeyword.Add("boolean", Token.Keyword.BOOL);
            strToKeyword.Add("char", Token.Keyword.CHAR);
            strToKeyword.Add("void", Token.Keyword.VOID);
            strToKeyword.Add("var", Token.Keyword.VAR);
            strToKeyword.Add("static", Token.Keyword.STATIC);
            strToKeyword.Add("field", Token.Keyword.FIELD);
            strToKeyword.Add("let", Token.Keyword.LET);
            strToKeyword.Add("do", Token.Keyword.DO);
            strToKeyword.Add("if", Token.Keyword.IF);
            strToKeyword.Add("else", Token.Keyword.ELSE);
            strToKeyword.Add("while", Token.Keyword.WHILE);
            strToKeyword.Add("for", Token.Keyword.FOR);
            strToKeyword.Add("continue", Token.Keyword.CONTINUE);
            strToKeyword.Add("break", Token.Keyword.BREAK);
            strToKeyword.Add("switch", Token.Keyword.SWITCH);
            strToKeyword.Add("case", Token.Keyword.CASE);
            strToKeyword.Add("default", Token.Keyword.DEFAULT);
            strToKeyword.Add("return", Token.Keyword.RETURN);
            strToKeyword.Add("true", Token.Keyword.TRUE);

            strToKeyword.Add("false", Token.Keyword.FALSE);
            strToKeyword.Add("null", Token.Keyword.NULL);
            strToKeyword.Add("this", Token.Keyword.THIS);

            keywordToStr = new Dictionary<Keyword, string>();
            foreach (string key in strToKeyword.Keys)
            {
                keywordToStr.Add(strToKeyword[key], key);
            }

            symbols = new Dictionary<char, string>();
            symbols.Add('{', "{"); symbols.Add('}', "}");
            symbols.Add('[', "["); symbols.Add(']', "]");
            symbols.Add('(', "("); symbols.Add(')', ")");
            symbols.Add('.', "."); symbols.Add(',', ","); symbols.Add(';', ";"); symbols.Add(':', ":");
            symbols.Add('+', "+"); symbols.Add('-', "-");
            symbols.Add('*', "*"); symbols.Add('/', "/");
            symbols.Add('&', "&amp;"); symbols.Add('|', "|");
            symbols.Add('=', "="); symbols.Add('~', "~"); symbols.Add('!', "!");
            symbols.Add('<', "&lt;"); symbols.Add('>', "&gt;");
            symbols.Add('%', "%"); symbols.Add('^', "^");

            // op: '~' | '!'  >  '*' | '/' | '%'  >  '+' | '-'  >  '<' | '>'  >  '='  >  '&'  >  '|'
            // ( int values are C++ operator precedence https://en.cppreference.com/w/cpp/language/operator_precedence )
            ops = new Dictionary<char, int>();
            ops.Add('~', 3); ops.Add('!', 3);
            ops.Add('*', 5); ops.Add('/', 5); ops.Add('%', 5);
            ops.Add('+', 6); ops.Add('-', 6);
            ops.Add('<', 9); ops.Add('>', 9);
            ops.Add('=', 10); // ==
            ops.Add('&', 11);
            ops.Add('^', 12);
            ops.Add('|', 13);

            Token.mInitialized = true;
        }

        public static Keyword GetKeyword(string str)
        {
            InitIfNeeded();
            Keyword keyword;
            if (strToKeyword.TryGetValue(str, out keyword))
            {
                return keyword;
            }

            return Token.Keyword.NONE;
        }

        public static string KeywordString(Keyword keyword)
        {
            InitIfNeeded();
            string keywordStr;
            if (keywordToStr.TryGetValue(keyword, out keywordStr))
            {
                return keywordStr;
            }

            return "";
        }

        public static string TypeString(Type type)
        {
            InitIfNeeded();
            string typeStr;
            if (typeToStr.TryGetValue(type, out typeStr))
            {
                return typeStr;
            }

            return "";
        }

        public static bool IsSymbol(char c)
        {
            InitIfNeeded();
            string symbolStr;
            if (symbols.TryGetValue(c, out symbolStr))
            {
                return true;
            }

            return false;
        }

        public static bool IsOp(char c)
        {
            InitIfNeeded();
            int precedence;
            if (ops.TryGetValue(c, out precedence))
            {
                return true;
            }

            return false;
        }

        public static int OpPrecedence(char c)
        {
            InitIfNeeded();
            int precedence;
            if (ops.TryGetValue(c, out precedence))
            {
                return precedence;
            }

            return 0;
        }

        public bool IsType(Compiler compiler)
        {
            if (type == Type.KEYWORD && (keyword == Keyword.INT || keyword == Keyword.BOOL || keyword == Keyword.CHAR))
                return true;
            if (type == Type.IDENTIFIER && compiler.mClasses.ContainsKey(identifier))
                return true;
            return false;
        }

        public static bool IsUnaryOp(char c)
        {
            return c == '~' || c == '-';
        }

        public static string SymbolString(char c)
        {
            InitIfNeeded();
            string symbolStr;
            if (symbols.TryGetValue(c, out symbolStr))
            {
                return symbolStr;
            }

            return "";
        }

        public static bool IsNumber(char c)
        {
            return (c >= '0' && c <= '9');
        }

        public static bool IsWhitespace(char c)
        {
            return (c == ' ' || c == '\t');
        }

        // Instance data and members//
        public Type type;
        public Keyword keyword;
        public char symbol;
        public int intVal;
        public string stringVal;
        public string identifier;

        // For error reporting
        public int lineNumber;
        public int lineCharacter;
        public Tokenizer tokenizer;

        public Token( Tokenizer parent, int lineNum, int lineChar, Type typeIn = Type.NONE)
        {
            tokenizer = parent;
            lineNumber = lineNum;
            lineCharacter = lineChar;
            type = typeIn;
        }

        public string GetXMLString()
        {
            string lineStr = "<" + TypeString(type) + "> ";
            lineStr = lineStr + GetTokenString();
            lineStr = lineStr + " </" + TypeString(type) + ">";
            return lineStr;
        }

        public string GetTokenString()
        {
            string tokenString = "";

            switch (type)
            {
                case Token.Type.KEYWORD:
                    tokenString = KeywordString(keyword);
                    break;
                case Token.Type.IDENTIFIER:
                    tokenString = identifier;
                    break;
                case Token.Type.INT_CONST:
                    tokenString = "" + intVal;
                    break;
                case Token.Type.STRING_CONST:
                    tokenString = stringVal;
                    break;
                case Token.Type.SYMBOL:
                    tokenString = Token.SymbolString(symbol);
                    break;
                case Token.Type.EOF:
                    tokenString = "End of file";
                    break;
            }

            return tokenString;
        }
    }

    public class Tokenizer : IEnumerable
    {
        // All the tokens saved in a list
        public List<Token> mTokens;     // the set of all tokens
        public string mSource;          // path name of source file or resource name

        protected int mTokenCurrent;

        // Token parsing vars
        protected StreamReader mFile;
        protected int mLine;
        protected string mLineStr;
        protected int mLineChar;
        protected bool mCommentTerminateWait;
        protected bool mReadingToken;
        protected bool mReadingString;
        protected bool mReadingChar;
        protected string mTokenStr;

        public class State
        {
            public int mTokenCurrent;
            public State(int tokenCurrent) { mTokenCurrent = tokenCurrent; }
            public State() { mTokenCurrent = -1; }
            public bool IsDefined() { return mTokenCurrent >= 0; }
        }

        public Tokenizer(string fileInput)
        {
            Init(new StreamReader(fileInput), fileInput);
        }

        public Tokenizer(StreamReader streamReader, string source = "" )
        {
            Init(streamReader, source);
        }

        protected void Init(StreamReader streamReader, string source = "" )
        {
            SetSource(source);
            mFile = streamReader;
            mTokens = new List<Token>();
            mLineStr = "";
            mTokenStr = "";
            mTokenCurrent = -1;
        }

        public void SetSource( string source )
        {
            // Set it to the final file name of whatever the path is
            // e.g. "Folder/OtherFolder/File.ext"
            //      becomes "File.ext"
            mSource = source;
            string[] parts = source.Split(new char[3] { '/', '\\', '.' });
            if (parts.Length >= 2)
                mSource = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
        }

        public void ReadAll()
        {
            while (HasMoreTokens())
            {
                Advance();
            }
            Close();
            Reset();
        }

        public State StateGet()
        {
            return new State(mTokenCurrent);
        }

        public void StateSet(State state)
        {
            mTokenCurrent = state.mTokenCurrent;
        }

        public void Reset()
        {
            Rollback(mTokens.Count + 1);
        }

        public void Close()
        {
            if (mFile != null)
            {
                mFile.Close();
                mFile = null;
            }
        }

        public bool HasMoreTokens()
        {
            if (mLineStr.Length > 0 && mLineChar < mLineStr.Length)
                return true;

            if (mFile != null && !mFile.EndOfStream)
                return true;

            if (mTokenCurrent < mTokens.Count - 1)
                return true;

            return false;
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)mTokens).GetEnumerator();
        }

        protected bool ReadLine()
        {
            mLineChar = 0;
            mLineStr = "";

            while (!mFile.EndOfStream && mLineStr == "")
            {
                mLineStr = mFile.ReadLine();
                mLine++;
            }

            mLineStr = mLineStr + " "; // add a space to every line to cheat the parser logic when there is only one word in the line

            if (mLineStr != " ")
            {
                return true;
            }

            return false;
        }

        public Token Get()
        {
            if (mTokenCurrent < mTokens.Count)
                return mTokens[mTokenCurrent];

            if (mTokens.Count > 0)
                return new Token(this, mTokens[mTokens.Count - 1].lineNumber, mTokens[mTokens.Count - 1].lineCharacter, Token.Type.EOF);

            return new Token(this, 0, 0, Token.Type.EOF);
        }

        public Token GetAndAdvance()
        {
            Token result = Get();
            Advance();
            return result;
        }

        public Token GetAndRollback(int count = 1)
        {
            Token result = Get();
            Rollback(count);
            return result;
        }

        public Token AdvanceAndRollback(int count = 1)
        {
            Token result = Advance();
            Rollback(count);
            return result;
        }

        public Token Advance()
        {
            if (mFile == null)
            {
                mTokenCurrent++;
                if (mTokens.Count > 0)
                    return mTokens[Math.Min(mTokenCurrent, mTokens.Count - 1)];
                return null;
            }

            if ((mLineStr == "" || mLineChar >= mLineStr.Length) && !mFile.EndOfStream)
            {
                ReadLine();
            }

            while (mLineChar < mLineStr.Length || mCommentTerminateWait)
            {
                if (mCommentTerminateWait && mLineChar >= mLineStr.Length)
                {
                    if (ReadLine())
                        continue;

                    // no terminating */ in the file 
                    break;
                }

                char c = mLineStr[mLineChar];

                // Check for comments
                if (mLineChar < mLineStr.Length - 1 || mCommentTerminateWait)
                {
                    if (mCommentTerminateWait)
                    {
                        // Waiting for terminating "*/"
                        if (mLineStr[mLineChar] == '*' && mLineChar < mLineStr.Length - 1 && mLineStr[mLineChar + 1] == '/')
                        {
                            mLineChar = mLineChar + 2;
                            if (mLineChar >= mLineStr.Length)
                                ReadLine();
                            mCommentTerminateWait = false;
                        }
                        else
                        {
                            mLineChar++;
                        }

                        continue;
                    }

                    if (mLineStr[mLineChar] == '/' && mLineStr[mLineChar + 1] == '*')
                    {
                        mLineChar = mLineChar + 2;
                        mCommentTerminateWait = true;
                        continue;
                    }
                    else if (mLineStr[mLineChar] == '/' && mLineStr[mLineChar + 1] == '/')
                    {
                        if (!ReadLine())
                            mLineChar = mLineStr.Length;
                        continue;
                    }
                }

                bool isWhitespace = Token.IsWhitespace(c);
                bool isSymbol = Token.IsSymbol(c);
                bool isQuote = (c == '"');
                bool isSingleQuote = (c == '\'');

                if (mReadingToken)
                {
                    if (!mReadingChar && isSingleQuote)
                    {
                        mReadingChar = true;
                        mLineChar++;
                        continue;
                    }
                    else if (mReadingChar && isSingleQuote)
                    {
                        // Add the char
                        Token token = new Token(this, mLine, mLineChar - mTokenStr.Length - 1);
                        token.type = Token.Type.INT_CONST;
                        try
                        {
                            token.intVal = char.Parse(mTokenStr);
                        }
                        catch
                        {
                            token.intVal = 0;
                        }
                        mTokens.Add(token);

                        mReadingToken = false;
                        mReadingChar = false;
                        mTokenStr = "";
                        mLineChar++;
                        mTokenCurrent++;
                        return token;
                    }
                    else if (mReadingChar)
                    {
                        // keep reading without caring about anything but the end single quote
                        mTokenStr += c;
                        mLineChar++;
                        continue;
                    }

                    if (!mReadingString && isQuote)
                    {
                        mReadingString = true;
                        mLineChar++;
                        continue;
                    }
                    else if (mReadingString && isQuote)
                    {
                        // Add the string
                        Token token = new Token(this, mLine, mLineChar - mTokenStr.Length - 1);
                        token.type = Token.Type.STRING_CONST;
                        token.stringVal = mTokenStr;
                        mTokens.Add(token);

                        mReadingToken = false;
                        mReadingString = false;
                        mTokenStr = "";
                        mLineChar++;
                        mTokenCurrent++;
                        return token;
                    }
                    else if (mReadingString)
                    {
                        // keep reading without caring about anything but the end quote
                        mTokenStr += c;
                        mLineChar++;
                        continue;
                    }

                    if (isSymbol || isWhitespace)
                    {
                        if (mTokenStr.Length > 0)
                        {
                            // Have a token string to add before adding the symbol
                            Token token = new Token(this, mLine, mLineChar - mTokenStr.Length);
                            Token.Keyword keyword = Token.GetKeyword(mTokenStr);
                            if (keyword != Token.Keyword.NONE)
                            {
                                token.type = Token.Type.KEYWORD;
                                token.keyword = keyword;
                            }
                            else if (Token.IsNumber(mTokenStr[0]))
                            {
                                token.type = Token.Type.INT_CONST;
                                token.intVal = ByteCode.IntParse(mTokenStr);
                            }
                            else
                            {
                                token.type = Token.Type.IDENTIFIER;
                                token.identifier = mTokenStr;
                            }

                            mTokens.Add(token);
                            mReadingToken = false;
                            mTokenStr = "";
                            mTokenCurrent++;
                            return token;
                        }

                        if (isSymbol)
                        {
                            // Add the symbol
                            Token token = new Token(this, mLine, mLineChar);
                            token.type = Token.Type.SYMBOL;
                            token.symbol = c;
                            mTokens.Add(token);
                            mTokenCurrent++;
                            mLineChar++;
                            return token;
                        }

                        mReadingToken = false;
                        mTokenStr = "";
                        mLineChar++;
                        continue;
                    }
                    else
                    {
                        mTokenStr += c;
                        mLineChar++;
                        continue;
                    }
                }
                else if (isWhitespace)
                {
                    // skip whitespace
                    mLineChar++;
                    continue;
                }
                else
                {
                    // have something to read - start reading the token
                    mReadingToken = true;
                    continue;
                }
            }

            return null;
        }

        public bool Rollback(int count = 1)
        {
            mTokenCurrent = mTokenCurrent - count;

            if (mTokenCurrent < 0)
            {
                mTokenCurrent = 0;
            }

            return true;
        }
    }
}