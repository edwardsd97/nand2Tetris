using System;
using System.IO;
using System.Collections.Generic;

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
// statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | 'continue' | 'break' | varDec
// letStatement: ('let')? varName ('[' expression ']')? '=' expression ';'
// doStatement: ('do')? subroutineCall ';'
// ifStatement: 'if' '(' expression ')' ( statement | '{' statements '}' ) ('else' ( statement | '{' statements '}' ) )?
// whileStatement: 'while' '(' expression ')' ( statement | '{' statements '}' )
// forStatement: 'for' '(' statement ';' expression; statement ')' ( statement | '{' statements '}' )
// switchStatement: 'switch' '(' expression ')' '{' ( ( 'case' expressionConst | 'default' ) ':' ( statement | '{' statements '}' ) )* '}'
// returnStatement: 'return' expression? ';'

// EXPRESSIONS
// expressionConst: expression (limited to constant values)
// expression: term (op term)*
// opTerm: op term
// expressionAdd: ',' expression
// expressionParenth: '(' expression ')
// arrayValue: varName '[' expression ']'
// term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | classMember | classArrayValue | identifier )
// unaryTerm: unaryOp term
// subroutineObject: ( className | varName ) '.'
// classMember: varName '.' varName
// classArrayValue: varName '.' varName '[' expression ']'
// subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')'
// expressionList: ( expression (',' expression)* )?
// op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
// unaryOp: '-' | '~'
// keywordConstant: 'true'|'false'|'null'|'this'

class CompilationEngine
{
    static public Dictionary<string, ClassSpec> mClasses = new Dictionary<string, ClassSpec>(); // dictionary of known classes
    static public Dictionary<string, FuncSpec> mFunctions = new Dictionary<string, FuncSpec>(); // dictionary of function specs
    static public Dictionary<string, int> mStrings = new Dictionary<string, int>(); // static strings

    Tokenizer mTokens;
    IVMWriter mVMWriter;
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

    public class ClassSpec
    {
        public string name;
        public SymbolTable.SymbolScope fields;
    };

    public CompilationEngine(Tokenizer tokens, IVMWriter writer)
    {
        mTokens = tokens;

        Reset();

        mVMWriter = writer;
    }

    public CompilationEngine(Tokenizer tokens)
    {
        mTokens = tokens;
        Reset();
    }

    public void CompilePrePass(string filePath, int phase)
    {
        ValidateTokenAdvance(Token.Keyword.CLASS);
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);
        ValidateTokenAdvance('{');

        if (!mClasses.ContainsKey(mClassName))
        {
            ClassSpec classSpec = new ClassSpec();
            classSpec.name = mClassName;
            mClasses.Add(mClassName, classSpec);
        }

        mClasses[mClassName].fields = SymbolTable.ScopePush("class");

        while (CompileClassVarDec())
        {
            // continue with classVarDec
        }

        SymbolTable.ScopePop();

        if (phase == 0)
            return;

        Token token = mTokens.Get();

        while (mTokens.HasMoreTokens())
        {
            if (token.type == Token.Type.STRING_CONST)
            {
                if (!mStrings.ContainsKey(token.stringVal))
                    mStrings.Add(token.stringVal, mStrings.Count);
            }
            else if (token.keyword == Token.Keyword.METHOD || token.keyword == Token.Keyword.FUNCTION || token.keyword == Token.Keyword.CONSTRUCTOR)
            {
                // Register which functions are methods vs functions so that we now how to call them when they are encountered while compiling
                FuncSpec spec = new FuncSpec();
                spec.className = mClassName;
                spec.type = token.keyword;
                spec.filePath = filePath;

                mTokens.Advance();
                spec.returnType = mTokens.Get();
                mTokens.Advance();
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out spec.funcName);
                ValidateTokenAdvance('(');
                spec.parmTypes = CompileParameterList(false);

                if (!mFunctions.ContainsKey(spec.className + "." + spec.funcName))
                    mFunctions.Add(spec.className + "." + spec.funcName, spec);
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
        string line = "ERROR: < " + token.lineNumber + ", " + token.lineCharacter + " > " + msg;
        Console.WriteLine(line);
    }

