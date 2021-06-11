using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

class JackCompiler
{
    static public bool mComments = false;
    static public bool mVerbose = false;
    static public bool mDumpTokenFile = false;
    static public bool mDumpXmlFile = false;
    static public bool mDumpVMFile = true;
    static public Dictionary<string, Tokenizer> mTokenizerDic = new Dictionary<string, Tokenizer>();

    static void Main(string[] args)
    {
        ArrayList paths = new ArrayList();

        foreach (string arg in args)
        {
            string lwrArg = arg.ToLower();
            if (lwrArg == "-c")
                JackCompiler.mComments = true;
            else if (lwrArg == "-v")
                JackCompiler.mVerbose = true;
            else if (lwrArg == "-t")
                JackCompiler.mDumpTokenFile = true;
            else if (lwrArg == "-x")
                JackCompiler.mDumpXmlFile = true;
            else
                paths.Add(arg);
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
                string[] files = Directory.GetFiles((string)paths[i], "*.jack");
                foreach (string file in files)
                {
                    paths.Add(file);
                }
                paths.RemoveAt(i--);
            }
        }

        // Setup global scope symbol table
        SymbolTable.ScopePush( "global" );

        // Preprocess the files
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Preprocessing... " + (string)paths[i]);
            PreProcessFile((string)paths[i]);
        }

        // Process the files
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Compiling... " + (string)paths[i]);
            CompileFile((string)paths[i]);
        }
    }

    static void PreProcessFile(string path)
    {
        Tokenizer tokenizer = new Tokenizer(path);

        // Read all tokens into memory
        while (tokenizer.HasMoreTokens())
        {
            tokenizer.Advance();
        }
        tokenizer.Close();
        tokenizer.Reset();

        if (JackCompiler.mDumpTokenFile)
        {
            // Dump the tokens to token xml file
            StreamWriter writer = new StreamWriter(GetOutTokenfile(path));
            writer.AutoFlush = true;
            writer.WriteLine("<tokens>");
            foreach (Token token in tokenizer)
            {
                writer.WriteLine(token.GetXMLString());
            }
            writer.WriteLine("</tokens>");
            tokenizer.Reset();
        }

        mTokenizerDic.Add(path, tokenizer);

        CompilationEngine compiler = new CompilationEngine(tokenizer);
        compiler.CompilePrePass();
    }

    static void CompileFile(string path)
    {
        Tokenizer tokenizer;
        if (mTokenizerDic.TryGetValue(path, out tokenizer))
        {
            CompilationEngine compiler = new CompilationEngine(tokenizer, GetOutBasefile(path));

            // Compile the tokens into output file
            compiler.CompileGrammar("class");
            compiler.Reset();
            compiler.CompileClass();
        }
        else
        {
            Console.WriteLine("Tokens not found for " + path);
        }
    }

    public static string GetOutTokenfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + "T.xml";
        return outFile;
    }

    public static string GetOutBasefile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name;
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

class SymbolTable
{
    static List<SymbolScope> mScopes = new List<SymbolScope>();
    static SymbolTable mTheTable = new SymbolTable();

    public class Symbol
    {
        public string   mVarName;   // varName
        public Kind     mKind;      // STATIC, FIELD, ARG, VAR
        public Token    mType;      // int, boolean, char, ClassName
        public int      mOffset;     // segment offset
    }

    class SymbolScope
    {
        public Dictionary<string, Symbol> mSymbols = new Dictionary<string, Symbol>();
        public string mName;
        public bool mMethod;
        public SymbolScope(string name, bool isMethod = false)
        {
            mName = name;
            mMethod = isMethod;
        }
    };

    public enum Kind
    {
        NONE, GLOBAL, STATIC, FIELD, ARG, VAR
    }

    public static void ScopePush( string name, bool isMethod = false )
    {
        SymbolScope scope = new SymbolScope(name, isMethod);
        mScopes.Add(scope);
    }

    public static void ScopePop()
    {
        if( mScopes.Count > 0 )
        {
            mScopes.RemoveAt(mScopes.Count - 1);
        }
    }

    public static void Define(string varName, Token type, Kind kind)
    {
        if (mScopes.Count == 0)
            return;

        Symbol newVar = new Symbol();
        newVar.mKind = kind;
        newVar.mType = type;
        newVar.mVarName = varName;
        newVar.mOffset = 0;

        foreach (Symbol symbol in mScopes[mScopes.Count - 1].mSymbols.Values)
        {
            if (symbol.mKind == newVar.mKind)
            {
                newVar.mOffset = newVar.mOffset + 1;
            }
        }

        mScopes[mScopes.Count - 1].mSymbols.Add(varName, newVar);
    }

    public static bool Exists(string varName)
    {
        // Walk backwards from most recently added scope backward to oldest looking for the symbol
        int iScope = mScopes.Count - 1;

        while ( iScope >= 0)
        {
            Symbol result = null;
            if ( varName != null && mScopes[iScope].mSymbols.TryGetValue(varName, out result))
                return true;
            iScope--;
        }

        return false;
    }

    public static Symbol Find( string varName )
    {
        // Walk backwards from most recently added scope backward to oldest looking for the symbol
        Symbol result = null;
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if (varName != null && mScopes[iScope].mSymbols.TryGetValue(varName, out result))
                return result;

            iScope--;
        }

        return result;
    }

    public static bool CompilingMethod()
    {
        // Walk backwards from most recently added scope backward to oldest looking for method
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if ( mScopes[iScope].mMethod )
                return true;

            iScope--;
        }

        return false;
    }

    public static Kind KindOf(string varName)
    {
        Symbol symbol = SymbolTable.Find( varName );
        if (symbol != null)
            return symbol.mKind;
        return Kind.NONE;
    }

    public static string TypeOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
            return symbol.mType.GetTokenString();
        return null;
    }

    public static int OffsetOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
        {
            if ( SymbolTable.CompilingMethod() && symbol.mKind == Kind.ARG )
                return symbol.mOffset + 1; // skip over argument 0 (this)
            return symbol.mOffset;
        }
        return 0;
    }

    public static VMWriter.Segment SegmentOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
        {
            switch (symbol.mKind)
            {
                case Kind.ARG: return VMWriter.Segment.ARG;
                case Kind.FIELD: return VMWriter.Segment.THIS;
                case Kind.STATIC: return VMWriter.Segment.STATIC;
                case Kind.VAR: return VMWriter.Segment.LOCAL;
            }
        }

        return VMWriter.Segment.INVALID;
    }

    public static int KindSize( Kind kind )
    {
        int result = 0;

        for( int iScope = 0; iScope < mScopes.Count; iScope++ )
        {
            foreach ( Symbol symbol in mScopes[iScope].mSymbols.Values )
            {
                if (symbol.mKind == kind)
                {
                    // in Jack all symbols are 1 word and size is measured in words
                    result++;
                }
            }
        }

        return result;
    }
}

