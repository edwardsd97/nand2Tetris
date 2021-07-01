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
// op: '~' | '*' | '/' | '%' | '+' | '-' | '<' | '>' | '=' | '&' | '|' | '^' | '%' | '!'
// unaryOp: '-' | '~'
// keywordConstant: 'true'|'false'|'null'|'this'

namespace VM
{
    public class Compiler
    {
        public Dictionary<string, ClassSpec> mClasses;  // dictionary of known classes
        public Dictionary<string, FuncSpec> mFunctions; // dictionary of function specs
        public Dictionary<string, int> mStrings;        // static strings

        protected SymbolTable mSymbolTable;
        protected List<Tokenizer> mTokensSet;
        protected Tokenizer mTokens;
        protected IWriter mWriter;
        protected string mClassName;
        protected string mFuncName;
        protected bool mIgnoreErrors;
        protected Dictionary<string, int> mFuncLabel = new Dictionary<string, int>();
        protected Debugger mDebugger;

        protected int mOptions;
        protected Dictionary<int, string> mOptionStrings = new Dictionary<int, string>();

        public List<string> mErrors = new List<string>();

        public enum Option
        {
            FUNCTION,   // Supports functions
            CLASS,      // Supports classes

            FUNC_HALT,  // Function to call to halt
            FUNC_ALLOC, // Function to call to alloc heap memory

            COUNT
        }

        public class FuncSpec
        {
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

        public Compiler(List<Tokenizer> files, Debugger debug = null)
        {
            OptionSetDefaults();
            mDebugger = debug;
            mTokensSet = new List<Tokenizer>();
            mTokensSet.AddRange(files);
            ResetAll();
        }

        public Compiler(List<Tokenizer> files, IWriter writer, Debugger debug = null)
        {
            OptionSetDefaults();
            mDebugger = debug;
            mTokensSet = new List<Tokenizer>();
            mTokensSet.AddRange(files);
            SetWriter(writer);
            ResetAll();
        }

        public void Compile()
        {
            mSymbolTable.ScopePush("global");

            // Pre process the source code
            foreach (Tokenizer tokens in mTokensSet)
            {
                SetCurrentTokens(tokens);
                CompilePrePass();
            }

            bool calledMain = false;
            bool calledHalt = false;

            foreach (Tokenizer tokens in mTokensSet)
            {
                SetCurrentTokens(tokens);
                mTokens.Reset();

                Token token = null;

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
                        case Token.Keyword.CLASS:
                            if (!calledMain)
                                calledMain = MainCheck();
                            CompileClass();
                            break;

                        case Token.Keyword.FUNCTION:
                            if (!calledMain)
                                calledMain = MainCheck();
                            CompileSubroutineDec();
                            break;

                        default:
                            if (CompileStatements() > 0)
                            {
                                calledMain = MainCheck();
                                if (!calledMain)
                                {
                                    WriteHalt();
                                    calledHalt = true;
                                }
                            }
                            else if (mTokens.mTokens.Count > 0)
                            {
                                Error("Expected statement");
                            }
                            break;
                    }

                } while (mTokens.HasMoreTokens());
            }

            if (!calledHalt)
            {
                WriteHalt();
                calledHalt = true;
            }

            mSymbolTable.ScopePop(mTokens.Get()); // "global"
        }

        public void CompilePrePass()
        {
            mTokens.Reset();

            mIgnoreErrors = true;

            Token token = mTokens.Get();

            mWriter.Disable();
            if (mDebugger != null)
                mDebugger.Disable();

            // Find and register all classes and their member variables so that we know what types are valid
            while (mTokens.HasMoreTokens())
            {
                if (token.keyword == Token.Keyword.CLASS)
                {
                    Token classToken = token;
                    ValidateTokenAdvance(Token.Keyword.CLASS);
                    ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);
                    ValidateTokenAdvance('{');

                    if (!mClasses.ContainsKey(mClassName))
                    {
                        ClassSpec classSpec = new ClassSpec();
                        classSpec.name = mClassName;
                        mClasses.Add(mClassName, classSpec);
                    }

                    mClasses[mClassName].fields = mSymbolTable.ScopePush("class", classToken);

                    while (CompileClassVarDec())
                    {
                        // continue with classVarDec
                    }

                    mSymbolTable.ScopePop(mTokens.Get());

                    mClassName = "";
                }

                token = mTokens.Advance();
            }

            mTokens.Reset();
            mClassName = "";
            token = mTokens.Get();
            int classBracket = 0;

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

