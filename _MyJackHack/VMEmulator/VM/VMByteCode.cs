using System.IO;
using System.Collections.Generic;

public static class VMStream
{
    public static int Write(this Stream file, int value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public static int Read(this Stream file, out int value)
    {
        byte[] bytes = new byte[4];
        file.Read(bytes, 0, bytes.Length);
        value = System.BitConverter.ToInt32(bytes, 0);
        return bytes.Length;
    }
}

class VMByteCode
{
    const int BITS_COMMAND = 5;
    const int BITS_SEGMENT = 4;
    const int BITS_INDEX = 32 - BITS_SEGMENT - BITS_COMMAND;

    const uint MASK_COMMAND = 4160749568; // 1111 1000  0000 0000  0000 0000  0000 0000
    const uint MASK_SEGMENT =  125829120; // 0000 0111  1000 0000  0000 0000  0000 0000
    const uint MASK_INDEX   =    8388607; // 0000 0000  0111 1111  1111 1111  1111 1111

    static Dictionary<string, VM.Command> mStringToCommand;
    static Dictionary<string, VM.Segment> mStringToSegment;

    Dictionary<string, int> mLabels = new Dictionary<string, int>();
    VMBuiltIn mLabelsBuiltIn = null;
    Stream mWriter = null;

    public VMByteCode(VMBuiltIn builtIns = null)
    {
        if (builtIns == null)
            builtIns = VM.DefaultBuiltIns();
        mLabelsBuiltIn = builtIns;
    }

    public static int IntParse(string str)
    {
        int result = 0;
        try
        {
            result = int.Parse(str);
        }
        catch
        {
            result = 0;
        }

        return result;
    }
    
    public int ConvertVMText( Stream input, Stream output )
    {
        int written = 0;

        VMByteCode.InitIfNeeded();

        StreamReader reader = new StreamReader(input);
        mWriter = output;

        TranslateLabels( reader );

        reader.BaseStream.Seek( 0, SeekOrigin.Begin );

        while (!reader.EndOfStream)
        {
            string commandStr = reader.ReadLine();
            string[] commandElems = CommandElements(commandStr);
            if ( mStringToCommand.ContainsKey(commandElems[0]) )
            {
                VM.Command command = mStringToCommand[commandElems[0]];
                switch (command)
                {
                    case VM.Command.LABEL:
                        // Do nothing - labels are not a command
                        break;

                    case VM.Command.PUSH:
                    case VM.Command.POP:
                        if (mStringToSegment.ContainsKey(commandElems[1]))
                        {
                            WriteSegmentCommand(command, mStringToSegment[commandElems[1]], IntParse(commandElems[2]));
                            written++;
                        }
                        break;

                    case VM.Command.GOTO:
                    case VM.Command.IF_GOTO:
                        if (mLabels.ContainsKey(commandElems[1]))
                        {
                            WriteJumpCommand(command, mLabels[commandElems[1]]);
                            written++;
                        }
                        else if (mLabelsBuiltIn.Find(commandElems[1]) != null)
                        {
                            WriteJumpCommand(command, mLabelsBuiltIn.FindLabel(commandElems[1]));
                            written++;
                        }
                        break;

                    case VM.Command.FUNCTION:
                    case VM.Command.CALL:
                        if (mLabels.ContainsKey(commandElems[1]))
                        {
                            WriteFunctionOrCall(command, mLabels[commandElems[1]], IntParse(commandElems[2]));
                            written++;
                        }
                        else if (mLabelsBuiltIn.Find(commandElems[1]) != null)
                        {
                            WriteFunctionOrCall(command, mLabelsBuiltIn.FindLabel(commandElems[1]), IntParse(commandElems[2]));
                            written++;
                        }
                        break;

                    default:
                        WriteCommand(command);
                        written++;
                        break;
                }
            }
        }

        return written;
    }

    protected int TranslateLabels( StreamReader reader )
    {
        // Convert all labels and functions to their command index
        int commandIndex = 0;

        reader.BaseStream.Seek(0, SeekOrigin.Begin);

        while (!reader.EndOfStream)
        {
            string commandStr = reader.ReadLine();
            string[] commandElems = CommandElements( commandStr );
            if (mStringToCommand.ContainsKey(commandElems[0]))
            {
                VM.Command command = mStringToCommand[commandElems[0]];
                if (command == VM.Command.LABEL)
                {
                    // Labels are not actually a command but a reference to the command that follows
                    if (!mLabels.ContainsKey(commandElems[1]))
                        mLabels.Add(commandElems[1], commandIndex);
                    continue;
                }
                else if (command == VM.Command.FUNCTION)
                {
                    if (!mLabels.ContainsKey(commandElems[1]))
                        mLabels.Add(commandElems[1], commandIndex);
                }
            }

            commandIndex++;
        }

        return mLabels.Count;
    }