class Token
{
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
    private static Dictionary<char, string> ops;
    private static Dictionary<Keyword, bool> statements;

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
        symbols.Add('.', "."); symbols.Add(',', ","); symbols.Add(';', ";");
        symbols.Add('+', "+"); symbols.Add('-', "-");
        symbols.Add('*', "*"); symbols.Add('/', "/");
        symbols.Add('&', "&amp;"); symbols.Add('|', "|");
        symbols.Add('=', "="); symbols.Add('~', "~");
        symbols.Add('<', "&lt;"); symbols.Add('>', "&gt;");

        // op: '+' | '-' | '*' | '/' | '&' | '|' | '<' | '>' | '='
        ops = new Dictionary<char, string>();
        ops.Add('+', "+"); ops.Add('-', "-");
        ops.Add('*', "*"); ops.Add('/', "/");
        ops.Add('&', "&amp;"); ops.Add('|', "|");
        ops.Add('=', "="); ops.Add('~', "~");
        ops.Add('<', "&lt;"); ops.Add('>', "&gt;");

        statements = new Dictionary<Keyword, bool>();
        statements.Add(Token.Keyword.LET, true);
        statements.Add(Token.Keyword.DO, true);
        statements.Add(Token.Keyword.IF, true);
        statements.Add(Token.Keyword.ELSE, true);
        statements.Add(Token.Keyword.WHILE, true);
        statements.Add(Token.Keyword.RETURN, true);

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
        string symbolStr;
        if (ops.TryGetValue(c, out symbolStr))
        {
            return true;
        }

        return false;
    }

    public static bool IsUnaryOp(char c)
    {
        return c == '~' || c == '-';
    }

    public static bool IsStatement( Keyword keyword  )
    {
        bool result = false;
        statements.TryGetValue(keyword, out result);
        return result;
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

    public Token(int lineNum, int lineChar)
    {
        lineNumber = lineNum;
        lineCharacter = lineChar;
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
        }

        return tokenString;
    }
}

class Tokenizer : IEnumerable
{
    // All the tokens saved in a list
    public List<Token> mTokens;
    protected int mTokenCurrent;

