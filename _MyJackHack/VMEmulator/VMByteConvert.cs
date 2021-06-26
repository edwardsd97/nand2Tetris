using System.IO;
using System.Collections.Generic;

class VMByteConvert
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
    Stream mWriter = null;

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

    public int ConvertVMToByteCode( Stream input, Stream output )
    {
        int written = 0;

        VMByteConvert.InitIfNeeded();

        StreamReader reader = new StreamReader(input);
        //mWriter = new MemoryStream( output );
        //mWriter.AutoFlush = true;
        mWriter = output;

        TranslateLabels( reader );

        reader.BaseStream.Seek( 0, SeekOrigin.Begin );

        while (!reader.EndOfStream)
        {
            string commandStr = reader.ReadLine();
            string[] commandElems = CommandElements(commandStr);
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
                    break;

                case VM.Command.FUNCTION:
                case VM.Command.CALL:
                    if (mLabels.ContainsKey(commandElems[1]))
                    {
                        WriteFunctionOrCall(command, mLabels[commandElems[1]], IntParse(commandElems[2]));
                        written++;
                    }
                    break;

                default:
                    WriteCommand(command);
                    written++;
                    break;
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
                // FIXME: tie in API functions here with a different indexing - say negative indexes refer to built in functions
                if ( !mLabels.ContainsKey(commandElems[1]) )
                    mLabels.Add(commandElems[1], commandIndex);
            }

            commandIndex++;
        }

        return mLabels.Count;
    }

    protected static string[] CommandElements( string commandStr )
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
        mStringToCommand.Add("neg", VM.Command.NEG);
        mStringToCommand.Add("not", VM.Command.NOT);
        mStringToCommand.Add("and", VM.Command.AND);
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
        return ( (int) segment ) << BITS_INDEX;
    }

    public int CommandMask(VM.Command command)
    {
        return ((int)command) << (BITS_INDEX + BITS_SEGMENT);
    }

    public int IndexMask(int index)
    {
        return index;
    }

    public virtual void Write(int value)
    {
        StreamExtensions.Write(mWriter, value);
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

        return new VM.VMCommand((VM.Command)command, (VM.Segment) segment, (int) index );
    }

    public static VM.VMCommand Translate(string commandStr)
    {
        string[] commandElems = CommandElements(commandStr);
        return new VM.VMCommand(mStringToCommand[commandElems[0]], mStringToSegment[commandElems[1]], int.Parse( commandElems[2] ) );
    }
}