                    mTokens.Advance();
                    spec.returnType = mTokens.Get();
                    mTokens.Advance();
                    ValidateTokenAdvance(Token.Type.IDENTIFIER, out spec.funcName);
                    if (mClassName == "" && token.keyword != Token.Keyword.FUNCTION)
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
                else if (token.keyword == Token.Keyword.CLASS)
                {
                    ValidateTokenAdvance(Token.Keyword.CLASS);
                    ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);
                    ValidateToken('{');
                    classBracket = 1; 
                }
                else if ( mClassName != "" && token.symbol == '{' )
                {
                    classBracket++;
                }
                else if ( mClassName != "" && token.symbol == '}')
                {
                    classBracket--;
                    if (classBracket == 0)
                        mClassName = "";
                }

                token = mTokens.Advance();
            }

            mTokens.Reset();
            mWriter.Enable();
            mIgnoreErrors = false;
            if (mDebugger != null)
                mDebugger.Enable();
        }

        public void OptionSetDefaults()
        {
            OptionSet(Option.CLASS, true);
            OptionSet(Option.FUNCTION, true);

            OptionSet(Option.FUNC_HALT, "Sys.halt");
            OptionSet(Option.FUNC_ALLOC, "Memory.alloc" );
        }

        public void OptionSet(Option op, bool enabled)
        {
            if (enabled)
                mOptions = mOptions | (1 << (int)op);
            else
                mOptions = mOptions & ~(1 << (int)op);
        }

        public void OptionSet(Option op, string strOption)
        {
            OptionSet(op, true);
            if (mOptionStrings.ContainsKey((int)op) )
                mOptionStrings[(int)op] = strOption;
            else
                mOptionStrings.Add( (int)op, strOption );
        }

        public bool OptionGet(Option op)
        {
            return (mOptions & (1 << (int)op)) != 0;
        }

        public string OptionGetString(Option op)
        {
            if (mOptionStrings.ContainsKey((int)op))
                return mOptionStrings[(int)op];
            return null;
        }

        public void ResetAll()
        {
            mClasses = new Dictionary<string, ClassSpec>(); // dictionary of known classes
            mFunctions = new Dictionary<string, FuncSpec>(); // dictionary of function specs
            mStrings = new Dictionary<string, int>(); // static strings
            mSymbolTable = new SymbolTable(mDebugger);
        }

        public void SetWriter(IWriter writer)
        {
            mWriter = writer;
            mWriter.SetDebugger( mDebugger );
        }

        public void SetCurrentTokens(Tokenizer tokens)
        {
            mTokens = tokens;
            if (mDebugger != null)
                mDebugger.mTokens = tokens;
        }

        public void Warning(string msg = "", bool forceWarning = false)
        {
            if (!mIgnoreErrors || forceWarning)
            {
                Token token = mTokens.Get();
                string line = "Warn: < " + token.lineNumber + ", " + token.lineCharacter + " > " + msg;
                mErrors.Add(line);
                Console.WriteLine(line);
            }
        }

        public void Error(string msg = "", bool forceError = false)
        {
            if (!mIgnoreErrors || forceError)
            {
                Token token = mTokens.Get();
                string line = "ERROR: < " + token.lineNumber + ", " + token.lineCharacter + " > " + msg;
                mErrors.Add(line);
                Console.WriteLine(line);
            }
        }

        public bool ValidateSymbol(string varName)
        {
            Segment seg = mSymbolTable.SegmentOf(varName);
            if (seg == Segment.INVALID)
            {
                Error("Undefined symbol '" + varName + "'");
                return false;
            }
            else if (seg == Segment.THIS)
            {
                Token.Keyword funcType = mSymbolTable.FunctionType();
                if (funcType != Token.Keyword.METHOD && funcType != Token.Keyword.CONSTRUCTOR)
                {
                    Error("Cannot access class member outside of constructor or method '" + varName + "'");
                    return false;
                }
            }

            return true;
        }

        public bool ValidateConstTerm(string termType, bool constOnly)
        {
            if (!constOnly)
                return true;

            Error("case value cannot use " + termType);
            return false;
        }

        public Token ValidateToken(object tokenCheck)
        {
            string dontCare = "";
            return ValidateTokenInternal(tokenCheck, out dontCare, false );
        }

        public Token ValidateToken(object tokenCheck, out string tokenString)
        {
            return ValidateTokenInternal(tokenCheck, out tokenString, false);
        }

        public Token ValidateTokenAdvance(object tokenCheck)
        {
            string dontCare = "";
            return ValidateTokenInternal(tokenCheck, out dontCare, true );
        }

        public Token ValidateTokenAdvance(object tokenCheck, out string tokenString)
        {
            return ValidateTokenInternal(tokenCheck, out tokenString, true);
        }

        protected Token ValidateTokenInternal(object tokenCheck, out string tokenString, bool advance )
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

            if ( advance )
                mTokens.Advance();

            return mTokens.Get();
        }

        public bool ValidateFunctionReturnType(Token varType)
        {
            if (varType.IsType(this))
                return true;

            return varType.keyword == Token.Keyword.VOID;
        }

        public string NewFuncFlowLabel(string info)
        {
            if (!mFuncLabel.ContainsKey(info))
                mFuncLabel.Add(info, 0);
            if (mClassName != null && mClassName != "" && mFuncName != "")
                return mClassName + "_" + mFuncName + "_" + info + "_L" + ++mFuncLabel[info];
            if (mFuncName != null && mFuncName != "")
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

        public bool MainCheck(string funcName)
        {            
            if (mFunctions.ContainsKey(funcName))
            {
                FuncSpec func = mFunctions[funcName];
                if (func.type == Token.Keyword.FUNCTION)
                {
                    if (mDebugger != null)
                        mDebugger.mAddedCode = true;
                    WriteCall(funcName, func.parmTypes.Count);
                    mWriter.WritePop(Segment.TEMP, 0);
                    WriteCall("Sys.halt", 0);
                    if (mDebugger != null)
                        mDebugger.mAddedCode = false;
                    return true;
                }
            }

            return false;
        }

        protected void WriteCall(string funcName, int argCount)
        {
            // mark this function as referenced
            Compiler.FuncSpec funcSpec;
            if (mFunctions.TryGetValue(funcName, out funcSpec))
            {
                funcSpec.referenced = true;
            }
            mWriter.WriteCall(funcName, argCount);
        }

        protected void WriteHalt()
        {
            string haltFunc = OptionGetString(Option.FUNC_HALT);
            if (mDebugger != null)
                mDebugger.mAddedCode = true;
            if ( haltFunc != null && OptionGet( Option.FUNC_HALT ) && mFunctions.ContainsKey(haltFunc))
            {
                WriteCall(haltFunc, 0);
            }
            else
            {
                mWriter.WriteLabel("_VM_PROGRAM_HALT_");
                mWriter.WriteGoto("_VM_PROGRAM_HALT_");
            }
            if (mDebugger != null)
                mDebugger.mAddedCode = false;
        }

        public void CompileClass()
        {
            // class: 'class' className '{' classVarDec* subroutineDec* '}'

            Token classToken = mTokens.Get();
            ValidateTokenAdvance(Token.Keyword.CLASS);
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out mClassName);
            ValidateTokenAdvance('{');

            mSymbolTable.ScopePush("class", classToken);

            while (CompileClassVarDec())
            {
                // continue with classVarDec
            }

            while (CompileSubroutineDec())
            {
                // continue with subroutineDec
            }

            ValidateTokenAdvance('}');

            mSymbolTable.ScopePop( mTokens.Get() );

            mClassName = "";
        }

        public void CompileVarDecSet(Token varType, SymbolTable.Kind varKind)
        {
            do
            {
                mTokens.Advance();

                string varName;
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

                if (mSymbolTable.ExistsCurrentScope(varName))
                    Error("Symbol already defined '" + varName + "'");

                if (varKind == SymbolTable.Kind.GLOBAL)
                {
                    string globalSym = mClassName + "." + varName;

                    // Only define globals on the pre pass
                    if (!mWriter.IsEnabled())
                    {
                        if (mSymbolTable.Exists(globalSym, "global"))
                            Error("Global symbol already defined '" + globalSym + "'");
                        mSymbolTable.Define(globalSym, varType, varKind, "global");
                    }

                    mSymbolTable.Define(varName, varType, varKind, null, mSymbolTable.OffsetOf(globalSym));
                }
                else
                {
                    mSymbolTable.Define(varName, varType, varKind);
                }

                if (mTokens.Get().symbol == '=')
                {
                    ValidateTokenAdvance('=');

                    if (!CompileExpression()) // push value onto stack
                        Error("Expected expression after =");

                    if (ValidateSymbol(varName))
                        mWriter.WritePop(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
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
                varKind = (token.keyword == Token.Keyword.STATIC) ? SymbolTable.Kind.GLOBAL : SymbolTable.Kind.FIELD;
                token = mTokens.Advance();
                tokenNext = mTokens.AdvanceAndRollback();
            }

            if (token.IsType(this) && tokenNext.type == Token.Type.IDENTIFIER)
            {
                Token varType = mTokens.Get();

                CompileVarDecSet(varType, varKind);

                token = ValidateTokenAdvance(';');

                return true;
            }

            return false;
        }

        public bool CompileVarDec(bool eatSemiColon = true)
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

            if (token.IsType(this) && tokenNext.type == Token.Type.IDENTIFIER)
            {
                Token varType = mTokens.Get();

                CompileVarDecSet(varType, SymbolTable.Kind.VAR);

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

        public string FunctionName(string className, string funcName)
        {
            if (className != null && className != "")
            {
                return className + "." + funcName;
            }

            return funcName;
        }

        public bool CompileSubroutineDec()
        {
            // compiles a method, function, or constructor
            // subroutineDec: ('constructor'|'function'|'method') ('void'|type) subroutineName '(' paramaterList ')' subroutineBody

            Token token = mTokens.Get();

            if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.CONSTRUCTOR || token.keyword == Token.Keyword.FUNCTION || token.keyword == Token.Keyword.METHOD))
            {
                Token functionToken = token;
                Token.Keyword funcCallType = token.keyword;
                mTokens.Advance();

                Token funcReturnType = mTokens.GetAndAdvance();
                if (!ValidateFunctionReturnType(funcReturnType))
                {
                    Error("Return type unrecognized '" + funcReturnType.GetTokenString() + "'");
                }
                if (functionToken.keyword == Token.Keyword.CONSTRUCTOR && funcReturnType.identifier != mClassName)
                {
                    Error("Constructor must return its class type");
                }

                ValidateTokenAdvance(Token.Type.IDENTIFIER, out mFuncName);

                ValidateTokenAdvance('(');

                //////////////////////////////////////////////////////////////////////////
                // pre-compile statements to find peak local var space needed
                mIgnoreErrors = true;
                if (mDebugger != null)
                    mDebugger.Disable();
                mSymbolTable.ScopePush("function", funcCallType, functionToken);
                Tokenizer.State tokenStart = null;
                int localVarSize = 0;
                tokenStart = mTokens.StateGet();
                List<Token> parameterTypes = CompileParameterList();
                ValidateTokenAdvance(')');
                if (mTokens.Get().symbol == ';')
                {
                    // Function declaration only
                    ValidateTokenAdvance(';');
                }
                else
                {
                    ValidateTokenAdvance('{');
                    mWriter.Disable();
                    mSymbolTable.VarSizeBegin();
                    CompileStatements();
                    localVarSize = mSymbolTable.VarSizeEnd();
                }
                mSymbolTable.ScopePop(mTokens.Get()); // "function"
                mIgnoreErrors = false;
                if (mDebugger != null)
                    mDebugger.Enable();

                //////////////////////////////////////////////////////////////////////////
                // Rewind tokenizer and compile the function ignoring root level var declarations
                mSymbolTable.ScopePush("function", funcCallType, functionToken);
                mTokens.StateSet(tokenStart);
                mWriter.Enable();
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
                    mWriter.WriteFunction(FunctionName(mClassName, mFuncName), localVarSize);
                    if (funcCallType == Token.Keyword.CONSTRUCTOR || funcCallType == Token.Keyword.METHOD)
                    {
                        if (funcCallType == Token.Keyword.CONSTRUCTOR)
                        {
                            // Alloc "this" ( and it is pushed onto the stack )
                            mWriter.WritePush(Segment.CONST, mSymbolTable.KindSize(SymbolTable.Kind.FIELD));
                            string memAllocFunc = OptionGetString(Option.FUNC_ALLOC);
                            if ( memAllocFunc != null )
                                WriteCall(memAllocFunc, 1);
                        }

                        if (funcCallType == Token.Keyword.METHOD)
                        {
                            // grab argument 0 (this) and push it on the stack
                            mWriter.WritePush(Segment.ARG, 0);
                        }

                        // pop "this" off the stack
                        mWriter.WritePop(Segment.POINTER, 0);
                    }

                    int compiledReturn = 0;

                    CompileStatements(out compiledReturn);

                    FuncSpec funcSpec;
                    if (mFunctions.TryGetValue(FunctionName(mClassName, mFuncName), out funcSpec))
                    {
                        funcSpec.compiled = true;

                        if (funcSpec.type == Token.Keyword.CONSTRUCTOR && compiledReturn == 0)
                        {
                            // Just return 'this' for them
                            mWriter.WritePush(Segment.POINTER, 0);
                            mWriter.WriteReturn();
                            compiledReturn = 2;
                        }
                        if (funcSpec.returnType.keyword != Token.Keyword.VOID && compiledReturn < 2)
                        {
                            Error("Subroutine " + FunctionName(mClassName, mFuncName) + " missing return value");
                        }
                        else if (funcSpec.returnType.keyword == Token.Keyword.VOID && compiledReturn == 2)
                        {
                            Error("void Subroutine " + FunctionName(mClassName, mFuncName) + " returning value");
                        }

                        if (compiledReturn == 0)
                        {
                            mWriter.WritePush(Segment.CONST, 0);
                            mWriter.WriteReturn();
                        }
                    }

                    ValidateTokenAdvance('}');
                }

                mSymbolTable.ScopePop( mTokens.Get() ); // "function"

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

            ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
            if (mTokens.Get().symbol == '.')
            {
                objectName = subroutineName;
                mTokens.Advance();
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out subroutineName);
            }

            ValidateTokenAdvance('(');

            // Only for functions that are methods, we need to push the this pointer as first argument
            if (mClassName != "" && objectName == null && mFunctions.ContainsKey(FunctionName(mClassName, subroutineName)))
            {
                funcSpec = mFunctions[FunctionName(mClassName, subroutineName)];

                if (funcSpec.type != Token.Keyword.METHOD)
                {
                    Error("Calling function as a method '" + subroutineName + "'");
                }

                // push pointer to object (this for object)
                mWriter.WritePush(Segment.POINTER, 0); // this
                argCount = argCount + 1;
            }
            else if (mSymbolTable.Exists(objectName) && mFunctions.ContainsKey(FunctionName(mSymbolTable.TypeOf(objectName), subroutineName)))
            {
                funcSpec = mFunctions[FunctionName(mSymbolTable.TypeOf(objectName), subroutineName)];

                if (funcSpec.type != Token.Keyword.METHOD)
                {
                    Error("Calling function as a method '" + FunctionName(objectName, subroutineName) + "' (use " + mSymbolTable.TypeOf(objectName) + "." + subroutineName + ")");
                }

                // push pointer to object (this for object)
                mWriter.WritePush(mSymbolTable.SegmentOf(objectName), mSymbolTable.OffsetOf(objectName)); // object pointer
                argCount = argCount + 1;
            }
            else if (mFunctions.ContainsKey(FunctionName(objectName, subroutineName)))
            {
                funcSpec = mFunctions[FunctionName(objectName, subroutineName)];
                if (funcSpec.type == Token.Keyword.METHOD)
                {
                    Error("Calling method as a function '" + subroutineName + "'");
                }
            }
            else
            {
                Error("Calling unknown function '" + FunctionName(objectName, subroutineName) + "' (check case)");
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
                    WriteCall(FunctionName(mSymbolTable.TypeOf(objectName), subroutineName), argCount);
                else
                    WriteCall(FunctionName(objectName, subroutineName), argCount);
            }
            else
            {
                WriteCall(FunctionName(mClassName, subroutineName), argCount);
            }
        }

        public List<Token> CompileParameterList(bool doCompile = true)
        {
            List<Token> result = new List<Token>();

            // compiles a parameter list within () without dealing with the ()s
            // can be completely empty

            // parameterList: ( type varName (',' type varName)* )?
            while (mTokens.Get().IsType(this))
            {
                // handle argument
                Token varType = mTokens.GetAndAdvance();

                result.Add(varType);

                string varName;
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

                if (doCompile)
                {
                    mSymbolTable.Define(varName, varType, SymbolTable.Kind.ARG);
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

        public int CompileStatements(out int returnCompiled)
        {
            // compiles a series of statements without handling the enclosing {}s

            // statements: statement*

            returnCompiled = 0;

            int returnCompiledIt = 0;
            int statementCount = 0;

            while (CompileStatementSingle(out returnCompiledIt, true))
            {
                // keep compiling more statements
                returnCompiled = Math.Max(returnCompiled, returnCompiledIt);
                statementCount++;
            }

            return statementCount;
        }

        public bool CompileStatementSingle(out int returnCompiled, bool eatSemiColon = true)
        {
            // compiles a series of statements without handling the enclosing {}s

            // statement: letStatement | ifStatement | whileStatement | forStatement | doStatement | returnStatement | 'continue' | 'break' | varDec

            returnCompiled = 0;

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
                    returnCompiled = CompileStatementReturn();
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
                    if (token.keyword == Token.Keyword.VAR || (token.IsType(this) && tokenNext.type == Token.Type.IDENTIFIER))
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

                mWriter.WritePush(Segment.CONST, 0);
            }

            if (ValidateSymbol(varName))
                mWriter.WritePush(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));

            if (memberName != null && mClasses.ContainsKey(mSymbolTable.TypeOf(varName)))
            {
                ClassSpec classSpec = mClasses[mSymbolTable.TypeOf(varName)];
                if (!classSpec.fields.mSymbols.ContainsKey(memberName))
                {
                    Error("Class identifier unknown '" + memberName + "'");
                }
                else
                {
                    mWriter.WritePush(Segment.CONST, classSpec.fields.mSymbols[memberName].mOffset);
                    mWriter.WriteArithmetic(Command.ADD);

                    if (array)
                    {
                        // member array
                        mWriter.WritePop(Segment.POINTER, 1);
                        mWriter.WritePush(Segment.THAT, 0);
                    }
                }
            }

            mWriter.WriteArithmetic(Command.ADD);
        }

        public void CompileArrayValue()
        {
            // Push the array indexed address onto stack
            CompileArrayAddress();

            // set THAT and push THAT[0]
            mWriter.WritePop(Segment.POINTER, 1);
            mWriter.WritePush(Segment.THAT, 0);
        }

        public void CompileClassMember()
        {
            if (mTokens.Get().type == Token.Type.IDENTIFIER && mClasses.ContainsKey(mTokens.Get().identifier))
            {
                // static class member 
                string className;
                string varName;
                string classGlobal;
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out className);
                ValidateTokenAdvance('.');
                ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

                classGlobal = className + '.' + varName;

                if (ValidateSymbol(classGlobal))
                    mWriter.WritePush(mSymbolTable.SegmentOf(classGlobal), mSymbolTable.OffsetOf(classGlobal));

                if (mTokens.Get().symbol == '[')
                {
                    // static class member array
                    ValidateTokenAdvance('[');
                    CompileExpression();
                    ValidateTokenAdvance(']');
                    mWriter.WriteArithmetic(Command.ADD);
                    mWriter.WritePop(Segment.POINTER, 1);
                    mWriter.WritePush(Segment.THAT, 0);
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
                ValidateTokenAdvance(Token.Keyword.LET);
            }

            string varName;
            bool isArray = false;
            ValidateTokenAdvance(Token.Type.IDENTIFIER, out varName);

            if (mTokens.Get().symbol == '[' || mTokens.Get().symbol == '.')
            {
                if (mTokens.Get().symbol == '.' && mClasses.ContainsKey(varName))
                {
                    // assigning static global class member
                    string memberName;
                    ValidateTokenAdvance('.');
                    ValidateTokenAdvance(Token.Type.IDENTIFIER, out memberName);
                    varName = varName + "." + memberName;

                    if (mTokens.Get().symbol == '[')
                    {
                        isArray = true;

                        ValidateTokenAdvance('[');
                        CompileExpression();
                        ValidateTokenAdvance(']');

                        if (ValidateSymbol(varName))
                            mWriter.WritePush(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
                        mWriter.WriteArithmetic(Command.ADD);
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
                mWriter.WritePop(Segment.TEMP, 0);
                mWriter.WritePop(Segment.POINTER, 1);
                mWriter.WritePush(Segment.TEMP, 0);
                mWriter.WritePop(Segment.THAT, 0);
            }
            else
            {
                if (ValidateSymbol(varName))
                    mWriter.WritePop(mSymbolTable.SegmentOf(varName), mSymbolTable.OffsetOf(varName));
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

            mWriter.WritePop(Segment.TEMP, 0); // ignore return value

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
            mWriter.WriteArithmetic(Command.LNOT);

            ValidateTokenAdvance(')');

            string labelFalse = NewFuncFlowLabel("IF_FALSE");
            string labelEnd = null;

            mWriter.WriteIfGoto(labelFalse);

            int returnCompiled = 0;

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');

                mSymbolTable.ScopePush("statements", mTokens.Get());

                CompileStatements();

                mSymbolTable.ScopePop(mTokens.Get());

                ValidateTokenAdvance('}');
            }
            else
            {
                CompileStatementSingle(out returnCompiled);
            }

            if (mTokens.Get().keyword == Token.Keyword.ELSE)
            {
                labelEnd = NewFuncFlowLabel("IF_END");
                mWriter.WriteGoto(labelEnd);
            }

            mWriter.WriteLabel(labelFalse);

            if (mTokens.Get().keyword == Token.Keyword.ELSE)
            {
                mTokens.Advance();

                if (mTokens.Get().symbol == '{')
                {
                    ValidateTokenAdvance('{');

                    mSymbolTable.ScopePush("statements", mTokens.Get());

                    CompileStatements();

                    mSymbolTable.ScopePop(mTokens.Get());

                    ValidateTokenAdvance('}');
                }
                else
                {
                    CompileStatementSingle(out returnCompiled);
                }

                mWriter.WriteLabel(labelEnd);
            }
        }

        public void CompileStatementContinue()
        {
            // 'continue'

            ValidateTokenAdvance(Token.Keyword.CONTINUE);

            string label = mSymbolTable.GetLabelContinue();
            if (label != null)
            {
                mWriter.WriteGoto(label);
            }
            else
            {
                Error("continue not supported at current scope");
            }

            ValidateTokenAdvance(';');
        }

        public void CompileStatementBreak()
        {
            // 'break'

            ValidateTokenAdvance(Token.Keyword.BREAK);

            string label = mSymbolTable.GetLabelBreak();
            if (label != null)
            {
                mWriter.WriteGoto(label);
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

            Token whileToken = mTokens.Get();
            ValidateTokenAdvance(Token.Keyword.WHILE);

            string labelExp = NewFuncFlowLabel("WHILE_EXP");
            string labelEnd = NewFuncFlowLabel("WHILE_END");

            mSymbolTable.ScopePush("whileStatement", whileToken );

            mSymbolTable.DefineContinueBreak(labelExp, labelEnd);

            mWriter.WriteLabel(labelExp);

            // invert the expression to make the jumps simpler
            ValidateTokenAdvance('(');
            CompileExpression();
            ValidateTokenAdvance(')');

            mWriter.WriteArithmetic(Command.LNOT);
            mWriter.WriteIfGoto(labelEnd);

            int returnCompiled = 0;

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');

                mSymbolTable.ScopePush("statements", mTokens.Get());

                CompileStatements();

                mSymbolTable.ScopePop(mTokens.Get());

                ValidateTokenAdvance('}');
            }
            else
            {
                CompileStatementSingle(out returnCompiled);
            }

            // Rollback and advance around goto so that goto is correctly marked as the right code line
            mTokens.Rollback(1);
            mWriter.WriteGoto(labelExp);
            mTokens.Advance();

            mWriter.WriteLabel(labelEnd);

            mSymbolTable.ScopePop( mTokens.Get() ); // "whileStatement"

        }

        public void CompileStatementFor()
        {
            // forStatement: 'for' '(' statements ';' expression; statements ')' ( statement | '{' statements '}' )

            int returnCompiled = 0;

            Token forToken = mTokens.Get();
            ValidateTokenAdvance(Token.Keyword.FOR);
            ValidateTokenAdvance('(');

            string labelExp = NewFuncFlowLabel("FOR_EXP");
            string labelEnd = NewFuncFlowLabel("FOR_END");
            string labelInc = NewFuncFlowLabel("FOR_INC");
            string labelBody = NewFuncFlowLabel("FOR_BODY");

            mSymbolTable.ScopePush("forStatement", forToken );

            mSymbolTable.DefineContinueBreak(labelInc, labelEnd);

            CompileStatementSingle(out returnCompiled, false);

            ValidateTokenAdvance(';');

            mWriter.WriteLabel(labelExp);

            CompileExpression();

            mWriter.WriteIfGoto(labelBody);

            mWriter.WriteGoto(labelEnd);

            ValidateTokenAdvance(';');

            mWriter.WriteLabel(labelInc);

            CompileStatementSingle(out returnCompiled, false);

            mWriter.WriteGoto(labelExp);

            ValidateTokenAdvance(')');

            mWriter.WriteLabel(labelBody);

            if (mTokens.Get().symbol == '{')
            {
                ValidateTokenAdvance('{');

                mSymbolTable.ScopePush("statements", mTokens.Get());

                CompileStatements();

                mSymbolTable.ScopePop(mTokens.Get()); // "statements"

                ValidateTokenAdvance('}');
            }
            else
            {
                CompileStatementSingle(out returnCompiled);
            }

            // Rollback and advance around goto so that goto is correctly marked as the right code line
            mTokens.Rollback(1);
            mWriter.WriteGoto(labelInc);
            mTokens.Advance();

            mWriter.WriteLabel(labelEnd);

            mSymbolTable.ScopePop(mTokens.Get()); // "forStatement"
        }

        public void CompileStatementSwitch()
        {
            // switchStatement: 'switch' '(' expression ')' '{' ( ( 'case' expressionConst | 'default' ) ':' ( statement | '{' statements '}' ) )* '}'

            // There are several ways to handle a switch statement
            // Modern compilers often reduce it to a simple if/else if/... sequence as that can be more efficient than allocating a static jump table when the number of cases is small
            // If the case values are non-sequential it often requires breaking it up into multiple sets and implementations or a binary search
            // http://lazarenko.me/switch

            // ** For the purposes of this compiler we will treat all switch statements as an if/else if/.... **

            Token switchToken = mTokens.Get();
            ValidateTokenAdvance(Token.Keyword.SWITCH);

            string labelEnd = NewFuncFlowLabel("SWITCH_END");
            List<string> caseLabels = new List<string>();
            List<WriterStream> caseExpressions = new List<WriterStream>();
            List<WriterStream> caseStatements = new List<WriterStream>();
            int caseLabelIndex = -1;
            int defaultLabelIndex = -1;

            mSymbolTable.ScopePush("switchStatement", switchToken );

            mSymbolTable.DefineContinueBreak(null, labelEnd);

            ValidateTokenAdvance('(');
            WriterStream switchExpression = new WriterStream( mTokens );
            mWriter.OutputPush(switchExpression);
            CompileExpression();
            mWriter.OutputPop();
            ValidateTokenAdvance(')');

            ValidateTokenAdvance('{');

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
                WriterStream memFile = new WriterStream( mTokens );
                mWriter.OutputPush(memFile);

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

                mWriter.OutputPop();

                caseExpressions.Add(memFile);

                ValidateTokenAdvance(':');

                // Compile case statements to a memory file
                memFile = new WriterStream( mTokens );
                mWriter.OutputPush(memFile);

                if (mTokens.Get().symbol == '{')
                {
                    ValidateTokenAdvance('{');
                    mSymbolTable.ScopePush("case", mTokens.Get());
                    wrapped = true;
                }

                CompileStatements();

                if (wrapped)
                {
                    ValidateTokenAdvance('}');
                    mSymbolTable.ScopePop(mTokens.Get()); // "case"
                }

                mWriter.OutputPop();

                caseStatements.Add(memFile);
            }

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // compile out the expressions and statement bodies appropriately
            for (caseLabelIndex = 0; caseLabelIndex < caseLabels.Count; caseLabelIndex++)
            {
                if (caseLabelIndex != defaultLabelIndex)
                {
                    // switch( expression )
                    mWriter.WriteStream(switchExpression);

                    // case expression:
                    mWriter.WriteStream(caseExpressions[caseLabelIndex]);

                    // for debug line tracking
                    Tokenizer.State prevState = mTokens.StateGet();
                    if (caseExpressions[caseLabelIndex].mTokenStates.Count > 0 )
                        mTokens.StateSet(caseExpressions[caseLabelIndex].mTokenStates[0]);

                    // is equal?
                    mWriter.WriteArithmetic(Command.EQ);

                    // then goto that case label
                    mWriter.WriteIfGoto(caseLabels[caseLabelIndex]);

                    mTokens.StateSet(prevState);
                }
            }

            if (defaultLabelIndex >= 0)
            {
                // default: 
                mWriter.WriteGoto(caseLabels[defaultLabelIndex]);
            }
            else
            {
                mWriter.WriteGoto(labelEnd);
            }

            // Then write out the statement code with labels
            for (caseLabelIndex = 0; caseLabelIndex < caseLabels.Count; caseLabelIndex++)
            {
                mWriter.WriteLabel(caseLabels[caseLabelIndex]);
                mWriter.WriteStream(caseStatements[caseLabelIndex]);
            }

            ValidateTokenAdvance('}');

            mWriter.WriteLabel(labelEnd);

            mSymbolTable.ScopePop(mTokens.Get()); // "switchStatement"
        }

        public int CompileStatementReturn()
        {
            // returnStatement: 'return' expression? ';'

            ValidateTokenAdvance(Token.Keyword.RETURN);

            Token token = mTokens.Get();

            FuncSpec funcSpec;
            if (mFunctions.TryGetValue(FunctionName(mClassName, mFuncName), out funcSpec))
            {
                if ( funcSpec.type == Token.Keyword.CONSTRUCTOR )
                {
                    if ( token.keyword != Token.Keyword.THIS && token.symbol != ';' )
                        Error("Constructors must return 'this' or no value to be done for you" );

                    if (token.symbol == ';')
                    {
                        mTokens.Advance();
                    }
                    else
                    {
                        CompileExpression();
                        ValidateTokenAdvance(';');
                        mWriter.WritePop(Segment.TEMP, 0);
                    }

                    mWriter.WritePush(  Segment.POINTER, 0 );
                    mWriter.WriteReturn();
                    return 2;
                }
            }

            if (token.symbol == ';')
            {
                mWriter.WritePush(Segment.CONST, 0);
                mTokens.Advance();
                mWriter.WriteReturn();
                return 1;
            }
            else
            {
                CompileExpression();
                ValidateTokenAdvance(';');
                mWriter.WriteReturn();
                return 2;
            }
        }

        public void CompileStringConst()
        {
            string str;

            ValidateTokenAdvance(Token.Type.STRING_CONST, out str);

            // Precompiled static strings
            int strIndex;

            if (mStrings.TryGetValue(str, out strIndex))
            {
                mWriter.WritePush(Segment.CONST, strIndex + 1);
                mWriter.WriteArithmetic( Command.NEG );
            }
            else
            {
                Error("String not found '" + str + "'");
            }
        }

        public bool CompileExpression(List<object> expressionTerms = null, bool constOnly = false)
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

            // Re-direct the Emulator Writer to write to a memory file to hold the output for each term
            WriterStream memFile = new WriterStream( mTokens );
            mWriter.OutputPush(memFile);

            bool compiledExpression = CompileTerm(constOnly);

            mWriter.OutputPop();

            Token token = mTokens.Get();

            if (compiledExpression)
            {
                expressionTerms.Add(memFile);

                if (Token.IsOp(token.symbol))
                {
                    char symbol = token.symbol;

                    expressionTerms.Add(token.symbol);

                    mTokens.Advance();
                    bool nextTerm = CompileExpression(expressionTerms, constOnly);
                    if (!nextTerm)
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

                int delta = Token.OpPrecedence((char)a) - Token.OpPrecedence((char)b);
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

        protected object ExpressionTopResolve(List<object> stack)
        {
            object opPopped = null;
            Tokenizer.State state = null; // used to keep the current token aligned with expression so debugger can build the line number map correctly

            if (stack.Count >= 3 && stack[stack.Count - 3].GetType() == typeof(WriterStream))
            {
                WriterStream ws = (WriterStream) stack[stack.Count - 3];
                if (state == null & ws.mTokenStates.Count > 0)
                    state = ws.mTokenStates[0];
                mWriter.WriteStream((WriterStream)stack[stack.Count - 3]);
            }

            if (stack.Count >= 1 && stack[stack.Count - 1].GetType() == typeof(WriterStream))
            {
                WriterStream ws = (WriterStream) stack[stack.Count - 1];
                if (state == null & ws.mTokenStates.Count > 0)
                    state = ws.mTokenStates[0];
                mWriter.WriteStream((WriterStream)stack[stack.Count - 1]);
            }

            if (stack.Count >= 2 && stack[stack.Count - 2].GetType() == typeof(char))
            {
                Tokenizer.State prevState = mTokens.StateGet();
                if ( state != null )
                    mTokens.StateSet(state);
                CompileOp((char)stack[stack.Count - 2]);
                opPopped = stack[stack.Count - 2];
                mTokens.StateSet(prevState);
            }

            for (int i = 0; i < 3; i++)
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
            }

            stack.Add("stackValue"); // placeholder to write nothing when it is encountered

            return opPopped;
        }

        protected void ExpressionResolvePrecedence(List<object> expressionTerms)
        {
            // expressionTerms is a list of term? (op term)* that needs to be resolved with operator prededence

            // This will always be either empty or an odd number of entries always following term op term op term ...
            // Each term is a pre-compiled set of Emulator commands before arriving here
            // 0: (do nothing)
            // 1: x
            // 3: x + y
            // 5: x + y * 5
            // 7: x + y * 5 - 6 = 9
            // etc...

            if (expressionTerms.Count == 1)
            {
                mWriter.WriteStream((WriterStream)expressionTerms[0]);
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

                    if (a == null && b.GetType() == typeof(WriterStream) )
                    {
                        mWriter.WriteStream((WriterStream)b);
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
                            object op = ExpressionTopResolve(stack);
                            if (op != null)
                                opPopped = op;
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
                case '+': mWriter.WriteArithmetic(Command.ADD); break;
                case '-': mWriter.WriteArithmetic(Command.SUB); break;
                case '*': mWriter.WriteArithmetic(Command.MUL); break;
                case '/': mWriter.WriteArithmetic(Command.DIV); break;
                case '^': mWriter.WriteArithmetic(Command.XOR); break;
                case '%': mWriter.WriteArithmetic(Command.MOD); break;
                case '|': mWriter.WriteArithmetic(Command.OR); break;
                case '&': mWriter.WriteArithmetic(Command.AND); break;
                case '<': mWriter.WriteArithmetic(Command.LT); break;
                case '>': mWriter.WriteArithmetic(Command.GT); break;
                case '=': mWriter.WriteArithmetic(Command.EQ); break;
            }
        }

        public bool CompileTerm(bool constOnly = false)
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
                if (!CompileExpression())
                    Error("Expected expression after (");
                ValidateTokenAdvance(')');
                return true;
            }
            else if (token.symbol == '~' || token.symbol == '-' || token.symbol == '!')
            {
                // unaryTerm: ('-'|'~') term
                char symbol = token.symbol;
                mTokens.Advance();
                if (mTokens.Get().symbol == '(')
                    CompileExpression();
                else
                    CompileTerm();
                if (symbol == '~')
                {
                    mWriter.WriteArithmetic(Command.NOT);
                }
                else if (symbol == '!')
                {
                    mWriter.WriteArithmetic(Command.LNOT);
                }
                else // symbol == '-' )
                {
                    mWriter.WriteArithmetic(Command.NEG);
                }
                return true;
            }
            else if (token.type == Token.Type.INT_CONST)
            {
                // integer constant : e.g 723
                if (token.intVal < 0)
                {
                    // negative value
                    mWriter.WritePush(Segment.CONST, -token.intVal);
                    mWriter.WriteArithmetic(Command.NEG);
                }
                else
                {
                    // positive value
                    mWriter.WritePush(Segment.CONST, token.intVal);
                }
                mTokens.Advance();
                return true;
            }
            else if (token.type == Token.Type.STRING_CONST)
            {
                // string constant: e.g. "string constant"
                ValidateConstTerm("string constant", constOnly);
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
            else if (token.type == Token.Type.IDENTIFIER && mSymbolTable.Exists(token.identifier))
            {
                // varName
                ValidateConstTerm("variable", constOnly);
                if (ValidateSymbol(token.identifier))
                    mWriter.WritePush(mSymbolTable.SegmentOf(token.identifier), mSymbolTable.OffsetOf(token.identifier));
                mTokens.Advance();
                return true;
            }
            else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.TRUE)
            {
                // true
                mWriter.WritePush(Segment.CONST, 1);
                mWriter.WriteArithmetic(Command.NEG);
                mTokens.Advance();
                return true;
            }
            else if (token.type == Token.Type.KEYWORD && (token.keyword == Token.Keyword.FALSE || token.keyword == Token.Keyword.NULL))
            {
                // false / null
                mWriter.WritePush(Segment.CONST, 0);
                mTokens.Advance();
                return true;
            }
            else if (token.type == Token.Type.KEYWORD && token.keyword == Token.Keyword.THIS)
            {
                // this
                ValidateConstTerm("this", constOnly);
                mWriter.WritePush(Segment.POINTER, 0);
                mTokens.Advance();
                return true;
            }
            else if (token.type == Token.Type.IDENTIFIER && !mSymbolTable.Exists(token.identifier))
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
}