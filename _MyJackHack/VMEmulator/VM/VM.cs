﻿using System;
using System.IO;
using System.Collections.Generic;

using VMBuiltInFunc = System.Action<VM>;

public class VM
{
    public List<string> mErrors = new List<string>();

    public List<VMCommand> mCode;
    public int mCodeFrame;

    public int[] mMemory;
    public int mMemoryDwords = 0;
    public int mStackDwords;
    public int mGlobalDwords;
    public int mHeapDwords;

    public VMHeap mHeap;

    public VMBuiltIn mBuiltIns;

    public int mStackPushedCount;
    public int mStackPoppedCount;

    public enum SegPointer : byte
    {
        SP, ARG, LOCAL, GLOBAL, THIS, THAT, TEMP,
        COUNT, POINTER,
    }

    public enum Command : byte
    {
        PUSH, POP, FUNCTION, CALL, LABEL, GOTO, IF_GOTO, RETURN,
        ADD, SUB, NEG, EQ, LT, GT, AND, LAND, OR, XOR, LOR, NOT, LNOT, MUL, DIV, MOD,
        INVALID
    }

    public enum Segment : byte
    {
        GLOBAL, CONST, ARG, LOCAL, THIS, THAT, POINTER, TEMP,
        INVALID
    }

    public class VMCommand
    {
        public Command mCommand;
        public Segment mSegment;
        public int mIndex;

        public VMCommand( Command command, Segment segment, int index )
        {
            mCommand = command;
            mSegment = segment;
            mIndex = index;
        }
    }

    public static int Align(int value, int alignment)
    {
        int mod = value % alignment;
        if (mod == 0)
            return value;        
        return value + alignment - mod;
    }

    public VM( int stackSizeBytes = 8192, int heapSizeBytes = 14336, int globalSizeBytes = 1024, VMBuiltIn builtIns = null )
    {
        if (builtIns == null)
            builtIns = VM.DefaultBuiltIns();
        mBuiltIns = builtIns;

        Reset(stackSizeBytes, heapSizeBytes, globalSizeBytes);
    }

    public static VMBuiltIn DefaultBuiltIns()
    {
        VMBuiltIn result = new VMBuiltIn();

        VMOS_Sys.Register(result);
        VMOS_Memory.Register(result);
        VMOS_Math.Register(result);
        VMOS_Array.Register(result);

        return result;
    }

    public void Reset()
    {
        Reset(mStackDwords * 4, mHeapDwords * 4, mGlobalDwords * 4);
    }

    public void ClearErrors()
    {
        mErrors = new List<string>();
    }

    public void Reset(int stackSizeBytes, int heapSizeBytes, int globalSizeBytes)
    {
        // Round up the byte sizes to be 4 byte aligned
        heapSizeBytes = Align(heapSizeBytes, 4);
        stackSizeBytes = Align(stackSizeBytes, 4);
        globalSizeBytes = Align(globalSizeBytes, 4);

        mStackDwords = stackSizeBytes / 4;
        mHeapDwords = heapSizeBytes / 4;
        mGlobalDwords = globalSizeBytes / 4;

        int prevMemDwords = mMemoryDwords;
        mMemoryDwords = mStackDwords + mHeapDwords + mGlobalDwords + (int)SegPointer.COUNT;

        if (prevMemDwords != mMemoryDwords)
        {
            // Allocate new memory
            mMemory = new int[mMemoryDwords];
        }

        // Zero out the memory
        for (int i = 0; i < mMemory.Length; i++)
            mMemory[i] = 0;

        // Set stack pointer and global pointer
        mMemory[(int)SegPointer.SP] = (int)SegPointer.COUNT;
        mMemory[(int)SegPointer.GLOBAL] = (int)SegPointer.COUNT + mStackDwords;

        mHeap = new VMHeap(mMemory, mMemoryDwords - mHeapDwords, mHeapDwords);

        mCodeFrame = 0;

        ClearErrors();
    }

