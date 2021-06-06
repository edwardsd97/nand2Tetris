using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

class JackAnalyzer
{
    static bool mComments = false;
    static bool mVerbose = false;

    static void Main(string[] args)
    {
        ArrayList paths = new ArrayList();

        foreach (string arg in args)
        {
            string lwrArg = arg.ToLower();
            if (lwrArg == "-c")
                JackAnalyzer.mComments = true;
            else if (lwrArg == "-v")
                JackAnalyzer.mVerbose = true;
            else paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            Console.WriteLine("No filename or directory provided.");
            return;
        }

        // Convert directories to actual files recursively
        for (int i = 0; i < paths.Count; i++)
        {
            FileAttributes attrib = File.GetAttributes((string)paths[i]);
            if (attrib.HasFlag(FileAttributes.Directory))
            {
                string[] files = Directory.GetFiles((string)paths[i], "*.Jack");
                foreach (string file in files)
                {
                    paths.Add(file);
                }
                paths.RemoveAt(i--);
            }
        }

        // Process the files
        for (int i = 0; i < paths.Count; i++)
        {
            ProcessFile((string)paths[i]);
        }
    }

    static void ProcessFile(string path)
    {
        JackTokenizer tokenizer = new JackTokenizer( path );

        while (tokenizer.HasMoreTokens())
        {
            tokenizer.Advance();
        }

        // Dump the tokens to file
        StreamWriter writer = new StreamWriter( GetOutTokenfile( path ) );
        writer.AutoFlush = true;
        writer.WriteLine("<tokens>");
        foreach (JackToken token in tokenizer)
        {
            string lineStr = "<" + JackToken.TypeString(token.type) + "> ";
            lineStr = lineStr + token.GetTokenString();
            lineStr = lineStr + " </" + JackToken.TypeString(token.type) + ">";
            writer.WriteLine(lineStr);
        }
        writer.WriteLine("</tokens>");
    }

    public static string GetOutTokenfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + "T.xml";
        return outFile;
    }

    public static string SwitchExtension(string file, string extension)
    {
        string[] fileParts = file.Split(new char[1] { '.' });
        string outFile = "";
        for (int i = 0; i < fileParts.Length; i++)
        {
            if (i < fileParts.Length - 1)
                outFile = outFile + fileParts[i] + ".";
            else
                outFile = outFile + extension;
        }

        return outFile;
    }

    public static string FileToName(string file)
    {
        string[] fileParts = file.Split(new char[3] { '.', '/', '\\' });
        if (fileParts.Length > 1)
        {
            return fileParts[fileParts.Length - 2];
        }
        return "";
    }

    public static string FileToPath(string file)
    {
        string[] fileParts = file.Split(new char[3] { '.', '/', '\\' });
        string result = "";
        if (fileParts.Length > 1)
        {
            for (int i = 0; i < fileParts.Length - 2; i++)
                result = result + fileParts[i] + "/";
        }
        return result;
    }
}

class JackToken
{
    // Enum types //
    public enum Type
    {
        NONE,
        KEYWORD, SYMBOL, IDENTIFIER, INT_CONST, STRING_CONST
    };

    public enum Keyword
    {
        NONE,
        CLASS, METHOD, FUNCTION, CONSTRUCTOR,
        INT, BOOL, CHAR, VOID,
        VAR, STATIC, FIELD, LET,
        DO, IF, ELSE, WHILE,
        RETURN, TRUE, FALSE, NULL,
        THIS
    };

    // Static data and members //
    private static bool mInitialized = false;
    private static Dictionary<string, Keyword> strToKeyword;
    private static Dictionary<Keyword, string> keywordToStr;
    private static Dictionary<Type, string> typeToStr;
    private static Dictionary<char, string> symbols;

