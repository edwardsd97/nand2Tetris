using System;
using System.IO;

namespace VM
{
    public enum SegPointer : byte
    {
        SP, ARG, LOCAL, GLOBAL, THIS, THAT, TEMP,
        COUNT, POINTER,
    }

    public enum Command : byte
    {
        PUSH, POP, FUNCTION, CALL, LABEL, GOTO, IF_GOTO, RETURN,
        ADD, SUB, NEG, EQ, LT, GT, AND, LAND, OR, XOR, LOR, NOT, LNOT, MUL, DIV, MOD,
        STATIC_STRING, // used for loading only
        INVALID
    }

    public enum Segment : byte
    {
        CONST, GLOBAL, ARG, LOCAL, THIS, THAT, POINTER, TEMP,
        INVALID
    }

    public class Instruction
    {
        public Command mCommand;
        public Segment mSegment;
        public int mIndex;

        public Instruction(Command command, Segment segment, int index)
        {
            mCommand = command;
            mSegment = segment;
            mIndex = index;
        }
    }

    public class Timer : System.Diagnostics.Stopwatch
    {
        readonly double _microSecPerTick =
            1000000D / System.Diagnostics.Stopwatch.Frequency;

        public Timer()
        {
            if (!System.Diagnostics.Stopwatch.IsHighResolution)
            {
                throw new Exception("On this system the high-resolution " +
                                    "performance counter is not available");
            }
        }

        public long ElapsedMicroseconds
        {
            get
            {
                return (long)(ElapsedTicks * _microSecPerTick);
            }
        }
    }

    public static class StreamExtensions
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

        public static int Write(this Stream file, string value)
        {
            int strLength = value.Length;
            int dwordAligned = Emulator.Align(strLength, 4);

            StreamExtensions.Write(file, strLength);

            for (int i = 0; i < dwordAligned; i++)
            {
                if (i < strLength)
                    file.WriteByte((byte)value[i]);
                else
                    file.WriteByte(0);
            }

            return dwordAligned;
        }

        public static int Read(this Stream file, out string value)
        {
            int strLength = 0;

            StreamExtensions.Read(file, out strLength);

            int dwordAligned = Emulator.Align(strLength, 4);

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
}