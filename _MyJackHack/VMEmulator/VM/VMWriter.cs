using System.IO;
using System.Collections.Generic;

namespace VM
{
    // IWriter interface: Compiler interfaces with this
    public interface IWriter
    {
        public void WriteLine(string line); // for comments mainly
        public void WritePush(Segment segment, int index);
        public void WritePop(Segment segment, int index);
        public void WriteArithmetic(Command command);
        public void WriteLabel(string label);
        public void WriteGoto(string label);
        public void WriteIfGoto(string label);
        public void WriteFunction(string function, int argCount);
        public void WriteCall(string function, int argCount);
        public void WriteReturn();
        public void WriteStream(Stream stream);

        public void Enable();
        public void Disable();
        public bool IsEnabled();

        public void OutputPush(Stream stream);
        public void OutputPop();

        public void SetDebugger(Debugger debugger);
    }

    public class WriterBase
    {
        protected StreamWriter mFile;
        protected bool mEnabled = true;
        protected List<StreamWriter> mOutput = new List<StreamWriter>();

        public Debugger mDebugger;

        public WriterBase(string outFile)
        {
            mFile = new StreamWriter(outFile);
            mFile.AutoFlush = true;
            mOutput.Add(mFile);
        }

        public WriterBase(Stream outStream)
        {
            mFile = new StreamWriter(outStream);
            mFile.AutoFlush = true;
            mOutput.Add(mFile);
        }

        public virtual void WriteLine(string line)
        {
            if (mEnabled)
            {
                mOutput[mOutput.Count - 1].WriteLine(line);
                if (mOutput[mOutput.Count - 1] == mFile && mDebugger != null)
                    mDebugger.WriteCommand(line);
            }
        }

        public virtual void WriteStream(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            stream.Seek(0, SeekOrigin.Begin);
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                mOutput[mOutput.Count - 1].WriteLine(line);
                if (mOutput[mOutput.Count - 1] == mFile && mDebugger != null)
                    mDebugger.WriteCommand(line );
            }
        }

        public void SetDebugger(Debugger debugger)
        {
            mDebugger = debugger;
        }

        public void Enable()
        {
            mEnabled = true;
        }

        public void Disable()
        {
            mEnabled = false;
        }

        public bool IsEnabled()
        {
            return mEnabled;
        }

        public void OutputPush(Stream stream)
        {
            mOutput.Add(new StreamWriter(stream));
            mOutput[mOutput.Count - 1].AutoFlush = true;
        }

        public void OutputPop()
        {
            if (mOutput.Count > 1)
            {
                mOutput[mOutput.Count - 1].Flush();
                mOutput.RemoveAt(mOutput.Count - 1);
            }
        }
    }

    class Writer : WriterBase, IWriter
    {
        public Writer(string fileName) : base(fileName)
        {
        }

        public Writer(Stream stream) : base(stream)
        {
        }

        public string SegmentString(Segment segment)
        {
            switch (segment)
            {
                case Segment.ARG: return "argument";
                case Segment.LOCAL: return "local";
                case Segment.GLOBAL: return "global";
                case Segment.THIS: return "this";
                case Segment.THAT: return "that";
                case Segment.POINTER: return "pointer";
                case Segment.TEMP: return "temp";
                default: return "constant";
            }
        }

        public string CommandString(Command command)
        {
            switch (command)
            {
                case Command.ADD: return "add";
                case Command.SUB: return "sub";
                case Command.MUL: return "mul";
                case Command.DIV: return "div";
                case Command.MOD: return "mod";
                case Command.XOR: return "xor";
                case Command.NEG: return "neg";
                case Command.EQ: return "eq";
                case Command.LT: return "lt";
                case Command.GT: return "gt";
                case Command.AND: return "and";
                case Command.OR: return "or";
                case Command.LNOT: return "lnot";
                case Command.LAND: return "land";
                default: return "not";
            }
        }

        public void WritePush(Segment segment, int index)
        {
            // push segment int
            WriteLine("push " + SegmentString(segment) + " " + index);
        }

        public void WritePop(Segment segment, int index)
        {
            // push segment int
            WriteLine("pop " + SegmentString(segment) + " " + index);
        }

        public void WriteArithmetic(Command command)
        {
            // command
            WriteLine(CommandString(command));
        }

        public void WriteLabel(string label)
        {
            // label
            WriteLine("label " + label);
        }

        public void WriteGoto(string label)
        {
            // goto
            WriteLine("goto " + label);
        }

        public void WriteIfGoto(string label)
        {
            // if-goto
            WriteLine("if-goto " + label);
        }

        public void WriteFunction(string function, int argCount)
        {
            // function function argCount
            WriteLine("function " + function + " " + argCount);
        }

        public void WriteCall(string function, int argCount)
        {
            // call function argCount
            WriteLine("call " + function + " " + argCount);
        }

        public void WriteReturn()
        {
            // return
            WriteLine("return");
        }
    }
}