    protected static void InitIfNeeded()
    {
        if (JackToken.mInitialized)
            return;

        typeToStr = new Dictionary<JackToken.Type, string>();
        typeToStr.Add(JackToken.Type.KEYWORD, "keyword" );
        typeToStr.Add(JackToken.Type.SYMBOL, "symbol");
        typeToStr.Add(JackToken.Type.IDENTIFIER, "identifier");
        typeToStr.Add(JackToken.Type.INT_CONST, "integerConstant");
        typeToStr.Add(JackToken.Type.STRING_CONST, "stringConstant");

        strToKeyword = new Dictionary<string, Keyword>();
        strToKeyword.Add("class", JackToken.Keyword.CLASS );
        strToKeyword.Add("method", JackToken.Keyword.METHOD);
        strToKeyword.Add("function", JackToken.Keyword.FUNCTION);
        strToKeyword.Add("constructor", JackToken.Keyword.CONSTRUCTOR);
        strToKeyword.Add("int", JackToken.Keyword.INT);
        strToKeyword.Add("bool", JackToken.Keyword.BOOL);
        strToKeyword.Add("char", JackToken.Keyword.CHAR);
        strToKeyword.Add("void", JackToken.Keyword.VOID);
        strToKeyword.Add("var", JackToken.Keyword.VAR);
        strToKeyword.Add("static", JackToken.Keyword.STATIC);
        strToKeyword.Add("field", JackToken.Keyword.FIELD);
        strToKeyword.Add("let", JackToken.Keyword.LET);
        strToKeyword.Add("do", JackToken.Keyword.DO);
        strToKeyword.Add("if", JackToken.Keyword.IF);
        strToKeyword.Add("else", JackToken.Keyword.ELSE);
        strToKeyword.Add("while", JackToken.Keyword.WHILE);
        strToKeyword.Add("return", JackToken.Keyword.RETURN);
        strToKeyword.Add("true", JackToken.Keyword.TRUE);
        strToKeyword.Add("false", JackToken.Keyword.FALSE);
        strToKeyword.Add("null", JackToken.Keyword.NULL);
        strToKeyword.Add("this", JackToken.Keyword.THIS);

        keywordToStr = new Dictionary<Keyword, string>();
        foreach (string key in strToKeyword.Keys)
        {
            keywordToStr.Add(strToKeyword[key], key );
        }

        symbols = new Dictionary<char, string>();
        symbols.Add('{', "{"); symbols.Add('}', "}");
        symbols.Add('[', "["); symbols.Add(']', "]");
        symbols.Add('(', "("); symbols.Add(')', ")");
        symbols.Add('.', "."); symbols.Add(',', ",");  symbols.Add(';', ";");
        symbols.Add('+', "+"); symbols.Add('-', "-");  
        symbols.Add('*', "*"); symbols.Add('/', "/");
        symbols.Add('&', "&amp;"); symbols.Add('|', "|");
        symbols.Add('=', "="); symbols.Add('~', "~");
        symbols.Add('<', "&lt;"); symbols.Add('>', "&gt;");

        JackToken.mInitialized = true;
    }

    public static Keyword GetKeyword(string str)
    {
        InitIfNeeded();
        Keyword keyword;
        if (strToKeyword.TryGetValue(str, out keyword))
        {
            return keyword;
        }

        return Keyword.NONE;
    }

    public static string KeywordString( Keyword keyword )
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
    public Type     type;
    public Keyword  keyword;
    public char     symbol;
    public int      intVal;
    public string   stringVal;
    public string   identifier;

    // For error reporting
    public int      lineNumber;
    public int      lineCharacter;

    public JackToken( int lineNum, int lineChar )
    {
        lineNumber = lineNum;
        lineCharacter = lineChar;
    }

    public string GetTokenString()
    {
        string tokenString = "";

        switch (type)
        {
            case Type.KEYWORD:
                tokenString = JackToken.KeywordString(keyword);
                break;
            case Type.IDENTIFIER:
                tokenString = identifier;
                break;
            case Type.INT_CONST:
                tokenString = "" + intVal;
                break;
            case Type.STRING_CONST:
                tokenString = stringVal;
                break;
            case Type.SYMBOL:
                tokenString = JackToken.SymbolString( symbol );
                break;
        }

        return tokenString;
    }
}

class JackTokenizer : IEnumerable
{
    public int mLine;

    public string mLineStr;
    public int mLineChar;

    public System.IO.StreamReader mFile;
    public ArrayList mTokens;
    public int mTokenCurrent;
    public bool mCommentTerminateWait;
    public bool mReadingToken;
    public bool mReadingString;
    public string mTokenStr;

