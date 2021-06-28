using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

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
// op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|' | '^' | '%' | '!'
// unaryOp: '-' | '~'
// keywordConstant: 'true'|'false'|'null'|'this'

class VMCompiler
{
    public Dictionary<string, ClassSpec> mClasses = new Dictionary<string, ClassSpec>(); // dictionary of known classes
    public Dictionary<string, FuncSpec> mFunctions = new Dictionary<string, FuncSpec>(); // dictionary of function specs
    public Dictionary<string, int> mStrings = new Dictionary<string, int>(); // static strings

    VMSymbolTable mSymbolTable;
    List<VMTokenizer> mTokensSet;
    VMTokenizer mTokens;
    IVMWriter mVMWriter;
    string mClassName;
    string mFuncName;
    bool mIgnoreErrors;
    Dictionary<string, int> mFuncLabel = new Dictionary<string, int>();

    public List<string> mErrors = new List<string>();

    public class FuncSpec
    {
        public string funcName;
        public string className;
        public VMToken.Keyword type;
        public List<VMToken> parmTypes;
        public VMToken returnType;
        public bool referenced;
        public bool compiled;
    };

    public class ClassSpec
    {
        public string name;
        public VMSymbolTable.SymbolScope fields;
    };

    public VMCompiler(VMTokenizer tokens, IVMWriter writer )
    {
        mTokensSet = new List<VMTokenizer>();
        mTokensSet.Add(tokens);
        mVMWriter = writer;
        ResetAll();
    }

    public VMCompiler( VMTokenizer tokens )
    {
        mTokensSet = new List<VMTokenizer>();
        mTokensSet.Add(tokens);
        ResetAll();
    }

    public VMCompiler(List<VMTokenizer> tokens)
    {
        mTokensSet = new List<VMTokenizer>();
        mTokensSet.AddRange( tokens );
        ResetAll();
    }

    public VMCompiler(List<VMTokenizer> tokens, IVMWriter writer)
    {
        mTokensSet = new List<VMTokenizer>();
        mTokensSet.AddRange(tokens);
        mVMWriter = writer;
        ResetAll();
    }

    public void ResetAll()
    {
        mClasses = new Dictionary<string, ClassSpec>(); // dictionary of known classes
        mFunctions = new Dictionary<string, FuncSpec>(); // dictionary of function specs
        mStrings = new Dictionary<string, int>(); // static strings
        mStrings.Add("", mStrings.Count);

        mSymbolTable = new VMSymbolTable();
        mSymbolTable.Reset();
    }

    public void SetWriter( IVMWriter writer )
    {
        mVMWriter = writer;
    }

    public void CompilePrePass( VMTokenizer tokenizer )
    {
        VMTokenizer tokensPrev = mTokens;
        mTokens = tokenizer;

        mTokens.Reset();

        mIgnoreErrors = true;

        VMToken token = mTokens.Get();

        mVMWriter.Disable();

        // Find and register all classes and their member variables so that we know what types are valid
        while (mTokens.HasMoreTokens())
        {
            if (token.keyword == VMToken.Keyword.CLASS)
            {
                ValidateTokenAdvance(VMToken.Keyword.CLASS);
                ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out mClassName);
                ValidateTokenAdvance('{');

                if (!mClasses.ContainsKey(mClassName))
                {
                    ClassSpec classSpec = new ClassSpec();
                    classSpec.name = mClassName;
                    mClasses.Add(mClassName, classSpec);
                }

                mClasses[mClassName].fields = mSymbolTable.ScopePush("class");

                while (CompileClassVarDec())
                {
                    // continue with classVarDec
                }

                mSymbolTable.ScopePop();

                mClassName = "";
            }

            token = mTokens.Advance();
        }
    
        mIgnoreErrors = false;

        mTokens.Reset();
        mClassName = "";
        token = mTokens.Get();

