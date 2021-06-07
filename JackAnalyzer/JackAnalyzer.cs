using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

class JackAnalyzer
{
    static public bool mComments = false;
    static public bool mVerbose = false;
    static public bool mDumpTokenFile = true;

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
            else if (lwrArg == "-t")
                JackAnalyzer.mDumpTokenFile = true;
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

        // Read all tokens into memory
        while (tokenizer.HasMoreTokens())
        {
            tokenizer.Advance();
        }
        tokenizer.Close();

        if ( JackAnalyzer.mDumpTokenFile )
        {
            // Dump the tokens to token xml file
            StreamWriter writer = new StreamWriter(GetOutTokenfile(path));
            writer.AutoFlush = true;
            writer.WriteLine("<tokens>");
            foreach (JackToken token in tokenizer)
            {
                writer.WriteLine( token.GetXMLString() );
            }
            writer.WriteLine("</tokens>");
        }

        // Compile the tokens into output file
        CompilationEngine compiler = new CompilationEngine(tokenizer, GetOutCompilefile(path) );
        compiler.CompileClass();
    }

    public static string GetOutTokenfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + "T.xml";
        return outFile;
    }

    public static string GetOutCompilefile(string file)
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

class JackToken
{
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

        typeToStr = new Dictionary<Type, string>();
        typeToStr.Add(Type.KEYWORD, "keyword" );
        typeToStr.Add(Type.SYMBOL, "symbol");
        typeToStr.Add(Type.IDENTIFIER, "identifier");
        typeToStr.Add(Type.INT_CONST, "integerConstant");
        typeToStr.Add(Type.STRING_CONST, "stringConstant");

        strToKeyword = new Dictionary<string, Keyword>();
        strToKeyword.Add("class", Keyword.CLASS );
        strToKeyword.Add("method", Keyword.METHOD);
        strToKeyword.Add("function", Keyword.FUNCTION);
        strToKeyword.Add("constructor", Keyword.CONSTRUCTOR);
        strToKeyword.Add("int", Keyword.INT);
        strToKeyword.Add("boolean", Keyword.BOOL);
        strToKeyword.Add("char", Keyword.CHAR);
        strToKeyword.Add("void", Keyword.VOID);
        strToKeyword.Add("var", Keyword.VAR);
        strToKeyword.Add("static", Keyword.STATIC);
        strToKeyword.Add("field", Keyword.FIELD);
        strToKeyword.Add("let", Keyword.LET);
        strToKeyword.Add("do", Keyword.DO);
        strToKeyword.Add("if", Keyword.IF);
        strToKeyword.Add("else", Keyword.ELSE);
        strToKeyword.Add("while", Keyword.WHILE);
        strToKeyword.Add("return", Keyword.RETURN);
        strToKeyword.Add("true", Keyword.TRUE);
        strToKeyword.Add("false", Keyword.FALSE);
        strToKeyword.Add("null", Keyword.NULL);
        strToKeyword.Add("this", Keyword.THIS);

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
            case Type.KEYWORD:
                tokenString = KeywordString(keyword);
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

