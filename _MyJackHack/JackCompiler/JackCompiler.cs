﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

class JackCompiler
{
    // Options - default false
    static public bool mComments = false;
    static public bool mVerbose = false;
    static public bool mRecursiveFolders = false;
    static public bool mInvertedConditions = false;

    // Options - default true
    static public bool mStaticStrings = true;
    static public bool mOSClasses = true;

    static string HACK_OS = ".HackOS.";

    static public Dictionary<string, Tokenizer> mTokenizerDic = new Dictionary<string, Tokenizer>();

    static void Main(string[] args)
    {
        List<string> paths = new List<string>();

        foreach (string arg in args)
        {
            string lwrArg = arg.ToLower();
            if (lwrArg == "-c")
                JackCompiler.mComments = !JackCompiler.mComments;
            else if (lwrArg == "-v")
                JackCompiler.mVerbose = !JackCompiler.mVerbose;
            else if (lwrArg == "-r")
                JackCompiler.mRecursiveFolders = !JackCompiler.mRecursiveFolders;
            else if (lwrArg == "-s")
                JackCompiler.mStaticStrings = !JackCompiler.mStaticStrings;
            else if (lwrArg == "-o")
                JackCompiler.mOSClasses = !JackCompiler.mOSClasses;
            else if (lwrArg == "-f")
                JackCompiler.mInvertedConditions = !JackCompiler.mInvertedConditions;
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
            FileAttributes attrib = File.GetAttributes(paths[i]);
            if (attrib.HasFlag(FileAttributes.Directory))
            {
                string[] files = Directory.GetFiles(paths[i], "*.jack");
                foreach (string file in files)
                {
                    if ( !paths.Contains( file ) && ( JackCompiler.mRecursiveFolders || !File.GetAttributes(file).HasFlag(FileAttributes.Directory) ) )
                        paths.Add(file);
                }
                paths.RemoveAt(i--);
            }
        }

        // Setup global scope symbol table
        SymbolTable.ScopePush( "global" );

        // Pre-process in 2 phases - this allows the compiler to realize all possible class types before compiling 
        Assembly asm = Assembly.GetExecutingAssembly();
        for ( int phase = 0; phase < 2; phase++ )
        {
            // Pre-process the operating system classes that are part of the compiler itself
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(HACK_OS))
                    continue;
                if (ClassInList(osName, paths))
                    continue;
                Stream resourceStream = asm.GetManifestResourceStream(osName);
                if (resourceStream != null)
                {
                    if (phase == 0)
                        Console.WriteLine("Preprocessing... " + osName);
                    StreamReader sRdr = new StreamReader(resourceStream);
                    PreProcessFile(osName, sRdr, phase);
                }
            }

