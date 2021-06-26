using System;
using System.IO;
using System.Collections.Generic;

public class VM
{
    public List<VMCommand> mCode;
    public int mCodeFrame;

    public int[] mMemory;
    public int mMemoryDwords;
    public int mStackDwords;
    public int mGlobalDwords;
    public int mHeapDwords;
    public int mHeap;

    public enum SegPointer : byte
    {
        SP, ARG, LOCAL, GLOBAL, THIS, THAT, POINTER, TEMP,
        COUNT
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

    public VM( int stackSizeBytes = 8192, int heapSizeBytes = 14336, int globalSizeBytes = 1024 )
    {
        Reset(stackSizeBytes, heapSizeBytes, globalSizeBytes);
    }

    public void Reset()
    {
        Reset(mStackDwords * 4, mHeapDwords * 4, mGlobalDwords * 4);
    }

    public void Reset(int stackSizeBytes, int heapSizeBytes, int globalSizeBytes)
    {
        // Round up the byte sizes to be 4 byte aligned
        heapSizeBytes = heapSizeBytes + (heapSizeBytes % 4);
        stackSizeBytes = stackSizeBytes + (stackSizeBytes % 4);
        globalSizeBytes = globalSizeBytes + (globalSizeBytes % 4);

        mStackDwords = stackSizeBytes / 4;
        mHeapDwords = heapSizeBytes / 4;
        mGlobalDwords = globalSizeBytes / 4;

        mMemoryDwords = mStackDwords + mHeapDwords + mGlobalDwords + (int)SegPointer.COUNT;

        mMemory = new int[mMemoryDwords];
        mMemory[(int)SegPointer.SP] = (int)SegPointer.COUNT;
        mMemory[(int)SegPointer.GLOBAL] = mMemory[(int)SegPointer.SP] + mStackDwords;
        mHeap = mMemory[(int)SegPointer.GLOBAL] + mGlobalDwords;

        mCodeFrame = 0;
    }

    protected void Error(string msg)
    {
        // FIXME
    }

    public bool Load(Stream code)
    {
        mCode = new List<VMCommand>();

        code.Seek(0, SeekOrigin.Begin);

        int commandInt;

        while ( code.Position < code.Length )
        {
            StreamExtensions.Read(code, out commandInt);
            mCode.Add(VMByteConvert.Translate(commandInt));
        }

        return false;
    }

    public void ExecuteStep()
    {
        if (mCodeFrame < mCode.Count)
        {
            VMCommand cmd = mCode[mCodeFrame];
            switch (cmd.mCommand)
            {
                case VM.Command.PUSH:
                    DoPush( cmd.mSegment, cmd.mIndex );
                    break;

                case VM.Command.POP:
                    DoPop( cmd.mSegment, cmd.mIndex);
                    break;

                case VM.Command.LABEL:
                    break;

                case VM.Command.GOTO:
                    DoGoto( cmd.mIndex );
                    break;

                case VM.Command.IF_GOTO:
                    DoIfGoto(cmd.mIndex);
                    break;

                case VM.Command.FUNCTION:
                    DoFunction( cmd.mSegment );
                    break;

                case VM.Command.CALL:
                    DoCall( cmd.mSegment, cmd.mIndex );
                    break;

                case VM.Command.RETURN:
                    DoReturn();
                    break;

                default:
                    DoArithmetic( cmd.mCommand );
                    break;
            }
        }
    }

    protected int StackPop()
    {
        if (mMemory[(int)SegPointer.SP] == (int)SegPointer.COUNT)
            Error("Stack cannot be popped when empty");

        mMemory[(int)SegPointer.SP]--;
        return mMemory[mMemory[(int)SegPointer.SP]];
    }

    protected void StackPush( int value )
    {
        mMemory[mMemory[(int)SegPointer.SP]] = value;
        mMemory[(int)SegPointer.SP]++;

        if (mMemory[(int)SegPointer.SP] - (int)SegPointer.COUNT > mStackDwords )
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
        int x = StackPop();
        int y = StackPop();
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

