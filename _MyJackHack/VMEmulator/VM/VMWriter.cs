using System;
using System.IO;
using System.Collections.Generic;

// IVMWriter interface: VMCompiler interfaces with this
public interface IVMWriter
{
    public void WriteLine(string line); // for comments mainly
    public void WritePush(VM.Segment segment, int index);
    public void WritePop(VM.Segment segment, int index);
    public void WriteArithmetic(VM.Command command);
    public void WriteLabel(string label);
    public void WriteGoto(string label);
    public void WriteIfGoto(string label);
    public void WriteFunction(string function, int argCount);
    public void WriteCall(string function, int argCount);
    public void WriteReturn();
    public void WriteStream(Stream stream);

    public void Enable();
    public void Disable();

    public void OutputPush(Stream stream);
    public void OutputPop();
}

class VMWriterBase
{
    protected StreamWriter mFile;
    protected bool mEnabled = true;
    protected List<StreamWriter> mOutput = new List<StreamWriter>();

    public int mCommandsWritten = 0;
    public int mCommandStart = 0;

    public VMWriterBase(string outFile)
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
        mOutput.Add(mFile);
    }

    public VMWriterBase(Stream outStream)
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
            mCommandsWritten++;
        }
    }

    public virtual void Write(int value)
    {
        if (mEnabled)
        {
            mOutput[mOutput.Count - 1].Write(value);
            mCommandsWritten++;
        }
    }

    public void Enable()
    {
        mEnabled = true;
    }

    public void Disable()
    {
        mEnabled = false;
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

    public virtual void WriteStream(Stream stream)
    {
        StreamReader reader = new StreamReader(stream);
        stream.Seek(0, SeekOrigin.Begin);
        while (!reader.EndOfStream)
        {
            string line = reader.ReadLine();
            mOutput[mOutput.Count - 1].WriteLine(line);
            mCommandsWritten++;
        }
    }
}

class VMWriter : VMWriterBase, IVMWriter
{
    public VMWriter(string fileName) : base(fileName)
    {
    }

    public VMWriter(Stream stream) : base(stream)
    {
    }

    public string SegmentString(VM.Segment segment)
    {
        switch (segment)
        {
            case VM.Segment.ARG: return "argument";
            case VM.Segment.LOCAL: return "local";
            case VM.Segment.GLOBAL: return "global";
            case VM.Segment.THIS: return "this";
            case VM.Segment.THAT: return "that";
            case VM.Segment.POINTER: return "pointer";
            case VM.Segment.TEMP: return "temp";
            default: return "constant";
        }
    }

    public string CommandString(VM.Command command)
    {
        switch (command)
        {
            case VM.Command.ADD: return "add";
            case VM.Command.SUB: return "sub";
            case VM.Command.MUL: return "mul";
            case VM.Command.DIV: return "div";
            case VM.Command.MOD: return "mod";
            case VM.Command.XOR: return "xor";
            case VM.Command.NEG: return "neg";
            case VM.Command.EQ: return "eq";
            case VM.Command.LT: return "lt";
            case VM.Command.GT: return "gt";
            case VM.Command.AND: return "and";
            case VM.Command.OR: return "or";
            case VM.Command.LNOT: return "lnot";
            case VM.Command.LAND: return "land";
            default: return "not";
        }
    }

    public void WritePush(VM.Segment segment, int index)
    {
        // push segment int
        WriteLine("push " + SegmentString(segment) + " " + index);
    }

    public void WritePop(VM.Segment segment, int index)
    {
        // push segment int
        WriteLine("pop " + SegmentString(segment) + " " + index);
    }

    public void WriteArithmetic(VM.Command command)
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
        // mark this function as referenced
        VMCompiler.FuncSpec funcSpec;
        if (VMCompiler.mFunctions.TryGetValue(function, out funcSpec))
        {
            funcSpec.referenced = true;
        }

        // call function argCount
        WriteLine("call " + function + " " + argCount);
    }

    public void WriteReturn()
    {
        // return
        WriteLine("return");
    }
}