    public void Error(string msg)
    {
        mErrors.Add( msg );
    }

    public bool Load(Stream code)
    {
        mCode = new List<VMCommand>();

        code.Seek(0, SeekOrigin.Begin);

        int commandInt;

        while ( code.Position < code.Length )
        {
            VMStream.Read(code, out commandInt);
            mCode.Add(VMByteCode.Translate(commandInt));
        }

        return false;
    }

    public bool Finished()
    {
        return mCodeFrame >= mCode.Count;
    }

    public bool Halted()
    {
        return mErrors.Count > 0;
    }

    public bool Running()
    {
        return !(Halted() || Finished());
    }

    public bool ExecuteStep()
    {
        if ( !Running() )
            return false;

        if (mCodeFrame < mCode.Count)
        {
            VMCommand cmd = mCode[mCodeFrame];
            switch (cmd.mCommand)
            {
                case VM.Command.PUSH:
                    DoPush(cmd.mSegment, cmd.mIndex);
                    break;

                case VM.Command.POP:
                    DoPop(cmd.mSegment, cmd.mIndex);
                    break;

                case VM.Command.LABEL:
                    break;

                case VM.Command.GOTO:
                    DoGoto(cmd.mIndex);
                    break;

                case VM.Command.IF_GOTO:
                    DoIfGoto(cmd.mIndex);
                    break;

                case VM.Command.FUNCTION:
                    DoFunction(cmd.mSegment);
                    break;

                case VM.Command.CALL:
                    DoCall(cmd.mSegment, cmd.mIndex);
                    break;

                case VM.Command.RETURN:
                    DoReturn();
                    break;

                default:
                    DoArithmetic(cmd.mCommand);
                    break;
            }

            return true;
        }

        return false;
    }

    public int StackPop()
    {
        int sp = mMemory[(int)SegPointer.SP];
        if (sp == (int)SegPointer.COUNT)
        {
            Error("Stack cannot be popped when empty");
            return 0;
        }
        if (sp < (int)SegPointer.COUNT)
        {
            Error("Stack segment violation ( below stack frame )");
            return 0;
        }
        if (sp > (int)SegPointer.COUNT + mStackDwords) 
        {
            Error("Stack segment violation ( above stack frame )");
            return 0;
        }

        mStackPoppedCount++;
        mMemory[(int)SegPointer.SP]--;
        return mMemory[mMemory[(int)SegPointer.SP]];
    }

    public void StackPush( int value )
    {
        mMemory[mMemory[(int)SegPointer.SP]] = value;
        mMemory[(int)SegPointer.SP]++;
        mStackPushedCount++;

        if (mMemory[(int)SegPointer.SP] - (int)SegPointer.COUNT > mStackDwords)
            Error("Stack overflow");
    }

    protected int SegmentAddress(Segment segment)
    {
        switch ( segment )
        {
            case Segment.ARG:
                return mMemory[(int)SegPointer.ARG];
            case Segment.LOCAL:
                return mMemory[(int)SegPointer.LOCAL];
            case Segment.THIS:
                return mMemory[(int)SegPointer.THIS];
            case Segment.THAT:
                return mMemory[(int)SegPointer.THAT];
            case Segment.GLOBAL:
                return mMemory[(int)SegPointer.GLOBAL];

            case Segment.TEMP:
                return (int)SegPointer.TEMP;

            case Segment.POINTER:
                return (int)SegPointer.THIS;
        }

        Error( "Invalid segment" );
        return -1;
    }

    protected void DoPush(Segment segment, int index)
    {
        int value = 0;
        if (segment == Segment.CONST)
            value = index;
        else
            value = mMemory[SegmentAddress(segment) + index];
        StackPush(value);
        mCodeFrame++;
    }

    protected void DoPop(Segment segment, int index)
    {
        int value = StackPop(); 
        mMemory[SegmentAddress(segment) + index] = value;
        mCodeFrame++;
    }