    public void Close()
    {
        if (mFile != null)
        {
            mFile.Close();
            mFile = null;
        }

        Rollback( mTokens.Count + 1 );
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

    public JackToken Get()
    {
        if (mTokenCurrent < mTokens.Count)
            return mTokens[mTokenCurrent] as JackToken;
        return null;
    }

    public JackToken Advance()
    {
        if (mTokenCurrent < mTokens.Count - 1)
        {
            // Just advance to the next already parsed token
            mTokenCurrent++;
            return mTokens[mTokenCurrent] as JackToken;
        }

        if (mFile == null)
        {
            return null;
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
                    token.type = Type.STRING_CONST;
                    token.stringVal = mTokenStr;
                    mTokens.Add( token );

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
                        JackToken token = new JackToken( mLine, mLineChar - mTokenStr.Length );
                        Keyword keyword = JackToken.GetKeyword(mTokenStr);
                        if (keyword != Keyword.NONE)
                        {
                            token.type = Type.KEYWORD;
                            token.keyword = keyword;
                        }
                        else if (JackToken.IsNumber(mTokenStr[0]))
                        {
                            token.type = Type.INT_CONST;
                            token.intVal = int.Parse(mTokenStr);
                        }
                        else
                        {
                            token.type = Type.IDENTIFIER;
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
                        JackToken token = new JackToken( mLine, mLineChar );
                        token.type = Type.SYMBOL;
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

    public enum Gram { NONE, ZERO_OR_MORE, OR, OPTIONAL };

    public enum Enclose { NEVER, NOT_EMPTY, ALWAYS };

    public static void InitIfNeeded()
    {
        if (Grammar.mInitialized)
            return;

        mNodeDic = new Dictionary<string, int>();
        int nodeIndex = 0;
        bool done = false;
        while ( !done )
        {
            System.Type type = mNodes[nodeIndex].GetType();
            if (type == typeof(int) && (int) mNodes[nodeIndex] == 0)
            {
                // End of list
                done = true;
            }
            else if (type == typeof(string))
            {
                // Add entry point and advance to the next
                mNodeDic.Add((string)mNodes[nodeIndex], nodeIndex );
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
        "class", Enclose.ALWAYS, Keyword.CLASS, Type.IDENTIFIER, '{', Gram.ZERO_OR_MORE, "classVarDec", Gram.ZERO_OR_MORE, "subroutineDec", '}', 0,

        // classVarDec: ('static'|'field) type varName (',' varName)* ';'
        "classVarDec", Enclose.NOT_EMPTY, Gram.OR, 2, Keyword.STATIC, Keyword.FIELD, "type", Type.IDENTIFIER,  Gram.ZERO_OR_MORE, "varDecAdd", ';', 0,

        // varDecAdd: ',' varName
        "varDecAdd", Enclose.NEVER, ',', Type.IDENTIFIER, 0,

        // type: 'int'|'char'|'boolean'|className
        "type", Enclose.NEVER, Gram.OR, 4, Keyword.INT, Keyword.CHAR, Keyword.BOOL, Type.IDENTIFIER, 0,

        // subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody
        "subroutineDec", Enclose.NOT_EMPTY, Gram.OR, 3, Keyword.CONSTRUCTOR, Keyword.FUNCTION, Keyword.METHOD, Gram.OR, 2, Keyword.VOID, "type", Type.IDENTIFIER, '(', "parameterList", ')', "subroutineBody", 0,

        // parameter: type varName
        "parameter", Enclose.NEVER, "type", Type.IDENTIFIER, 0,

        // parameterAdd: ',' type varName
        "parameterAdd", Enclose.NEVER, ',', "type", Type.IDENTIFIER, 0,

        // parameterList: ( parameter (',' parameter)* )?
        "parameterList", Enclose.ALWAYS, Gram.OPTIONAL, "parameter", Gram.ZERO_OR_MORE, "parameterAdd", 0,

        // subroutineBody: '{' varDec* statements '}'
        "subroutineBody", Enclose.NOT_EMPTY, '{', Gram.ZERO_OR_MORE, "varDec", "statements", '}', 0,

        // varDec: 'var' type varName (',' varName)* ';'
        "varDec", Enclose.NOT_EMPTY, Keyword.VAR, "type", Type.IDENTIFIER, Gram.ZERO_OR_MORE, "varDecAdd", ';', 0,


        // STATEMENTS

        // statements: statement*
        "statements", Enclose.ALWAYS, Gram.ZERO_OR_MORE, "statement", 0,

        // statement: letStatement | ifStatement | whileStatement | doStatement | returnStatement
        "statement", Enclose.NEVER, Gram.OR, 5, "letStatement", "ifStatement", "whileStatement", "doStatement", "returnStatement", 0,

        // arrayIndex: '[' expression ']'
        "arrayIndex", Enclose.NEVER, '[', "expression", ']', 0,

        // elseClause: 'else' '{' statements '}'
        "elseClause", Enclose.NEVER, Keyword.ELSE, '{', "statements", '}', 0,

        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'
        "letStatement", Enclose.NOT_EMPTY, Keyword.LET, Type.IDENTIFIER, Gram.OPTIONAL, "arrayIndex", '=', "expression", ';', 0, 

        // ifStatement: 'if' '(' expression ')' '{' statements '}' ('else' '{' statements '}')?
        "ifStatement", Enclose.NOT_EMPTY, Keyword.IF, '(', "expression", ')', '{', "statements", '}', Gram.OPTIONAL, "elseClause", 0, 

        // whileStatement: 'while' '(' expression ')' '{' statements '}'
        "whileStatement", Enclose.NOT_EMPTY, Keyword.WHILE, '(', "expression", ')', '{', "statements", '}', 0,

        // doStatement: 'do' subroutineCall ';'
        "doStatement", Enclose.NOT_EMPTY, Keyword.DO, "subroutineCall", ';', 0,

        // returnStatement: 'return' expression? ';'
        "returnStatement", Enclose.NOT_EMPTY, Keyword.RETURN, Gram.OPTIONAL, "expression", ';', 0, 


        // EXPRESSIONS

        // opTerm: op term
        "opTerm", Enclose.NEVER, "op", "term", 0,

        // expressionAdd: ',' expression
        "expressionAdd", Enclose.NEVER, ',', "expression", 0,

        // expression: term (op term)*
        "expression", Enclose.NOT_EMPTY, "term", Gram.ZERO_OR_MORE, "opTerm", 0,

        // term: 
        "term", Enclose.NOT_EMPTY, Gram.OR, 4, Type.STRING_CONST, Type.INT_CONST, "keywordConstant", Type.IDENTIFIER, 0,

        // subroutineObject: ( className | varName ) '.'
        "subroutineObject", Enclose.NEVER, Type.IDENTIFIER, '.', 0,

        // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
        "subroutineCall", Enclose.NEVER, Gram.OPTIONAL, "subroutineObject", Type.IDENTIFIER, '(', "expressionList", ')', 0,

        // expressionList: ( expression (',' expression)* )?
        "expressionList", Enclose.ALWAYS, Gram.OPTIONAL, "expression", Gram.ZERO_OR_MORE, "expressionAdd", 0, 

        // op: '+'|'-'|'*'|'/'|'&'|'|'|'<'|'>'|'='
        "op", Enclose.NEVER, Gram.OR, 9, '+', '-', '*', '/', '&', '|', '<', '>', '=', 0,

        // unaryOp: '-'|'~'
        "unaryOp", Enclose.NEVER, Gram.OR, 2, '-', '~', 0, 

        // keywordConstant: 'true'|'false'|'null'|'this'
        "keywordConstant", Enclose.NEVER, Gram.OR, 4, Keyword.TRUE, Keyword.FALSE, Keyword.NULL, Keyword.THIS, 0, 

        0
    };

    static public object[] GetNode(string nodeName, out int index)
    {
        InitIfNeeded();

        if ( mNodeDic.TryGetValue(nodeName, out index) )
            return mNodes;

        return null;
    }
}

class CompilationEngine
{
    StreamWriter    mFile;
    JackTokenizer   mTokens;
    int             mIndent;
    ArrayList       mQueueLine;
    int             mLinesWritten = 0;

    public CompilationEngine( JackTokenizer tokens, string outFile )
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
        mTokens = tokens;
        mQueueLine = new ArrayList();
    }

    public void Error()
    {
        // FIXME
        Console.WriteLine("ERROR:");
    }

    public void QueueLineAdd( string line )
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

    public void WriteLine(string line)
    {
        if (mQueueLine.Count > 0)
        {
            ArrayList queue = mQueueLine;
            mQueueLine = new ArrayList();
            foreach ( string qline in queue )
                WriteLine(qline);
        }

        string indent = "";
        for (int i = 0; i < mIndent; i++)
            indent = indent + " ";
        if( JackAnalyzer.mVerbose )
            Console.WriteLine(indent + line);
        mFile.WriteLine( indent + line );
        mLinesWritten++;
    }

    public void CompileClass()
    {
        CompileGrammar( "class", false, 0 );
    }

    public bool CompileGrammar(string nodeName, bool optional, int indentAdd)
    {
        int node;
        int linesWrittenPrev = mLinesWritten;

        object[] nodes = Grammar.GetNode(nodeName, out node);

        if (nodes == null)
        {
            Error();
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
        else if ( enclose == Grammar.Enclose.NOT_EMPTY )
        {
            QueueLineAdd( "<" + nodeName + ">" );
            linesWrittenPrev = mLinesWritten;
        }

        bool result = CompileGrammar( nodeName, nodes, node, optional );

        if (enclose == Grammar.Enclose.ALWAYS || (enclose == Grammar.Enclose.NOT_EMPTY && mLinesWritten > linesWrittenPrev))
        {
            WriteLine("</" + nodeName + ">");
        }
        else if ( enclose == Grammar.Enclose.NOT_EMPTY )
        {
            QueueLineRemove( "<" + nodeName + ">" );
        }

        mIndent = mIndent - indentAdd;

        return result;
    }

    public bool CompileGrammar(string nodeName, object[] nodes, int node, bool optional, int earlyTerminate = -1 )
    {
        bool done = false;

        while (!done)
        {
            JackToken token = mTokens.Get();

            System.Type type = nodes[node].GetType();

            if (type == typeof(Type))
            {
                if (token.type != (Type)nodes[node])
                {
                    if (!optional)
                        Error();
                    return false;
                }

                WriteLine(token.GetXMLString());
                token = mTokens.Advance();
                optional = false;
            }
            else if (type == typeof(Keyword))
            {
                if (token.type != Type.KEYWORD || token.keyword != (Keyword)nodes[node])
                {
                    if (!optional)
                        Error();
                    return false;
                }

                WriteLine(token.GetXMLString());
                token = mTokens.Advance();
                optional = false;
            }
            else if (type == typeof(char))
            {
                if (token.type != Type.SYMBOL || token.symbol != (char) nodes[node] )
                {
                    if ( !optional )
                        Error();
                    return false;
                }

                WriteLine(token.GetXMLString());
                token = mTokens.Advance();
                optional = false;
            }
            else if (type == typeof(Grammar.Gram))
            {
                int optionalEnd = 0;
                Grammar.Gram gram = (Grammar.Gram)nodes[node];
                switch (gram)
                {
                    case Grammar.Gram.OPTIONAL:
                        optionalEnd = node + 1;
                        break;

                    case Grammar.Gram.OR:
                        node++;
                        optionalEnd = node + (int)nodes[node];
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        optionalEnd = -1;
                        break;
                }

                node++;

                switch (gram)
                {
                    case Grammar.Gram.OR:
                        while( !CompileGrammar( nodeName, nodes, node, node <= optionalEnd, node + 1) )
                        {
                            node++;

                            if (node > optionalEnd)
                            {
                                if ( !optional )
                                    Error();
                                return false;
                            }
                        }
                        node = optionalEnd;
                        break;

                    case Grammar.Gram.OPTIONAL:
                        CompileGrammar(nodeName, nodes, node, true, node + 1);
                        break;

                    case Grammar.Gram.ZERO_OR_MORE:
                        while ( CompileGrammar(nodeName, nodes, node, true, node + 1 ) )
                        {
                            // do nothing
                        }
                        break;
                }

                gram = Grammar.Gram.NONE;
            }
            else if (type == typeof(string))
            {
                if( !CompileGrammar( (string)nodes[node], optional, 2 ) )
                {
                    if ( !optional )
                        Error();
                    return false;
                }

                optional = false;
            }
            else if (type == typeof(int) && (int)nodes[node] == 0)
            {
                done = true;
            }
            
            node++;

            if ( !done && earlyTerminate > 0 && node >= earlyTerminate )
            {
                done = true;
            }
        }

        return true;
    }
}