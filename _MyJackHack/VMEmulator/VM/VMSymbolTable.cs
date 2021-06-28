using System;
using System.Collections.Generic;

class VMSymbolTable
{
    List<SymbolScope> mScopes = new List<SymbolScope>();
    Dictionary<string, int> mLabels = new Dictionary<string, int>();

    int mVarSize;

    public class Symbol
    {
        public string mVarName;   // varName
        public Kind mKind;      // STATIC, FIELD, ARG, VAR
        public VMToken mType;      // int, boolean, char, ClassName
        public int mOffset;     // segment offset
    }

    public class SymbolScope
    {
        public Dictionary<string, Symbol> mSymbols = new Dictionary<string, Symbol>();
        public string mName;
        public string mLabelContinue;
        public string mLabelBreak;
        public VMToken.Keyword mFunctionType = VMToken.Keyword.NONE;

        public SymbolScope(string name, VMToken.Keyword functionType )
        {
            mName = name;
            mFunctionType = functionType;
        }

        public SymbolScope(string name)
        {
            mName = name;
        }
    };

    public enum Kind
    {
        NONE, GLOBAL, FIELD, ARG, VAR
    }

    public void Reset()
    {
        mScopes = new List<SymbolScope>();
        ScopePush( "global" );
    }

    public void VarSizeBegin()
    {
        // Begins tracking the high water mark of VAR kind variables
        mVarSize = 0;
    }

    public int VarSizeEnd()
    {
        // Returns high water mark of VAR kind variables
        return mVarSize;
    }

    public SymbolScope ScopePush(string name, VMToken.Keyword functionType = VMToken.Keyword.NONE )
    {
        SymbolScope scope = new SymbolScope(name, functionType);
        mScopes.Add(scope);
        return scope;
    }

    public SymbolScope ScopePush(string name )
    {
        SymbolScope scope = new SymbolScope(name);
        mScopes.Add(scope);
        return scope;
    }

    public void ScopePop()
    {
        if (mScopes.Count > 0)
        {
            mScopes.RemoveAt(mScopes.Count - 1);
        }
    }

    public bool Define(string varName, VMToken type, Kind kind, string specificScope = null, int specificOffset = -1 )
    {
        if (mScopes.Count == 0)
            return false;

        if (kind == Kind.VAR)
        {
            // function local var kind must be turned to global when there is no function on the scope stack
            bool isFunctionScope = false;
            foreach (SymbolScope scope in mScopes)
            {
                if (scope.mFunctionType != VMToken.Keyword.NONE)
                {
                    isFunctionScope = true;
                    break;
                }
            }

            if (!isFunctionScope)
            {
                kind = Kind.GLOBAL;
            }
        }

        Symbol newVar = new Symbol();
        newVar.mKind = kind;
        newVar.mType = type;
        newVar.mVarName = varName;
        newVar.mOffset = 0;

        SymbolScope defineScope = mScopes[mScopes.Count - 1];

        foreach (SymbolScope scope in mScopes)
        {
            if (specificScope != null && scope.mName == specificScope )
            {
                defineScope = scope;
            }

            foreach (Symbol symbol in scope.mSymbols.Values)
            {
                if (symbol.mKind == newVar.mKind)
                {
                    newVar.mOffset = Math.Max( symbol.mOffset + 1, newVar.mOffset );
                }
            }
        }

        if (specificOffset >= 0 )
            newVar.mOffset = specificOffset;

        if ( defineScope.mSymbols.ContainsKey(varName) )
            return false;

        defineScope.mSymbols.Add(varName, newVar);
        mVarSize = Math.Max(mVarSize, KindSize(Kind.VAR));
        return true;
    }

    public VMToken.Keyword FunctionType()
    {
        foreach (SymbolScope scope in mScopes)
        {
            if (scope.mFunctionType != VMToken.Keyword.NONE)
            {
                return scope.mFunctionType;
            }
        }

        return VMToken.Keyword.NONE;
    }

    public void DefineContinueBreak( string labelContinue, string labelBreak )
    {
        if (mScopes.Count == 0)
            return;

        mScopes[mScopes.Count - 1].mLabelContinue = labelContinue;
        mScopes[mScopes.Count - 1].mLabelBreak = labelBreak;
    }

    public string GetLabelContinue()
    {
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if ( mScopes[iScope].mLabelContinue != null )
                return mScopes[iScope].mLabelContinue;
            iScope--;
        }

        return null;
    }

    public string GetLabelBreak()
    {
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if (mScopes[iScope].mLabelBreak != null)
                return mScopes[iScope].mLabelBreak;
            iScope--;
        }

        return null;
    }

    public bool Exists(string varName, string specificScope = null )
    {
        // Walk backwards from most recently added scope backward to oldest looking for the symbol
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if (specificScope != null && mScopes[iScope].mName != specificScope)
            {
                iScope--;
                continue;
            }

            Symbol result = null;
            if (varName != null && mScopes[iScope].mSymbols.TryGetValue(varName, out result))
                return true;

            iScope--;
        }

        return false;
    }

    public bool ExistsCurrentScope(string varName)
    {
        if (mScopes.Count == 0)
            return false;

        Symbol result = null;
        if (varName != null && mScopes[mScopes.Count-1].mSymbols.TryGetValue(varName, out result))
            return true;

        return false;
    }

    public Symbol Find(string varName)
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

    public bool CompilingMethod()
    {
        // Walk backwards from most recently added scope backward to oldest looking for method
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            if ( mScopes[iScope].mFunctionType == VMToken.Keyword.METHOD )
                return true;

            iScope--;
        }

        return false;
    }

    public Kind KindOf(string varName)
    {
        Symbol symbol = Find(varName);
        if (symbol != null)
            return symbol.mKind;
        return Kind.NONE;
    }

    public string TypeOf(string varName)
    {
        Symbol symbol = Find(varName);
        if (symbol != null)
            return symbol.mType.GetTokenString();
        return "";
    }

    public int OffsetOf(string varName)
    {
        Symbol symbol = Find(varName);
        if (symbol != null)
        {
            if (CompilingMethod() && symbol.mKind == Kind.ARG)
                return symbol.mOffset + 1; // skip over argument 0 (this)
            return symbol.mOffset;
        }
        return 0;
    }

    public VM.Segment SegmentOf(string varName)
    {
        Symbol symbol = Find(varName);
        if (symbol != null)
        {
            switch (symbol.mKind)
            {
                case Kind.ARG: return VM.Segment.ARG;
                case Kind.FIELD: return VM.Segment.THIS;
                case Kind.VAR: return VM.Segment.LOCAL;
                case Kind.GLOBAL: return VM.Segment.GLOBAL;
            }
        }

        return VM.Segment.INVALID;
    }

    public int KindSize(Kind kind)
    {
        int result = 0;

        for (int iScope = 0; iScope < mScopes.Count; iScope++)
        {
            foreach (Symbol symbol in mScopes[iScope].mSymbols.Values)
            {
                if (symbol.mKind == kind)
                {
                    // All symbols are 1 dword and size is measured in dwords
                    result++;
                }
            }
        }

        return result;
    }
}
