using System.IO;

class VMWriter : Writer, IVMWriter
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
            case VM.Command.NEG: return "neg";
            case VM.Command.EQ: return "eq";
            case VM.Command.LT: return "lt";
            case VM.Command.GT: return "gt";
            case VM.Command.AND: return "and";
            case VM.Command.OR: return "or";
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
        Compiler.FuncSpec funcSpec;
        if (Compiler.mFunctions.TryGetValue(function, out funcSpec))
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
