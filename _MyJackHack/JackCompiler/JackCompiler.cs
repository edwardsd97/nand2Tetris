using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

class JackCompiler
{
    static public bool mComments = false;
    static public bool mVerbose = false;
    static public bool mRecursiveFolders = false;
    static public bool mStaticStrings = false;
    static public bool mOSClasses = false;
    static public bool mInvertedConditions = false;

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
            else if (lwrArg == "-r")
                JackCompiler.mRecursiveFolders = true;
            else if (lwrArg == "-s")
                JackCompiler.mStaticStrings = true;
            else if (lwrArg == "-o")
                JackCompiler.mOSClasses = true;
            else if (lwrArg == "-f")
                JackCompiler.mInvertedConditions = true;
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
                    if ( !paths.Contains( file ) && ( JackCompiler.mRecursiveFolders || !File.GetAttributes(file).HasFlag(FileAttributes.Directory) ) )
                        paths.Add(file);
                }
                paths.RemoveAt(i--);
            }
        }

        // Setup global scope symbol table
        SymbolTable.ScopePush( "global" );

        // Pre-process the operating system classes that are part of the compiler itself
        Assembly asm = Assembly.GetExecutingAssembly();
        foreach (string osName in asm.GetManifestResourceNames())
        {
            if (!osName.Contains(".OS."))
                continue;
            Stream resourceStream = asm.GetManifestResourceStream( osName );
            if (resourceStream != null)
            {
                Console.WriteLine("Preprocessing... " + osName);
                StreamReader sRdr = new StreamReader(resourceStream);
                PreProcessFile(osName, sRdr);
            }
        }

        // Pre-process the target files
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Preprocessing... " + (string)paths[i]);
            StreamReader sRdr = new StreamReader((string)paths[i]);
            PreProcessFile((string)paths[i], sRdr);
        }

        // Process the files
        List<string> destFolders = new List<string>();
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Compiling... " + (string)paths[i]);
            CompileFile((string)paths[i], GetOutBasefile((string)paths[i]));
            string destFolder = FileToPath( (string)paths[i] );
            if ( !destFolders.Contains(destFolder))
            {
                destFolders.Add(destFolder);
            }
        }

        // Finally compile any OS classes that were referenced and any other OS classes those reference as well
        bool doneOS = false;
        while ( !doneOS && JackCompiler.mOSClasses )
        {
            doneOS = true;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".OS."))
                    continue;
                foreach( CompilationEngine.FuncSpec funcSpec in CompilationEngine.mFunctions.Values )
                {
                    if ( funcSpec.filePath == osName && funcSpec.referenced && !funcSpec.compiled )
                    {
                        Console.WriteLine("Compiling... " + osName);
                        foreach ( string destFolder in destFolders )
                        {
                            CompileFile(osName, destFolder + FileToName(osName));
                        }
                        doneOS = false;
                    }
                }
            }
        }
    }

    static void PreProcessFile(string filePath, StreamReader streamReader)
    {
        Tokenizer tokenizer = new Tokenizer(streamReader);

        // Read all tokens into memory
        while (tokenizer.HasMoreTokens())
        {
            tokenizer.Advance();
        }
        tokenizer.Close();
        tokenizer.Reset();

        if ( !mTokenizerDic.ContainsKey( filePath ) )
            mTokenizerDic.Add(filePath, tokenizer);

        CompilationEngine compiler = new CompilationEngine(tokenizer);
        compiler.CompilePrePass( filePath );
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

// GRAMMAR DEFINITION

// CLASS
// class: 'class' className '{' clasVarDec* subroutineDec* '}'
// classVarDec: ('static'|'field) type varName (',' varName)* ';'

// FUNCTION
// varDecAdd: ',' varName
// type: 'int'|'char'|'boolean'|className
// subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody
// parameter: type varName
// parameterAdd: ',' type varName
// parameterList: ( parameter (',' parameter)* )?
// subroutineBody: '{' varDec* statements '}'
// varDec: 'var' type varName (',' varName)* ';'

// STATEMENTS
// statements: statement*
// statement: letStatement | ifStatement | whileStatement | doStatement | returnStatement
// arrayIndex: '[' expression ']'
// arrayValue: varName '[' expression ']'
// elseClause: 'else' '{' statements '}'
// letStatement: ('let')? varName ('[' expression ']')? '=' expression ';'
// ifStatement: 'if' '(' expression ')' ( statement | '{' statements '}' ) ('else' ( statement | '{' statements '}' ) )?
// whileStatement: 'while' '(' expression ')' ( statement | '{' statements '}' )
// doStatement: ('do')? subroutineCall ';'
// returnStatement: 'return' expression? ';'

// EXPRESSIONS
// opTerm: op term
// expressionAdd: ',' expression
// expression: term (op term)*
// expressionParenth: '(' expression ')
// term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | identifier )
// unaryTerm: unaryOp term
// subroutineObject: ( className | varName ) '.'
// subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
// expressionList: ( expression (',' expression)* )?
// op: '+'|'-'|'*'|'/'|'&'|'|'|'<'|'>'|'='
// unaryOp: '-'|'~'
// keywordConstant: 'true'|'false'|'null'|'this'

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
    static public Dictionary<string, FuncSpec> mFunctions = new Dictionary<string, FuncSpec>();
    static public Dictionary<string, int> mStrings = new Dictionary<string, int>();

    Tokenizer mTokens;
    VMWriter mVMWriter;
    string mClassName;
    string mFuncName;
    Dictionary<string, int> mFuncLabel = new Dictionary<string, int>();

    public class FuncSpec
    {
        public string           filePath;
        public string           funcName;
        public string           className;
        public Token.Keyword    type;
        public List<Token>      parmTypes;
        public Token            returnType;
        public bool             referenced;
        public bool             compiled;
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

    public void CompilePrePass( string filePath )
    {
        ValidateTokenAdvance(Token.Keyword.CLASS);
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);

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

            List<Token> parameterTypes = CompileParameterList();

            ValidateTokenAdvance(')');

            ValidateTokenAdvance('{');

            while (CompileVarDec())
            {
                // keep going with more varDec
            }

            // Compile function beginning
            mFuncLabel = new Dictionary<string, int>();
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

            // Before starting with Main.main, inject the allocation of all the static string constants
            if (mClassName == "Main" && mFuncName == "main")
            {
                CompileStaticStrings();
            }

            bool compiledReturn = CompileStatements();

            FuncSpec funcSpec;
            if (CompilationEngine.mFunctions.TryGetValue(mClassName + "." + mFuncName, out funcSpec))
            {
                funcSpec.compiled = true;

                if (!compiledReturn)
                {
                    mVMWriter.WriteReturn();
                    if ( funcSpec.returnType.keyword != Token.Keyword.VOID )
                        Error("Subroutine " + mClassName + "." + mFuncName  + " missing return value");
                }
            }

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
        while (ValidateVarType(mTokens.Get()))
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

    public bool CompileStatements()
    {
        // compiles a series of statements without handling the enclosing {}s

        // statements: statement*

        bool resultReturnCompiled = false;
        bool returnCompiled = false;

        while ( CompileStatementSingle( out returnCompiled ) )
        {
            // keep compiling more statements
            resultReturnCompiled = resultReturnCompiled || returnCompiled;
        }

        return resultReturnCompiled;
    }

    public bool CompileStatementSingle( out bool returnCompiled )
    {
        // compiles a series of statements without handling the enclosing {}s

        // statement: letStatement | ifStatement | whileStatement | doStatement | returnStatement

        returnCompiled = false;

        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.Get();
        mTokens.Rollback(1);

        switch (token.keyword)
        {
            case Token.Keyword.IF:
                CompileStatementIf();
                return true;

            case Token.Keyword.WHILE:
                CompileStatementWhile();
                return true;

            case Token.Keyword.RETURN:
                CompileStatementReturn();
                returnCompiled = true;
                return true;

            case Token.Keyword.DO:
                CompileStatementDo(true);
                return true;

            case Token.Keyword.LET:
                CompileStatementLet(true);
                return true;

            default:
                // Check for non-keyword do/let
                if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '=' || tokenNext.symbol == '['))
                {
                    CompileStatementLet(false);
                    return true;
                }
                else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
                {
                    CompileStatementDo(false);
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

    public void CompileStatementLet(bool eatKeyword = true)
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
            mVMWriter.WritePop(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
        }

        ValidateTokenAdvance(';');
    }

    public void CompileStatementDo( bool eatKeyword = true )
    {
        // doStatement: 'do' subroutineCall ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(Token.Keyword.DO);
        }

        CompileSubroutineCall();

        mVMWriter.WritePop( VMWriter.Segment.TEMP, 0 ); // ignore return value

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

            CompileStatements();

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

                CompileStatements();

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

            CompileStatements();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        mVMWriter.WriteGoto(labelExp);

        mVMWriter.WriteLabel(labelEnd);

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

        if (JackCompiler.mStaticStrings)
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
        if ( JackCompiler.mStaticStrings )
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