    // Token parsing vars
    protected System.IO.StreamReader mFile;
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
        public State( int tokenCurrent ) { mTokenCurrent = tokenCurrent; }
        public State() { mTokenCurrent = -1; }
        public bool IsDefined() { return mTokenCurrent >= 0; }
    }

    public Tokenizer(string fileInput)
    {
        mFile = new System.IO.StreamReader(fileInput);
        mTokens = new List<Token>();
        mLineStr = "";
        mTokenStr = "";
        mTokenCurrent = -1;
    }

    public State StateGet()
    {
        return new State(mTokenCurrent);
    }

    public void StateSet( State state )
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

        return mFile != null && !mFile.EndOfStream;
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


        if (mLineStr != "")
        {
            return true;
        }

        return false;
    }

    public Token Get()
    {
        if (mTokenCurrent < mTokens.Count)
            return mTokens[mTokenCurrent];
        return null;
    }

    public Token GetAndAdvance()
    {
        Token result = null;
        if (mTokenCurrent < mTokens.Count)
            result = mTokens[mTokenCurrent];
        Advance();
        return result;
    }

    public Token Advance()
    {
        if (mTokenCurrent < mTokens.Count - 1)
        {
            // Just advance to the next already parsed token
            mTokenCurrent++;
            return mTokens[mTokenCurrent];
        }

        if (mFile == null)
        {
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
                    Token token = new Token(mLine, mLineChar - mTokenStr.Length - 1);
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
                        Token token = new Token(mLine, mLineChar - mTokenStr.Length);
                        Token.Keyword keyword = Token.GetKeyword(mTokenStr);
                        if (keyword != Token.Keyword.NONE)
                        {
                            token.type = Token.Type.KEYWORD;
                            token.keyword = keyword;
                        }
                        else if (Token.IsNumber(mTokenStr[0]))
                        {
                            token.type = Token.Type.INT_CONST;
                            token.intVal = int.Parse(mTokenStr);
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
                        Token token = new Token(mLine, mLineChar);
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

class Grammar
{
    static bool mInitialized;
    static Dictionary<string, Node> mNodeDic;

    public class Node : List<object> { }

    public enum Gram
    {
        NONE,
        ZERO_OR_MORE, // Zero or more of the following grammar nodeStep
        OR,           // One of the N next nodes where N is the following entry
        OPTIONAL,     // Next entry is optional
        READ_AHEAD1   // Only outputs anything if the next 2 entries are valid in the token list
    };

    public enum Enclose
    {
        NEVER,        // never encloses this nodeStep in the xml <name> </name>
        NOT_EMPTY,    // only encloses this nodeStep in the xml <name> </name> when there is something inside of it
        ALWAYS        // always encloses this nodeStep in the xml <name> </name>  
    };

    public static void InitIfNeeded()
    {
        if (Grammar.mInitialized)
            return;

        mNodeDic = new Dictionary<string, Node>();
        int nodeIndex = 0;
        bool done = false;
        while (!done)
        {
            System.Type type = mParseLangNodeDefs[nodeIndex].GetType();
            if (type == typeof(int) && (int)mParseLangNodeDefs[nodeIndex] == 0)
            {
                // End of list
                done = true;
            }
            else if (type == typeof(string))
            {
                // Add entry point and advance to the next
                Node nodeEntry = new Node();
                mNodeDic.Add((string)mParseLangNodeDefs[nodeIndex], nodeEntry);
                bool advanced = false;
                while (!advanced)
                {
                    type = mParseLangNodeDefs[nodeIndex].GetType();
                    if (type == typeof(int) && (int)mParseLangNodeDefs[nodeIndex] == 0)
                    {
                        advanced = true;
                        break;
                    }
                    else
                    {
                        nodeEntry.Add(mParseLangNodeDefs[nodeIndex]);
                    }
                    nodeIndex++;
                }
            }

            nodeIndex++;
        }

        Grammar.mInitialized = true;
    }

    static object[] mParseLangNodeDefs =
    {
        // Each entry consists of a
        // nodeStep name,
        // NEVER, NOT_EMPTY, ALWAYS enclose with <name> </name>,
        // and then the definitions

        // FIXME - this could be a data file

        // CLASS

        // class: 'class' className '{' clasVarDec* subroutineDec* '}'
        "class", Enclose.ALWAYS, Token.Keyword.CLASS, Token.Type.IDENTIFIER, '{', Gram.ZERO_OR_MORE, "classVarDec", Gram.ZERO_OR_MORE, "subroutineDec", '}', 0,

        // classVarDec: ('static'|'field) type varName (',' varName)* ';'
        "classVarDec", Enclose.NOT_EMPTY, Gram.OR, 2, Token.Keyword.STATIC, Token.Keyword.FIELD, "type", Token.Type.IDENTIFIER,  Gram.ZERO_OR_MORE, "varDecAdd", ';', 0,

        // varDecAdd: ',' varName
        "varDecAdd", Enclose.NEVER, ',', Token.Type.IDENTIFIER, 0,

        // type: 'int'|'char'|'boolean'|className
        "type", Enclose.NEVER, Gram.OR, 4, Token.Keyword.INT, Token.Keyword.CHAR, Token.Keyword.BOOL, Token.Type.IDENTIFIER, 0,

        // subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody
        "subroutineDec", Enclose.NOT_EMPTY, Gram.OR, 3, Token.Keyword.CONSTRUCTOR, Token.Keyword.FUNCTION, Token.Keyword.METHOD, Gram.OR, 2, Token.Keyword.VOID, "type", Token.Type.IDENTIFIER, '(', "parameterList", ')', "subroutineBody", 0,

        // parameter: type varName
        "parameter", Enclose.NEVER, "type", Token.Type.IDENTIFIER, 0,

        // parameterAdd: ',' type varName
        "parameterAdd", Enclose.NEVER, ',', "type", Token.Type.IDENTIFIER, 0,

        // parameterList: ( parameter (',' parameter)* )?
        "parameterList", Enclose.ALWAYS, Gram.OPTIONAL, "parameter", Gram.ZERO_OR_MORE, "parameterAdd", 0,

        // subroutineBody: '{' varDec* statements '}'
        "subroutineBody", Enclose.NOT_EMPTY, '{', Gram.ZERO_OR_MORE, "varDec", "statements", '}', 0,

        // varDec: 'var' type varName (',' varName)* ';'
        "varDec", Enclose.NOT_EMPTY, Token.Keyword.VAR, "type", Token.Type.IDENTIFIER, Gram.ZERO_OR_MORE, "varDecAdd", ';', 0,


        // STATEMENTS

        // statements: statement*
        "statements", Enclose.ALWAYS, Gram.ZERO_OR_MORE, "statement", 0,

        // statement: letStatement | ifStatement | whileStatement | doStatement | returnStatement
        "statement", Enclose.NEVER, Gram.OR, 5, "letStatement", "ifStatement", "whileStatement", "doStatement", "returnStatement", 0,

        // arrayIndex: '[' expression ']'
        "arrayIndex", Enclose.NEVER, '[', "expression", ']', 0,

        // arrayValue: varName '[' expression ']'
        "arrayValue", Enclose.NEVER, Gram.READ_AHEAD1, Token.Type.IDENTIFIER, '[', "expression", ']', 0,

        // elseClause: 'else' '{' statements '}'
        "elseClause", Enclose.NEVER, Token.Keyword.ELSE, '{', "statements", '}', 0,

        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'
        "letStatement", Enclose.NOT_EMPTY, Token.Keyword.LET, Token.Type.IDENTIFIER, Gram.OPTIONAL, "arrayIndex", '=', "expression", ';', 0, 

        // ifStatement: 'if' '(' expression ')' '{' statements '}' ('else' '{' statements '}')?
        "ifStatement", Enclose.NOT_EMPTY, Token.Keyword.IF, '(', "expression", ')', '{', "statements", '}', Gram.OPTIONAL, "elseClause", 0, 

        // whileStatement: 'while' '(' expression ')' '{' statements '}'
        "whileStatement", Enclose.NOT_EMPTY, Token.Keyword.WHILE, '(', "expression", ')', '{', "statements", '}', 0,

        // doStatement: 'do' subroutineCall ';'
        "doStatement", Enclose.NOT_EMPTY, Token.Keyword.DO, "subroutineCall", ';', 0,

        // returnStatement: 'return' expression? ';'
        "returnStatement", Enclose.NOT_EMPTY, Token.Keyword.RETURN, Gram.OPTIONAL, "expression", ';', 0, 


        // EXPRESSIONS

        // opTerm: op term
        "opTerm", Enclose.NEVER, "op", "term", 0,

        // expressionAdd: ',' expression
        "expressionAdd", Enclose.NEVER, ',', "expression", 0,

        // expression: term (op term)*
        "expression", Enclose.NOT_EMPTY, "term", Gram.ZERO_OR_MORE, "opTerm", 0,

        // expressionParenth: '(' expression ')
        "expressionParenth", Enclose.NEVER, '(', "expression", ')', 0,

        // term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | identifier )
        "term", Enclose.NOT_EMPTY, Gram.OR, 8, "expressionParenth", "unaryTerm", Token.Type.STRING_CONST, Token.Type.INT_CONST, "keywordConstant", "subroutineCall", "arrayValue", Token.Type.IDENTIFIER, 0,
        
        // unaryTerm: unaryOp term
        "unaryTerm", Enclose.NEVER, "unaryOp", "term", 0,

        // subroutineObject: ( className | varName ) '.'
        "subroutineObject", Enclose.NEVER, Gram.READ_AHEAD1, Token.Type.IDENTIFIER, '.', 0,

        // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
        "subroutineCall", Enclose.NEVER, Gram.OPTIONAL, "subroutineObject", Gram.READ_AHEAD1, Token.Type.IDENTIFIER, '(', "expressionList", ')', 0,

        // expressionList: ( expression (',' expression)* )?
        "expressionList", Enclose.ALWAYS, Gram.OPTIONAL, "expression", Gram.ZERO_OR_MORE, "expressionAdd", 0, 

        // op: '+'|'-'|'*'|'/'|'&'|'|'|'<'|'>'|'='
        "op", Enclose.NEVER, Gram.OR, 9, '+', '-', '*', '/', '&', '|', '<', '>', '=', 0,

        // unaryOp: '-'|'~'
        "unaryOp", Enclose.NEVER, Gram.OR, 2, '-', '~', 0, 

        // keywordConstant: 'true'|'false'|'null'|'this'
        "keywordConstant", Enclose.NEVER, Gram.OR, 4, Token.Keyword.TRUE, Token.Keyword.FALSE, Token.Keyword.NULL, Token.Keyword.THIS, 0,

        0
    };

    static public Node GetNode(string nodeName)
    {
        InitIfNeeded();

        Node nodeStep;
        if (mNodeDic.TryGetValue(nodeName, out nodeStep))
            return nodeStep;

        return null;
    }
}

class Writer
{
    protected StreamWriter mFile;
    public int mLinesWritten = 0;

    public Writer(string outFile)
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
    }

    public virtual void WriteLine(string line)
    {
        if (JackCompiler.mVerbose)
            Console.WriteLine(line);
        mFile.WriteLine(line);
        mLinesWritten++;
    }
}
    
class GrammarWriter : Writer
{
    public GrammarWriter( string outFile ) : base( outFile )
    {
    }

    public virtual void PreGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
    public virtual void WriteGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
    public virtual void PostGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
}

class XMLWriter : GrammarWriter
{
    ArrayList mQueueLine;
    List<int> mLinesWrittenPrev;

    public XMLWriter(string outFile) : base( outFile )
    {
        mQueueLine = new ArrayList();
        mLinesWrittenPrev = new List<int>();
    }

    public void QueueLineAdd(string line)
    {
        mQueueLine.Add(line);
    }

    public void QueueLineRemove(string line)
    {
        for (int i = 0; i < mQueueLine.Count; i++)
        {
            if ((string)mQueueLine[i] == line)
            {
                mQueueLine.RemoveAt(i);
                return;
            }
        }
    }

    public override void WriteLine(string line)
    {
        if (mQueueLine.Count > 0)
        {
            ArrayList queue = mQueueLine;
            mQueueLine = new ArrayList();
            foreach (string qline in queue)
                WriteLine(qline);
        }

        base.WriteLine(line);
    }

    public override void PreGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack)
    {
        string nodeName = (string) nodeObj[0];
        Grammar.Enclose enclose = (Grammar.Enclose)nodeObj[1];

        if (enclose == Grammar.Enclose.ALWAYS)
        {
            WriteLine("<" + nodeName + ">");
        }
        else if (enclose == Grammar.Enclose.NOT_EMPTY)
        {
            QueueLineAdd("<" + nodeName + ">");
            mLinesWrittenPrev.Add( mLinesWritten );
        }
    }

    public override void PostGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack)
    {
        string nodeName = (string)nodeObj[0];
        Grammar.Enclose enclose = (Grammar.Enclose)nodeObj[1];

        int linesWrittenPrev = 0;
        if (enclose == Grammar.Enclose.NOT_EMPTY)
        {
            linesWrittenPrev = mLinesWrittenPrev[mLinesWrittenPrev.Count - 1];
            mLinesWrittenPrev.RemoveAt(mLinesWrittenPrev.Count - 1);
        }

        // Post process grammar node for each writer
        if (enclose == Grammar.Enclose.ALWAYS || (enclose == Grammar.Enclose.NOT_EMPTY && mLinesWritten > linesWrittenPrev))
        {
            WriteLine("</" + nodeName + ">");
        }
        else if (enclose == Grammar.Enclose.NOT_EMPTY)
        {
            QueueLineRemove("<" + nodeName + ">");
        }
    }

    public override void WriteGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack)
    {
        Token token = tokenStack.Get();
        string line = token.GetXMLString();
        WriteLine(line);
    }
}

