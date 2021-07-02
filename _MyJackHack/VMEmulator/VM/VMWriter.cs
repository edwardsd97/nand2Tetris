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
        public void WriteStream(WriterStream stream);

        public void Enable();
        public void Disable();
        public bool IsEnabled();

        public void OutputPush(WriterStream stream);
        public void OutputPop();

        public void SetDebugger(Debugger debugger);
    }

    // WriterStream: A MemoryStream that works together with WriterStreamWriter to track the current token as each line is written
    public class WriterStream : MemoryStream
    {
        public Tokenizer mTokens;
        public List<Tokenizer.State> mTokenStates = new List<Tokenizer.State>();

        // cached info so that it does not need to repeatedly parse the same stream
        protected int mConstant;
        protected bool mIsConstant;
        protected long mConstantCache = -1;

        public bool IsConstant( bool allowMultipleConstants = false )
        {
            int dontCare;
            return IsConstant(out dontCare, allowMultipleConstants );
        }

        public bool IsConstant( out int value, bool allowMultipleConstants = false )
        {
            if ( mConstantCache == Position )
            {
                value = mConstant;
                return mIsConstant;
            }

            mConstantCache = Position;
            Seek(0, SeekOrigin.Begin);
            mConstant = 0;
            bool isConstant = false;
            int commands = 0;

            /*
              A constant command can only consist of

              push constant N
            
               or
             
              push constant N
              neg
            
             */

            StreamReader rdr = new StreamReader(this);
            while (!rdr.EndOfStream && ( commands < 3 || allowMultipleConstants ) )
            {
                string[] parts = rdr.ReadLine().Split(new char[2] { ' ', '\t' });
                if (parts.Length >= 2 && parts[0] == "push" && parts[1] == "constant")
                {
                    mConstant = int.Parse(parts[2]);
                    isConstant = true;
                }
                if (parts.Length > 0 && parts[0] == "neg" && commands == 1)
                {
                    mConstant = -mConstant;
                    commands--;
                }
                commands++;
            }

            if (commands > 2)
                isConstant = false;

            Seek(mConstantCache, SeekOrigin.Begin);

            mIsConstant = isConstant;
            value = mConstant;

            return mIsConstant;
        }

        public WriterStream(Tokenizer tokens)
        {
            mTokens = tokens;
        }
    }

    public class WriterStreamWriter : StreamWriter
    {
        public long mLastStreamPos;

        public WriterStreamWriter( WriterStream stream ) : base(stream)
        {
        }

        public override void WriteLine( string line )
        {
            WriterStream stream = (WriterStream)BaseStream;
            stream.mTokenStates.Add(stream.mTokens.StateGet());
            mLastStreamPos = stream.Position;
            base.WriteLine( line );
        }
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

        public virtual void WriteStream(WriterStream stream)
        {
            Tokenizer.State tokenState = stream.mTokens.StateGet();

            StreamReader reader = new StreamReader(stream);
            stream.Seek(0, SeekOrigin.Begin);
            int t = 0;
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                mOutput[mOutput.Count - 1].WriteLine(line);
                if (mOutput[mOutput.Count - 1] == mFile && mDebugger != null)
                {
                    stream.mTokens.StateSet(stream.mTokenStates[t]);
                    mDebugger.WriteCommand(line);
                }
                t++;
            }

            stream.mTokens.StateSet( tokenState );
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

        public void OutputPush(WriterStream stream)
        {
            mOutput.Add(new WriterStreamWriter(stream));
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