            // Pre-process the target files
            for (int i = 0; i < paths.Count; i++)
            {
                if ( phase == 0 )
                    Console.WriteLine("Preprocessing... " + paths[i]);
                StreamReader sRdr = new StreamReader(paths[i]);
                PreProcessFile(paths[i], sRdr, phase);
            }
        }

        // Compile the files
        List<string> destFolders = new List<string>();
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Compiling... " + paths[i]);
            CompileFile(paths[i], GetOutBasefile(paths[i]));
            string destFolder = FileToPath( paths[i] );
            if ( !destFolders.Contains(destFolder))
            {
                destFolders.Add(destFolder);
            }
        }

        // Finally compile any OS classes that were referenced and any other OS classes those reference as well
        bool doneOS = false;
        Dictionary<string, bool> compiled = new Dictionary<string, bool>();
        while ( !doneOS && JackCompiler.mOSClasses )
        {
            doneOS = true;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(HACK_OS))
                    continue;
                if (ClassInList(osName, paths))
                    continue;
                foreach ( CompilationEngine.FuncSpec funcSpec in CompilationEngine.mFunctions.Values )
                {
                    if ( compiled.ContainsKey( osName ) )
                        break;
                    if ( funcSpec.filePath == osName && funcSpec.referenced && !funcSpec.compiled )
                    {
                        Console.WriteLine("Compiling... " + osName);
                        foreach ( string destFolder in destFolders )
                        {
                            CompileFile(osName, destFolder + FileToName(osName));
                            compiled.Add(osName, true);
                        }
                        doneOS = false;
                    }
                }
            }
        }
    }

    static void PreProcessFile(string filePath, StreamReader streamReader, int phase )
    {
        Tokenizer tokenizer;

        if (mTokenizerDic.ContainsKey(filePath))
        {
            tokenizer = mTokenizerDic[filePath];
        }
        else
        {
            tokenizer = new Tokenizer(streamReader);

            // Read all tokens into memory
            while (tokenizer.HasMoreTokens())
            {
                tokenizer.Advance();
            }
            tokenizer.Close();
            tokenizer.Reset();

            mTokenizerDic.Add(filePath, tokenizer);
        }

        CompilationEngine compiler = new CompilationEngine(tokenizer);
        compiler.CompilePrePass( filePath, phase );
    }

    static void CompileFile( string srcPath, string destPath )
    {
        Tokenizer tokenizer;
        if (mTokenizerDic.TryGetValue(srcPath, out tokenizer))
        {
            CompilationEngine compiler = new CompilationEngine( tokenizer, destPath );

            // Compile the tokens into output file
            compiler.Reset();
            compiler.CompileClass();
        }
        else
        {
            Console.WriteLine("Tokens not found for " + srcPath);
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

    public static bool ClassInList(string classFile, List<string> files)
    {
        string fileName = FileToName(classFile).ToLower();
        foreach (string file in files)
        {
            if ( fileName == FileToName(file).ToLower() )
                return true;
        }
        return false;
    }
}

class SymbolTable
{
    static List<SymbolScope> mScopes = new List<SymbolScope>();
    static SymbolTable mTheTable = new SymbolTable();
    static int mVarSize;

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

    public static void VarSizeBegin()
    {
        // Begins tracking the high water mark of VAR kind variables
        SymbolTable.mVarSize = 0;
    }

    public static int VarSizeEnd()
    {
        // Returns high water mark of VAR kind variables
        return SymbolTable.mVarSize;
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

        foreach (SymbolScope scope in mScopes)
        {
            foreach (Symbol symbol in scope.mSymbols.Values)
            {
                if (symbol.mKind == newVar.mKind)
                {
                    newVar.mOffset = newVar.mOffset + 1;
                }
            }
        }

        mScopes[mScopes.Count - 1].mSymbols.Add(varName, newVar);

        mVarSize = Math.Max( mVarSize, SymbolTable.KindSize(SymbolTable.Kind.VAR ) );
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
                    // in Hack all symbols are 1 word and size is measured in words
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
        DO, IF, ELSE, WHILE, FOR,
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
        strToKeyword.Add("for", Token.Keyword.FOR);
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
        symbols.Add('%', "%");

        // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
        // ( int values are C++ operator precedence https://en.cppreference.com/w/cpp/language/operator_precedence )
        ops = new Dictionary<char, int>();
        ops.Add('~', 3);
        ops.Add('*', 5); ops.Add('/', 5); ops.Add('%', 5);
        ops.Add('+', 6); ops.Add('-', 6);
        ops.Add('<', 9); ops.Add('>', 9);
        ops.Add('=', 10); // ==
        ops.Add('&', 11); 
        ops.Add('|', 13);

        statements = new Dictionary<Keyword, bool>();
        statements.Add(Token.Keyword.LET, true);
        statements.Add(Token.Keyword.DO, true);
        statements.Add(Token.Keyword.IF, true);
        statements.Add(Token.Keyword.ELSE, true);
        statements.Add(Token.Keyword.WHILE, true);
        statements.Add(Token.Keyword.FOR, true);
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
        if (type == Type.KEYWORD && ( keyword == Keyword.INT || keyword == Keyword.BOOL || keyword == Keyword.CHAR ) )
            return true;
        if (type == Type.IDENTIFIER && CompilationEngine.mClasses.Contains(identifier) )
            return true;
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
        public State( int tokenCurrent ) { mTokenCurrent = tokenCurrent; }
        public State() { mTokenCurrent = -1; }
        public bool IsDefined() { return mTokenCurrent >= 0; }
    }

    public Tokenizer(string fileInput)
    {
        Init(new StreamReader(fileInput));
    }

    public Tokenizer(StreamReader streamReader )
    {
        Init(streamReader);
    }

    protected void Init( StreamReader streamReader )
    {
        mFile = streamReader;
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
        Token result = Get();
        Advance();
        return result;
    }

    public Token GetAndRollback( int count = 1 )
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

// GRAMMAR DEFINITION
// Key: node* : zero or more of this node
//      nodeX | nodeY : nodeX or nodeY
//      node? : optional node
//      (node) : grouping
//      'node' : literal symbol or keyword

// CLASS
// class: 'class' className '{' classVarDec* subroutineDec* '}'
// classVarDec: ('static'|'field')? type varName ('=' expression)? (',' varName ('=' expression)? )* ';'

// FUNCTION
// type: 'int'|'char'|'boolean'|className
// subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody
// parameter: type varName
// parameterAdd: ',' type varName
// parameterList: ( parameter (',' parameter)* )?
// varDec: 'var'? type varName ('=' expression)? (',' varName ('=' expression)? )* ';'
// subroutineBody: '{' varDec* statements '}'

// STATEMENTS
// statements: statement*
// statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | varDec
// letStatement: ('let')? varName ('[' expression ']')? '=' expression ';'
// ifStatement: 'if' '(' expression ')' ( statement | '{' statements '}' ) ('else' ( statement | '{' statements '}' ) )?
// whileStatement: 'while' '(' expression ')' ( statement | '{' statements '}' )
// forStatement: 'for' '(' statement ';' expression; statement ')' ( statement | '{' statements '}' )
// doStatement: ('do')? subroutineCall ';'
// returnStatement: 'return' expression? ';'

// EXPRESSIONS
// expression: term (op term)*
// opTerm: op term
// expressionAdd: ',' expression
// expressionParenth: '(' expression ')
// arrayValue: varName '[' expression ']'
// term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | identifier )
// unaryTerm: unaryOp term
// subroutineObject: ( className | varName ) '.'
// subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')'
// expressionList: ( expression (',' expression)* )?
// op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
// unaryOp: '-' | '~'
// keywordConstant: 'true'|'false'|'null'|'this'

class Writer
{
    protected StreamWriter mFile;
    protected bool mEnabled = true;
    protected List<StreamWriter> mOutput = new List<StreamWriter>();

    public Writer(string outFile)
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
        mOutput.Add( mFile );
    }

    public virtual void WriteLine(string line)
    {
        if( mEnabled )
        {
            if (JackCompiler.mVerbose)
                Console.WriteLine(line);
            mOutput[mOutput.Count - 1].WriteLine(line);
        }
    }

    public void Enable()
    {
        mEnabled = true;
    }

    public void Disable()
    {
        mEnabled = false;
    }

    public void OutputPush(Stream stream)
    {
        mOutput.Add(new StreamWriter(stream));
        mOutput[mOutput.Count - 1].AutoFlush = true;
    }
    
    public void OutputPop()
    { 
        if ( mOutput.Count > 1 )
        {
            mOutput[mOutput.Count - 1].Flush();
            mOutput.RemoveAt(mOutput.Count - 1);
        }
    }

    public void WriteStream(Stream stream)
    {
        StreamReader reader = new StreamReader(stream);
        stream.Seek( 0, SeekOrigin.Begin );
        while ( !reader.EndOfStream )
        {
            string line = reader.ReadLine();
            mOutput[mOutput.Count - 1].WriteLine( line );
        }
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
        // mark this function as referenced
        CompilationEngine.FuncSpec funcSpec;
        if ( CompilationEngine.mFunctions.TryGetValue( function, out funcSpec ) )
        {
            funcSpec.referenced = true;
        }

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
    static public List<string> mClasses = new List<string>(); // list of known class types
    static public Dictionary<string, FuncSpec> mFunctions = new Dictionary<string, FuncSpec>(); // dictionary of function specs
    static public Dictionary<string, int> mStrings = new Dictionary<string, int>(); // static strings

    Tokenizer mTokens;
    VMWriter mVMWriter;
    string mClassName;
    string mFuncName;
    Dictionary<string, int> mFuncLabel = new Dictionary<string, int>();

    public class FuncSpec
    {
        public string filePath;
        public string funcName;
        public string className;
        public Token.Keyword type;
        public List<Token> parmTypes;
        public Token returnType;
        public bool referenced;
        public bool compiled;
    };

    public class ExpNode
    {
        public object value;
        public ExpNode parent;
        public ExpNode left;
        public ExpNode right;

        public ExpNode(object expObj, ExpNode parentIn = null )
        {
            parent = parentIn;
            value = expObj;
        }
    };

    public CompilationEngine(Tokenizer tokens, string baseOutFile)
    {
        mTokens = tokens;

        Reset();

        mVMWriter = new VMWriter(baseOutFile + ".vm");
    }

    public CompilationEngine(Tokenizer tokens)
    {
        mTokens = tokens;
        Reset();
    }

    public void CompilePrePass( string filePath, int phase )
    {
        ValidateTokenAdvance(Token.Keyword.CLASS);
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);

        if (!mClasses.Contains(mClassName))
            mClasses.Add(mClassName);

        if (phase == 0)
            return;

        Token token = mTokens.Advance();

        while ( token != null )
        {
            if ( token.type == Token.Type.STRING_CONST )
            {
                if ( !mStrings.ContainsKey( token.stringVal ) )
                    mStrings.Add( token.stringVal, mStrings.Count );
            }
            else if ( token.keyword == Token.Keyword.METHOD || token.keyword == Token.Keyword.FUNCTION || token.keyword == Token.Keyword.CONSTRUCTOR )
            {
                // Register which functions are methods vs functions so that we now how to call them when they are encountered while compiling
                FuncSpec spec = new FuncSpec();
                spec.className = mClassName;
                spec.type = token.keyword;
                spec.filePath = filePath;

                mTokens.Advance();
                spec.returnType = mTokens.Get();
                mTokens.Advance();
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out spec.funcName );
                ValidateTokenAdvance( '(' );
                spec.parmTypes = CompileParameterList( false );

                if ( !mFunctions.ContainsKey( spec.className + "." + spec.funcName ) )
                    mFunctions.Add( spec.className + "." + spec.funcName, spec );
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

    public bool ValidateSymbol( string varName )
    {
        if (SymbolTable.SegmentOf(varName) == VMWriter.Segment.INVALID)
        {
            Error("Undefined symbol '" + varName + "'");
            System.Environment.Exit(-1);
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
        if ( varType.IsType() )
            return true;

        return varType.keyword == Token.Keyword.VOID;
    }

    public string NewFuncFlowLabel( string info )
    {
        if (!mFuncLabel.ContainsKey(info))
            mFuncLabel.Add(info, 0);
        string result = mClassName + "_" + mFuncName + "_" + info + "_L" + ++mFuncLabel[info];
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

    public void CompileVarDecSet(Token varType, SymbolTable.Kind varKind, bool defineVars = true )
    {
        do
        {
            mTokens.Advance();

            string varName;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            if ( defineVars )
                SymbolTable.Define(varName, varType, varKind);

            if (mTokens.Get().symbol == '=')
            {
                ValidateTokenAdvance('=');

                CompileExpression(); // push value onto stack

                if ( ValidateSymbol( varName ) )
                    mVMWriter.WritePop(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
            }

        } while (mTokens.Get().symbol == ',');
    }

    public bool CompileClassVarDec()
    {
        // compiles class fields and static vars
        // classVarDec: ('static'|'field)? type varName (',' varName)* ';'

        SymbolTable.Kind varKind = SymbolTable.Kind.FIELD;
        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.GetAndRollback();

        if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.FIELD || token.keyword == Token.Keyword.STATIC))
        {
            varKind = (token.keyword == Token.Keyword.STATIC) ? SymbolTable.Kind.STATIC : SymbolTable.Kind.FIELD;
            token = mTokens.Advance();
            tokenNext = mTokens.AdvanceAndRollback();
        }
        
        if ( token.IsType() && tokenNext.type == Token.Type.IDENTIFIER )
        {
            Token varType = mTokens.Get();

            CompileVarDecSet( varType, varKind );

            token = ValidateTokenAdvance(';');

            return true;
        }

        return false;
    }

    public bool CompileVarDec( bool eatSemiColon = true, bool defineVars = true )
    {
        // handles local variables in a subroutine
        // varDec: 'var'? type varName (',' varName)* ';'

        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.GetAndRollback();

        if (token.keyword == Token.Keyword.VAR)
        {
            token = mTokens.Advance();
            tokenNext = mTokens.AdvanceAndRollback();
        }

        if (token.IsType() && tokenNext.type == Token.Type.IDENTIFIER)
        {
            Token varType = mTokens.Get();

            CompileVarDecSet(varType, SymbolTable.Kind.VAR, defineVars );

            if ( eatSemiColon )
                ValidateTokenAdvance(';');

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

            SymbolTable.ScopePush("function", funcCallType == Token.Keyword.METHOD);

            ValidateTokenAdvance('(');

            List<Token> parameterTypes = CompileParameterList();

            ValidateTokenAdvance(')');

            ValidateTokenAdvance('{');

            Tokenizer.State tokenStart = null;
            int varSize = 0;

            for (int stage = 0; stage < 2; stage++)
            {
                switch (stage)
                {
                    case 0: // pre-compile to find out how much local var space is needed
                        tokenStart = mTokens.StateGet();
                        mVMWriter.Disable();
                        SymbolTable.VarSizeBegin();
                        CompileStatements( true );
                        varSize = SymbolTable.VarSizeEnd();
                        break;

                    case 1: // compile the function
                        mTokens.StateSet(tokenStart);
                        mVMWriter.Enable();

                        // Compile function beginning
                        mVMWriter.WriteFunction(mClassName + "." + mFuncName, varSize);
                        if (funcCallType == Token.Keyword.CONSTRUCTOR || funcCallType == Token.Keyword.METHOD)
                        {
                            if (funcCallType == Token.Keyword.CONSTRUCTOR)
                            {
                                // Alloc "this" ( and it is pushed onto the stack )
                                mVMWriter.WritePush( VMWriter.Segment.CONST, SymbolTable.KindSize(SymbolTable.Kind.FIELD) );
                                mVMWriter.WriteCall("Memory.alloc", 1);
                            }

                            if (funcCallType == Token.Keyword.METHOD)
                            {
                                // grab argument 0 (this) and push it on the stack
                                mVMWriter.WritePush(VMWriter.Segment.ARG, 0);
                            }

                            // pop "this" off the stack
                            mVMWriter.WritePop(VMWriter.Segment.POINTER, 0);
                        }

                        // Before starting with Main.main, inject the allocation of all the static string constants
                        if ( mClassName == "Main" && mFuncName == "main" )
                        {
                            CompileStaticStrings();
                        }

                        bool compiledReturn = CompileStatements( false );

                        FuncSpec funcSpec;
                        if (CompilationEngine.mFunctions.TryGetValue(mClassName + "." + mFuncName, out funcSpec))
                        {
                            funcSpec.compiled = true;

                            if (!compiledReturn)
                            {
                                mVMWriter.WriteReturn();
                                if (funcSpec.returnType.keyword != Token.Keyword.VOID)
                                    Error("Subroutine " + mClassName + "." + mFuncName + " missing return value");
                            }
                        }

                        break;
                }
            }

            ValidateTokenAdvance('}');

            SymbolTable.ScopePop(); // "function"

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
        FuncSpec funcSpec = null;

        ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
        if (mTokens.Get().symbol == '.')
        {
            objectName = subroutineName;
            mTokens.Advance();
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
        }

        ValidateTokenAdvance('(');

        // Only for functions that are methods, we need to push the this pointer as first argument
        if ( objectName == null && CompilationEngine.mFunctions.ContainsKey(mClassName + "." + subroutineName) )
        {
            funcSpec = CompilationEngine.mFunctions[mClassName + "." + subroutineName];

            if ( funcSpec.type != Token.Keyword.METHOD )
            {
                Error("Calling function as a method '" + subroutineName + "'");
            }

            // push pointer to object (this for object)
            mVMWriter.WritePush(VMWriter.Segment.POINTER, 0); // this
            argCount = argCount + 1;
        }
        else if ( SymbolTable.Exists(objectName) && CompilationEngine.mFunctions.ContainsKey( SymbolTable.TypeOf( objectName ) + "." + subroutineName ) )
        {
            funcSpec = CompilationEngine.mFunctions[SymbolTable.TypeOf(objectName) + "." + subroutineName];

            if (funcSpec.type != Token.Keyword.METHOD)
            {
                Error("Calling function as a method '" + objectName + "." + subroutineName + "' (use " + SymbolTable.TypeOf(objectName) + "." + subroutineName + ")");
            }

            // push pointer to object (this for object)
            mVMWriter.WritePush(SymbolTable.SegmentOf(objectName), SymbolTable.OffsetOf(objectName)); // object pointer
            argCount = argCount + 1;
        }
        else if ( objectName != null && CompilationEngine.mFunctions.ContainsKey(objectName + "." + subroutineName))
        {
            funcSpec = CompilationEngine.mFunctions[objectName + "." + subroutineName];
            if (funcSpec.type == Token.Keyword.METHOD )
            {
                Error("Calling method as a function '" + subroutineName + "'");
            }
        }
        else
        {
            if ( objectName != null )
                Error("Calling unknown function '" + objectName + "." + subroutineName + "' (check case)");
            else
                Error("Calling unknown function '" + subroutineName + "' (check case)");
        }

        int expressionCount = CompileExpressionList();

        if (funcSpec != null)
        {
            if (funcSpec.parmTypes.Count != expressionCount)
            {
                Error(funcSpec.type.ToString() + " " + subroutineName + " expects " + funcSpec.parmTypes.Count + " parameters.");
            }
        }

        argCount = argCount + expressionCount;

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
            mVMWriter.WriteCall( mClassName + "." + subroutineName, argCount);
        }
    }

    public List<Token> CompileParameterList( bool doCompile = true )
    {
        List<Token> result = new List<Token>();

        // compiles a parameter list within () without dealing with the ()s
        // can be completely empty

        // parameterList: ( type varName (',' type varName)* )?
        while ( mTokens.Get().IsType() )
        {
            // handle argument
            Token varType = mTokens.GetAndAdvance();

            result.Add( varType );

            string varName;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            if ( doCompile )
            {
                SymbolTable.Define(varName, varType, SymbolTable.Kind.ARG);
            }

            if (mTokens.Get().symbol != ',')
                break;

            mTokens.Advance();
        }

        return result;
    }

    public bool CompileStatements( bool defineVars = true )
    {
        // compiles a series of statements without handling the enclosing {}s

        // statements: statement*

        bool resultReturnCompiled = false;
        bool returnCompiled = false;

        while ( CompileStatementSingle( out returnCompiled, true, defineVars ) )
        {
            // keep compiling more statements
            resultReturnCompiled = resultReturnCompiled || returnCompiled;
        }

        return resultReturnCompiled;
    }

    public bool CompileStatementSingle( out bool returnCompiled, bool eatSemiColon = true, bool defineVars = true )
    {
        // compiles a series of statements without handling the enclosing {}s

        // statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | varDec

        returnCompiled = false;

        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.GetAndRollback();

        switch (token.keyword)
        {
            case Token.Keyword.IF:
                CompileStatementIf();
                return true;

            case Token.Keyword.WHILE:
                CompileStatementWhile();
                return true;

            case Token.Keyword.FOR:
                CompileStatementFor();
                return true;

            case Token.Keyword.RETURN:
                CompileStatementReturn();
                returnCompiled = true;
                return true;

            case Token.Keyword.DO:
                CompileStatementDo(true, eatSemiColon);
                return true;

            case Token.Keyword.LET:
                CompileStatementLet(true, eatSemiColon);
                return true;

            default:
                // Check for non-keyword do/let/varDec
                if ( token.keyword == Token.Keyword.VAR || ( token.IsType() && tokenNext.type == Token.Type.IDENTIFIER ) )
                {
                    CompileVarDec( eatSemiColon, defineVars );
                    return true;
                }
                else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '=' || tokenNext.symbol == '['))
                {
                    CompileStatementLet(false, eatSemiColon);
                    return true;
                }
                else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
                {
                    CompileStatementDo(false, eatSemiColon);
                    return true;
                }

                return false;
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
        if ( ValidateSymbol( varName ) )
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

    public void CompileStatementLet(bool eatKeyword = true, bool eatSemiColon = true )
    {
        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(Token.Keyword.LET);
        }

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
            if ( ValidateSymbol(varName) )
                mVMWriter.WritePop(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
        }

        if ( eatSemiColon )
            ValidateTokenAdvance(';');
    }

    public void CompileStatementDo( bool eatKeyword = true, bool eatSemiColon = true )
    {
        // doStatement: 'do' subroutineCall ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(Token.Keyword.DO);
        }

        CompileSubroutineCall();

        mVMWriter.WritePop( VMWriter.Segment.TEMP, 0 ); // ignore return value

        if (eatSemiColon)
            ValidateTokenAdvance(';');
    }

    public void CompileStatementIf()
    {
        // ifStatement: 'if' '(' expression ')' ( statement | '{' statements '}' ) ('else' ( statement | '{' statements '}' ) )?

        ValidateTokenAdvance(Token.Keyword.IF);
        ValidateTokenAdvance('(');

        // invert the expression to make the jumps simpler
        CompileExpression();
        if ( JackCompiler.mInvertedConditions )
            mVMWriter.WriteArithmetic(VMWriter.Command.NOT);

        ValidateTokenAdvance(')');

        string labelFalse = NewFuncFlowLabel( "IF_FALSE" );
        string labelTrue = null;
        string labelEnd = null;

        if (JackCompiler.mInvertedConditions)
        {
            mVMWriter.WriteIfGoto(labelFalse);
        }
        else
        {
            labelTrue = NewFuncFlowLabel( "IF_TRUE" );
            mVMWriter.WriteIfGoto(labelTrue);
            mVMWriter.WriteGoto(labelFalse);
            mVMWriter.WriteLabel(labelTrue);
        }

        bool returnCompiled;

        if (mTokens.Get().symbol == '{')
        {
            ValidateTokenAdvance('{');

            SymbolTable.ScopePush( "statements", false );

            CompileStatements();

            SymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle( out returnCompiled );
        }

        if (mTokens.Get().keyword == Token.Keyword.ELSE)
        {
            labelEnd = NewFuncFlowLabel("IF_END");
            mVMWriter.WriteGoto(labelEnd);
        }

        mVMWriter.WriteLabel(labelFalse);

        if (mTokens.Get().keyword == Token.Keyword.ELSE)
        {
            mTokens.Advance();

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');

                SymbolTable.ScopePush("statements", false);

                CompileStatements();

                SymbolTable.ScopePop();

                ValidateTokenAdvance('}');
            }
            else
            {
                CompileStatementSingle( out returnCompiled );
            }

            mVMWriter.WriteLabel(labelEnd);
        }
    }

    public void CompileStatementWhile()
    {
        // whileStatement: 'while' '(' expression ')' ( statement | '{' statements '}' )

        ValidateTokenAdvance(Token.Keyword.WHILE);

        string labelExp = NewFuncFlowLabel( "WHILE_EXP" );
        string labelEnd = NewFuncFlowLabel( "WHILE_END" );

        mVMWriter.WriteLabel(labelExp);

        // invert the expression to make the jumps simpler
        ValidateTokenAdvance('(');
        CompileExpression();
        ValidateTokenAdvance(')');

        mVMWriter.WriteArithmetic(VMWriter.Command.NOT);
        mVMWriter.WriteIfGoto(labelEnd);

        bool returnCompiled;

        if (mTokens.Get().symbol == '{')
        {
            ValidateTokenAdvance('{');

            SymbolTable.ScopePush("statements", false);

            CompileStatements();

            SymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        mVMWriter.WriteGoto(labelExp);

        mVMWriter.WriteLabel(labelEnd);

    }

    public void CompileStatementFor()
    {
        // forStatement: 'for' '(' statements ';' expression; statements ')' ( statement | '{' statements '}' )

        bool returnCompiled = false;

        ValidateTokenAdvance(Token.Keyword.FOR);
        ValidateTokenAdvance('(');

        string labelExp = NewFuncFlowLabel("FOR_EXP");
        string labelEnd = NewFuncFlowLabel("FOR_END");
        string labelInc = NewFuncFlowLabel("FOR_INC");
        string labelBody = NewFuncFlowLabel("FOR_BODY");

        SymbolTable.ScopePush("forStatement", false);

        CompileStatementSingle(out returnCompiled, false);

        ValidateTokenAdvance(';');

        mVMWriter.WriteLabel(labelExp);

        CompileExpression();

        mVMWriter.WriteIfGoto( labelBody );

        mVMWriter.WriteGoto(labelEnd);

        ValidateTokenAdvance(';');

        mVMWriter.WriteLabel(labelInc);

        CompileStatementSingle( out returnCompiled, false );

        mVMWriter.WriteGoto(labelExp);

        ValidateTokenAdvance(')');

        mVMWriter.WriteLabel(labelBody);

        if (mTokens.Get().symbol == '{')
        {
            ValidateTokenAdvance('{');

            SymbolTable.ScopePush("statements", false);

            CompileStatements();

            SymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        mVMWriter.WriteGoto(labelInc);

        mVMWriter.WriteLabel(labelEnd);

        SymbolTable.ScopePop(); // "forStatement"
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
        string str;

        ValidateTokenAdvance(Token.Type.STRING_CONST, out str);

        if ( JackCompiler.mStaticStrings && JackCompiler.mOSClasses )
        {
            // Precompiled static strings
            int strIndex;

            if (CompilationEngine.mStrings.TryGetValue(str, out strIndex))
            {
                mVMWriter.WritePush(VMWriter.Segment.CONST, strIndex);
                mVMWriter.WriteCall("String.staticGet", 1);
            }
            else
            {
                Error("String not found '" + str + "'");
            }
        }
        else
        {
            // On the fly string creation (HUGE MEMORY LEAK)
            mVMWriter.WritePush(VMWriter.Segment.CONST, str.Length);
            mVMWriter.WriteCall("String.new", 1);
            for (int i = 0; i < str.Length; i++)
            {
                mVMWriter.WritePush(VMWriter.Segment.CONST, str[i]);
                mVMWriter.WriteCall("String.appendChar", 2);
            }
        }
    }

    public void CompileStaticStrings()
    {
        if ( JackCompiler.mStaticStrings && JackCompiler.mOSClasses )
        {
            mVMWriter.WriteLine("/* Static String Allocation (Inserted by the compiler at the beginning of Main.main) */");

            mVMWriter.WritePush(VMWriter.Segment.CONST, CompilationEngine.mStrings.Keys.Count );
            mVMWriter.WriteCall("String.staticAlloc", 1);

            foreach (string staticString in CompilationEngine.mStrings.Keys)
            {
                int strLen = staticString.Length;
                mVMWriter.WriteLine("// \"" + staticString + "\"");
                mVMWriter.WritePush(VMWriter.Segment.CONST, strLen);
                mVMWriter.WriteCall("String.new", 1);
                for (int i = 0; i < strLen; i++)
                {
                    mVMWriter.WritePush(VMWriter.Segment.CONST, staticString[i]);
                    mVMWriter.WriteCall("String.appendChar", 2);
                }
                mVMWriter.WritePush(VMWriter.Segment.CONST, CompilationEngine.mStrings[staticString]);
                mVMWriter.WriteCall("String.staticSet", 2);
            }

            mVMWriter.WriteLine("/* Main.main statements begin ... */");
        }
    }

    public bool CompileExpression( List<object> expressionTerms = null )
    {
        // Grammar:
        // ---------
        // expression: term (op term)*
        // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'

        bool doResolve = false;
        if ( expressionTerms == null )
        {
            doResolve = true;
            expressionTerms = new List<object>();
        }

        // Re-direct the VM Writer to write to a memory file to hold the output for each term
        MemoryStream memFile = new MemoryStream();
        mVMWriter.OutputPush( memFile );

        bool compiledExpression = CompileTerm();

        mVMWriter.OutputPop();

        Token token = mTokens.Get();

        if (compiledExpression)
        {
            expressionTerms.Add(memFile);

            if (Token.IsOp(token.symbol))
            {
                expressionTerms.Add(token.symbol);

                mTokens.Advance();
                CompileExpression(expressionTerms);
            }
        }

        if ( doResolve && expressionTerms.Count > 0 )
        {
            ExpressionResolvePrecedence( expressionTerms );
        }

        return compiledExpression;
    }

    protected int ExpressionPrecCompare(object x, object y)
    {
        if (x == null && y != null)
            return -1; // null < non-null

        if (x != null && y == null)
            return 1; // non-null > null

        if (x == null && y == null)
            return 0; // null == null

        bool isXOp = x.GetType() == typeof(char);
        bool isYOp = y.GetType() == typeof(char);

        if (isXOp && isYOp)
        {
            // same operator is left-associative: + > +
            if ((char)x == (char)y)
                return 1;

            int delta = Token.OpPrecedence((char)x) - Token.OpPrecedence((char)y);
            return -delta;
        }

        if ( !isXOp && isYOp)
            return 1; // term > op

        if (isXOp && !isYOp)
            return -1; // op < term

        return 0;
    }

    protected object ExpressionTopOp( List<object> stack )
    {
        object a = null;
        for (int sp = stack.Count - 1; sp >= 0; sp--)
        {
            if (stack[sp].GetType() == typeof(char))
            {
                a = stack[sp];
                break;
            }
        }
        return a;
    }

    protected void ExpressionResolvePrecedence(List<object> expressionTerms)
    {
        // expressionTerms is a list of term? (op term)* that needs to be resolved with operator prededence

        // This will always be either empty or an odd number of entries always following term op term op term ...
        // Each term is a pre-compiled set of VM commands before arriving here
        // 0: (do nothing)
        // 1: x
        // 3: x + y
        // 5: x + y * 5
        // 7: x + y * 5 - 6 = 9
        // etc...

        if (expressionTerms.Count == 1)
        {
            mVMWriter.WriteStream((Stream)expressionTerms[0]);
        }
        else if( expressionTerms.Count > 0 )
        {
            // Handle operator precedence as a form of what is explained here:
            // https://en.wikipedia.org/wiki/Operator-precedence_grammar

            int ip = 0;
            List<object> stack = new List<object>();
            object a, b, opPopped = null;

            while ( ip < expressionTerms.Count || stack.Count > 0 )
            {
                // Let a be the top terminal on the stack, and b the symbol pointed to by ip
                b = ip < expressionTerms.Count ? expressionTerms[ip] : null;
                a = ExpressionTopOp(stack);

                if (a == null && b == null)
                    return;

                // if a < b or a == b then
                if ( ExpressionPrecCompare( a, b ) <= 0 )
                {
                    // push b onto the stack
                    stack.Add(b);
                    // advance ip to the next input symbol
                    ip++;
                }
                else // a > b
                {
                    // repeat
                    //   pop the stack
                    // until the top stack terminal is related by < to the terminal most recently popped
                    do
                    {
                        if (stack[stack.Count - 3].GetType().IsSubclassOf( typeof( Stream ) ) ) 
                            mVMWriter.WriteStream((Stream)stack[stack.Count - 3]);
                        if (stack[stack.Count - 1].GetType().IsSubclassOf(typeof(Stream)))
                            mVMWriter.WriteStream((Stream)stack[stack.Count - 1]);
                        CompileOp((char)stack[stack.Count - 2]);
                        opPopped = stack[stack.Count - 2];
                        stack.RemoveAt(stack.Count - 1);
                        stack.RemoveAt(stack.Count - 1);
                        stack.RemoveAt(stack.Count - 1);
                        stack.Add("stackValue"); // placeholder to write nothing when it is encountered

                    } while (stack.Count >= 3 && ExpressionPrecCompare(ExpressionTopOp(stack), opPopped) < 0);
                }
            }
        }
    }

    public void CompileOp(char op)
    {
        switch ( op )
        {
            // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
            case '+': mVMWriter.WriteArithmetic(VMWriter.Command.ADD); break;
            case '-': mVMWriter.WriteArithmetic(VMWriter.Command.SUB); break;
            case '*': mVMWriter.WriteCall("Math.multiply", 2); break;
            case '/': mVMWriter.WriteCall("Math.divide", 2); break;
            case '%': mVMWriter.WriteCall("Math.mod", 2); break;
            case '|': mVMWriter.WriteArithmetic(VMWriter.Command.OR); break;
            case '&': mVMWriter.WriteArithmetic(VMWriter.Command.AND); break;
            case '<': mVMWriter.WriteArithmetic(VMWriter.Command.LT); break;
            case '>': mVMWriter.WriteArithmetic(VMWriter.Command.GT); break;
            case '=': mVMWriter.WriteArithmetic(VMWriter.Command.EQ); break;
        }
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
            if ( ValidateSymbol(token.identifier) )
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