class VMWriter : Writer
{
    public enum Segment
    {
        INVALID, CONST, ARG, LOCAL, STATIC, THIS, THAT, POINTER, TEMP
    }

    public enum Command
    {
        ADD, SUB, NEG, EQ, LT, GT, AND, OR, NOT
    }

    public VMWriter(string fileName) : base(fileName)
    {
    }

    public string SegmentString(Segment segment)
    {
        switch (segment)
        {
            case Segment.ARG: return "argument";
            case Segment.LOCAL: return "local";
            case Segment.STATIC: return "static";
            case Segment.THIS: return "this";
            case Segment.THAT: return "that";
            case Segment.POINTER: return "pointer";
            case Segment.TEMP: return "temp";
            default: return "constant";
        }
    }

    public string CommandString(Command command)
    {
        switch (command)
        {
            case Command.ADD: return "add";
            case Command.SUB: return "sub";
            case Command.NEG: return "neg";
            case Command.EQ: return "eq";
            case Command.LT: return "lt";
            case Command.GT: return "gt";
            case Command.AND: return "and";
            case Command.OR: return "or";
            default: return "not";
        }
    }

    public void WritePush(Segment segment, int index)
    {
        // push segment int
        WriteLine("push " + SegmentString(segment) + " " + index);
    }

    public void WritePop(Segment segment, int index)
    {
        // push segment int
        WriteLine("pop " + SegmentString(segment) + " " + index);
    }

    public void WriteArithmetic(Command command)
    {
        // command
        WriteLine(CommandString(command));
    }

    public void WriteLabel(string label)
    {
        // label
        WriteLine("label " + label);
    }

    public void WriteGoto(string label)
    {
        // goto
        WriteLine("goto " + label);
    }

    public void WriteIfGoto(string label)
    {
        // if-goto
        WriteLine("if-goto " + label);
    }

    public void WriteFunction(string function, int argCount)
    {
        // function function argCount
        WriteLine("function " + function + " " + argCount);
    }

    public void WriteCall(string function, int argCount)
    {
        // call function argCount
        WriteLine("call " + function + " " + argCount);
    }

    public void WriteReturn()
    {
        // return
        WriteLine("return");
    }
}

class CompilationEngine
{
    static public Dictionary<string, bool> mMethods = new Dictionary<string, bool>();
    static public Dictionary<string, int> mStrings = new Dictionary<string, int>();

    Tokenizer mTokens;
    List<GrammarWriter> mGrammarWriters = new List<GrammarWriter>();
    VMWriter mVMWriter;
    string mClassName;
    string mFuncName;
    int mFuncLabel;

    public CompilationEngine(Tokenizer tokens, string baseOutFile)
    {
        mTokens = tokens;

        Reset();

        if (JackCompiler.mDumpVMFile)
        {
            mVMWriter = new VMWriter(baseOutFile + ".vm");
        }

        if (JackCompiler.mDumpXmlFile)
        {
            mGrammarWriters.Add(new XMLWriter(baseOutFile + ".xml"));
        }
    }

