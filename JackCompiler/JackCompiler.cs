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
        CompilationEngine compiler = new CompilationEngine(tokenizer, GetOutXMLfile(path));
        compiler.CompileClass();
    }

    public static string GetOutTokenfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + "T.xml";
        return outFile;
    }

    public static string GetOutXMLfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + ".xml";
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
        NONE, STATIC, FIELD, ARG, VAR
    }

    public static void Scope( string name )
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

    public Tokenizer(string fileInput)
    {
        mFile = new System.IO.StreamReader(fileInput);
        mTokens = new ArrayList();
        mLineStr = "";
        mTokenStr = "";
        mTokenCurrent = -1;
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
            return mTokens[mTokenCurrent] as Token;
        return null;
    }

    public Token Advance()
    {
        if (mTokenCurrent < mTokens.Count - 1)
        {
            // Just advance to the next already parsed token
            mTokenCurrent++;
            return mTokens[mTokenCurrent] as Token;
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
    static Dictionary<string, int> mNodeDic;

    public enum Gram
    {
        NONE,
        ZERO_OR_MORE, // Zero or more of the following grammar node
        OR,           // One of the N next nodes where N is the following entry
        OPTIONAL,     // Next entry is optional
        READ_AHEAD1   // Only outputs anything if the next 2 entries are valid in the token list
    };

    public enum Enclose
    {
        NEVER,        // never encloses this node in the xml <name> </name>
        NOT_EMPTY,    // only encloses this node in the xml <name> </name> when there is something inside of it
        ALWAYS        // always encloses this node in the xml <name> </name>  
    };

    public static void InitIfNeeded()
    {
        if (Grammar.mInitialized)
            return;

        mNodeDic = new Dictionary<string, int>();
        int nodeIndex = 0;
        bool done = false;
        while (!done)
        {
            System.Type type = mNodes[nodeIndex].GetType();
            if (type == typeof(int) && (int)mNodes[nodeIndex] == 0)
            {
                // End of list
                done = true;
            }
            else if (type == typeof(string))
            {
                // Add entry point and advance to the next
                mNodeDic.Add((string)mNodes[nodeIndex], nodeIndex);
                bool advanced = false;
                while (!advanced)
                {
                    type = mNodes[nodeIndex].GetType();
                    if (type == typeof(int) && (int)mNodes[nodeIndex] == 0)
                    {
                        advanced = true;
                        break;
                    }
                    nodeIndex++;
                }
            }

            nodeIndex++;
        }

        Grammar.mInitialized = true;
    }

    static object[] mNodes =
    {
        // Each entry consists of a
        // node name,
        // NEVER, NOT_EMPTY, ALWAYS enclose with <name> </name>,
        // and then the definitions

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

    static public object[] GetNode(string nodeName, out int index)
    {
        InitIfNeeded();

        if (mNodeDic.TryGetValue(nodeName, out index))
            return mNodes;

        return null;
    }
}

class CompilationEngine
{
    StreamWriter mFile;
    Tokenizer mTokens;
    int mIndent;
    ArrayList mQueueLine;
    int mLinesWritten = 0;

    public CompilationEngine(Tokenizer tokens, string outFile)
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
        mTokens = tokens;
        mQueueLine = new ArrayList();
    }

    public void Error(string msg = "")
    {
        // FIXME
        Token token = mTokens.Get();
        Console.WriteLine("ERROR: Line< " + token.lineNumber + " > Char< " + token.lineCharacter + " > " + msg);
    }

    public void QueueLineAdd(string line, bool doIndent = true)
    {
        string indent = "";
        for (int i = 0; doIndent && i < mIndent; i++)
            indent = indent + " ";
        mQueueLine.Add(indent + line);
    }

    public void QueueLineRemove(string line, bool doIndent = true)
    {
        string indent = "";
        for (int i = 0; doIndent && i < mIndent; i++)
            indent = indent + " ";
        for (int i = 0; i < mQueueLine.Count; i++)
        {
            if ((string)mQueueLine[i] == indent + line)
            {
                mQueueLine.RemoveAt(i);
                return;
            }
        }
    }

    public void WriteLine(string line, bool doIndent = true)
    {
        if (mQueueLine.Count > 0)
        {
            ArrayList queue = mQueueLine;
            mQueueLine = new ArrayList();
            foreach (string qline in queue)
                WriteLine(qline, false);
        }

        string indent = "";
        for (int i = 0; doIndent && i < mIndent; i++)
            indent = indent + " ";
        if (JackCompiler.mVerbose)
            Console.WriteLine(indent + line);
        mFile.WriteLine(indent + line);
        mLinesWritten++;
    }

    public void CompileClass()
    {
        CompileGrammar("class", false, 0);
    }

    public bool CompileGrammar(string nodeName, bool optional, int indentAdd)
    {
        int node;
        int linesWrittenPrev = mLinesWritten;

        object[] nodes = Grammar.GetNode(nodeName, out node);

        if (nodes == null)
        {
            Error("Internal - Missing grammar node");
            return false;
        }

        node++;

        Grammar.Enclose enclose = (Grammar.Enclose)nodes[node];

        node++;

        mIndent = mIndent + indentAdd;

        if (enclose == Grammar.Enclose.ALWAYS)
        {
            WriteLine("<" + nodeName + ">");
        }
        else if (enclose == Grammar.Enclose.NOT_EMPTY)
        {
            QueueLineAdd("<" + nodeName + ">");
            linesWrittenPrev = mLinesWritten;
        }

        bool result = CompileGrammar(nodeName, nodes, node, optional);

        if (enclose == Grammar.Enclose.ALWAYS || (enclose == Grammar.Enclose.NOT_EMPTY && mLinesWritten > linesWrittenPrev))
        {
            WriteLine("</" + nodeName + ">");
        }
        else if (enclose == Grammar.Enclose.NOT_EMPTY)
        {
            QueueLineRemove("<" + nodeName + ">");
        }

        mIndent = mIndent - indentAdd;

        return result;
    }

    public bool CompileGrammar(string nodeName, object[] nodes, int node, bool optional, int earlyTerminate = -1)
    {
        bool done = false;

        int readAheadNode = -1;
        int readAheadToken = -1;

        while (!done)
        {
            Token token = mTokens.Get();

            System.Type type = nodes[node].GetType();

            if (type == typeof(Type))
            {
                if (token.type != (Type)nodes[node])
                {
                    if (!optional)
                        Error("Expected " + Token.TypeString((Type)nodes[node]));
                    if (readAheadToken >= 0)
                        mTokens.mTokenCurrent = readAheadToken;
                    return false;
                }

                if (readAheadNode < 0)
                    WriteLine(token.GetXMLString());
                token = mTokens.Advance();
            }
            else if (type == typeof(Keyword))
            {
                if (token.type != Token.Type.KEYWORD || token.keyword != (Keyword)nodes[node])
                {
                    if (!optional)
                        Error("Expected " + Token.KeywordString((Keyword)nodes[node]));
                    if (readAheadToken >= 0)
                        mTokens.mTokenCurrent = readAheadToken;
                    return false;
                }

                if (readAheadNode < 0)
                    WriteLine(token.GetXMLString());
                token = mTokens.Advance();
            }
            else if (type == typeof(char))
            {
                if (token.type != Token.Type.SYMBOL || token.symbol != (char)nodes[node])
                {
                    if (!optional)
                        Error("Expected " + (char)nodes[node]);
                    if (readAheadToken >= 0)
                        mTokens.mTokenCurrent = readAheadToken;
                    return false;
                }

                if (readAheadNode < 0)
                    WriteLine(token.GetXMLString());
                token = mTokens.Advance();
            }
            else if (type == typeof(Grammar.Gram))
            {
                int optionalEnd = 0;
                Grammar.Gram gram = (Grammar.Gram)nodes[node];
                switch (gram)
                {
                    case Grammar.Gram.OPTIONAL:
                        optionalEnd = node + 1;
                        node++;
                        break;

                    case Grammar.Gram.OR:
                        node++;
                        optionalEnd = node + (int)nodes[node];
                        node++;
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        optionalEnd = -1;
                        node++;
                        break;
                }

                switch (gram)
                {
                    case Grammar.Gram.OR:
                        while (!CompileGrammar(nodeName, nodes, node, node <= optionalEnd, node + 1))
                        {
                            node++;

                            if (node > optionalEnd)
                            {
                                if (!optional)
                                    Error();
                                if (readAheadToken >= 0)
                                    mTokens.mTokenCurrent = readAheadToken;
                                return false;
                            }
                        }
                        node = optionalEnd;
                        break;

                    case Grammar.Gram.OPTIONAL:
                        CompileGrammar(nodeName, nodes, node, true, node + 1);
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        while (CompileGrammar(nodeName, nodes, node, true, node + 1))
                        {
                            // do nothing
                        }
                        break;

                    case Grammar.Gram.READ_AHEAD1:
                        readAheadNode = node + 3;
                        readAheadToken = mTokens.mTokenCurrent;
                        break;
                }

                gram = Grammar.Gram.NONE;
            }
            else if (type == typeof(string))
            {
                if (!CompileGrammar((string)nodes[node], optional, 2))
                {
                    if (!optional)
                        Error();
                    if (readAheadToken >= 0)
                        mTokens.mTokenCurrent = readAheadToken;
                    return false;
                }
            }
            else if (type == typeof(int) && (int)nodes[node] == 0)
            {
                done = true;
            }

            node++;

            if (!done && earlyTerminate > 0 && node >= earlyTerminate)
            {
                done = true;
            }

            if (readAheadNode > 0 && node >= readAheadNode)
            {
                // Rewind and write it for real now
                mTokens.mTokenCurrent = readAheadToken;
                node = readAheadNode - 2;
                readAheadNode = -1;
                readAheadToken = -1;
            }
        }

        return true;
    }
}