    public JackTokenizer(string fileInput)
    {
        mFile = new System.IO.StreamReader(fileInput);
        mTokens = new ArrayList();
        mLineStr = "";
        mTokenStr = "";
        mTokenCurrent = -1;
    }

    public bool HasMoreTokens()
    {
        if (mLineStr.Length > 0 && mLineChar < mLineStr.Length)
            return true;

        return !mFile.EndOfStream;
    }

    public IEnumerator GetEnumerator()
    {
        return ((IEnumerable)mTokens).GetEnumerator();
    }

    protected bool ReadLine()
    {
        mLineChar = 0;
        mLineStr = "";

        while ( !mFile.EndOfStream && mLineStr == "" )
        {
            mLineStr = mFile.ReadLine();
            mLine++;
        }

        if (mLineStr != "")
        {
            return true;
        }

        return false;
    }

    public bool Advance()
    {
        if (mTokenCurrent < mTokens.Count - 1)
        {
            // Just advance to the next already parsed token
            mTokenCurrent++;
            return true;
        }

        if ( ( mLineStr == "" || mLineChar >= mLineStr.Length ) && !mFile.EndOfStream )
        {
            ReadLine();
        }

        while ( mLineChar < mLineStr.Length || mCommentTerminateWait )
        {
            if ( mCommentTerminateWait && mLineChar >= mLineStr.Length )
            {
                if ( ReadLine() )
                    continue;

                // no terminating */ in the file 
                break;
            }

            char c = mLineStr[mLineChar];

            // Check for comments
            if (mLineChar < mLineStr.Length - 1)
            {
                if (mCommentTerminateWait)
                {
                    // Waiting for terminating "*/"
                    if (mLineStr[mLineChar] == '*' && mLineStr[mLineChar + 1] == '/')
                    {
                        mLineChar = mLineChar + 2;
                        if ( mLineChar >= mLineStr.Length )
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
                    if ( !ReadLine() )
                        mLineChar = mLineStr.Length;
                    continue;
                }
            }

            bool isWhitespace = JackToken.IsWhitespace(c);
            bool isSymbol = JackToken.IsSymbol(c);
            bool isQuote = ( c == '"' );

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
                    JackToken token = new JackToken( mLine, mLineChar - mTokenStr.Length - 1 );
                    token.type = JackToken.Type.STRING_CONST;
                    token.stringVal = mTokenStr;
                    mTokens.Add( token );

                    mReadingToken = false;
                    mReadingString = false;
                    mTokenStr = "";
                    mLineChar++;
                    mTokenCurrent++;
                    return true;
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
                        JackToken token = new JackToken( mLine, mLineChar - mTokenStr.Length );
                        JackToken.Keyword keyword = JackToken.GetKeyword(mTokenStr);
                        if (keyword != JackToken.Keyword.NONE)
                        {
                            token.type = JackToken.Type.KEYWORD;
                            token.keyword = keyword;
                        }
                        else if (JackToken.IsNumber(mTokenStr[0]))
                        {
                            token.type = JackToken.Type.INT_CONST;
                            token.intVal = int.Parse(mTokenStr);
                        }
                        else
                        {
                            token.type = JackToken.Type.IDENTIFIER;
                            token.identifier = mTokenStr;
                        }

                        mTokens.Add(token);
                        mReadingToken = false;
                        mTokenStr = "";
                        mTokenCurrent++;
                        return true;
                    }

                    if (isSymbol)
                    {
                        // Add the symbol
                        JackToken token = new JackToken( mLine, mLineChar );
                        token.type = JackToken.Type.SYMBOL;
                        token.symbol = c;
                        mTokens.Add(token);
                        mTokenCurrent++;
                        mLineChar++;
                        return true;
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

        return true;
    }

    public bool Rollback(int count = 1)
    {
        if (mTokenCurrent == -1)
        {
            // No tokens have been read
            return false;
        }

        mTokenCurrent = mTokenCurrent - count;
        if (mTokenCurrent < 0)
        {
            mTokenCurrent = 0;
        }

        return true;
    }
}