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

    public static int Write(this Stream file, string value )
    {
        int strLength = value.Length;
        int dwordAligned = VM.Align(strLength, 4);

        VMStream.Write( file, strLength);

        for (int i = 0; i < dwordAligned; i++)
        {
            if ( i < strLength )
                file.WriteByte((byte)value[i]);
            else
                file.WriteByte(0);
        }

        return dwordAligned;
    }

    public static int Read(this Stream file, out string value )
    {
        int strLength = 0;
        
        VMStream.Read(file, out strLength);

        int dwordAligned = VM.Align(strLength, 4);

        value = "";

        for (int i = 0; i < dwordAligned; i++)
        {
            if (i < strLength)
                value += (char)file.ReadByte();
            else
                file.ReadByte(); // read the padding 0's
        }

        return dwordAligned;
    }
}

class VMByteCode
{
    const int BITS_COMMAND = 5;
    const int BITS_SEGMENT = 4;
    const int BITS_INDEX = 31 - BITS_SEGMENT - BITS_COMMAND;

    const uint MASK_COMMAND     = 2080374784; // 0111 1100  0000 0000  0000 0000  0000 0000
    const uint MASK_SEGMENT     =   62914560; // 0000 0011  1100 0000  0000 0000  0000 0000
    const uint MASK_INDEX       =    4194303; // 0000 0000  0011 1111  1111 1111  1111 1111
    const uint MASK_PUSH_CONST  = 2147483648; // 1000 0000  0000 0000  0000 0000  0000 0000

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

    public static int IntMax()
    {
        return int.MaxValue;
    }

    public static int IntParse( string str )
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

        WriteStaticStrings();

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

    protected int WriteStaticStrings()
    {
        int count = 0;
        foreach (string str in VMCompiler.mStrings.Keys)
        {
            WriteStaticString(VM.Command.STATIC_STRING, str, VMCompiler.mStrings[str]);
            count++;
        }
        return count;
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
    public static int SegmentMask(VM.Segment segment)
    {
        return (( (int) segment ) << BITS_INDEX) & (int) MASK_SEGMENT;
    }

    public static int CommandMask(VM.Command command)
    {
        return (((int)command) << (BITS_INDEX + BITS_SEGMENT)) & unchecked( (int) MASK_COMMAND );
    }

    public static int IndexMask(int index)
    {
        return index & (int) MASK_INDEX;
    }

    public virtual void Write(int value)
    {
        VMStream.Write(mWriter, value);
    }

    public void WriteSegmentCommand( VM.Command command, VM.Segment segment, int index)
    {
        // push constant N is a special case where it uses all 32 bits - every other command has the high bit turned on
        if ( command == VM.Command.PUSH && segment == VM.Segment.CONST )
            Write( index );
        else
            Write( unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | SegmentMask(segment) | IndexMask( index ) );
    }

    public void WriteCommand(VM.Command command)
    {
        Write(unchecked((int)MASK_PUSH_CONST) | CommandMask( command ) );
    }

    public void WriteJumpCommand( VM.Command command, int labelOffset )
    {
        Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | IndexMask(labelOffset) );
    }

    public void WriteFunctionOrCall(VM.Command command, int labelOffset, int argCount )
    {
        Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | SegmentMask((VM.Segment)argCount) | IndexMask(labelOffset) );
    }

    public void WriteStaticString(VM.Command command, string str, int strIndex )
    {
        Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | IndexMask(strIndex));
        VMStream.Write(mWriter, str);
    }

    public static int Translate( VM.VMCommand command )
    {
        if (command.mCommand == VM.Command.PUSH && command.mSegment == VM.Segment.CONST)
            return command.mIndex;
        else
            return CommandMask(command.mCommand) | SegmentMask(command.mSegment) | IndexMask(command.mIndex);
    }

    public static VM.VMCommand Translate(int commandInt)
    {
        uint command;
        uint segment;
        uint index;

        if ((commandInt & unchecked((int)MASK_PUSH_CONST)) != 0)
        {
            // Any command other than push constant N
            command = ((uint)commandInt & MASK_COMMAND) >> (BITS_SEGMENT + BITS_INDEX);
            segment = ((uint)commandInt & MASK_SEGMENT) >> BITS_INDEX;
            index = ((uint)commandInt & MASK_INDEX);

            if ((index & (1 << BITS_INDEX - 1)) != 0)
            {
                // Index is negative, fill in the rest of the left bits
                index = index | MASK_SEGMENT | MASK_COMMAND | MASK_PUSH_CONST;
            }

            return new VM.VMCommand((VM.Command)command, (VM.Segment)segment, (int)index);
        }
        else
        {
            // Special case of push const to use 32 bits
            return new VM.VMCommand(VM.Command.PUSH, VM.Segment.CONST, commandInt);
        }

    }

    public static VM.VMCommand Translate(string commandStr)
    {
        string[] commandElems = CommandElements(commandStr);
        return new VM.VMCommand(mStringToCommand[commandElems[0]], mStringToSegment[commandElems[1]], int.Parse( commandElems[2] ) );
    }
}