    protected void DoGoto(int index)
    {
        mCodeFrame = index;
    }

    protected bool DoIfGoto(int index)
    {
        int value = StackPop();
        if ( value != 0 )
        {
            mCodeFrame = index;
            return true;
        }

        mCodeFrame++;
        return false;
    }

    protected void DoFunction(Segment segment)
    {
        int args = (int)segment;
        for (int i = 0; i < args; i++)
            StackPush(0);
        mCodeFrame++;
    }

    protected void DoCall(Segment segment, int index)
    {
        int args = (int)segment;

        if (index < 0)
        {
            mStackPushedCount = 0;
            mStackPoppedCount = 0;

            // A built in function - call it directly and resume on the next instruction
            VMBuiltInFunc func = mBuiltIns.Find(index);
            if (func != null)
            {
                func(this);
            }
            else
            {
                Error("Built in function not found");
            }

            // Make sure we keep the stack correct even if function did not 
            for (int i = 0; i < args - mStackPoppedCount; i++)
                StackPop();
            if (mStackPushedCount == 0)
                StackPush(0);

            mCodeFrame++;
            return;
        }

        // push returnAddress
        StackPush(mCodeFrame + 1);

        // push LCL pointer value
        StackPush(mMemory[(int)SegPointer.LOCAL]);

        // push ARG pointer value
        StackPush(mMemory[(int)SegPointer.ARG]);

        // push THIS pointer value
        StackPush(mMemory[(int)SegPointer.THIS]);

        // push THAT pointer value
        StackPush(mMemory[(int)SegPointer.THAT]);

        // ARG = SP - 5 - nArgs
        mMemory[(int)SegPointer.ARG] = mMemory[(int)SegPointer.SP] - 5 - args;

        // LCL = SP
        mMemory[(int)SegPointer.LOCAL] = mMemory[(int)SegPointer.SP];

        // goto functionName
        mCodeFrame = index;
    }

    protected void DoReturn()
    {
        // endFrame = LCL 
        int endFrame = mMemory[(int)SegPointer.LOCAL];

        // retAddr = *(endFrame-5)
        int retAddr = mMemory[endFrame - 5];

        // *ARG = pop()
        mMemory[mMemory[(int)SegPointer.ARG]] = StackPop();

        // SP=ARG+1
        mMemory[(int)SegPointer.SP] = mMemory[(int)SegPointer.ARG] + 1;

        // THAT = *(endFrame-1)
        mMemory[(int)SegPointer.THAT] = mMemory[endFrame - 1];

        // THIS = *(endFrame-2)
        mMemory[(int)SegPointer.THIS] = mMemory[endFrame - 2];

        // ARG = *(endFrame-3)
        mMemory[(int)SegPointer.ARG] = mMemory[endFrame - 3];

        // LCL = *(endFrame-4)
        mMemory[(int)SegPointer.LOCAL] = mMemory[endFrame - 4];

        // goto retAddr
        mCodeFrame = retAddr;
    }

    protected void DoArithmetic( Command command )
    {
        switch (command)
        {
            case Command.NEG:
            case Command.NOT:
            case Command.LNOT:
                DoMathX(command);
                break;

            default:
                DoMathXandY(command);
                break;
        }

        mCodeFrame++;
    }

    protected void DoMathXandY(Command command)
    {
        int y = StackPop();
        int x = StackPop();
        int result = 0;

        switch (command)
        {
            case Command.ADD: result = x + y; break;
            case Command.SUB: result = x - y; break;
            case Command.MUL: result = x * y; break;
            case Command.DIV: result = x / y; break;
            case Command.MOD: result = x % y; break;
            case Command.AND: result = x & y; break;
            case Command.LAND: result = ((x != 0) && (y != 0)) ? -1 : 0; break;
            case Command.EQ: result = ( x == y ) ? -1 : 0; break;
            case Command.GT: result = ( x > y ) ? -1 : 0; break;
            case Command.LT: result = ( x < y ) ? -1 : 0; break;
            case Command.OR: result = x | y; break;
            case Command.XOR: result = x ^ y; break;
            case Command.LOR: result = ((x != 0) || (y != 0)) ? -1 : 0; break;
        }

        StackPush( result );
    }

