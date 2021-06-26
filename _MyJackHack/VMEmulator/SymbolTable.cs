using System;
using System.Collections.Generic;

class SymbolTable
{
    static List<SymbolScope> mScopes = new List<SymbolScope>();
    static Dictionary<string, int> mLabels = new Dictionary<string, int>();
    static SymbolTable mTheTable = new SymbolTable();
    static int mVarSize;

    public class Symbol
    {
        public string mVarName;   // varName
        public Kind mKind;      // STATIC, FIELD, ARG, VAR
        public Token mType;      // int, boolean, char, ClassName
        public int mOffset;     // segment offset
    }

    public class SymbolScope
    {
        public Dictionary<string, Symbol> mSymbols = new Dictionary<string, Symbol>();
        public string mName;
        public bool mMethod;
        public string mLabelContinue;
        public string mLabelBreak;
        public Kind mForcedKind = Kind.NONE;

        public SymbolScope(string name, bool isMethod = false)
        {
            mName = name;
            mMethod = isMethod;
        }

        public SymbolScope(string name, Kind forcedKind)
        {
            mName = name;
            mMethod = false;
            mForcedKind = forcedKind;
        }
    };

    public enum Kind
    {
        NONE, GLOBAL, FIELD, ARG, VAR
    }

    public static void Reset()
    {
        SymbolTable.mScopes = new List<SymbolScope>();
        SymbolTable.ScopePush("global", Kind.GLOBAL );
    }

    public static void VarSizeBegin()
    {
        // Begins tracking the high water mark of VAR kind variables
        SymbolTable.mVarSize = 0;
    }

    public static int VarSizeEnd()
    {
        // Returns high water mark of VAR kind variables
        return SymbolTable.mVarSize;
    }

    public static SymbolScope ScopePush(string name, bool isMethod = false)
    {
        SymbolScope scope = new SymbolScope(name, isMethod);
        mScopes.Add(scope);
        return scope;
    }

    public static SymbolScope ScopePush(string name, Kind forcedKind )
    {
        SymbolScope scope = new SymbolScope(name, forcedKind);
        mScopes.Add(scope);
        return scope;
    }

    public static void ScopePop()
    {
        if (mScopes.Count > 0)
        {
            mScopes.RemoveAt(mScopes.Count - 1);
        }
    }

    public static bool Define(string varName, Token type, Kind kind)
    {
        if (mScopes.Count == 0)
            return false;

        if ( mScopes[mScopes.Count - 1].mForcedKind != Kind.NONE )
        {
            kind = mScopes[mScopes.Count - 1].mForcedKind;
        }

        Symbol newVar = new Symbol();
        newVar.mKind = kind;
        newVar.mType = type;
        newVar.mVarName = varName;
        newVar.mOffset = 0;

        foreach (SymbolScope scope in mScopes)
        {
            foreach (Symbol symbol in scope.mSymbols.Values)
            {
                if (symbol.mKind == newVar.mKind)
                {
                    newVar.mOffset = newVar.mOffset + 1;
                }
            }
        }

        if ( mScopes[mScopes.Count - 1].mSymbols.ContainsKey(varName) )
            return false;

        mScopes[mScopes.Count - 1].mSymbols.Add(varName, newVar);
        mVarSize = Math.Max(mVarSize, SymbolTable.KindSize(SymbolTable.Kind.VAR));
        return true;
    }

    public static void DefineContinueBreak( string labelContinue, string labelBreak )
    {
        if (mScopes.Count == 0)
            return;

        mScopes[mScopes.Count - 1].mLabelContinue = labelContinue;
        mScopes[mScopes.Count - 1].mLabelBreak = labelBreak;
    }

    public static string GetLabelContinue()
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

    public static string GetLabelBreak()
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

    public static bool Exists(string varName)
    {
        // Walk backwards from most recently added scope backward to oldest looking for the symbol
        int iScope = mScopes.Count - 1;

        while (iScope >= 0)
        {
            Symbol result = null;
            if (varName != null && mScopes[iScope].mSymbols.TryGetValue(varName, out result))
                return true;
            iScope--;
        }

        return false;
    }

    public static bool ExistsCurrentScope(string varName)
    {
        if (mScopes.Count == 0)
            return false;

        Symbol result = null;
        if (varName != null && mScopes[mScopes.Count-1].mSymbols.TryGetValue(varName, out result))
            return true;

        return false;
    }

    public static Symbol Find(string varName)
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
            if (mScopes[iScope].mMethod)
                return true;

            iScope--;
        }

        return false;
    }

    public static Kind KindOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
            return symbol.mKind;
        return Kind.NONE;
    }

    public static string TypeOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
            return symbol.mType.GetTokenString();
        return "";
    }

    public static int OffsetOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
        {
            if (SymbolTable.CompilingMethod() && symbol.mKind == Kind.ARG)
                return symbol.mOffset + 1; // skip over argument 0 (this)
            return symbol.mOffset;
        }
        return 0;
    }

    public static VM.Segment SegmentOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
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

    public static int KindSize(Kind kind)
    {
        int result = 0;

        for (int iScope = 0; iScope < mScopes.Count; iScope++)
        {
            foreach (Symbol symbol in mScopes[iScope].mSymbols.Values)
            {
                if (symbol.mKind == kind)
                {
                    // in Hack all symbols are 1 word and size is measured in words
                    result++;
                }
            }
        }

        return result;
    }
}
