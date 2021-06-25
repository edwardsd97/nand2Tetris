using System.IO;

class VMWriter : Writer, IVMWriter
{
    public VMWriter(string fileName) : base(fileName)
    {
    }

    public VMWriter(Stream stream) : base(stream)
    {
    }

    public string SegmentString(IVMWriter.Segment segment)
    {
        switch (segment)
        {
            case IVMWriter.Segment.ARG: return "argument";
            case IVMWriter.Segment.LOCAL: return "local";
            case IVMWriter.Segment.GLOBAL: return "global";
            case IVMWriter.Segment.STATIC: return "static";
            case IVMWriter.Segment.THIS: return "this";
            case IVMWriter.Segment.THAT: return "that";
            case IVMWriter.Segment.POINTER: return "pointer";
            case IVMWriter.Segment.TEMP: return "temp";
            default: return "constant";
        }
    }

    public string CommandString(IVMWriter.Command command)
    {
        switch (command)
        {
            case IVMWriter.Command.ADD: return "add";
            case IVMWriter.Command.SUB: return "sub";
            case IVMWriter.Command.NEG: return "neg";
            case IVMWriter.Command.EQ: return "eq";
            case IVMWriter.Command.LT: return "lt";
            case IVMWriter.Command.GT: return "gt";
            case IVMWriter.Command.AND: return "and";
            case IVMWriter.Command.OR: return "or";
            default: return "not";
        }
    }

    public void WritePush(IVMWriter.Segment segment, int index)
    {
        // push segment int
        WriteLine("push " + SegmentString(segment) + " " + index);
    }

    public void WritePop(IVMWriter.Segment segment, int index)
    {
        // push segment int
        WriteLine("pop " + SegmentString(segment) + " " + index);
    }

    public void WriteArithmetic(IVMWriter.Command command)
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
