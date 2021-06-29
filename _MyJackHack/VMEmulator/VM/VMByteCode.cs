using System.IO;
using System.Collections.Generic;

namespace VM
{
    class ByteCode
    {
        const int BITS_COMMAND = 5;
        const int BITS_SEGMENT = 4;
        const int BITS_INDEX = 31 - BITS_SEGMENT - BITS_COMMAND;

        const uint MASK_COMMAND     = 2080374784; // 0111 1100  0000 0000  0000 0000  0000 0000
        const uint MASK_SEGMENT     =   62914560; // 0000 0011  1100 0000  0000 0000  0000 0000
        const uint MASK_INDEX       =    4194303; // 0000 0000  0011 1111  1111 1111  1111 1111
        const uint MASK_PUSH_CONST  = 2147483648; // 1000 0000  0000 0000  0000 0000  0000 0000  (indicates command is NOT push constant)

        const uint MASK_NEG_BIT = (1 << BITS_INDEX - 1);
        const uint MASK_NEG_IDX = MASK_SEGMENT | MASK_COMMAND | MASK_PUSH_CONST;

        static Dictionary<string, Command> mStringToCommand;
        static Dictionary<string, Segment> mStringToSegment;

        Dictionary<string, int> mLabels = new Dictionary<string, int>();
        BuiltIn mLabelsBuiltIn = null;
        Stream mWriter = null;

        public ByteCode(BuiltIn builtIns = null)
        {
            if (builtIns == null)
                builtIns = Emulator.DefaultBuiltIns();
            mLabelsBuiltIn = builtIns;
        }