    public bool ValidateSymbol(string varName)
    {
        if (SymbolTable.SegmentOf(varName) == IVMWriter.Segment.INVALID)
        {
            Error("Undefined symbol '" + varName + "'");
            System.Environment.Exit(-1);
        }

        return true;
    }

    public bool ValidateConstTerm( string termType, bool constOnly )
    {
        if ( !constOnly )
            return true;

        Error ("case value cannot use " + termType );
        return false;
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
            error = "Expected " + tokenCheck.ToString() + " at " + tokenString;
        }
        else if (type == typeof(Token.Keyword) && token.keyword != (Token.Keyword)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString() + " at " + tokenString;
        }
        else if (type == typeof(char) && token.symbol != (char)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString() + " at " + tokenString;
        }

        if (error != null)
            Error(error);

        mTokens.Advance();

        return mTokens.Get();
    }

    public bool ValidateFunctionReturnType(Token varType)
    {
        if (varType.IsType())
            return true;

        return varType.keyword == Token.Keyword.VOID;
    }

    public string NewFuncFlowLabel(string info)
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

    public void CompileVarDecSet(Token varType, SymbolTable.Kind varKind)
    {
        do
        {
            mTokens.Advance();

            string varName;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            SymbolTable.Define(varName, varType, varKind);

            if (mTokens.Get().symbol == '=')
            {
                ValidateTokenAdvance('=');

                CompileExpression(); // push value onto stack

                if (ValidateSymbol(varName))
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

        if (token.IsType() && tokenNext.type == Token.Type.IDENTIFIER)
        {
            Token varType = mTokens.Get();

            CompileVarDecSet(varType, varKind);

            token = ValidateTokenAdvance(';');

            return true;
        }

        return false;
    }

    public bool CompileVarDec(bool eatSemiColon = true )
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

            CompileVarDecSet(varType, SymbolTable.Kind.VAR);

            if (eatSemiColon)
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

            ValidateTokenAdvance('(');

            //////////////////////////////////////////////////////////////////////////
            // pre-compile statements to find peak local var space needed
            SymbolTable.ScopePush("function", funcCallType == Token.Keyword.METHOD);
            Tokenizer.State tokenStart = null;
            int localVarSize = 0;
            tokenStart = mTokens.StateGet();
            List<Token> parameterTypes = CompileParameterList();
            ValidateTokenAdvance(')');
            ValidateTokenAdvance('{');
            mVMWriter.Disable();
            SymbolTable.VarSizeBegin();
            CompileStatements();
            localVarSize = SymbolTable.VarSizeEnd();
            SymbolTable.ScopePop(); // "function"

            //////////////////////////////////////////////////////////////////////////
            // Rewind tokenizer and compile the function ignoring root level var declarations
            SymbolTable.ScopePush("function", funcCallType == Token.Keyword.METHOD);
            mTokens.StateSet(tokenStart);
            mVMWriter.Enable();
            parameterTypes = CompileParameterList();
            ValidateTokenAdvance(')');
            ValidateTokenAdvance('{');

            // Compile function beginning
            mVMWriter.WriteFunction(mClassName + "." + mFuncName, localVarSize);
            if (funcCallType == Token.Keyword.CONSTRUCTOR || funcCallType == Token.Keyword.METHOD)
            {
                if (funcCallType == Token.Keyword.CONSTRUCTOR)
                {
                    // Alloc "this" ( and it is pushed onto the stack )
                    mVMWriter.WritePush(IVMWriter.Segment.CONST, SymbolTable.KindSize(SymbolTable.Kind.FIELD));
                    mVMWriter.WriteCall("Memory.alloc", 1);
                }

                if (funcCallType == Token.Keyword.METHOD)
                {
                    // grab argument 0 (this) and push it on the stack
                    mVMWriter.WritePush(IVMWriter.Segment.ARG, 0);
                }

                // pop "this" off the stack
                mVMWriter.WritePop(IVMWriter.Segment.POINTER, 0);
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
                    if (funcSpec.returnType.keyword != Token.Keyword.VOID)
                        Error("Subroutine " + mClassName + "." + mFuncName + " missing return value");
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
        if (objectName == null && CompilationEngine.mFunctions.ContainsKey(mClassName + "." + subroutineName))
        {
            funcSpec = CompilationEngine.mFunctions[mClassName + "." + subroutineName];

            if (funcSpec.type != Token.Keyword.METHOD)
            {
                Error("Calling function as a method '" + subroutineName + "'");
            }

            // push pointer to object (this for object)
            mVMWriter.WritePush(IVMWriter.Segment.POINTER, 0); // this
            argCount = argCount + 1;
        }
        else if (SymbolTable.Exists(objectName) && CompilationEngine.mFunctions.ContainsKey(SymbolTable.TypeOf(objectName) + "." + subroutineName))
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
        else if (objectName != null && CompilationEngine.mFunctions.ContainsKey(objectName + "." + subroutineName))
        {
            funcSpec = CompilationEngine.mFunctions[objectName + "." + subroutineName];
            if (funcSpec.type == Token.Keyword.METHOD)
            {
                Error("Calling method as a function '" + subroutineName + "'");
            }
        }
        else
        {
            if (objectName != null)
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

        if (objectName != null)
        {
            if (SymbolTable.Exists(objectName))
                mVMWriter.WriteCall(SymbolTable.TypeOf(objectName) + "." + subroutineName, argCount);
            else
                mVMWriter.WriteCall(objectName + "." + subroutineName, argCount);
        }
        else
        {
            mVMWriter.WriteCall(mClassName + "." + subroutineName, argCount);
        }
    }

    public List<Token> CompileParameterList(bool doCompile = true)
    {
        List<Token> result = new List<Token>();

        // compiles a parameter list within () without dealing with the ()s
        // can be completely empty

        // parameterList: ( type varName (',' type varName)* )?
        while (mTokens.Get().IsType())
        {
            // handle argument
            Token varType = mTokens.GetAndAdvance();

            result.Add(varType);

            string varName;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            if (doCompile)
            {
                SymbolTable.Define(varName, varType, SymbolTable.Kind.ARG);
            }

            if (mTokens.Get().symbol != ',')
                break;

            mTokens.Advance();
        }

        return result;
    }

    public bool CompileStatements()
    {
        // compiles a series of statements without handling the enclosing {}s

        // statements: statement*

        bool resultReturnCompiled = false;
        bool returnCompiled = false;

        while (CompileStatementSingle(out returnCompiled, true))
        {
            // keep compiling more statements
            resultReturnCompiled = resultReturnCompiled || returnCompiled;
        }

        return resultReturnCompiled;
    }

    public bool CompileStatementSingle(out bool returnCompiled, bool eatSemiColon = true)
    {
        // compiles a series of statements without handling the enclosing {}s

        // statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | 'continue' | 'break' | varDec

        returnCompiled = false;

        Token token = mTokens.GetAndAdvance();
        Token tokenNext = mTokens.GetAndAdvance();
        Token tokenNextNext = mTokens.GetAndAdvance();
        Token tokenNextNextNext = mTokens.GetAndRollback(3);

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

            case Token.Keyword.SWITCH:
                CompileStatementSwitch();
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

            case Token.Keyword.CONTINUE:
                CompileStatementContinue();
                return true;

            case Token.Keyword.BREAK:
                CompileStatementBreak();
                return true;

            default:
                // Check for non-keyword do/let/varDec
                if (token.keyword == Token.Keyword.VAR || (token.IsType() && tokenNext.type == Token.Type.IDENTIFIER))
                {
                    CompileVarDec(eatSemiColon);
                    return true;
                }
                else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '=' || tokenNext.symbol == '['))
                {
                    CompileStatementLet(false, eatSemiColon);
                    return true;
                }
                else if (token.type == Token.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == Token.Type.IDENTIFIER && (tokenNextNextNext.symbol == '=' || tokenNextNextNext.symbol == '['))
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

    public void CompileArrayAddress(string varNameKnown = null)
    {
        // Push the array indexed address onto stack
        string varName = varNameKnown;
        string memberName = null;
        bool array = false;

        if (varName == null)
        {
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);
        }

        if (mTokens.Get().symbol == '.')
        {
            ValidateTokenAdvance('.');
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out memberName);
        }

        if (mTokens.Get().symbol == '[')
        {
            ValidateTokenAdvance('[');
            CompileExpression();
            ValidateTokenAdvance(']');
            array = true;
        }
        else
        {
            if (memberName == null)
                Error("Expected [ after '" + varName + "'");
            mVMWriter.WritePush(IVMWriter.Segment.CONST, 0);
        }

        if (ValidateSymbol(varName))
            mVMWriter.WritePush(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));

        if (memberName != null && mClasses.ContainsKey(SymbolTable.TypeOf(varName)))
        {
            ClassSpec classSpec = mClasses[SymbolTable.TypeOf(varName)];
            if (!classSpec.fields.mSymbols.ContainsKey(memberName))
            {
                Error("Class identifier unknown '" + memberName + "'");
            }
            else
            {
                mVMWriter.WritePush(IVMWriter.Segment.CONST, classSpec.fields.mSymbols[memberName].mOffset);
                mVMWriter.WriteArithmetic(IVMWriter.Command.ADD);

                if (array)
                {
                    // member array
                    mVMWriter.WritePop(IVMWriter.Segment.POINTER, 1);
                    mVMWriter.WritePush(IVMWriter.Segment.THAT, 0);
                }
            }
        }

        mVMWriter.WriteArithmetic(IVMWriter.Command.ADD);
    }

    public void CompileArrayValue()
    {
        // Push the array indexed address onto stack
        CompileArrayAddress();

        // set THAT and push THAT[0]
        mVMWriter.WritePop(IVMWriter.Segment.POINTER, 1);
        mVMWriter.WritePush(IVMWriter.Segment.THAT, 0);
    }

    public void CompileClassMember()
    {
        CompileArrayValue(); // handles class members as well
    }

    public void CompileStatementLet(bool eatKeyword = true, bool eatSemiColon = true)
    {
        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(Token.Keyword.LET);
        }

        string varName;
        bool isArray = false;
        ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

        if (mTokens.Get().symbol == '[' || mTokens.Get().symbol == '.')
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
            mVMWriter.WritePop(IVMWriter.Segment.TEMP, 0);
            mVMWriter.WritePop(IVMWriter.Segment.POINTER, 1);
            mVMWriter.WritePush(IVMWriter.Segment.TEMP, 0);
            mVMWriter.WritePop(IVMWriter.Segment.THAT, 0);
        }
        else
        {
            if (ValidateSymbol(varName))
                mVMWriter.WritePop(SymbolTable.SegmentOf(varName), SymbolTable.OffsetOf(varName));
        }

        if (eatSemiColon)
            ValidateTokenAdvance(';');
    }

    public void CompileStatementDo(bool eatKeyword = true, bool eatSemiColon = true)
    {
        // doStatement: 'do' subroutineCall ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(Token.Keyword.DO);
        }

        CompileSubroutineCall();

        mVMWriter.WritePop(IVMWriter.Segment.TEMP, 0); // ignore return value

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
        if (JackCompiler.mInvertedConditions)
            mVMWriter.WriteArithmetic(IVMWriter.Command.NOT);

        ValidateTokenAdvance(')');

        string labelFalse = NewFuncFlowLabel("IF_FALSE");
        string labelTrue = null;
        string labelEnd = null;

        if (JackCompiler.mInvertedConditions)
        {
            mVMWriter.WriteIfGoto(labelFalse);
        }
        else
        {
            labelTrue = NewFuncFlowLabel("IF_TRUE");
            mVMWriter.WriteIfGoto(labelTrue);
            mVMWriter.WriteGoto(labelFalse);
            mVMWriter.WriteLabel(labelTrue);
        }

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
                CompileStatementSingle(out returnCompiled);
            }

            mVMWriter.WriteLabel(labelEnd);
        }
    }

    public void CompileStatementContinue()
    {
        // 'continue'

        ValidateTokenAdvance(Token.Keyword.CONTINUE);

        string label = SymbolTable.GetLabelContinue();
        if (label != null)
        {
            mVMWriter.WriteGoto(label);
        }
        else
        {
            Error( "continue not supported at current scope" );
        }

        ValidateTokenAdvance(';');
    }

    public void CompileStatementBreak()
    {
        // 'break'

        ValidateTokenAdvance(Token.Keyword.BREAK);

        string label = SymbolTable.GetLabelBreak();
        if (label != null)
        {
            mVMWriter.WriteGoto(label);
        }
        else
        {
            Error("break not supported at current scope");
        }

        ValidateTokenAdvance(';');
    }

    public void CompileStatementWhile()
    {
        // whileStatement: 'while' '(' expression ')' ( statement | '{' statements '}' )

        ValidateTokenAdvance(Token.Keyword.WHILE);

        string labelExp = NewFuncFlowLabel("WHILE_EXP");
        string labelEnd = NewFuncFlowLabel("WHILE_END");

        SymbolTable.ScopePush("whileStatement", false);

        SymbolTable.DefineContinueBreak(labelExp, labelEnd);

        mVMWriter.WriteLabel(labelExp);

        // invert the expression to make the jumps simpler
        ValidateTokenAdvance('(');
        CompileExpression();
        ValidateTokenAdvance(')');

        mVMWriter.WriteArithmetic(IVMWriter.Command.NOT);
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

        SymbolTable.ScopePop(); // "whileStatement"

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

        SymbolTable.DefineContinueBreak( labelInc, labelEnd );

        CompileStatementSingle(out returnCompiled, false);

        ValidateTokenAdvance(';');

        mVMWriter.WriteLabel(labelExp);

        CompileExpression();

        mVMWriter.WriteIfGoto(labelBody);

        mVMWriter.WriteGoto(labelEnd);

        ValidateTokenAdvance(';');

        mVMWriter.WriteLabel(labelInc);

        CompileStatementSingle(out returnCompiled, false);

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

    public void CompileStatementSwitch()
    {
        // switchStatement: 'switch' '(' expression ')' '{' ( ( 'case' expressionConst | 'default' ) ':' ( statement | '{' statements '}' ) )* '}'

        // There are several ways to handle a switch statement
        // Modern compilers often reduce it to a simple if/else if/... sequence as that can be more efficient than allocating a static jump table when the number of cases is small
        // If the case values are non-sequential it often requires breaking it up into multiple sets and implementations or a binary search
        // http://lazarenko.me/switch

        // ** For the purposes of this compiler we will treat all switch statements as an if/else if/.... **

        ValidateTokenAdvance(Token.Keyword.SWITCH);

        string labelEnd = NewFuncFlowLabel("SWITCH_END");
        List<string> caseLabels = new List<string>();
        List<Stream> caseExpressions = new List<Stream>();
        List<Stream> caseStatements = new List<Stream>();
        int caseLabelIndex = -1;
        int defaultLabelIndex = -1;

        SymbolTable.ScopePush("switchStatement", false);

        SymbolTable.DefineContinueBreak( null, labelEnd );

        ValidateTokenAdvance('(');
        CompileExpression();
        ValidateTokenAdvance(')');

        ValidateTokenAdvance('{');

        // Hold the switch input index in a temp register
        mVMWriter.WritePop( IVMWriter.Segment.TEMP, 0 );

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // pre-compile to find out all the cases needed and generate code for each expression and statements body
        while (mTokens.Get().keyword == Token.Keyword.CASE || mTokens.Get().keyword == Token.Keyword.DEFAULT)
        {
            bool wrapped = false;
            bool isDefault = mTokens.Get().keyword == Token.Keyword.DEFAULT;

            mTokens.Advance();

            caseLabelIndex = caseLabelIndex + 1;

            // Generate a new case label
            caseLabels.Add(NewFuncFlowLabel("SWITCH_CASE"));

            // Compile case expression to a memory file
            // FIXME: this should be evaluating the expression in the compiler and only using constant values
            MemoryStream memFile = new MemoryStream();
            mVMWriter.OutputPush(memFile);

            if (!isDefault)
            {
                // compile expression made up of only constants
                CompileExpression(null, true);
            }
            else if (isDefault && defaultLabelIndex >= 0)
            {
                Error("Only one default case allowed");
            }
            else if (isDefault)
            {
                defaultLabelIndex = caseLabelIndex;
            }

            mVMWriter.OutputPop();

            caseExpressions.Add( memFile );

            ValidateTokenAdvance(':');

            // Compile case statements to a memory file
            memFile = new MemoryStream();
            mVMWriter.OutputPush(memFile);

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');
                SymbolTable.ScopePush("case", false);
                wrapped = true;
            }

            CompileStatements();

            if (wrapped)
            {
                ValidateTokenAdvance('}');
                SymbolTable.ScopePop(); // "case"
            }

            mVMWriter.OutputPop();

            caseStatements.Add( memFile);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // compile out the expressions and statement bodies appropriately
        for ( caseLabelIndex = 0;  caseLabelIndex < caseLabels.Count; caseLabelIndex++ )
        {
            if( caseLabelIndex != defaultLabelIndex )
            {
                // switch( expression )
                mVMWriter.WritePush(IVMWriter.Segment.TEMP, 0);

                // case expression:
                mVMWriter.WriteStream(caseExpressions[caseLabelIndex]);

                // is equal?
                mVMWriter.WriteArithmetic(IVMWriter.Command.EQ);

                // then goto that case label
                mVMWriter.WriteIfGoto(caseLabels[caseLabelIndex]);
            }
        }

        if (defaultLabelIndex >= 0)
        {
            // default: 
            mVMWriter.WriteGoto(caseLabels[defaultLabelIndex]);
        }
        else
        {
            mVMWriter.WriteGoto(labelEnd);
        }

        // Then write out the statement code with labels
        for (caseLabelIndex = 0; caseLabelIndex < caseLabels.Count; caseLabelIndex++)
        {
            mVMWriter.WriteLabel(caseLabels[caseLabelIndex]);
            mVMWriter.WriteStream(caseStatements[caseLabelIndex]);
        }

        ValidateTokenAdvance('}');

        mVMWriter.WriteLabel( labelEnd );

        SymbolTable.ScopePop(); // "switchStatement"
    }

    public void CompileStatementReturn()
    {
        // returnStatement: 'return' expression? ';'

        ValidateTokenAdvance(Token.Keyword.RETURN);

        Token token = mTokens.Get();

        if (token.symbol == ';')
        {
            mVMWriter.WritePush(IVMWriter.Segment.CONST, 0);
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

        if (JackCompiler.mStaticStrings && JackCompiler.mOSClasses)
        {
            // Precompiled static strings
            int strIndex;

            if (CompilationEngine.mStrings.TryGetValue(str, out strIndex))
            {
                mVMWriter.WritePush(IVMWriter.Segment.CONST, strIndex);
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
            mVMWriter.WritePush(IVMWriter.Segment.CONST, str.Length);
            mVMWriter.WriteCall("String.new", 1);
            for (int i = 0; i < str.Length; i++)
            {
                mVMWriter.WritePush(IVMWriter.Segment.CONST, str[i]);
                mVMWriter.WriteCall("String.appendChar", 2);
            }
        }
    }

    public void CompileStaticStrings()
    {
        if (JackCompiler.mStaticStrings && JackCompiler.mOSClasses)
        {
            mVMWriter.WriteLine("/* Static String Allocation (Inserted by the compiler at the beginning of Main.main) */");

            mVMWriter.WritePush(IVMWriter.Segment.CONST, CompilationEngine.mStrings.Keys.Count);
            mVMWriter.WriteCall("String.staticAlloc", 1);

            foreach (string staticString in CompilationEngine.mStrings.Keys)
            {
                int strLen = staticString.Length;
                mVMWriter.WriteLine("// \"" + staticString + "\"");
                mVMWriter.WritePush(IVMWriter.Segment.CONST, strLen);
                mVMWriter.WriteCall("String.new", 1);
                for (int i = 0; i < strLen; i++)
                {
                    mVMWriter.WritePush(IVMWriter.Segment.CONST, staticString[i]);
                    mVMWriter.WriteCall("String.appendChar", 2);
                }
                mVMWriter.WritePush(IVMWriter.Segment.CONST, CompilationEngine.mStrings[staticString]);
                mVMWriter.WriteCall("String.staticSet", 2);
            }

            mVMWriter.WriteLine("/* Main.main statements begin ... */");
        }
    }

    public bool CompileExpression(List<object> expressionTerms = null, bool constOnly = false )
    {
        // Grammar:
        // ---------
        // expression: term (op term)*
        // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'

        bool doResolve = false;
        if (expressionTerms == null)
        {
            doResolve = true;
            expressionTerms = new List<object>();
        }

        // Re-direct the VM Writer to write to a memory file to hold the output for each term
        MemoryStream memFile = new MemoryStream();
        mVMWriter.OutputPush(memFile);

        bool compiledExpression = CompileTerm( constOnly );

        mVMWriter.OutputPop();

        Token token = mTokens.Get();

        if (compiledExpression)
        {
            expressionTerms.Add(memFile);

            if (Token.IsOp(token.symbol))
            {
                expressionTerms.Add(token.symbol);

                mTokens.Advance();
                CompileExpression(expressionTerms, constOnly);
            }
        }

        if (doResolve && expressionTerms.Count > 0)
        {
            ExpressionResolvePrecedence(expressionTerms);
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

        if (!isXOp && isYOp)
            return 1; // term > op

        if (isXOp && !isYOp)
            return -1; // op < term

        return 0;
    }

    protected object ExpressionTopOp(List<object> stack)
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
        else if (expressionTerms.Count > 0)
        {
            // Handle operator precedence as a form of what is explained here:
            // https://en.wikipedia.org/wiki/Operator-precedence_grammar

            int ip = 0;
            List<object> stack = new List<object>();
            object a, b, opPopped = null;

            while (ip < expressionTerms.Count || stack.Count > 0)
            {
                // Let a be the top terminal on the stack, and b the symbol pointed to by ip
                b = ip < expressionTerms.Count ? expressionTerms[ip] : null;
                a = ExpressionTopOp(stack);

                if (a == null && b == null)
                    return;

                // if a < b or a == b then
                if (ExpressionPrecCompare(a, b) <= 0)
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
                        if (stack[stack.Count - 3].GetType().IsSubclassOf(typeof(Stream)))
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
        switch (op)
        {
            // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
            case '+': mVMWriter.WriteArithmetic(IVMWriter.Command.ADD); break;
            case '-': mVMWriter.WriteArithmetic(IVMWriter.Command.SUB); break;
            case '*': mVMWriter.WriteCall("Math.multiply", 2); break;
            case '/': mVMWriter.WriteCall("Math.divide", 2); break;
            case '%': mVMWriter.WriteCall("Math.mod", 2); break;
            case '|': mVMWriter.WriteArithmetic(IVMWriter.Command.OR); break;
            case '&': mVMWriter.WriteArithmetic(IVMWriter.Command.AND); break;
            case '<': mVMWriter.WriteArithmetic(IVMWriter.Command.LT); break;
            case '>': mVMWriter.WriteArithmetic(IVMWriter.Command.GT); break;
            case '=': mVMWriter.WriteArithmetic(IVMWriter.Command.EQ); break;
        }
    }

    public bool CompileTerm( bool constOnly = false )
    {
        // term: ( expressionParenth | unaryTerm | string_const | int_const | keywordConstant | subroutineCall | arrayValue | classMember | classArrayValue | identifier )
        // expressionParenth: '(' expression ')
        // unaryTerm: ('-'|'~') term
        // keywordConstant: 'true'|'false'|'null'|'this'
        // arrayValue: varName ('.' varName) ?  '[' expression ']'
        // classMember: varName '.' varName
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
        Token tokenNext = mTokens.GetAndAdvance();
        Token tokenNextNext = mTokens.GetAndAdvance();
        Token tokenNextNextNext = mTokens.GetAndRollback(3);

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
                mVMWriter.WriteArithmetic(IVMWriter.Command.NOT);
            }
            else // symbol == '-' )
            {
                mVMWriter.WriteArithmetic(IVMWriter.Command.NEG);
            }
            return true;
        }
        else if (token.type == Token.Type.INT_CONST)
        {
            // integer constant : e.g 723
            if (token.intVal < 0)
            {
                // negative value
                mVMWriter.WritePush(IVMWriter.Segment.CONST, -token.intVal);
                mVMWriter.WriteArithmetic(IVMWriter.Command.NEG);
            }
            else
            {
                // positive value
                mVMWriter.WritePush(IVMWriter.Segment.CONST, token.intVal);
            }
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.STRING_CONST)
        {
            // string constant: e.g. "string constant"
            ValidateConstTerm( "string constant", constOnly );
            CompileStringConst();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == Token.Type.IDENTIFIER && tokenNextNextNext.symbol == '[')
        {
            // arrayValue: varName ('.' varName)? '[' expression ']'
            ValidateConstTerm("array value", constOnly);
            CompileArrayValue();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == Token.Type.IDENTIFIER && tokenNextNextNext.symbol != '(')
        {
            // classMember: varName '.' varName
            ValidateConstTerm("class field", constOnly);
            CompileClassMember();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
        {
            // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
            ValidateConstTerm("function call", constOnly);
            CompileSubroutineCall();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && tokenNext.symbol == '[')
        {
            // arrayValue: varName ('.' varName)? '[' expression ']'
            ValidateConstTerm("array value", constOnly);
            CompileArrayValue();
            return true;
        }
        else if (token.type == Token.Type.IDENTIFIER && SymbolTable.Exists(token.identifier))
        {
            // varName
            ValidateConstTerm("variable", constOnly);
            if (ValidateSymbol(token.identifier))
                mVMWriter.WritePush(SymbolTable.SegmentOf(token.identifier), SymbolTable.OffsetOf(token.identifier));
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.TRUE)
        {
            // true
            mVMWriter.WritePush(IVMWriter.Segment.CONST, 0);
            mVMWriter.WriteArithmetic(IVMWriter.Command.NOT);
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.FALSE || token.keyword == Token.Keyword.NULL))
        {
            // false / null
            mVMWriter.WritePush(IVMWriter.Segment.CONST, 0);
            mTokens.Advance();
            return true;
        }
        else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.THIS)
        {
            // this
            ValidateConstTerm("this", constOnly);
            mVMWriter.WritePush(IVMWriter.Segment.POINTER, 0);
            mTokens.Advance();
            return true;
        }

        return false;
    }

    public int CompileExpressionList()
    {
        // expressionList: ( expression (',' expression)* )?
        int expressions = 0;

        while (true)
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