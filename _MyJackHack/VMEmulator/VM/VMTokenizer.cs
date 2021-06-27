using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

class VMToken
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
        if (VMToken.mInitialized)
            return;

        typeToStr = new Dictionary<Type, string>();
        typeToStr.Add(VMToken.Type.KEYWORD, "keyword");
        typeToStr.Add(VMToken.Type.SYMBOL, "symbol");
        typeToStr.Add(VMToken.Type.IDENTIFIER, "identifier");
        typeToStr.Add(VMToken.Type.INT_CONST, "integerConstant");
        typeToStr.Add(VMToken.Type.STRING_CONST, "stringConstant");

        strToKeyword = new Dictionary<string, Keyword>();
        strToKeyword.Add("class", VMToken.Keyword.CLASS);
        strToKeyword.Add("method", VMToken.Keyword.METHOD);
        strToKeyword.Add("function", VMToken.Keyword.FUNCTION);
        strToKeyword.Add("constructor", VMToken.Keyword.CONSTRUCTOR);
        strToKeyword.Add("int", VMToken.Keyword.INT);
        strToKeyword.Add("boolean", VMToken.Keyword.BOOL);
        strToKeyword.Add("char", VMToken.Keyword.CHAR);
        strToKeyword.Add("void", VMToken.Keyword.VOID);
        strToKeyword.Add("var", VMToken.Keyword.VAR);
        strToKeyword.Add("static", VMToken.Keyword.STATIC);
        strToKeyword.Add("field", VMToken.Keyword.FIELD);
        strToKeyword.Add("let", VMToken.Keyword.LET);
        strToKeyword.Add("do", VMToken.Keyword.DO);
        strToKeyword.Add("if", VMToken.Keyword.IF);
        strToKeyword.Add("else", VMToken.Keyword.ELSE);
        strToKeyword.Add("while", VMToken.Keyword.WHILE);
        strToKeyword.Add("for", VMToken.Keyword.FOR);
        strToKeyword.Add("continue", VMToken.Keyword.CONTINUE);
        strToKeyword.Add("break", VMToken.Keyword.BREAK);
        strToKeyword.Add("switch", VMToken.Keyword.SWITCH);
        strToKeyword.Add("case", VMToken.Keyword.CASE);
        strToKeyword.Add("default", VMToken.Keyword.DEFAULT);
        strToKeyword.Add("return", VMToken.Keyword.RETURN);
        strToKeyword.Add("true", VMToken.Keyword.TRUE);
        strToKeyword.Add("false", VMToken.Keyword.FALSE);
        strToKeyword.Add("null", VMToken.Keyword.NULL);
        strToKeyword.Add("this", VMToken.Keyword.THIS);

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

        VMToken.mInitialized = true;
    }

    public static Keyword GetKeyword(string str)
    {
        InitIfNeeded();
        Keyword keyword;
        if (strToKeyword.TryGetValue(str, out keyword))
        {
            return keyword;
        }

        return VMToken.Keyword.NONE;
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

    public bool IsType()
    {
        if (type == Type.KEYWORD && (keyword == Keyword.INT || keyword == Keyword.BOOL || keyword == Keyword.CHAR))
            return true;
        if (type == Type.IDENTIFIER && VMCompiler.mClasses.ContainsKey(identifier))
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

    public VMToken(int lineNum, int lineChar, Type typeIn = Type.NONE )
    {
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
            case VMToken.Type.KEYWORD:
                tokenString = KeywordString(keyword);
                break;
            case VMToken.Type.IDENTIFIER:
                tokenString = identifier;
                break;
            case VMToken.Type.INT_CONST:
                tokenString = "" + intVal;
                break;
            case VMToken.Type.STRING_CONST:
                tokenString = stringVal;
                break;
            case VMToken.Type.SYMBOL:
                tokenString = VMToken.SymbolString(symbol);
                break;
            case VMToken.Type.EOF:
                tokenString = "End of file";
                break;
        }

        return tokenString;
    }
}

class VMTokenizer : IEnumerable
{
    // All the tokens saved in a list
    public List<VMToken> mTokens;
    protected int mTokenCurrent;

    // VMToken parsing vars
    protected StreamReader mFile;
    protected int mLine;
    protected string mLineStr;
    protected int mLineChar;
    protected bool mCommentTerminateWait;
    protected bool mReadingToken;
    protected bool mReadingString;
    protected string mTokenStr;

    public class State
    {
        public int mTokenCurrent;
        public State(int tokenCurrent) { mTokenCurrent = tokenCurrent; }
        public State() { mTokenCurrent = -1; }
        public bool IsDefined() { return mTokenCurrent >= 0; }
    }

    public VMTokenizer(string fileInput)
    {
        Init(new StreamReader(fileInput));
    }

    public VMTokenizer(StreamReader streamReader)
    {
        Init(streamReader);
    }

    protected void Init(StreamReader streamReader)
    {
        mFile = streamReader;
        mTokens = new List<VMToken>();
        mLineStr = "";
        mTokenStr = "";
        mTokenCurrent = -1;
    }

    public static int IntParse(string str)
    {
        int result = 0;
        try
        {
            result = int.Parse(str);
        }
        catch
        {
            result = 0;
        }

        return result;
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

    public VMToken Get()
    {
        if ( mTokenCurrent < mTokens.Count )
            return mTokens[mTokenCurrent];

        if ( mTokens.Count > 0 )
            return new VMToken(mTokens[mTokens.Count - 1].lineNumber, mTokens[mTokens.Count - 1].lineCharacter, VMToken.Type.EOF);

        return new VMToken( 0, 0, VMToken.Type.EOF );
    }

    public VMToken GetAndAdvance()
    {
        VMToken result = Get();
        Advance();
        return result;
    }

    public VMToken GetAndRollback(int count = 1)
    {
        VMToken result = Get();
        Rollback(count);
        return result;
    }

    public VMToken AdvanceAndRollback(int count = 1)
    {
        VMToken result = Advance();
        Rollback(count);
        return result;
    }

    public VMToken Advance()
    {
        if (mFile == null)
        {
            mTokenCurrent++;
            if ( mTokens.Count > 0 )
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

            bool isWhitespace = VMToken.IsWhitespace(c);
            bool isSymbol = VMToken.IsSymbol(c);
            bool isQuote = (c == '"');

            if (mReadingToken)
            {
                if (!mReadingString && isQuote)
                {
                    mReadingString = true;
                    mLineChar++;
                    continue;
                }
                else if (mReadingString && isQuote)
                {
                    // Add the string
                    VMToken token = new VMToken(mLine, mLineChar - mTokenStr.Length - 1);
                    token.type = VMToken.Type.STRING_CONST;
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
                        VMToken token = new VMToken(mLine, mLineChar - mTokenStr.Length);
                        VMToken.Keyword keyword = VMToken.GetKeyword(mTokenStr);
                        if (keyword != VMToken.Keyword.NONE)
                        {
                            token.type = VMToken.Type.KEYWORD;
                            token.keyword = keyword;
                        }
                        else if (VMToken.IsNumber(mTokenStr[0]))
                        {
                            token.type = VMToken.Type.INT_CONST;
                            try
                            {
                                token.intVal = IntParse(mTokenStr);
                            }
                            catch
                            {
                                token.intVal = 0;
                            }
                        }
                        else
                        {
                            token.type = VMToken.Type.IDENTIFIER;
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
                        VMToken token = new VMToken(mLine, mLineChar);
                        token.type = VMToken.Type.SYMBOL;
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