        public static int IntMax()
        {
            return int.MaxValue;
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

        public int ConvertVMText(Stream input, Stream output, Compiler compiler)
        {
            int written = 0;

            ByteCode.InitIfNeeded();

            StreamReader reader = new StreamReader(input);
            mWriter = output;

            TranslateLabels(reader);

            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            WriteStaticStrings(compiler);

            while (!reader.EndOfStream)
            {
                string commandStr = reader.ReadLine();
                string[] commandElems = CommandElements(commandStr);
                if (mStringToCommand.ContainsKey(commandElems[0]))
                {
                    Command command = mStringToCommand[commandElems[0]];
                    switch (command)
                    {
                        case Command.LABEL:
                            // Do nothing - labels are not a command
                            break;

                        case Command.PUSH:
                        case Command.POP:
                            if (mStringToSegment.ContainsKey(commandElems[1]))
                            {
                                WriteSegmentCommand(command, mStringToSegment[commandElems[1]], IntParse(commandElems[2]));
                                written++;
                            }
                            break;

                        case Command.GOTO:
                        case Command.IF_GOTO:
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

                        case Command.FUNCTION:
                        case Command.CALL:
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

        protected int WriteStaticStrings(Compiler compiler)
        {
            int count = 0;
            foreach (string str in compiler.mStrings.Keys)
            {
                WriteStaticString(Command.STATIC_STRING, str, compiler.mStrings[str]);
                count++;
            }
            return count;
        }

        protected int TranslateLabels(StreamReader reader)
        {
            // Convert all labels and functions to their command index
            int commandIndex = 0;

            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (!reader.EndOfStream)
            {
                string commandStr = reader.ReadLine();
                string[] commandElems = CommandElements(commandStr);
                if (mStringToCommand.ContainsKey(commandElems[0]))
                {
                    Command command = mStringToCommand[commandElems[0]];
                    if (command == Command.LABEL)
                    {
                        // Labels are not actually a command but a reference to the command that follows
                        if (!mLabels.ContainsKey(commandElems[1]))
                            mLabels.Add(commandElems[1], commandIndex);
                        continue;
                    }
                    else if (command == Command.FUNCTION)
                    {
                        if (!mLabels.ContainsKey(commandElems[1]))
                            mLabels.Add(commandElems[1], commandIndex);
                    }
                }

                commandIndex++;
            }

            return mLabels.Count;
        }

        public static string[] CommandElements(string commandStr)
        {
            return commandStr.Split(new char[3] { ' ', '\t', ',' });
        }

        protected static void InitIfNeeded()
        {
            if (mStringToCommand != null)
                return;

            mStringToCommand = new Dictionary<string, Command>();
            mStringToCommand.Add("push", Command.PUSH);
            mStringToCommand.Add("pop", Command.POP);
            mStringToCommand.Add("function", Command.FUNCTION);
            mStringToCommand.Add("call", Command.CALL);
            mStringToCommand.Add("label", Command.LABEL);
            mStringToCommand.Add("goto", Command.GOTO);
            mStringToCommand.Add("if-goto", Command.IF_GOTO);
            mStringToCommand.Add("return", Command.RETURN);
            mStringToCommand.Add("add", Command.ADD);
            mStringToCommand.Add("sub", Command.SUB);
            mStringToCommand.Add("mul", Command.MUL);
            mStringToCommand.Add("div", Command.DIV);
            mStringToCommand.Add("mod", Command.MOD);
            mStringToCommand.Add("xor", Command.XOR);
            mStringToCommand.Add("neg", Command.NEG);
            mStringToCommand.Add("not", Command.NOT);
            mStringToCommand.Add("and", Command.AND);
            mStringToCommand.Add("lnot", Command.LNOT);
            mStringToCommand.Add("land", Command.LAND);
            mStringToCommand.Add("or", Command.OR);
            mStringToCommand.Add("eq", Command.EQ);
            mStringToCommand.Add("gt", Command.GT);
            mStringToCommand.Add("lt", Command.LT);

            mStringToSegment = new Dictionary<string, Segment>();
            mStringToSegment.Add("argument", Segment.ARG);
            mStringToSegment.Add("constant", Segment.CONST);
            mStringToSegment.Add("global", Segment.GLOBAL);
            mStringToSegment.Add("local", Segment.LOCAL);
            mStringToSegment.Add("pointer", Segment.POINTER);
            mStringToSegment.Add("temp", Segment.TEMP);
            mStringToSegment.Add("that", Segment.THAT);
            mStringToSegment.Add("this", Segment.THIS);
        }
        public static int SegmentMask(Segment segment)
        {
            return (((int)segment) << BITS_INDEX) & (int)MASK_SEGMENT;
        }

        public static int CommandMask(Command command)
        {
            return (((int)command) << (BITS_INDEX + BITS_SEGMENT)) & unchecked((int)MASK_COMMAND);
        }

        public static int IndexMask(int index)
        {
            return index & (int)MASK_INDEX;
        }

        public virtual void Write(int value)
        {
            StreamExtensions.Write(mWriter, value);
        }

        public void WriteSegmentCommand(Command command, Segment segment, int index)
        {
            // push constant N is a special case where it uses all 32 bits - every other command has the high bit turned on
            if (command == Command.PUSH && segment == Segment.CONST)
                Write(index);
            else
                Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | SegmentMask(segment) | IndexMask(index));
        }

        public void WriteCommand(Command command)
        {
            Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command));
        }

        public void WriteJumpCommand(Command command, int labelOffset)
        {
            Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | IndexMask(labelOffset));
        }

        public void WriteFunctionOrCall(Command command, int labelOffset, int argCount)
        {
            Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | SegmentMask((Segment)argCount) | IndexMask(labelOffset));
        }

        public void WriteStaticString(Command command, string str, int strIndex)
        {
            Write(unchecked((int)MASK_PUSH_CONST) | CommandMask(command) | IndexMask(strIndex));
            StreamExtensions.Write(mWriter, str);
        }

        public static int Translate(Instruction command)
        {
            if (command.mCommand == Command.PUSH && command.mSegment == Segment.CONST)
                return command.mIndex;
            else
                return unchecked((int)MASK_PUSH_CONST) | CommandMask(command.mCommand) | SegmentMask(command.mSegment) | IndexMask(command.mIndex);
        }

        public static Instruction Translate(int commandInt)
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

                if ((index & MASK_NEG_BIT) != 0)
                {
                    // Index is negative, fill in the rest of the left bits
                    index = index | MASK_NEG_IDX;
                }

                return new Instruction((Command)command, (Segment)segment, (int)index);
            }
            else
            {
                // Special case of push const to use 32 bits
                return new Instruction(Command.PUSH, Segment.CONST, commandInt);
            }

        }

        public static Instruction Translate(string commandStr)
        {
            string[] commandElems = CommandElements(commandStr);
            return new Instruction(mStringToCommand[commandElems[0]], mStringToSegment[commandElems[1]], int.Parse(commandElems[2]));
        }
    }
}