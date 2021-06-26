using System.Collections;
using System.IO;

public static class StreamExtensions
{
    public static int Write(this Stream file, string value)
    {
        int len = 0;
        if (value != null)
            len = value.Length;
        file.Write(len);
        for (int i = 0; i < len; i++)
            file.WriteByte((byte)value[i]);
        return len + 4;
    }

    public static int Read(this Stream file, out string value)
    {
        int length;
        value = "";
        file.Read(out length);
        for (int i = 0; i < length; i++)
            value += (char)file.ReadByte();
        return length + 4;
    }

    public static int Write(this Stream file, short value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public static int Read(this Stream file, out short value)
    {
        byte[] bytes = new byte[2];
        file.Read(bytes, 0, bytes.Length);
        value = System.BitConverter.ToInt16(bytes, 0);
        return bytes.Length;
    }

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

    public static int Write(this Stream file, uint value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public static int Read(this Stream file, out uint value)
    {
        byte[] bytes = new byte[4];
        file.Read(bytes, 0, bytes.Length);
        value = System.BitConverter.ToUInt32(bytes, 0);
        return bytes.Length;
    }

    public static int Write(this Stream file, ushort value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public static int Read(this Stream file, out ushort value)
    {
        byte[] bytes = new byte[2];
        file.Read(bytes, 0, bytes.Length);
        value = System.BitConverter.ToUInt16(bytes, 0);
        return bytes.Length;
    }

    public static int Write(this Stream file, float value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public static int Read(this Stream file, out float value)
    {
        byte[] bytes = new byte[4];
        file.Read(bytes, 0, bytes.Length);
        value = System.BitConverter.ToSingle(bytes, 0);
        return bytes.Length;
    }

    public static int Write(this Stream file, bool value)
    {
        byte temp = value ? (byte)1 : (byte)0;
        return file.Write(temp);
    }

    public static int Read(this Stream file, out bool value)
    {
        byte temp;
        file.Read(out temp);
        value = (temp != 0) ? true : false;
        return 1;
    }

    public static int Write(this Stream file, byte value)
    {
        file.WriteByte(value);
        return 1;
    }

    public static int Read(this Stream file, out byte value)
    {
        value = (byte)file.ReadByte();
        return 1;
    }

    public static int Write(this Stream file, BitArray value)
    {
        byte entry = 0;
        int wrote = 0;
        int i = 0;
        file.Write(value.Length);
        for (i = 0; i < value.Length; i++)
        {
            if (i % 8 == 0)
            {
                if (i != 0)
                {
                    file.Write(entry);
                    wrote++;
                }
                entry = 0;
            }

            if (value[i])
                entry |= (byte)(1 << (i % 8));
        }
        if (i % 8 == 0)
        {
            file.Write(entry);
            wrote++;
        }
        return wrote + 4;
    }

    public static int Read(this Stream file, out BitArray value)
    {
        byte entry = 0;
        int read = 0;
        int i = 0;
        int size;
        file.Read(out size);
        value = new BitArray(size, false);
        for (i = 0; i < value.Length; i++)
        {
            if (i % 8 == 0)
            {
                file.Read(out entry);
                read++;
            }

            if ((entry & (byte)(1 << (i % 8))) != 0)
                value[i] = true;
        }
        return read + 4;
    }

    public static int WriteText(this Stream file, string text)
    {
        int wrote = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                file.Write((byte)0xD);
                file.Write((byte)0xA);
                wrote += 2;
            }
            else
            {
                file.Write((byte)text[i]);
                wrote++;
            }
        }
        return wrote;
    }
}