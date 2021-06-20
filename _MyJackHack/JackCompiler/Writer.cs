using System;
using System.IO;
using System.Collections.Generic;

class Writer
{
    protected StreamWriter mFile;
    protected bool mEnabled = true;
    protected List<StreamWriter> mOutput = new List<StreamWriter>();

    public Writer(string outFile)
    {
        mFile = new StreamWriter(outFile);
        mFile.AutoFlush = true;
        mOutput.Add(mFile);
    }

    public virtual void WriteLine(string line)
    {
        if (mEnabled)
        {
            if (JackCompiler.mVerbose)
                Console.WriteLine(line);
            mOutput[mOutput.Count - 1].WriteLine(line);
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

    public void WriteStream(Stream stream)
    {
        StreamReader reader = new StreamReader(stream);
        stream.Seek(0, SeekOrigin.Begin);
        while (!reader.EndOfStream)
        {
            string line = reader.ReadLine();
            mOutput[mOutput.Count - 1].WriteLine(line);
        }
    }
}
