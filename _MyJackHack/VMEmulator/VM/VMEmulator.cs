using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace VM
{
    using BuiltInFunc = System.Action<Emulator>;

    public class Emulator
    {
        public int[] mParameters;   // parameters passed to main() if it exists
        public Instruction[] mCode; // Loaded array of Emulator commands
        public int mCodeFrame;      // Current (next) execution point

        public int[] mMemory;
        public int mMemoryDwords = 0;
        public int mStackDwords;
        public int mGlobalDwords;
        public int mHeapDwords;

        public Heap mHeap;
        public List<string> mErrors = new List<string>();
        public EmulatedObjects mObjects; // string table and other emulated application objects
        public int mStringsStatic;       // reserved static string count in mStrings

        public BuiltIn mBuiltIns; // Builtin functions

        public int mStackPushedCount;
        public int mStackPoppedCount;

        protected Thread mExecuteThread;        // Execution thread
        protected bool mExecuteThreadStop;      // Execution thread stop request
        protected long mExecuteThreadRateMicro; // Execution thread tick rate in microseconds ( 1000 per 1 ms )

        protected int mOptions;

        public enum Option
        {
            HEAP_OBJECTS,   // EmulatedObject instances use the VM heap to store the id with optional "virtual" memory attached

            COUNT
        }

        public static int Align(int value, int alignment)
        {
            int mod = value % alignment;
            if (mod == 0)
                return value;
            return value + alignment - mod;
        }

        public Emulator(int stackSizeBytes = 8192, int heapSizeBytes = 14336, int globalSizeBytes = 1024, BuiltIn builtIns = null)
        {
            if (builtIns == null)
                builtIns = Emulator.DefaultBuiltIns();
            mBuiltIns = builtIns;

            mObjects = new EmulatedObjects(this);

            Reset(stackSizeBytes, heapSizeBytes, globalSizeBytes);
        }

        public static BuiltIn DefaultBuiltIns()
        {
            BuiltIn result = new BuiltIn();

            VMOS_Sys.Register(result);
            VMOS_Memory.Register(result);
            VMOS_Math.Register(result);
            VMOS_Array.Register(result);
            VMOS_String.Register(result);

            return result;
        }

        public void OptionSet(Option op, bool enabled )
        {
            if ( enabled )
                mOptions = mOptions | (1 << (int)op);
            else
                mOptions = mOptions & ~(1 << (int)op);
        }

        public bool OptionGet(Option op)
        {
            return (mOptions & (1 << (int)op)) != 0;
        }

        public void ResetAll()
        {
            Reset();
            mParameters = null;
            mObjects = new EmulatedObjects(this);
            mStringsStatic = 0;
            mOptions = 0;
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

            mHeap = new Heap(mMemory, mMemoryDwords - mHeapDwords, mHeapDwords);

            mCodeFrame = 0;

            ClearErrors();
        }

        public void Error(string msg)
        {
            mErrors.Add(msg);
        }

        public bool Load(Stream code)
        {
            List<Instruction> codeList = new List<Instruction>();

            code.Seek(0, SeekOrigin.Begin);

            int commandInt;

            while (code.Position < code.Length)
            {
                StreamExtensions.Read(code, out commandInt);
                Instruction cmd = Converter.Translate(commandInt);
                if (cmd.mCommand == Command.STATIC_STRING)
                {
                    // static string 
                    string str;
                    StreamExtensions.Read(code, out str);
                    mObjects.RegisterType("string");
                    mObjects.Alloc("string", str, str.ToCharArray() );
                    mStringsStatic = Math.Max(mStringsStatic, cmd.mIndex + 1);
                }
                else
                {
                    // Emulator byte code instruction
                    codeList.Add(cmd);
                }
            }

            // Convert the List to a simple array for indexing efficiency
            mCode = new Instruction[codeList.Count];
            for (int i = 0; i < codeList.Count; i++)
            {
                mCode[i] = codeList[i];
            }

            return false;
        }

        public bool Finished()
        {
            if (mCode == null)
                return true;

            return mCodeFrame >= mCode.Length;
        }

        public bool Halted()
        {
            return mErrors.Count > 0;
        }

        public bool Running()
        {
            return !(Halted() || Finished());
        }

        public void SetParameters(params int[] parms)
        {
            mParameters = new int[parms.Length];
            for ( int i = 0; i < parms.Length; i++ )
            {
                mParameters[i] = parms[i];
            }
        }

        public bool ExecuteThread(bool enabled, long tickRateMicroSeconds = -1, ThreadPriority threadPriority = ThreadPriority.Normal )
        {
            if (mExecuteThreadRateMicro != 0)
            {
                mExecuteThreadStop = true;

                while (mExecuteThread != null && mExecuteThreadStop)
                {
                    Thread.Sleep(1);
                }
            }

            if (enabled)
            {
                ThreadStart threadFunc = ExecuteThreadWorker;
                mExecuteThread = new Thread(threadFunc);
                mExecuteThread.Priority = threadPriority;
                mExecuteThreadRateMicro = tickRateMicroSeconds;
                mExecuteThreadStop = false;
                mExecuteThread.Start();
                return true;
            }

            return false;
        }

        protected void ExecuteThreadWorker()
        {
            Timer timer = new Timer();

            while (!mExecuteThreadStop)
            {
                timer.Start();

                if (!ExecuteStep())
                {
                    break;
                }

                if (mExecuteThreadRateMicro > 0)
                {
                    while ( ( timer.ElapsedMicroseconds < mExecuteThreadRateMicro ) && !mExecuteThreadStop )
                    {
                        // wait
                        long timeMicroToWait = mExecuteThreadRateMicro - timer.ElapsedMicroseconds;
                        if (timeMicroToWait >= 1000)
                        {
                            int suspendMs = (int)(timeMicroToWait / 1000);
                            Thread.Sleep( suspendMs );
                        }
                    }
                }

                timer.Reset();
            }

            mExecuteThreadStop = false;
            mExecuteThreadRateMicro = 0;
        }

        public bool ExecuteStep()
        {
            if (!Running())
                return false;

            // If the first instruction is calling a function we need to make sure we push the number of parameters needed for it even if they were not provided
            if ( mCodeFrame == 0 && mCode[mCodeFrame].mCommand == Command.CALL )
            {
                int parmCount = (int) mCode[mCodeFrame].mSegment;
                for (int i = 0; i < parmCount; i++)
                {
                    if (mParameters != null && mParameters.Length > i )
                        StackPush(mParameters[i]);
                    else
                        StackPush(0);
                }
            }

            try
            {
                if (mCodeFrame < mCode.Length)
                {
                    Instruction cmd = mCode[mCodeFrame];
                    switch (cmd.mCommand)
                    {
                        case Command.PUSH:
                            DoPush(cmd.mSegment, cmd.mIndex);
                            break;

                        case Command.POP:
                            DoPop(cmd.mSegment, cmd.mIndex);
                            break;

                        case Command.LABEL:
                            // Labels have no instructions - they index the instruction that follows
                            break;

                        case Command.GOTO:
                            DoGoto(cmd.mIndex);
                            break;

                        case Command.IF_GOTO:
                            DoIfGoto(cmd.mIndex);
                            break;

						case Command.POP_GOTO:
							DoPopGoto();
							break;

						case Command.FUNCTION:
                            DoFunction(cmd.mSegment);
                            break;

                        case Command.CALL:
                            DoCall(cmd.mSegment, cmd.mIndex);
                            break;

                        case Command.RETURN:
                            DoReturn();
                            break;

                        default:
                            DoArithmetic(cmd.mCommand);
                            break;
                    }

                    return true;
                }
            }
            catch
            {
                Error("Critical Error");
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

        public void StackPush(int value)
        {
            mMemory[mMemory[(int)SegPointer.SP]] = value;
            mMemory[(int)SegPointer.SP]++;
            mStackPushedCount++;

            if (mMemory[(int)SegPointer.SP] - (int)SegPointer.COUNT > mStackDwords)
                Error("Stack overflow");
        }

        protected int SegmentAddress(Segment segment)
        {
            switch (segment)
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

            Error("Invalid segment");
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
            if (value != 0)
            {
                mCodeFrame = index;
                return true;
            }

            mCodeFrame++;
            return false;
        }

		protected bool DoPopGoto()
		{
			int value = StackPop();
            if (value >= 0 && value < mCode.Length)
            {
                mCodeFrame = value;
                return true;
            }
            Error("Next command outside code segment: " + value + " not in [0," + (mCode.Length - 1) + "]");
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
                // BUILT IN FUNCTION
                mStackPushedCount = 0;
                mStackPoppedCount = 0;

                // A built in function - call it directly and resume on the next instruction
                BuiltInFunc func = mBuiltIns.Find(index);
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
            else
            {
                // Emulator CODE FUNCTION

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

        protected void DoArithmetic(Command command)
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
                case Command.EQ: result = (x == y) ? -1 : 0; break;
                case Command.GT: result = (x > y) ? -1 : 0; break;
                case Command.LT: result = (x < y) ? -1 : 0; break;
                case Command.OR: result = x | y; break;
                case Command.XOR: result = x ^ y; break;
                case Command.LOR: result = ((x != 0) || (y != 0)) ? -1 : 0; break;
            }

            StackPush(result);
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

    ///////////////////////////////////////////////
    // Built In functions object
    public class BuiltIn
    {
        protected Dictionary<string, BuiltInFunc> mBuiltIn = new Dictionary<string, BuiltInFunc>();
        protected Dictionary<int, BuiltInFunc> mBuiltInByLabel = new Dictionary<int, BuiltInFunc>();
        protected Dictionary<string, int> mBuiltInLabels = new Dictionary<string, int>();

        protected int mFuncIndex;

        public void Register(string funcName, BuiltInFunc func)
        {
            if (mBuiltIn.ContainsKey(funcName))
                return;

            int funcLabel = -(++mFuncIndex);

            mBuiltIn.Add(funcName, func);
            mBuiltInByLabel.Add(funcLabel, func);
            mBuiltInLabels.Add(funcName, funcLabel);
        }

        public BuiltInFunc Find(string funcName)
        {
            BuiltInFunc result = null;
            mBuiltIn.TryGetValue(funcName, out result);
            return result;
        }

        public BuiltInFunc Find(int label)
        {
            BuiltInFunc result = null;
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

}

