using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VM
{
    public class Debugger : ISerializable
    {
        // Persistent data
        public Dictionary<int, DebugCommand> mCommandMap = new Dictionary<int, DebugCommand>();
        public Dictionary<string, DebugSymbolMap> mSymbolMap = new Dictionary<string, DebugSymbolMap>();
        public SymbolTable.SymbolScope mGlobals;

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ((ISerializable)mSymbolMap).GetObjectData(info, context);
            ((ISerializable)mCommandMap).GetObjectData(info, context);
            ((ISerializable)mGlobals).GetObjectData(info, context);
        }

        // Data used while compiling
        public int mCommandsWritten = 0;
        public Tokenizer mTokens;
        public List<DebugSymbolsPush> mScopeStack = new List<DebugSymbolsPush>();
        public bool mAddedCode; // when true code is being compiled that is not part of source code

        public class DebugSymbolMap
        {
            // One of these per source file
            // Provide a line number and this will generate a SymbolTable by walking from the first line pushing and popping scope
            // Essentially a recording of symbol scopes while compiling the file
            public Dictionary<int, SymbolTable.SymbolScope> mScopes = new Dictionary<int, SymbolTable.SymbolScope>();
            public SymbolTable.SymbolScope mGlobals;

            public DebugSymbolMap(SymbolTable.SymbolScope globals)
            {
                mGlobals = globals;
            }

            public SymbolTable GetSymbolTable(int lineNumber)
            {
                SymbolTable result = new SymbolTable();

                result.ScopePush( mGlobals );

                // "Play back" the file from line 1 to the desired line re-pushing and re-popping scopes along the way
                for (int i = 1; i <= lineNumber; i++)
                {
                    SymbolTable.SymbolScope scope;
                    if (mScopes.TryGetValue(i, out scope))
                    {
                        if (scope != null)
                            result.ScopePush(scope);
                        else
                            result.ScopePop();
                    }
                }

                return result;
            }
        }

        public class DebugLine
        {
            public string mSource;
            public int mSourceLine;
            public int mSourceChar;

            public DebugLine(Tokenizer tokens)
            {
                Token token = tokens.Get();
                mSource = tokens.mSource;
                mSourceLine = token.lineNumber;
                mSourceChar = token.lineCharacter;
            }

            public DebugLine(Token token)
            {
                mSource = token.tokenizer.mSource;
                mSourceLine = token.lineNumber;
                mSourceChar = token.lineCharacter;
            }
        }

        public class DebugCommand : DebugLine
        {
            public int mCommandIndex;

            public DebugCommand(Tokenizer tokens, int cmdIndex ) : base(tokens)
            {
                mCommandIndex = cmdIndex;
            }
        }

        public class DebugSymbolsPush : DebugLine
        {
            public SymbolTable.SymbolScope mScope;

            public DebugSymbolsPush(Token token, SymbolTable.SymbolScope scope) : base(token)
            {
                mScope = scope;
            }
        }

        public bool GetSymbolValue(Emulator vm, string source, int line, string varName, out int value)
        {
            value = 0;

            SymbolTable table = GetSymbolTable(source, line);
            if (table != null)
            {
                SymbolTable.Symbol symbol = table.Find(varName);

                if (symbol != null)
                {
                    switch (symbol.mKind)
                    {
                        case SymbolTable.Kind.ARG:
                            value = vm.mMemory[vm.mMemory[(int)SegPointer.ARG] + symbol.mOffset];
                            break;
                        case SymbolTable.Kind.GLOBAL:
                            value = vm.mMemory[vm.mMemory[(int)SegPointer.GLOBAL] + symbol.mOffset];
                            break;
                        case SymbolTable.Kind.VAR:
                            value = vm.mMemory[vm.mMemory[(int)SegPointer.LOCAL] + symbol.mOffset];
                            break;
                        case SymbolTable.Kind.FIELD:
                            // FIXME
                            value = 999999;
                            break;
                    }

                    return true;
                }
            }

            return false;
        }

        public SymbolTable GetSymbolTable(string source, int lineNumber)
        {
            DebugSymbolMap map;
            if (mSymbolMap.TryGetValue( source, out map )) 
            {
                SymbolTable result = map.GetSymbolTable(lineNumber);
                return result;
            }

            return null;
        }

        public void WriteCommand( string vmTextLine )
        {
            if (mTokens == null || vmTextLine.StartsWith( "label" ) )
                return;

            DebugCommand cmd = new DebugCommand( mTokens, mCommandsWritten );

            if ( mAddedCode )
                cmd.mSourceLine = -1;

            mCommandMap.Add( mCommandsWritten, cmd );

            mCommandsWritten++;
        }

        public void ScopePush(SymbolTable.SymbolScope scope, Token tknSource)
        {
            if (tknSource == null)
                mGlobals = scope;
            else
                mScopeStack.Add(new DebugSymbolsPush(tknSource, scope));
        }

        public void ScopePop(SymbolTable.SymbolScope scope, Token tknSource)
        {
            if ( tknSource != null )
            {
                string key = tknSource.tokenizer.mSource;

                if (!mSymbolMap.ContainsKey(key))
                {
                    DebugSymbolMap map = new DebugSymbolMap( mGlobals );
                    mSymbolMap.Add(key, map);
                }
                
                DebugSymbolsPush pushed = mScopeStack[mScopeStack.Count - 1];
                if ( pushed.mScope.mSymbols.Count > 0 )
                {
                    if ( !mSymbolMap[key].mScopes.ContainsKey(pushed.mSourceLine) )
                        mSymbolMap[key].mScopes.Add(pushed.mSourceLine, pushed.mScope);

                    if (!mSymbolMap[key].mScopes.ContainsKey(tknSource.lineNumber))
                        mSymbolMap[key].mScopes.Add(tknSource.lineNumber, null);
                }

                mScopeStack.RemoveAt(mScopeStack.Count - 1);
            }
        }
    }
}