        while (mTokens.HasMoreTokens())
        {
            if (token.type == VMToken.Type.STRING_CONST)
            {
                if (!mStrings.ContainsKey(token.stringVal))
                    mStrings.Add(token.stringVal, mStrings.Count);
            }
            else if (token.keyword == VMToken.Keyword.METHOD || token.keyword == VMToken.Keyword.FUNCTION || token.keyword == VMToken.Keyword.CONSTRUCTOR)
            {
                // Register which functions are methods vs functions so that we now how to call them when they are encountered while compiling
                FuncSpec spec = new FuncSpec();
                spec.className = mClassName;
                spec.type = token.keyword;

                mTokens.Advance();
                spec.returnType = mTokens.Get();
                mTokens.Advance();
                ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out spec.funcName);
                if (mClassName == "" && token.keyword != VMToken.Keyword.FUNCTION)
                {
                    Error("method or constructor outside of a class '" + spec.funcName + "'");
                }
                ValidateTokenAdvance('(');
                spec.parmTypes = CompileParameterList(false);

                string funcName = FunctionName(spec.className, spec.funcName);
                if (!mFunctions.ContainsKey(funcName))
                    mFunctions.Add(funcName, spec);
                else
                    Error("Function redefinition " + funcName, true);
            }
            else if (token.keyword == VMToken.Keyword.CLASS)
            {
                ValidateTokenAdvance(VMToken.Keyword.CLASS);
                ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out mClassName);
            }

            token = mTokens.Advance();
        }

        mTokens.Reset();

        mTokens = tokensPrev;

        mVMWriter.Enable();
        mIgnoreErrors = false;
    }

    public void Warning(string msg = "", bool forceWarning = false)
    {
        if (!mIgnoreErrors || forceWarning)
        {
            VMToken token = mTokens.Get();
            string line = "Warn: < " + token.lineNumber + ", " + token.lineCharacter + " > " + msg;
            mErrors.Add(line);
            Console.WriteLine(line);
        }
    }

    public void Error(string msg = "", bool forceError = false )
    {
        if ( !mIgnoreErrors || forceError )
        {
            VMToken token = mTokens.Get();
            string line = "ERROR: < " + token.lineNumber + ", " + token.lineCharacter + " > " + msg;
            mErrors.Add(line);
            Console.WriteLine(line);
        }
    }

    public bool ValidateSymbol(string varName)
    {
        VM.Segment seg = mSymbolTable.SegmentOf(varName);
        if (seg == VM.Segment.INVALID)
        {
            Error("Undefined symbol '" + varName + "'");
            return false;
        }
        else if (seg == VM.Segment.THIS )
        {
            VMToken.Keyword funcType = mSymbolTable.FunctionType();
            if (funcType != VMToken.Keyword.METHOD && funcType != VMToken.Keyword.CONSTRUCTOR)
            {
                Error("Cannot access class member outside of constructor or method '" + varName + "'");
                return false;
            }
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

    public VMToken ValidateTokenAdvance(object tokenCheck)
    {
        string dontCare = "";
        return ValidateTokenAdvance(tokenCheck, out dontCare);
    }

    public VMToken ValidateTokenAdvance(object tokenCheck, out string tokenString)
    {
        VMToken token = mTokens.Get();

        string error = null;

        tokenString = token.GetTokenString();

        System.Type type = tokenCheck.GetType();

        if (type == typeof(VMToken.Type) && token.type != (VMToken.Type)tokenCheck)
        {
            error = "Expected " + tokenCheck.ToString() + " at " + tokenString;
        }
        else if (type == typeof(VMToken.Keyword) && token.keyword != (VMToken.Keyword)tokenCheck)
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

    public bool ValidateFunctionReturnType(VMToken varType)
    {
        if (varType.IsType( this ))
            return true;

        return varType.keyword == VMToken.Keyword.VOID;
    }

    public string NewFuncFlowLabel(string info)
    {
        if (!mFuncLabel.ContainsKey(info))
            mFuncLabel.Add(info, 0);
        if ( mClassName != null && mClassName != "" && mFuncName != "" )
            return mClassName + "_" + mFuncName + "_" + info + "_L" + ++mFuncLabel[info];
        if ( mFuncName != null && mFuncName != "")
            return mFuncName + "_" + info + "_L" + ++mFuncLabel[info];
        return info + "_L" + ++mFuncLabel[info];
    }

    public bool MainCheck()
    {
        bool hasMain = MainCheck("Main.main");
        if (!hasMain)
            hasMain = MainCheck("main");
        return hasMain;
    }

    public bool MainCheck( string funcName )
    {
        if (mFunctions.ContainsKey(funcName))
        {
            FuncSpec func = mFunctions[funcName];
            if ( func.type == VMToken.Keyword.FUNCTION )
            {
                if (func.parmTypes.Count > 0)
                    Warning("parameters for function main() will be passed 0");
                for (int i = 0; i < func.parmTypes.Count; i++)
                    mVMWriter.WritePush(VM.Segment.CONST, 0);
                WriteCall(funcName, func.parmTypes.Count);
                mVMWriter.WritePop(VM.Segment.TEMP, 0 );
                WriteCall("Sys.halt", 0);
                return true;
            }
        }

        return false;
    }

    protected void WriteCall( string funcName, int argCount )
    {
        // mark this function as referenced
        VMCompiler.FuncSpec funcSpec;
        if ( mFunctions.TryGetValue(funcName, out funcSpec))
        {
            funcSpec.referenced = true;
        }
        mVMWriter.WriteCall(funcName, argCount);
    }

    public void Compile()
    {
        // Pre-process the operating system classes that are part of the compiler itself
        Assembly asm = Assembly.GetExecutingAssembly();
        foreach (string osName in asm.GetManifestResourceNames() )
        {
            if (!osName.Contains(".OSVM."))
                continue;

            Stream resourceStream = asm.GetManifestResourceStream(osName);
            if (resourceStream != null)
            {
                StreamReader sRdr = new StreamReader(resourceStream);
                VMTokenizer tokens = new VMTokenizer(sRdr);
                tokens.ReadAll();
                CompilePrePass(tokens);
            }
        }

        // Pre process the source code
        foreach (VMTokenizer tokens in mTokensSet)
        {
            mTokens = tokens;
            CompilePrePass(mTokens);
        }

        bool calledMain = false;
        foreach (VMTokenizer tokens in mTokensSet)
        {
            mTokens = tokens;
            mTokens.Reset();

            VMToken token = null;

            do
            {
                mClassName = "";

                if (token == mTokens.Get())
                {
                    Error("Invalid Syntax");
                    break;
                }

                token = mTokens.Get();

                switch (token.keyword)
                {
                    case VMToken.Keyword.CLASS:
                        if (!calledMain)
                            calledMain = MainCheck();
                        CompileClass();
                        break;

                    case VMToken.Keyword.FUNCTION:
                        if (!calledMain)
                            calledMain = MainCheck();
                        CompileSubroutineDec();
                        break;

                    default:
                        if (CompileStatements() > 0)
                        {
                            calledMain = MainCheck();
                            if (!calledMain)
                                WriteCall("Sys.halt", 0);
                        }
                        else if ( mTokens.mTokens.Count > 0 )
                        {
                            Error( "Expected statement" );
                        }
                        break;
                }

            } while ( mTokens.HasMoreTokens() );
        }

        mVMWriter.WriteLabel("_VM_PROGRAM_ENDED_");
        mVMWriter.WriteGoto("_VM_PROGRAM_ENDED_");
    }

    public void CompileClass()
    {
        // class: 'class' className '{' classVarDec* subroutineDec* '}'

        ValidateTokenAdvance(VMToken.Keyword.CLASS);
        ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out mClassName);
        ValidateTokenAdvance('{');

        mSymbolTable.ScopePush("class");

        while (CompileClassVarDec())
        {
            // continue with classVarDec
        }

        while (CompileSubroutineDec())
        {
            // continue with subroutineDec
        }

        ValidateTokenAdvance('}');

        mSymbolTable.ScopePop();

        mClassName = "";
    }

    public void CompileVarDecSet(VMToken varType, VMSymbolTable.Kind varKind)
    {
        do
        {
            mTokens.Advance();

            string varName;
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out varName);

            if (mSymbolTable.ExistsCurrentScope(varName))
                Error("Symbol already defined '" + varName + "'");

            if (varKind == VMSymbolTable.Kind.GLOBAL)
            {
                string globalSym = mClassName + "." + varName;
                
                // Only define globals on the pre pass
                if ( !mVMWriter.IsEnabled() )
                {
                    if (mSymbolTable.Exists(globalSym, "global"))
                        Error("Global symbol already defined '" + globalSym + "'");
                    mSymbolTable.Define(globalSym, varType, varKind, "global");
                }

                mSymbolTable.Define(varName, varType, varKind, null, mSymbolTable.OffsetOf(globalSym));
            }
            else
            {
                mSymbolTable.Define(varName, varType, varKind );
            }

            if (mTokens.Get().symbol == '=')
            {
                ValidateTokenAdvance('=');

                if (!CompileExpression()) // push value onto stack
                    Error("Expected expression after =");

                if (ValidateSymbol(varName))
                    mVMWriter.WritePop(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
            }

        } while (mTokens.Get().symbol == ',');
    }

    public bool CompileClassVarDec()
    {
        // compiles class fields and static vars
        // classVarDec: ('static'|'field)? type varName (',' varName)* ';'

        VMSymbolTable.Kind varKind = VMSymbolTable.Kind.FIELD;
        VMToken token = mTokens.GetAndAdvance();
        VMToken tokenNext = mTokens.GetAndRollback();

        if (token.type == VMToken.Type.KEYWORD && (token.keyword == VMToken.Keyword.FIELD || token.keyword == VMToken.Keyword.STATIC))
        {
            varKind = (token.keyword == VMToken.Keyword.STATIC) ? VMSymbolTable.Kind.GLOBAL : VMSymbolTable.Kind.FIELD;
            token = mTokens.Advance();
            tokenNext = mTokens.AdvanceAndRollback();
        }

        if (token.IsType( this ) && tokenNext.type == VMToken.Type.IDENTIFIER)
        {
            VMToken varType = mTokens.Get();

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

        VMToken token = mTokens.GetAndAdvance();
        VMToken tokenNext = mTokens.GetAndRollback();

        if (token.keyword == VMToken.Keyword.VAR)
        {
            token = mTokens.Advance();
            tokenNext = mTokens.AdvanceAndRollback();
        }

        if (token.IsType(this) && tokenNext.type == VMToken.Type.IDENTIFIER)
        {
            VMToken varType = mTokens.Get();

            CompileVarDecSet(varType, VMSymbolTable.Kind.VAR);

            if (eatSemiColon)
                ValidateTokenAdvance(';');

            return true;
        }
        else if (token.IsType(this))
        {
            Error("Expected identifier after type '" + token.GetTokenString() + "'");
        }

        return false;
    }

    public string FunctionName( string className, string funcName )
    {
        if ( className != null && className != "" )
        {
            return className + "." + funcName;
        }

        return funcName;
    }

    public bool CompileSubroutineDec()
    {
        // compiles a method, function, or constructor
        // subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody

        VMToken token = mTokens.Get();

        if (token.type == VMToken.Type.KEYWORD && (token.keyword == VMToken.Keyword.CONSTRUCTOR || token.keyword == VMToken.Keyword.FUNCTION || token.keyword == VMToken.Keyword.METHOD))
        {
            VMToken.Keyword funcCallType = token.keyword;
            mTokens.Advance();

            VMToken funcReturnType = mTokens.GetAndAdvance();
            if (!ValidateFunctionReturnType(funcReturnType))
            {
                Error("Return type unrecognized '" + funcReturnType.GetTokenString() + "'");
            }

            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out mFuncName);

            ValidateTokenAdvance('(');

            //////////////////////////////////////////////////////////////////////////
            // pre-compile statements to find peak local var space needed
            mIgnoreErrors = true;
            mSymbolTable.ScopePush("function", funcCallType);
            VMTokenizer.State tokenStart = null;
            int localVarSize = 0;
            tokenStart = mTokens.StateGet();
            List<VMToken> parameterTypes = CompileParameterList();
            ValidateTokenAdvance(')');
            if (mTokens.Get().symbol == ';')
            {
                // Function declaration only
                ValidateTokenAdvance(';');
            }
            else
            {
                ValidateTokenAdvance('{');
                mVMWriter.Disable();
                mSymbolTable.VarSizeBegin();
                CompileStatements();
                localVarSize = mSymbolTable.VarSizeEnd();
            }
            mSymbolTable.ScopePop(); // "function"
            mIgnoreErrors = false;

            //////////////////////////////////////////////////////////////////////////
            // Rewind tokenizer and compile the function ignoring root level var declarations
            mSymbolTable.ScopePush("function", funcCallType);
            mTokens.StateSet(tokenStart);
            mVMWriter.Enable();
            parameterTypes = CompileParameterList();
            ValidateTokenAdvance(')');
            if (mTokens.Get().symbol == ';')
            {
                // Function declaration only
                ValidateTokenAdvance(';');
            }
            else
            {
                // function implementation
                ValidateTokenAdvance('{');

                // Compile function beginning
                mVMWriter.WriteFunction(FunctionName(mClassName, mFuncName), localVarSize);
                if (funcCallType == VMToken.Keyword.CONSTRUCTOR || funcCallType == VMToken.Keyword.METHOD)
                {
                    if (funcCallType == VMToken.Keyword.CONSTRUCTOR)
                    {
                        // Alloc "this" ( and it is pushed onto the stack )
                        mVMWriter.WritePush(VM.Segment.CONST, mSymbolTable.KindSize(VMSymbolTable.Kind.FIELD));
                        WriteCall("Memory.alloc", 1);
                    }

                    if (funcCallType == VMToken.Keyword.METHOD)
                    {
                        // grab argument 0 (this) and push it on the stack
                        mVMWriter.WritePush(VM.Segment.ARG, 0);
                    }

                    // pop "this" off the stack
                    mVMWriter.WritePop(VM.Segment.POINTER, 0);
                }

                int compiledReturn = 0;
                
                CompileStatements( out compiledReturn );

                FuncSpec funcSpec;
                if ( mFunctions.TryGetValue(FunctionName(mClassName, mFuncName), out funcSpec))
                {
                    funcSpec.compiled = true;

                    if (funcSpec.returnType.keyword != VMToken.Keyword.VOID && compiledReturn < 2)
                        Error("Subroutine " + FunctionName(mClassName, mFuncName) + " missing return value");

                    if (funcSpec.returnType.keyword == VMToken.Keyword.VOID && compiledReturn == 2)
                        Error("void Subroutine " + FunctionName(mClassName, mFuncName) + " returning value");

                    if (compiledReturn == 0)
                    {
                        mVMWriter.WritePush(VM.Segment.CONST, 0);
                        mVMWriter.WriteReturn();
                    }
                }

                ValidateTokenAdvance('}');
            }

            mSymbolTable.ScopePop(); // "function"

            mFuncName = null;

            return true;
        }

        mFuncName = null;

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

        ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out subroutineName);
        if (mTokens.Get().symbol == '.')
        {
            objectName = subroutineName;
            mTokens.Advance();
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out subroutineName);
        }

        ValidateTokenAdvance('(');

        // Only for functions that are methods, we need to push the this pointer as first argument
        if ( mClassName != "" && objectName == null && mFunctions.ContainsKey( FunctionName( mClassName, subroutineName ) ) )
        {
            funcSpec = mFunctions[FunctionName( mClassName, subroutineName)];

            if (funcSpec.type != VMToken.Keyword.METHOD)
            {
                Error("Calling function as a method '" + subroutineName + "'");
            }

            // push pointer to object (this for object)
            mVMWriter.WritePush(VM.Segment.POINTER, 0); // this
            argCount = argCount + 1;
        }
        else if (mSymbolTable.Exists(objectName) && mFunctions.ContainsKey( FunctionName( mSymbolTable.TypeOf(objectName), subroutineName )))
        {
            funcSpec = mFunctions[FunctionName(mSymbolTable.TypeOf(objectName), subroutineName)];

            if (funcSpec.type != VMToken.Keyword.METHOD)
            {
                Error("Calling function as a method '" + FunctionName( objectName, subroutineName ) + "' (use " + mSymbolTable.TypeOf(objectName) + "." + subroutineName + ")");
            }

            // push pointer to object (this for object)
            mVMWriter.WritePush(mSymbolTable.SegmentOf(objectName), mSymbolTable.OffsetOf(objectName)); // object pointer
            argCount = argCount + 1;
        }
        else if ( mFunctions.ContainsKey( FunctionName( objectName, subroutineName ) ))
        {
            funcSpec = mFunctions[FunctionName( objectName, subroutineName)];
            if (funcSpec.type == VMToken.Keyword.METHOD)
            {
                Error("Calling method as a function '" + subroutineName + "'");
            }
        }
        else
        {
            Error("Calling unknown function '" + FunctionName( objectName, subroutineName ) + "' (check case)");
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
            if (mSymbolTable.Exists(objectName))
                WriteCall( FunctionName( mSymbolTable.TypeOf(objectName), subroutineName ), argCount);
            else
                WriteCall( FunctionName( objectName, subroutineName ), argCount);
        }
        else
        {
            WriteCall( FunctionName( mClassName, subroutineName ), argCount);
        }
    }

    public List<VMToken> CompileParameterList(bool doCompile = true)
    {
        List<VMToken> result = new List<VMToken>();

        // compiles a parameter list within () without dealing with the ()s
        // can be completely empty

        // parameterList: ( type varName (',' type varName)* )?
        while (mTokens.Get().IsType(this))
        {
            // handle argument
            VMToken varType = mTokens.GetAndAdvance();

            result.Add(varType);

            string varName;
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out varName);

            if (doCompile)
            {
                mSymbolTable.Define(varName, varType, VMSymbolTable.Kind.ARG);
            }

            if (mTokens.Get().symbol != ',')
                break;

            mTokens.Advance();
        }

        return result;
    }

    public int CompileStatements()
    {
        int dontCare;
        return CompileStatements(out dontCare);
    }

    public int CompileStatements( out int returnCompiled )
    {
        // compiles a series of statements without handling the enclosing {}s

        // statements: statement*

        returnCompiled = 0;

        int returnCompiledIt = 0;
        int statementCount = 0;

        while ( CompileStatementSingle(out returnCompiledIt, true) )
        {
            // keep compiling more statements
            returnCompiled = Math.Max(returnCompiled, returnCompiledIt);
            statementCount++;
        }

        return statementCount;
    }

    public bool CompileStatementSingle(out int returnCompiled, bool eatSemiColon = true )
    {
        // compiles a series of statements without handling the enclosing {}s

        // statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | 'continue' | 'break' | varDec

        returnCompiled = 0;

        VMToken token = mTokens.GetAndAdvance();
        VMToken tokenNext = mTokens.GetAndAdvance();
        VMToken tokenNextNext = mTokens.GetAndAdvance();
        VMToken tokenNextNextNext = mTokens.GetAndRollback(3);

        switch (token.keyword)
        {
            case VMToken.Keyword.IF:
                CompileStatementIf();
                return true;

            case VMToken.Keyword.WHILE:
                CompileStatementWhile();
                return true;

            case VMToken.Keyword.FOR:
                CompileStatementFor();
                return true;

            case VMToken.Keyword.SWITCH:
                CompileStatementSwitch();
                return true;

            case VMToken.Keyword.RETURN:
                returnCompiled = CompileStatementReturn();
                return true;

            case VMToken.Keyword.DO:
                CompileStatementDo(true, eatSemiColon);
                return true;

            case VMToken.Keyword.LET:
                CompileStatementLet(true, eatSemiColon);
                return true;

            case VMToken.Keyword.CONTINUE:
                CompileStatementContinue();
                return true;

            case VMToken.Keyword.BREAK:
                CompileStatementBreak();
                return true;

            default:
                // Check for non-keyword do/let/varDec
                if (token.keyword == VMToken.Keyword.VAR || (token.IsType(this) && tokenNext.type == VMToken.Type.IDENTIFIER))
                {
                    CompileVarDec(eatSemiColon);
                    return true;
                }
                else if (token.type == VMToken.Type.IDENTIFIER && (tokenNext.symbol == '=' || tokenNext.symbol == '['))
                {
                    CompileStatementLet(false, eatSemiColon);
                    return true;
                }
                else if (token.type == VMToken.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == VMToken.Type.IDENTIFIER && (tokenNextNextNext.symbol == '=' || tokenNextNextNext.symbol == '['))
                {
                    CompileStatementLet(false, eatSemiColon);
                    return true;
                }
                else if (token.type == VMToken.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
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
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out varName);
        }

        if (mTokens.Get().symbol == '.')
        {
            ValidateTokenAdvance('.');
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out memberName);
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

            mVMWriter.WritePush(VM.Segment.CONST, 0);
        }

        if (ValidateSymbol(varName))
            mVMWriter.WritePush(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));

        if (memberName != null && mClasses.ContainsKey(mSymbolTable.TypeOf(varName)))
        {
            ClassSpec classSpec = mClasses[mSymbolTable.TypeOf(varName)];
            if (!classSpec.fields.mSymbols.ContainsKey(memberName))
            {
                Error("Class identifier unknown '" + memberName + "'");
            }
            else
            {
                mVMWriter.WritePush(VM.Segment.CONST, classSpec.fields.mSymbols[memberName].mOffset);
                mVMWriter.WriteArithmetic(VM.Command.ADD);

                if (array)
                {
                    // member array
                    mVMWriter.WritePop(VM.Segment.POINTER, 1);
                    mVMWriter.WritePush(VM.Segment.THAT, 0);
                }
            }
        }

        mVMWriter.WriteArithmetic(VM.Command.ADD);
    }

    public void CompileArrayValue()
    {
        // Push the array indexed address onto stack
        CompileArrayAddress();

        // set THAT and push THAT[0]
        mVMWriter.WritePop(VM.Segment.POINTER, 1);
        mVMWriter.WritePush(VM.Segment.THAT, 0);
    }

    public void CompileClassMember()
    {
        if (mTokens.Get().type == VMToken.Type.IDENTIFIER && mClasses.ContainsKey(mTokens.Get().identifier))
        {
            // static class member 
            string className;
            string varName;
            string classGlobal;
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out className);
            ValidateTokenAdvance('.');
            ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out varName);

            classGlobal = className + '.' + varName;

            if (ValidateSymbol(classGlobal))
                mVMWriter.WritePush(mSymbolTable.SegmentOf(classGlobal), mSymbolTable.OffsetOf(classGlobal));

            if (mTokens.Get().symbol == '[')
            {
                // static class member array
                ValidateTokenAdvance('[');
                CompileExpression();
                ValidateTokenAdvance(']');
                mVMWriter.WriteArithmetic(VM.Command.ADD);
                mVMWriter.WritePop(VM.Segment.POINTER, 1);
                mVMWriter.WritePush(VM.Segment.THAT, 0);
            }
        }
        else
        {
            // handles class members and array variables
            CompileArrayValue(); 
        }
    }

    public void CompileStatementLet(bool eatKeyword = true, bool eatSemiColon = true)
    {
        // letStatement: 'let' varName ('[' expression ']')? '=' expression ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(VMToken.Keyword.LET);
        }

        string varName;
        bool isArray = false;
        ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out varName);

        if (mTokens.Get().symbol == '[' || mTokens.Get().symbol == '.')
        {
            if (mTokens.Get().symbol == '.' && mClasses.ContainsKey(varName))
            {
                // assigning static global class member
                string memberName;
                ValidateTokenAdvance('.');
                ValidateTokenAdvance(VMToken.Type.IDENTIFIER, out memberName);
                varName = varName + "." + memberName;

                if ( mTokens.Get().symbol == '[')
                {
                    isArray = true;

                    ValidateTokenAdvance('[');
                    CompileExpression();
                    ValidateTokenAdvance(']');

                    if (ValidateSymbol(varName))
                        mVMWriter.WritePush(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
                    mVMWriter.WriteArithmetic(VM.Command.ADD);
                }
            }
            else
            {
                isArray = true;

                // Push the array indexed address onto stack
                CompileArrayAddress(varName);
            }
        }

        ValidateTokenAdvance('=');

        if (!CompileExpression()) // push value onto stack
            Error("Expected expression after =");

        if (isArray)
        {
            // requires use of the top 2 values on the stack
            //   value
            //   address
            mVMWriter.WritePop(VM.Segment.TEMP, 0);
            mVMWriter.WritePop(VM.Segment.POINTER, 1);
            mVMWriter.WritePush(VM.Segment.TEMP, 0);
            mVMWriter.WritePop(VM.Segment.THAT, 0);
        }
        else
        {
            if (ValidateSymbol(varName))
                mVMWriter.WritePop(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
        }

        if (eatSemiColon)
            ValidateTokenAdvance(';');
    }

    public void CompileStatementDo(bool eatKeyword = true, bool eatSemiColon = true)
    {
        // doStatement: 'do' subroutineCall ';'

        if (eatKeyword)
        {
            ValidateTokenAdvance(VMToken.Keyword.DO);
        }

        CompileSubroutineCall();

        mVMWriter.WritePop(VM.Segment.TEMP, 0); // ignore return value

        if (eatSemiColon)
            ValidateTokenAdvance(';');
    }

    public void CompileStatementIf()
    {
        // ifStatement: 'if' '(' expression ')' ( statement | '{' statements '}' ) ('else' ( statement | '{' statements '}' ) )?

        ValidateTokenAdvance(VMToken.Keyword.IF);
        ValidateTokenAdvance('(');

        // invert the expression to make the jumps simpler
        CompileExpression();
        mVMWriter.WriteArithmetic(VM.Command.LNOT);

        ValidateTokenAdvance(')');

        string labelFalse = NewFuncFlowLabel("IF_FALSE");
        string labelEnd = null;

        mVMWriter.WriteIfGoto(labelFalse);

        int returnCompiled = 0;

        if (mTokens.Get().symbol == '{')
        {
            ValidateTokenAdvance('{');

            mSymbolTable.ScopePush("statements");

            CompileStatements();

            mSymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        if (mTokens.Get().keyword == VMToken.Keyword.ELSE)
        {
            labelEnd = NewFuncFlowLabel("IF_END");
            mVMWriter.WriteGoto(labelEnd);
        }

        mVMWriter.WriteLabel(labelFalse);

        if (mTokens.Get().keyword == VMToken.Keyword.ELSE)
        {
            mTokens.Advance();

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');

                mSymbolTable.ScopePush("statements");

                CompileStatements();

                mSymbolTable.ScopePop();

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

        ValidateTokenAdvance(VMToken.Keyword.CONTINUE);

        string label = mSymbolTable.GetLabelContinue();
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

        ValidateTokenAdvance(VMToken.Keyword.BREAK);

        string label = mSymbolTable.GetLabelBreak();
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

        ValidateTokenAdvance(VMToken.Keyword.WHILE);

        string labelExp = NewFuncFlowLabel("WHILE_EXP");
        string labelEnd = NewFuncFlowLabel("WHILE_END");

        mSymbolTable.ScopePush("whileStatement");

        mSymbolTable.DefineContinueBreak(labelExp, labelEnd);

        mVMWriter.WriteLabel(labelExp);

        // invert the expression to make the jumps simpler
        ValidateTokenAdvance('(');
        CompileExpression();
        ValidateTokenAdvance(')');

        mVMWriter.WriteArithmetic(VM.Command.LNOT);
        mVMWriter.WriteIfGoto(labelEnd);

        int returnCompiled = 0;

        if (mTokens.Get().symbol == '{')
        {
            ValidateTokenAdvance('{');

            mSymbolTable.ScopePush("statements");

            CompileStatements();

            mSymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        mVMWriter.WriteGoto(labelExp);

        mVMWriter.WriteLabel(labelEnd);

        mSymbolTable.ScopePop(); // "whileStatement"

    }

    public void CompileStatementFor()
    {
        // forStatement: 'for' '(' statements ';' expression; statements ')' ( statement | '{' statements '}' )

        int returnCompiled = 0;

        ValidateTokenAdvance(VMToken.Keyword.FOR);
        ValidateTokenAdvance('(');

        string labelExp = NewFuncFlowLabel("FOR_EXP");
        string labelEnd = NewFuncFlowLabel("FOR_END");
        string labelInc = NewFuncFlowLabel("FOR_INC");
        string labelBody = NewFuncFlowLabel("FOR_BODY");

        mSymbolTable.ScopePush("forStatement");

        mSymbolTable.DefineContinueBreak( labelInc, labelEnd );

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

            mSymbolTable.ScopePush("statements");

            CompileStatements();

            mSymbolTable.ScopePop();

            ValidateTokenAdvance('}');
        }
        else
        {
            CompileStatementSingle(out returnCompiled);
        }

        mVMWriter.WriteGoto(labelInc);

        mVMWriter.WriteLabel(labelEnd);

        mSymbolTable.ScopePop(); // "forStatement"
    }

    public void CompileStatementSwitch()
    {
        // switchStatement: 'switch' '(' expression ')' '{' ( ( 'case' expressionConst | 'default' ) ':' ( statement | '{' statements '}' ) )* '}'

        // There are several ways to handle a switch statement
        // Modern compilers often reduce it to a simple if/else if/... sequence as that can be more efficient than allocating a static jump table when the number of cases is small
        // If the case values are non-sequential it often requires breaking it up into multiple sets and implementations or a binary search
        // http://lazarenko.me/switch

        // ** For the purposes of this compiler we will treat all switch statements as an if/else if/.... **

        ValidateTokenAdvance(VMToken.Keyword.SWITCH);

        string labelEnd = NewFuncFlowLabel("SWITCH_END");
        List<string> caseLabels = new List<string>();
        List<Stream> caseExpressions = new List<Stream>();
        List<Stream> caseStatements = new List<Stream>();
        int caseLabelIndex = -1;
        int defaultLabelIndex = -1;

        mSymbolTable.ScopePush("switchStatement");

        mSymbolTable.DefineContinueBreak( null, labelEnd );

        ValidateTokenAdvance('(');
        CompileExpression();
        ValidateTokenAdvance(')');

        ValidateTokenAdvance('{');

        // Hold the switch input index in a temp register
        mVMWriter.WritePop( VM.Segment.TEMP, 0 );

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // pre-compile to find out all the cases needed and generate code for each expression and statements body
        while (mTokens.Get().keyword == VMToken.Keyword.CASE || mTokens.Get().keyword == VMToken.Keyword.DEFAULT)
        {
            bool wrapped = false;
            bool isDefault = mTokens.Get().keyword == VMToken.Keyword.DEFAULT;

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
                mSymbolTable.ScopePush("case");
                wrapped = true;
            }

            CompileStatements();

            if (wrapped)
            {
                ValidateTokenAdvance('}');
                mSymbolTable.ScopePop(); // "case"
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
                mVMWriter.WritePush(VM.Segment.TEMP, 0);

                // case expression:
                mVMWriter.WriteStream(caseExpressions[caseLabelIndex]);

                // is equal?
                mVMWriter.WriteArithmetic(VM.Command.EQ);

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

        mSymbolTable.ScopePop(); // "switchStatement"
    }

    public int CompileStatementReturn()
    {
        // returnStatement: 'return' expression? ';'

        ValidateTokenAdvance(VMToken.Keyword.RETURN);

        VMToken token = mTokens.Get();

        if (token.symbol == ';')
        {
            mVMWriter.WritePush(VM.Segment.CONST, 0);
            mTokens.Advance();
            mVMWriter.WriteReturn();
            return 1;
        }
        else
        {
            CompileExpression();
            ValidateTokenAdvance(';');
            mVMWriter.WriteReturn();
            return 2;
        }
    }

    public void CompileStringConst()
    {
        string str;

        ValidateTokenAdvance(VMToken.Type.STRING_CONST, out str);

        // Precompiled static strings
        int strIndex;

        if ( mStrings.TryGetValue(str, out strIndex) )
        {
            mVMWriter.WritePush(VM.Segment.CONST, strIndex);
        }
        else
        {
            Error("String not found '" + str + "'");
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

        // Re-direct the VM VMWriter to write to a memory file to hold the output for each term
        MemoryStream memFile = new MemoryStream();
        mVMWriter.OutputPush(memFile);

        bool compiledExpression = CompileTerm( constOnly );

        mVMWriter.OutputPop();

        VMToken token = mTokens.Get();

        if (compiledExpression)
        {
            expressionTerms.Add(memFile);

            if (VMToken.IsOp(token.symbol))
            {
                char symbol = token.symbol;

                expressionTerms.Add(token.symbol);

                mTokens.Advance();
                bool nextTerm = CompileExpression(expressionTerms, constOnly);
                if ( !nextTerm )
                {
                    Error("Expected expression after " + symbol);
                }
            }
        }

        if (doResolve && expressionTerms.Count > 0)
        {
            ExpressionResolvePrecedence(expressionTerms);
        }

        return compiledExpression;
    }

    protected int ExpressionPrecCompare(object a, object b)
    {
        if (a == null && b != null)
            return -1; // null < non-null

        if (a != null && b == null)
            return 1; // non-null > null

        if (a == null && b == null)
            return 0; // null == null

        bool isAOp = a.GetType() == typeof(char);
        bool isBOp = b.GetType() == typeof(char);

        if (isAOp && isBOp)
        {
            // same operator is left-associative: + > +
            if ((char)a == (char)b)
                return 1;

            int delta = VMToken.OpPrecedence((char)a) - VMToken.OpPrecedence((char)b);
            return -delta;
        }

        if (!isAOp && isBOp)
            return 1; // term > op

        if (isAOp && !isBOp)
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

                if (a == null && b.GetType().IsSubclassOf(typeof(Stream)))
                {
                    mVMWriter.WriteStream((Stream)b);
                    ip++;
                    continue;
                }

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
                        if (stack.Count >= 3 && stack[stack.Count - 3].GetType().IsSubclassOf(typeof(Stream)))
                            mVMWriter.WriteStream((Stream)stack[stack.Count - 3]);
                        if (stack.Count >= 1 && stack[stack.Count - 1].GetType().IsSubclassOf(typeof(Stream)))
                            mVMWriter.WriteStream((Stream)stack[stack.Count - 1]);
                        if (stack.Count >= 2 && stack[stack.Count - 2].GetType() == typeof(char) )
                        {
                            CompileOp((char)stack[stack.Count - 2]);
                            opPopped = stack[stack.Count - 2];
                        }
                        if ( stack.Count > 0 )
                            stack.RemoveAt(stack.Count - 1);
                        if (stack.Count > 0)
                            stack.RemoveAt(stack.Count - 1);
                        if (stack.Count > 0)
                            stack.RemoveAt(stack.Count - 1);
                        stack.Add("stackValue"); // placeholder to write nothing when it is encountered

                        a = ExpressionTopOp(stack);

                    } while (stack.Count >= 3 && ExpressionPrecCompare(a, opPopped) < 0);
                }
            }
        }
    }

    public void CompileOp(char op)
    {
        switch (op)
        {
            // op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|'
            case '+': mVMWriter.WriteArithmetic(VM.Command.ADD); break;
            case '-': mVMWriter.WriteArithmetic(VM.Command.SUB); break;
            case '*': mVMWriter.WriteArithmetic(VM.Command.MUL); break;
            case '/': mVMWriter.WriteArithmetic(VM.Command.DIV); break;
            case '^': mVMWriter.WriteArithmetic(VM.Command.XOR); break;
            case '%': mVMWriter.WriteArithmetic(VM.Command.MOD); break;
            case '|': mVMWriter.WriteArithmetic(VM.Command.OR); break;
            case '&': mVMWriter.WriteArithmetic(VM.Command.AND); break;
            case '<': mVMWriter.WriteArithmetic(VM.Command.LT); break;
            case '>': mVMWriter.WriteArithmetic(VM.Command.GT); break;
            case '=': mVMWriter.WriteArithmetic(VM.Command.EQ); break;
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

        VMToken token = mTokens.GetAndAdvance();
        VMToken tokenNext = mTokens.GetAndAdvance();
        VMToken tokenNextNext = mTokens.GetAndAdvance();
        VMToken tokenNextNextNext = mTokens.GetAndRollback(3);

        if (token.symbol == '(')
        {
            // expressionParenth: '(' expression ')
            ValidateTokenAdvance('(');
            if (!CompileExpression())
                Error("Expected expression after (");
            ValidateTokenAdvance(')');
            return true;
        }
        else if (token.symbol == '~' || token.symbol == '-' || token.symbol == '!' )
        {
            // unaryTerm: ('-'|'~') term
            char symbol = token.symbol;
            mTokens.Advance();
            if ( mTokens.Get().symbol == '(' )
                CompileExpression();
            else
                CompileTerm();
            if (symbol == '~')
            {
                mVMWriter.WriteArithmetic(VM.Command.NOT);
            }
            else if (symbol == '!')
            {
                mVMWriter.WriteArithmetic(VM.Command.LNOT);
            }
            else // symbol == '-' )
            {
                mVMWriter.WriteArithmetic(VM.Command.NEG);
            }
            return true;
        }
        else if (token.type == VMToken.Type.INT_CONST)
        {
            // integer constant : e.g 723
            if (token.intVal < 0)
            {
                // negative value
                mVMWriter.WritePush(VM.Segment.CONST, -token.intVal);
                mVMWriter.WriteArithmetic(VM.Command.NEG);
            }
            else
            {
                // positive value
                mVMWriter.WritePush(VM.Segment.CONST, token.intVal);
            }
            mTokens.Advance();
            return true;
        }
        else if (token.type == VMToken.Type.STRING_CONST)
        {
            // string constant: e.g. "string constant"
            ValidateConstTerm("string constant", constOnly);
            CompileStringConst();
            return true;
        }
        else if (token.type == VMToken.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == VMToken.Type.IDENTIFIER && tokenNextNextNext.symbol == '[')
        {
            // arrayValue: varName ('.' varName)? '[' expression ']'
            ValidateConstTerm("array value", constOnly);
            CompileArrayValue();
            return true;
        }
        else if (token.type == VMToken.Type.IDENTIFIER && tokenNext.symbol == '.' && tokenNextNext.type == VMToken.Type.IDENTIFIER && tokenNextNextNext.symbol != '(')
        {
            // classMember: varName '.' varName
            ValidateConstTerm("class field", constOnly);
            CompileClassMember();
            return true;
        }
        else if (token.type == VMToken.Type.IDENTIFIER && (tokenNext.symbol == '.' || tokenNext.symbol == '('))
        {
            // subroutineCall: subroutineName '(' expressionList ') | ( className | varName ) '.' subroutineName '(' expressionList ')
            ValidateConstTerm("function call", constOnly);
            CompileSubroutineCall();
            return true;
        }
        else if (token.type == VMToken.Type.IDENTIFIER && tokenNext.symbol == '[')
        {
            // arrayValue: varName ('.' varName)? '[' expression ']'
            ValidateConstTerm("array value", constOnly);
            CompileArrayValue();
            return true;
        }
        else if (token.type == VMToken.Type.IDENTIFIER && mSymbolTable.Exists(token.identifier))
        {
            // varName
            ValidateConstTerm("variable", constOnly);
            if (ValidateSymbol(token.identifier))
                mVMWriter.WritePush(mSymbolTable.SegmentOf(token.identifier), mSymbolTable.OffsetOf(token.identifier));
            mTokens.Advance();
            return true;
        }
        else if (token.type == VMToken.Type.KEYWORD && token.keyword == VMToken.Keyword.TRUE)
        {
            // true
            mVMWriter.WritePush(VM.Segment.CONST, 1);
            mVMWriter.WriteArithmetic(VM.Command.NEG);
            mTokens.Advance();
            return true;
        }
        else if (token.type == VMToken.Type.KEYWORD && (token.keyword == VMToken.Keyword.FALSE || token.keyword == VMToken.Keyword.NULL))
        {
            // false / null
            mVMWriter.WritePush(VM.Segment.CONST, 0);
            mTokens.Advance();
            return true;
        }
        else if (token.type == VMToken.Type.KEYWORD && token.keyword == VMToken.Keyword.THIS)
        {
            // this
            ValidateConstTerm("this", constOnly);
            mVMWriter.WritePush(VM.Segment.POINTER, 0);
            mTokens.Advance();
            return true;
        }
        else if ( token.type == VMToken.Type.IDENTIFIER && !mSymbolTable.Exists(token.identifier) )
        {
            Error("Undefined symbol '" + token.identifier + "'");
        }

        return false;
    }

    public int CompileExpressionList()
    {
        // expressionList: ( expression (',' expression)* )?
        int expressions = 0;
        bool stillGoing = true;

        while (stillGoing)
        {
            stillGoing = false;

            if (CompileExpression())
            {
                stillGoing = true;
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