    public static string[] CommandElements( string commandStr )
    {
        return commandStr.Split(new char[3] { ' ', '\t', ',' });
    }

    protected static void InitIfNeeded()
    {
        if (mStringToCommand != null)
            return;

        mStringToCommand = new Dictionary<string, VM.Command>();
        mStringToCommand.Add("push", VM.Command.PUSH);
        mStringToCommand.Add("pop", VM.Command.POP);
        mStringToCommand.Add("function", VM.Command.FUNCTION);
        mStringToCommand.Add("call", VM.Command.CALL);
        mStringToCommand.Add("label", VM.Command.LABEL);
        mStringToCommand.Add("goto", VM.Command.GOTO);
        mStringToCommand.Add("if-goto", VM.Command.IF_GOTO);
        mStringToCommand.Add("return", VM.Command.RETURN);
        mStringToCommand.Add("add", VM.Command.ADD);
        mStringToCommand.Add("sub", VM.Command.SUB);
        mStringToCommand.Add("mul", VM.Command.MUL);
        mStringToCommand.Add("div", VM.Command.DIV);
        mStringToCommand.Add("mod", VM.Command.MOD);
        mStringToCommand.Add("xor", VM.Command.XOR);
        mStringToCommand.Add("neg", VM.Command.NEG);
        mStringToCommand.Add("not", VM.Command.NOT);
        mStringToCommand.Add("and", VM.Command.AND);
        mStringToCommand.Add("lnot", VM.Command.LNOT);
        mStringToCommand.Add("land", VM.Command.LAND);
        mStringToCommand.Add("or", VM.Command.OR);
        mStringToCommand.Add("eq", VM.Command.EQ);
        mStringToCommand.Add("gt", VM.Command.GT);
        mStringToCommand.Add("lt", VM.Command.LT);

        mStringToSegment = new Dictionary<string, VM.Segment>();
        mStringToSegment.Add("argument", VM.Segment.ARG);
        mStringToSegment.Add("constant", VM.Segment.CONST);
        mStringToSegment.Add("global", VM.Segment.GLOBAL);
        mStringToSegment.Add("local", VM.Segment.LOCAL);
        mStringToSegment.Add("pointer", VM.Segment.POINTER);
        mStringToSegment.Add("temp", VM.Segment.TEMP);
        mStringToSegment.Add("that", VM.Segment.THAT);
        mStringToSegment.Add("this", VM.Segment.THIS);
    }
    public int SegmentMask(VM.Segment segment)
    {
        return (( (int) segment ) << BITS_INDEX) & (int) MASK_SEGMENT;
    }

    public int CommandMask(VM.Command command)
    {
        return (((int)command) << (BITS_INDEX + BITS_SEGMENT)) & unchecked( (int) MASK_COMMAND );
    }

    public int IndexMask(int index)
    {
        return index & (int) MASK_INDEX;
    }

    public virtual void Write(int value)
    {
        VMStream.Write(mWriter, value);
    }

    public void WriteSegmentCommand( VM.Command command, VM.Segment segment, int index)
    {
        Write( CommandMask(command) | SegmentMask(segment) | IndexMask( index ) );
    }

    public void WriteCommand(VM.Command command)
    {
        Write( CommandMask( command ) );
    }

    public void WriteJumpCommand( VM.Command command, int labelOffset )
    {
        Write( CommandMask(command) | IndexMask(labelOffset) );
    }

    public void WriteFunctionOrCall(VM.Command command, int labelOffset, int argCount )
    {
        Write( CommandMask(command) | SegmentMask((VM.Segment)argCount) | IndexMask(labelOffset) );
    }

    public static VM.VMCommand Translate(int commandInt)
    {
        uint command;
        uint segment;
        uint index;

        command = ( (uint) commandInt & MASK_COMMAND ) >> ( BITS_SEGMENT + BITS_INDEX );
        segment = ( (uint) commandInt & MASK_SEGMENT ) >> BITS_INDEX;
        index =   ( (uint) commandInt & MASK_INDEX );

        if ( ( index & ( 1 << BITS_INDEX - 1) ) != 0 ) 
        {
            // Index is negative, fill in the rest of the left bits
            index = index | MASK_SEGMENT | MASK_COMMAND;
        }

        return new VM.VMCommand((VM.Command)command, (VM.Segment) segment, (int) index );
    }

    public static VM.VMCommand Translate(string commandStr)
    {
        string[] commandElems = CommandElements(commandStr);
        return new VM.VMCommand(mStringToCommand[commandElems[0]], mStringToSegment[commandElems[1]], int.Parse( commandElems[2] ) );
    }
}
