using System;
using System.Collections.Generic;

class SymbolTable
{
    static List<SymbolScope> mScopes = new List<SymbolScope>();
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
        public SymbolScope(string name = "", bool isMethod = false)
        {
            mName = name;
            mMethod = isMethod;
        }
    };

    public enum Kind
    {
        NONE, GLOBAL, STATIC, FIELD, ARG, VAR
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

    public static void ScopePop()
    {
        if (mScopes.Count > 0)
        {
            mScopes.RemoveAt(mScopes.Count - 1);
        }
    }

    public static void Define(string varName, Token type, Kind kind)
    {
        if (mScopes.Count == 0)
            return;

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

        mScopes[mScopes.Count - 1].mSymbols.Add(varName, newVar);

        mVarSize = Math.Max(mVarSize, SymbolTable.KindSize(SymbolTable.Kind.VAR));
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
        return null;
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

    public static IVMWriter.Segment SegmentOf(string varName)
    {
        Symbol symbol = SymbolTable.Find(varName);
        if (symbol != null)
        {
            switch (symbol.mKind)
            {
                case Kind.ARG: return IVMWriter.Segment.ARG;
                case Kind.FIELD: return IVMWriter.Segment.THIS;
                case Kind.STATIC: return IVMWriter.Segment.STATIC;
                case Kind.VAR: return IVMWriter.Segment.LOCAL;
            }
        }

        return IVMWriter.Segment.INVALID;
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
