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

        // Process the files
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Compiling... " + (string)paths[i]);
            ProcessFile((string)paths[i]);
        }
    }

    static void ProcessFile(string path)
    {
        Tokenizer tokenizer = new Tokenizer(path);

        // Read all tokens into memory
        while (tokenizer.HasMoreTokens())
        {
            tokenizer.Advance();
        }
        tokenizer.Close();

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
        }

        // Compile the tokens into output file
        CompilationEngine compiler = new CompilationEngine(tokenizer, GetOutBasefile(path));
        compiler.CompileClass();
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
        public string   mType;      // int, boolean, char, ClassName
        public int      mOffset;     // segment offset
    }

    class SymbolScope
    {
        public Dictionary<string, Symbol> mSymbols = new Dictionary<string, Symbol>();
        public string mName;
        public SymbolScope(string name)
        {
            mName = name;
        }
    };

    public enum Kind
    {
        NONE, GLOBAL, STATIC, FIELD, ARG, VAR
    }

    public static void ScopePush( string name )
    {
        SymbolScope scope = new SymbolScope(name);
        mScopes.Add(scope);
    }

    public static void ScopePop()
    {
        if( mScopes.Count > 0 )
        {
            mScopes.RemoveAt(mScopes.Count - 1);
        }
    }

    public static void Define(string varName, string type, Kind kind)
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

    public static Symbol Find( string varName )
    {
        // Walk backwards from most recently added scope backward to oldest looking for the symbol
        Symbol result = null;
        int iScope = mScopes.Count - 1;

        while (result == null && iScope >= 0)
        {
            mScopes[iScope].mSymbols.TryGetValue(varName, out result);
            iScope--;
        }

        return result;
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
            return symbol.mType;
        return "";
    }

    public static int OffsetOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
            return symbol.mOffset;
        return 0;
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

    public void Close()
    {
        if (mFile != null)
        {
            mFile.Close();
            mFile = null;
        }

        Rollback(mTokens.Count + 1);
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

        // term: 
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

    public Writer( string outFile )
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
    }

    public virtual void WriteLine(string line)
    {
        if ( JackCompiler.mVerbose )
            Console.WriteLine(line);
        mFile.WriteLine(line);
        mLinesWritten++;
    }

    public virtual void PreGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
    public virtual void WriteGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
    public virtual void PostGrammarEntry(Grammar.Node nodeObj, Tokenizer tokenStack) { }
}

class XMLWriter : Writer
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
        CONST, ARG, LOCAL, STATIC, THIS, THAT, POINTER, TEMP
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

    public void WriteIf(string label)
    {
        // if-goto
        WriteLine("if-goto " + label);
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
    Tokenizer mTokens;
    List<Writer> mWriters = new List<Writer>();

    public CompilationEngine(Tokenizer tokens, string baseOutFile)
    {
        mTokens = tokens;
        if (JackCompiler.mDumpVMFile)
        {
            mWriters.Add(new VMWriter(baseOutFile + ".vm"));
        }
        if (JackCompiler.mDumpXmlFile)
        {
            mWriters.Add( new XMLWriter(baseOutFile + ".xml") );
        }
    }

    public void Error(string msg = "")
    {
        // FIXME
        Token token = mTokens.Get();
        Console.WriteLine("ERROR: Line< " + token.lineNumber + " > Char< " + token.lineCharacter + " > " + msg);
    }

    public void WritersPreGrammarEntry( Grammar.Node nodeObj )
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (Writer writer in mWriters)
        {
            writer.PreGrammarEntry(nodeObj, mTokens);
            mTokens.StateSet(state);
        }
    }

    public void WritersWriteGrammarEntry(Grammar.Node nodeObj)
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (Writer writer in mWriters)
        {
            writer.WriteGrammarEntry(nodeObj, mTokens);
            mTokens.StateSet(state);
        }
    }

    public void WritersPostGrammarEntry(Grammar.Node nodeObj)
    {
        Tokenizer.State state = mTokens.StateGet();
        foreach (Writer writer in mWriters)
        {
            mTokens.StateSet(state);
            writer.PostGrammarEntry(nodeObj, mTokens);
        }
    }

    public bool CompileGrammar(string nodeName, bool optional, bool canWrite = true )
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

        bool result = CompileGrammar(nodeName, nodeObj, nodeStep, optional, canWrite );

        WritersPostGrammarEntry(nodeObj);

        return result;
    }

    public bool CompileGrammar(string nodeName, Grammar.Node nodeObj, int nodeStart, bool optional, bool canWrite = true, int earlyTerminate = -1)
    {
        bool done = false;

        int readAheadNode = -1;
        Tokenizer.State tokenStateRestore = null;

        for(int nodeStep = nodeStart; nodeStep < nodeObj.Count && !done; nodeStep++ )
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
                        Error();
                    if (tokenStateRestore != null)
                        mTokens.StateSet(tokenStateRestore);
                    return false;
                }
            }

            if (!done && earlyTerminate > 0 && ( nodeStep + 1 ) >= earlyTerminate)
            {
                // terminate
                done = true;
            }

            if (readAheadNode > 0 && ( nodeStep + 1 ) >= readAheadNode && tokenStateRestore != null )
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

    public void CompileClass()
    {
        // compiles a complete class
        CompileGrammar("class", false );
    }

    public void CompileClassVarDec()
    {
        // compiles class fields and static vars
        SymbolTable.ScopePush("class");
    }

    public void CompileSubroutineDec()
    {
        // compiles a method, function, or constructor

        // prep

        CompileSubroutineBody();

        // return
    }

    public void CompileParameterList()
    {
        // compiles a parameter list within () without dealing with the ()s
        // can be completely empty
    }

    public void CompileSubroutineBody()
    {
        // compiles the statements within a subroutine
        CompileVarDec();
        CompileStatements();
    }

    public void CompileVarDec()
    {
        // handles local variables in a subroutine
    }

    public void CompileStatements()
    {
        // compiles a series of statements without handling the enclosing {}s
    }

    public void CompileExpression()
    {
        // if exp is a number n:
        //   push constant n

        // if exp is a variable var:
        //   push segment i

        // if exp is "exp1 op exp2":
        //   CompileExpression( exp1 );
        //   CompileExpression( exp2 );
        //   op command (add, sub, call Math.multiply, ... )

        // if exp is "op exp"
        //   CompileExpression( exp )
        //   op command (add, sub, call Math.multiply, ... )

        // if exp is "f(exp1, exp2, ..., expN )"
        //   CompileExpression( exp1 )
        //   CompileExpression( exp2 )
        //    ...
        //   CompileExpression( expN )
        //   call f N
    }
}