    protected void DoMathX(Command command)
    {
        int x = StackPop();
        int result = 0;

        switch (command)
        {
            case Command.NEG: result = -x; break;
            case Command.NOT: result = ~x; break;
            case Command.LNOT: result = (x == 0) ? -1 : 0; break;
        }

        StackPush(result);
    }
}

/////////////////////////////////////////////////////////////////////////////////
// VMHeap - manages heap section of an already allocated int array of memory
public class VMHeap
{
    int[] mMemory;

    int mHeapBase;
    int mHeapFree;
    int mHeapCount;

    public VMHeap( int[] memory, int start, int count )
    {
        mMemory = memory;
        mHeapCount = count;
        mHeapBase = start;
        mHeapFree = start;

        mMemory[mHeapFree] = 0; // next
        mMemory[mHeapFree + 1] = mHeapCount; // size
    }

    public int Alloc( int size )
    {
        int freeList = mHeapFree;
        int resultBlock = 0;

        if (size == 0)
            return 0;

        while ( resultBlock == 0 && freeList != 0 )
        {
            // freeList[0]: next
            // freeList[1]: size

            if ( mMemory[freeList + 1] > (size + 1) )
            {
                // Found first fit block that is big enough

                // result block is allocated at the end of the free block's chunk of memory
                resultBlock = freeList + mMemory[freeList + 1] - (size + 2);
                mMemory[resultBlock] = 0;
                mMemory[resultBlock+1] = size;

                // reduce the free memory size from this block we just pulled from
                mMemory[freeList + 1] = mMemory[freeList + 1] - (size + 2);
            }

            freeList = mMemory[freeList];
        }

        if (resultBlock == 0)
        {
            // Could not allocate memory
            return 0;
        }

        // return the usable memory part of the block
        return resultBlock + 2;
    }

    public void Free( int addr )
    {
        int prevFreeList;

        if (addr == 0)
            return;

        // block address is usable memory pointer - 2
        int block = addr - 2;

        // block[0]: next
        // block[1]: size

        // append the block to the freeList
        prevFreeList = mHeapFree;
        mHeapFree = block;
        mMemory[block] = prevFreeList;
    }

    public bool DeFrag()
    {
        // FIXME
        return false;
    }
}

///////////////////////////////////////////////
// Built In functions object
public class VMBuiltIn
{
    protected Dictionary<string, VMBuiltInFunc> mBuiltIn = new Dictionary<string, VMBuiltInFunc>();
    protected Dictionary<int, VMBuiltInFunc> mBuiltInByLabel = new Dictionary<int, VMBuiltInFunc>();
    protected Dictionary<string, int> mBuiltInLabels = new Dictionary<string, int>();

    protected int mFuncIndex;

    public void Register(string funcName, VMBuiltInFunc func)
    {
        if (mBuiltIn.ContainsKey(funcName))
            return;

        int funcLabel = -(++mFuncIndex);

        mBuiltIn.Add(funcName, func);
        mBuiltInByLabel.Add(funcLabel, func);
        mBuiltInLabels.Add(funcName, funcLabel);
    }

    public VMBuiltInFunc Find(string funcName)
    {
        VMBuiltInFunc result = null;
        mBuiltIn.TryGetValue(funcName, out result);
        return result;
    }

    public VMBuiltInFunc Find(int label)
    {
        VMBuiltInFunc result = null;
        mBuiltInByLabel.TryGetValue(label, out result);
        return result;
    }

    public int FindLabel(string funcName)
    {
        int result = 0;
        mBuiltInLabels.TryGetValue(funcName, out result);
        return result;
    }
}
