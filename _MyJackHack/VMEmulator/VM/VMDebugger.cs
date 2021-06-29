using System;
using System.Collections.Generic;

namespace VM
{
    public class Debugger
    {
        // Persistent data
        public Dictionary<int, DebugCommand> mCommandMap = new Dictionary<int, DebugCommand>();
        public Dictionary<string, DebugSymbolMap> mSymbolMap = new Dictionary<string, DebugSymbolMap>();
        public SymbolTable.SymbolScope mGlobals;

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
            public Dictionary<uint, SymbolTable.SymbolScope> mScopes = new Dictionary<uint, SymbolTable.SymbolScope>();
            public Dictionary<int, int> mLineChars = new Dictionary<int, int>();
            public SymbolTable.SymbolScope mGlobals;

            public DebugSymbolMap(SymbolTable.SymbolScope globals)
            {
                mGlobals = globals;
            }

            public SymbolTable GetSymbolTable(int lineNum, int charNum )
            {
                SymbolTable result = new SymbolTable();
                SymbolTable.SymbolScope scope;

                result.ScopePush( mGlobals );

                // "Play back" the file from line 1 to the desired line and character re-pushing and re-popping scopes along the way
                for (int i = 1; i <= lineNum; i++)
                {
                    int lineChars;
                    if (mLineChars.TryGetValue(lineNum, out lineChars))
                    {
                        for (int c = 0; c <= lineChars; c++)
                        {
                            uint key = ScopeMarkerKey(i, c);
                            if (mScopes.TryGetValue(key, out scope))
                            {
                                if (scope != null)
                                    result.ScopePush(scope);
                                else
                                    result.ScopePop();
                            }
                        }
                    }
                }

                return result;
            }

            public uint ScopeMarkerKey(int lineNum, int charNum)
            {
                return (uint)(lineNum << 10) | (uint)charNum;
            }

            public void AddScopeMarker(int lineNum, int charNum, SymbolTable.SymbolScope scope)
            {
                // Make sure we are keeping track of the max character count per line where it matters
                if (mLineChars.ContainsKey(lineNum))
                    mLineChars[lineNum] = Math.Max(mLineChars[lineNum], charNum);
                else
                    mLineChars.Add(lineNum, charNum);

                uint key = ScopeMarkerKey(lineNum, charNum);
                if( !mScopes.ContainsKey(key) )
                    mScopes.Add(key, scope);
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

        public void SetMaxLineChar(int lineNum, int charNum)
        {

        }

        public string GetSegmentSymbol(SymbolTable.Kind kind, int offset, string source, int lineNum, int charNum )
        {
            string result = "";

            SymbolTable table = GetSymbolTable(source, lineNum, charNum);
            if (table != null)
            {
                result = table.SegmentSymbol( kind, offset );
            }

            return result;
        }

        public bool GetSymbolValue(Emulator vm, string source, int lineNum, int charNum, string varName, out int value)
        {
            value = 0;

            SymbolTable table = GetSymbolTable(source, lineNum, charNum );
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
                            int pThis = vm.mMemory[(int)SegPointer.THIS];
                            if ( pThis != 0 )
                                value = vm.mMemory[pThis + symbol.mOffset];
                            break;
                    }

                    return true;
                }
            }

            return false;
        }

        public SymbolTable GetSymbolTable(string source, int lineNum, int charNum )
        {
            DebugSymbolMap map;
            if (mSymbolMap.TryGetValue( source, out map )) 
            {
                SymbolTable result = map.GetSymbolTable(lineNum, charNum);
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
                DebugSymbolMap map;
                string key = tknSource.tokenizer.mSource;

                if (!mSymbolMap.TryGetValue(key, out map))
                {
                    map = new DebugSymbolMap(mGlobals);
                    mSymbolMap.Add(key, map);
                }

                if (mScopeStack.Count > 1)
                {
                    DebugSymbolsPush pushed = mScopeStack[mScopeStack.Count - 1];
                    if (pushed.mScope.mSymbols.Count > 0)
                    {
                        map.AddScopeMarker(pushed.mSourceLine, pushed.mSourceChar, pushed.mScope);
                        map.AddScopeMarker(tknSource.lineNumber, tknSource.lineCharacter, null);
                    }

                    mScopeStack.RemoveAt(mScopeStack.Count - 1);
                }
            }
        }
    }
}