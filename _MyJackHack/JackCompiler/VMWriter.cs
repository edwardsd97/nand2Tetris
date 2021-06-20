
class VMWriter : Writer
{
    public enum Segment
    {
        INVALID, CONST, ARG, LOCAL, STATIC, THIS, THAT, POINTER, TEMP
    }

    public enum Command
    {
        ADD, SUB, NEG, EQ, LT, GT, AND, OR, NOT
    }

    public VMWriter(string fileName) : base(fileName)
    {
    }

    public string SegmentString(Segment segment)
    {
        switch (segment)
        {
            case Segment.ARG: return "argument";
            case Segment.LOCAL: return "local";
            case Segment.STATIC: return "static";
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
            case Command.NEG: return "neg";
            case Command.EQ: return "eq";
            case Command.LT: return "lt";
            case Command.GT: return "gt";
            case Command.AND: return "and";
            case Command.OR: return "or";
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
        // mark this function as referenced
        CompilationEngine.FuncSpec funcSpec;
        if (CompilationEngine.mFunctions.TryGetValue(function, out funcSpec))
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