    public CompilationEngine(Tokenizer tokens)
    {
        mTokens = tokens;
        Reset();
    }

    public void CompilePrePass()
    {
        ValidateTokenAdvance(Token.Keyword.CLASS);
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);

        Token token = mTokens.Advance();

        while ( token != null )
        {
            if (token.type == Token.Type.STRING_CONST)
            {
                // FIXME - string constants need to be allocated and stored in a table with memory addresses starting with first static address below the stack downward.
                // 255, 254, 253, ... downward 
                mStrings.Add(token.stringVal, 255 - mStrings.Count);
            }
            else if (token.keyword == Token.Keyword.METHOD)
            {
                // Register which functions are methods so that we now how to call them when they are encountered while compiling
                mTokens.Advance();
                mTokens.Advance();

                mMethods.Add(mClassName + "." + mTokens.Get().identifier, true );
            }

            token = mTokens.Advance();
        }

        mTokens.Reset();
    }

    public void Reset()
    {
        mTokens.Reset();
        mClassName = "";
    }

    public void Error(string msg = "")
    {
        Token token = mTokens.Get();
        string line = "ERROR: Line< " + token.lineNumber + " > Char< " + token.lineCharacter + " > " + msg;
        Console.WriteLine(line);
    }

    public void WritersPreGrammarEntry(Grammar.Node nodeObj)
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (GrammarWriter writer in mGrammarWriters)
        {
            writer.PreGrammarEntry(nodeObj, mTokens);
            mTokens.StateSet(state);
        }
    }

    public void WritersWriteGrammarEntry(Grammar.Node nodeObj)
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (GrammarWriter writer in mGrammarWriters)
        {
            writer.WriteGrammarEntry(nodeObj, mTokens);
            mTokens.StateSet(state);
        }
    }

    public void WritersPostGrammarEntry(Grammar.Node nodeObj)
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (GrammarWriter writer in mGrammarWriters)
        {
            mTokens.StateSet(state);
            writer.PostGrammarEntry(nodeObj, mTokens);
        }
    }

    public bool CompileGrammar(string nodeName, bool optional = false, bool canWrite = true)
    {
        int nodeStep = 0;

        Grammar.Node nodeObj = Grammar.GetNode(nodeName);

        if (nodeObj == null)
        {
            Error("Internal - Missing grammar nodeStep");
            return false;
        }

        nodeStep++;

        Grammar.Enclose enclose = (Grammar.Enclose)nodeObj[nodeStep];

        nodeStep++;

        WritersPreGrammarEntry(nodeObj);

        bool result = CompileGrammar(nodeName, nodeObj, nodeStep, optional, canWrite);

        WritersPostGrammarEntry(nodeObj);

        return result;
    }

    public bool CompileGrammar(string nodeName, Grammar.Node nodeObj, int nodeStart, bool optional, bool canWrite = true, int earlyTerminate = -1)
    {
        bool done = false;

        int readAheadNode = -1;
        Tokenizer.State tokenStateRestore = null;

        for (int nodeStep = nodeStart; nodeStep < nodeObj.Count && !done; nodeStep++)
        {
            Token token = mTokens.Get();

            System.Type type = nodeObj[nodeStep].GetType();

            if (type == typeof(Token.Type))
            {
                if (token.type != (Token.Type)nodeObj[nodeStep])
                {
                    if (!optional)
                        Error("Expected " + nodeName + " type " + Token.TypeString((Token.Type)nodeObj[nodeStep]));
                    if (tokenStateRestore != null)
                        mTokens.StateSet(tokenStateRestore);
                    return false;
                }

                if (readAheadNode < 0 && canWrite)
                    WritersWriteGrammarEntry(nodeObj);
                token = mTokens.Advance();
            }
            else if (type == typeof(Token.Keyword))
            {
                if (token.type != Token.Type.KEYWORD || token.keyword != (Token.Keyword)nodeObj[nodeStep])
                {
                    if (!optional)
                        Error("Expected " + nodeName + " keyword " + Token.KeywordString((Token.Keyword)nodeObj[nodeStep]));
                    if (tokenStateRestore != null)
                        mTokens.StateSet(tokenStateRestore);
                    return false;
                }

                if (readAheadNode < 0 && canWrite)
                    WritersWriteGrammarEntry(nodeObj);
                token = mTokens.Advance();
            }
            else if (type == typeof(char))
            {
                if (token.type != Token.Type.SYMBOL || token.symbol != (char)nodeObj[nodeStep])
                {
                    if (!optional)
                        Error("Expected " + nodeName + " symbol " + (char)nodeObj[nodeStep]);
                    if (tokenStateRestore != null)
                        mTokens.StateSet(tokenStateRestore);
                    return false;
                }

                if (readAheadNode < 0 && canWrite)
                    WritersWriteGrammarEntry(nodeObj);
                token = mTokens.Advance();
            }
            else if (type == typeof(Grammar.Gram))
            {
                int optionalEnd = 0;
                Grammar.Gram gram = (Grammar.Gram)nodeObj[nodeStep];
                switch (gram)
                {
                    case Grammar.Gram.OPTIONAL:
                        optionalEnd = nodeStep + 1;
                        nodeStep++;
                        break;

                    case Grammar.Gram.OR:
                        nodeStep++;
                        optionalEnd = nodeStep + (int)nodeObj[nodeStep];
                        nodeStep++;
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        optionalEnd = -1;
                        nodeStep++;
                        break;
                }

                switch (gram)
                {
                    case Grammar.Gram.OR:
                        while (!CompileGrammar(nodeName, nodeObj, nodeStep, nodeStep <= optionalEnd, readAheadNode < 0, nodeStep + 1))
                        {
                            nodeStep++;

                            if (nodeStep > optionalEnd)
                            {
                                if (!optional)
                                    Error();
                                if (tokenStateRestore != null)
                                    mTokens.StateSet(tokenStateRestore);
                                return false;
                            }
                        }
                        nodeStep = optionalEnd;
                        break;

                    case Grammar.Gram.OPTIONAL:
                        CompileGrammar(nodeName, nodeObj, nodeStep, true, readAheadNode < 0, nodeStep + 1);
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        while (CompileGrammar(nodeName, nodeObj, nodeStep, true, readAheadNode < 0, nodeStep + 1))
                        {
                            // do nothing
                        }
                        break;

                    case Grammar.Gram.READ_AHEAD1:
                        readAheadNode = nodeStep + 3;
                        tokenStateRestore = mTokens.StateGet();
                        break;
                }

                gram = Grammar.Gram.NONE;
            }
            else if (type == typeof(string))
            {
                if (!CompileGrammar((string)nodeObj[nodeStep], optional, readAheadNode < 0))
                {
                    if (!optional)
                        Error("Expected " + nodeName + " " + (string)nodeObj[nodeStep]);
                    if (tokenStateRestore != null)
                        mTokens.StateSet(tokenStateRestore);
                    return false;
                }
            }

            if (!done && earlyTerminate > 0 && (nodeStep + 1) >= earlyTerminate)
            {
                // terminate
                done = true;
            }

            if (readAheadNode > 0 && (nodeStep + 1) >= readAheadNode && tokenStateRestore != null)
            {
                // Rewind and write it for real now
                mTokens.StateSet(tokenStateRestore);
                tokenStateRestore = null;
                nodeStep = readAheadNode - 3;
                readAheadNode = -1;
                done = false;
            }
        }

        return true;
    }

    public Token ValidateTokenAdvance(object tokenCheck)
    {
        string dontCare = "";
        return ValidateTokenAdvance(tokenCheck, out dontCare);
    }

    public Token ValidateTokenAdvance(object tokenCheck, out string tokenString)
    {
        Token token = mTokens.Get();

        string error = null;

        tokenString = token.GetTokenString();

        System.Type type = tokenCheck.GetType();

        if (type == typeof(Token.Type) && token.type != (Token.Type)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString();
        }
        else if (type == typeof(Token.Keyword) && token.keyword != (Token.Keyword)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString();
        }
        else if (type == typeof(char) && token.symbol != (char)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString();
        }

        if (error != null)
            Error(error);

        mTokens.Advance();

        return mTokens.Get();
    }

    public bool ValidateFunctionReturnType(Token varType)
    {
        if (ValidateVarType(varType))
            return true;

        return varType.keyword == Token.Keyword.VOID;
    }

    public bool ValidateVarType(Token varType)
    {
        switch (varType.keyword)
        {
            case Token.Keyword.INT:
            case Token.Keyword.BOOL:
            case Token.Keyword.CHAR:
                return true;
            default:
                return varType.type == Token.Type.IDENTIFIER;
        }
    }

    public string NewFuncFlowLabel()
    {
        string result = mClassName + "_" + mFuncName + "_L" + ++mFuncLabel;
        return result;
    }

    public void CompileClass()
    {
        // class: 'class' className '{' classVarDec* subroutineDec* '}'

        mTokens.Reset();

        ValidateTokenAdvance(Token.Keyword.CLASS);
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);
        ValidateTokenAdvance('{');

        SymbolTable.ScopePush("class");

        while (CompileClassVarDec())
        {
            // continue with classVarDec
        }

        while (CompileSubroutineDec())
        {
            // continue with subroutineDec
        }

        ValidateTokenAdvance('}');

        SymbolTable.ScopePop();
    }

    public bool CompileClassVarDec()
    {
        // compiles class fields and static vars
        // classVarDec: ('static'|'field) type varName (',' varName)* ';'

        Token token = mTokens.Get();

        if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.FIELD || token.keyword == Token.Keyword.STATIC))
        {
            SymbolTable.Kind varKind = (token.keyword == Token.Keyword.STATIC) ? SymbolTable.Kind.STATIC : SymbolTable.Kind.FIELD;
            mTokens.Advance();

            Token varType = mTokens.Get();

            do
            {
                mTokens.Advance();

                string varName;
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

                SymbolTable.Define(varName, varType, varKind);

            } while (mTokens.Get().symbol == ',');

            token = ValidateTokenAdvance(';');

            return true;
        }

        return false;
    }

    public bool CompileSubroutineDec()
    {
        // compiles a method, function, or constructor
        // subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody

        Token token = mTokens.Get();

        if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.CONSTRUCTOR || token.keyword == Token.Keyword.FUNCTION || token.keyword == Token.Keyword.METHOD))
        {
            Token.Keyword funcCallType = token.keyword;
            mTokens.Advance();

            Token funcReturnType = mTokens.GetAndAdvance();
            if (!ValidateFunctionReturnType(funcReturnType))
            {
                Error("Return type unrecognized '" + funcReturnType.GetTokenString() + "'");
            }

            ValidateTokenAdvance(Token.Type.IDENTIFIER, out mFuncName);

            SymbolTable.ScopePush( "function", funcCallType == Token.Keyword.METHOD );

            ValidateTokenAdvance('(');

            CompileParameterList();

            ValidateTokenAdvance(')');

            ValidateTokenAdvance('{');

            while (CompileVarDec())
            {
                // keep going with more varDec
            }

            // Compile function beginning
            mFuncLabel = 0;
            mVMWriter.WriteFunction(mClassName + "." + mFuncName, SymbolTable.KindSize( SymbolTable.Kind.VAR ) );
            if (funcCallType == Token.Keyword.CONSTRUCTOR || funcCallType == Token.Keyword.METHOD)
            {
                if (funcCallType == Token.Keyword.CONSTRUCTOR)
                {
                    // Alloc "this" ( and it is pushed onto the stack )
                    mVMWriter.WritePush(VMWriter.Segment.CONST, SymbolTable.KindSize(SymbolTable.Kind.FIELD));
                    mVMWriter.WriteCall("Memory.alloc", 1);
                }

                if ( funcCallType == Token.Keyword.METHOD)
                {
                    // grab argument 0 (this) and push it on the stack
                    mVMWriter.WritePush(VMWriter.Segment.ARG, 0);
                }

                // pop "this" off the stack
                mVMWriter.WritePop(VMWriter.Segment.POINTER, 0);
            }

            CompileStatements();

            ValidateTokenAdvance('}');

            SymbolTable.ScopePop();

            return true;
        }

        return false;
    }

    public void CompileSubroutineCall()
    {
        // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
        // expressionList: ( expression (',' expression)* )?

        string subroutineName = null;
        string objectName = null;
        int argCount = 0;

        ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
        if (mTokens.Get().symbol == '.')
        {
            objectName = subroutineName;
            mTokens.Advance();
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
        }
        else
        {
            // all calls without an object specified must be assumed to be the current class object
            objectName = mClassName;
        }

        ValidateTokenAdvance('(');

        // Only for functions that are methods, we need to push the this pointer as first argument
        if ( objectName == mClassName && CompilationEngine.mMethods.ContainsKey(objectName + "." + subroutineName) )
        {
            // push pointer to object (this for object)
            mVMWriter.WritePush(VMWriter.Segment.POINTER, 0); // this
            argCount = argCount + 1;
        }
        else if ( SymbolTable.Exists(objectName) && CompilationEngine.mMethods.ContainsKey( SymbolTable.TypeOf( objectName ) + "." + subroutineName ) )
        {
            // push pointer to object (this for object)
            mVMWriter.WritePush(SymbolTable.SegmentOf(objectName), SymbolTable.OffsetOf(objectName)); // object pointer
            argCount = argCount + 1;
        }

        argCount = argCount + CompileExpressionList();

        ValidateTokenAdvance(')');

        if ( objectName != null )
        {
            if ( SymbolTable.Exists(objectName) )   
                mVMWriter.WriteCall(SymbolTable.TypeOf( objectName ) + "." + subroutineName, argCount);
            else
                mVMWriter.WriteCall(objectName + "." + subroutineName, argCount);
        }
        else
        {
            mVMWriter.WriteCall(subroutineName, argCount);
        }
    }

    public void CompileParameterList()
    {
        // compiles a parameter list within () without dealing with the ()s
        // can be completely empty

        // parameterList: ( type varName (',' type varName)* )?
        while (ValidateVarType(mTokens.Get()))
        {
            // handle argument
            Token varType = mTokens.GetAndAdvance();

            string varName;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            SymbolTable.Define(varName, varType, SymbolTable.Kind.ARG);

            if (mTokens.Get().symbol != ',')
                break;

            mTokens.Advance();
        }
    }

    public bool CompileVarDec()
    {
        // handles local variables in a subroutine
        // varDec: 'var' type varName (',' varName)* ';'

        Token token = mTokens.Get();

        if (token.keyword == Token.Keyword.VAR)
        {
            mTokens.Advance();

            Token varType = mTokens.Get();

            do
            {
                mTokens.Advance();

                string varName;
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

                SymbolTable.Define(varName, varType, SymbolTable.Kind.VAR);

            } while (mTokens.Get().symbol == ',');

            token = ValidateTokenAdvance(';');

            return true;
        }

        return false;
    }

    public void CompileStatements()
    {
        // compiles a series of statements without handling the enclosing {}s

        // statements: statement*
        // statement: letStatement | ifStatement | whileStatement | doStatement | returnStatement

        while ( Token.IsStatement( mTokens.Get().keyword ) )
        {
            switch ( mTokens.Get().keyword )
            {
                case Token.Keyword.LET:
                    CompileStatementLet();
                    break;
                case Token.Keyword.IF:
                    CompileStatementIf();
                    break;
                case Token.Keyword.WHILE:
                    CompileStatementWhile();
                    break;
                case Token.Keyword.DO:
                    CompileStatementDo();
                    break;
                case Token.Keyword.RETURN:
                    CompileStatementReturn();
                    break;
                default:
                    Error("Expected let, do, while, if, or return");
                    break;
            }
        }
    }

    public void CompileArrayAddress( string varNameKnown = null )
    {
        // Push the array indexed address onto stack
        string varName = varNameKnown;
        if ( varName == null )
        {
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);
        }
        ValidateTokenAdvance('[');
        CompileExpression();
        ValidateTokenAdvance(']');
        mVMWriter.WritePush(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
        mVMWriter.WriteArithmetic(VMWriter.Command.ADD);
    }

    public void CompileArrayValue()
    {
        // Push the array indexed address onto stack
        CompileArrayAddress();

        // set THAT and push THAT[0]
        mVMWriter.WritePop(VMWriter.Segment.POINTER, 1 );
        mVMWriter.WritePush(VMWriter.Segment.THAT, 0);
    }

    public void CompileStatementLet()
    {
        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'

        ValidateTokenAdvance(Token.Keyword.LET);

        string varName;
        bool isArray = false;
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

        if (mTokens.Get().symbol == '[')
        {
            isArray = true;

            // Push the array indexed address onto stack
            CompileArrayAddress(varName);
        }

        ValidateTokenAdvance('=');

        CompileExpression(); // push value onto stack

        if (isArray)
        {
            // requires use of the top 2 values on the stack
            //   value
            //   address
            mVMWriter.WritePop(VMWriter.Segment.TEMP, 0);
            mVMWriter.WritePop(VMWriter.Segment.POINTER, 1);
            mVMWriter.WritePush(VMWriter.Segment.TEMP, 0);
            mVMWriter.WritePop(VMWriter.Segment.THAT, 0);
        }
        else
        {
            mVMWriter.WritePop(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
        }

        ValidateTokenAdvance(';');
    }

    public void CompileStatementDo()
    {
        // doStatement: 'do' subroutineCall ';'

        ValidateTokenAdvance(Token.Keyword.DO);

        CompileSubroutineCall();

        mVMWriter.WritePop( VMWriter.Segment.TEMP, 0 ); // ignore return value

        ValidateTokenAdvance(';');
    }

    public void CompileStatementIf()
    {
        // ifStatement: 'if' '(' expression ')' '{' statements '}' ('else' '{' statements '}')?

        ValidateTokenAdvance(Token.Keyword.IF);
        ValidateTokenAdvance('(');

        // invert the expression to make the jumps simpler
        CompileExpression();
        mVMWriter.WriteArithmetic(VMWriter.Command.NOT);

        ValidateTokenAdvance(')');

        string label1 = NewFuncFlowLabel();
        string label2 = null;

        mVMWriter.WriteIfGoto( label1 );

        ValidateTokenAdvance('{');

        CompileStatements();

        ValidateTokenAdvance('}');

        if (mTokens.Get().keyword == Token.Keyword.ELSE)
        {
            label2 = NewFuncFlowLabel();
            mVMWriter.WriteGoto(label2);
        }

        mVMWriter.WriteLabel(label1);

        if (mTokens.Get().keyword == Token.Keyword.ELSE)
        {
            mTokens.Advance();

            ValidateTokenAdvance('{');

            CompileStatements();

            ValidateTokenAdvance('}');

            mVMWriter.WriteLabel(label2);
        }
    }

    public void CompileStatementWhile()
    {
        // whileStatement: 'while' '(' expression ')' '{' statements '}'

        ValidateTokenAdvance(Token.Keyword.WHILE);

        string label1 = NewFuncFlowLabel();
        string label2 = NewFuncFlowLabel();

        mVMWriter.WriteLabel(label1);

        // invert the expression to make the jumps simpler
        ValidateTokenAdvance('(');
        CompileExpression();
        mVMWriter.WriteArithmetic(VMWriter.Command.NOT);
        ValidateTokenAdvance(')');

        mVMWriter.WriteIfGoto(label2);

        ValidateTokenAdvance('{');

        CompileStatements();

        ValidateTokenAdvance('}');

        mVMWriter.WriteGoto(label1);

        mVMWriter.WriteLabel(label2);

    }

    public void CompileStatementReturn()
    {
        // returnStatement: 'return' expression? ';'

        ValidateTokenAdvance(Token.Keyword.RETURN);

        Token token = mTokens.Get();

        if (token.symbol == ';')
        {
            mVMWriter.WritePush(VMWriter.Segment.CONST, 0);
            mTokens.Advance();
        }
        else
        {
            CompileExpression();
            ValidateTokenAdvance(';');
        }

        mVMWriter.WriteReturn();
    }

    public void CompileStringConst()
    {
        // FIXME - this is a HUGE memory leak allocating a string each reference - a system needs to be established when all static strings are allocated at static memory locations
        // 255, 254, 253, ... downward 
        string str = mTokens.Get().stringVal;
        int strLen = str.Length;
        mVMWriter.WritePush(VMWriter.Segment.CONST, strLen);
        mVMWriter.WriteCall("String.new", 1);
        for (int i = 0; i < strLen; i++)
        {
            mVMWriter.WritePush(VMWriter.Segment.CONST, str[i]);
            mVMWriter.WriteCall("String.appendChar", 2);
        }
        mTokens.Advance();
    }

    public bool CompileExpression()
    {
        // Grammar:
        // ---------
        // expression: term (op term)*
        // op: '+'|'-'|'*'|'/'|'&'|'|'|'<'|'>'|'='

        // Pseudo:
        // --------
        // if exp is a number n:
        //   push constant n

        // if exp is a variable var:
        //   push segment i

        // if exp is "exp1 op exp2":
        //   CompileExpression( exp1 );
        //   CompileExpression( exp2 );
        //   op command (add, sub, ... )

        // if exp is "op exp"
        //   CompileExpression( exp )
        //   op command (add, sub, ... )

        // if exp is "f(exp1, exp2, ..., expN )"
        //   N = CompileExpressionList()
        //   call f N

        bool compiledExpression = CompileTerm();

        Token token = mTokens.Get();

        if ( compiledExpression && Token.IsOp(token.symbol) )
        {
            // expression: term (op term)*
            char op = token.symbol;

            mTokens.Advance();
            CompileExpression();

            switch (op)
            {
                // op: '+'|'-'|'*'|'/'|'&'|'|'|'<'|'>'|'='
                case '+': mVMWriter.WriteArithmetic(VMWriter.Command.ADD); break;
                case '-': mVMWriter.WriteArithmetic(VMWriter.Command.SUB); break;
                case '*': mVMWriter.WriteCall("Math.multiply", 2); break;
                case '/': mVMWriter.WriteCall("Math.divide", 2); break;
                case '|': mVMWriter.WriteArithmetic(VMWriter.Command.OR); break;
                case '&': mVMWriter.WriteArithmetic(VMWriter.Command.AND); break;
                case '<': mVMWriter.WriteArithmetic(VMWriter.Command.LT); break;
                case '>': mVMWriter.WriteArithmetic(VMWriter.Command.GT); break;
                case '=': mVMWriter.WriteArithmetic(VMWriter.Command.EQ); break;
            }
        }

        return compiledExpression;
    }

    public bool CompileTerm()
    {
        // term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | identifier )
        // expressionParenth: '(' expression ')
        // unaryTerm: ('-'|'~') term
        // keywordConstant: 'true'|'false'|'null'|'this'
        // arrayValue: varName '[' expression ']'
        // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
        // expressionList: ( expression (',' expression)* )?

        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.Get();
        mTokens.Rollback(1);

        if (token.symbol == '(')
        {
            // expressionParenth: '(' expression ')
            ValidateTokenAdvance('(');
            CompileExpression();
            ValidateTokenAdvance(')');
            return true;
        }
        else if (token.symbol == '~' || token.symbol == '-')
        {
            // unaryTerm: ('-'|'~') term
            char symbol = token.symbol;
            mTokens.Advance();
            CompileExpression();
            if (symbol == '~')
            {
                mVMWriter.WriteArithmetic(VMWriter.Command.NOT);
            }
            else // symbol == '-' )
            {
                mVMWriter.WriteArithmetic(VMWriter.Command.NEG);
            }
            return true;
        }
        else if (token.type == Token.Type.INT_CONST)
        {
            // integer constant : e.g 723
            if (token.intVal < 0)
            {
                // negative value
                mVMWriter.WritePush(VMWriter.Segment.CONST, -token.intVal);
                mVMWriter.WriteArithmetic(VMWriter.Command.NEG);
            }
            else
            {
                // positive value
                mVMWriter.WritePush(VMWriter.Segment.CONST, token.intVal);
            }
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.STRING_CONST)
        {
            // string constant: e.g. "string constant"
            CompileStringConst();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
        {
            // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
            CompileSubroutineCall();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && tokenNext.symbol == '[')
        {
            // arrayValue: varName '[' expression ']'
            CompileArrayValue();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && SymbolTable.Exists(token.identifier))
        {
            // varName
            mVMWriter.WritePush(SymbolTable.SegmentOf(token.identifier), SymbolTable.OffsetOf(token.identifier));
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.TRUE)
        {
            // true
            mVMWriter.WritePush(VMWriter.Segment.CONST, 0);
            mVMWriter.WriteArithmetic(VMWriter.Command.NOT);
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.FALSE || token.keyword == Token.Keyword.NULL))
        {
            // false / null
            mVMWriter.WritePush(VMWriter.Segment.CONST, 0);
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.THIS)
        {
            // this
            mVMWriter.WritePush(VMWriter.Segment.POINTER, 0);
            mTokens.Advance();
            return true;
        }

        return false;
    }

    public int CompileExpressionList()
    {
        // expressionList: ( expression (',' expression)* )?
        int expressions = 0;

        while ( true )
        {
            if (CompileExpression())
            {
                expressions++;
            }

            if (mTokens.Get().symbol == ',')
            {
                mTokens.Advance();
                continue;
            }

            if (mTokens.Get().symbol == ')')
                break;
        }

        // return the number of expressions encountered
        return expressions;
    }

}