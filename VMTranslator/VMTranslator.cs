using System;
using System.IO;
using System.Collections;

class VMTranslator
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No filename or directory provided.");
        }

        for (int i = 0; i < args.Length; i++)
        {
            ProcessPath(args[i]);
        }
    }

    static void ProcessPath(string path)
    {
        bool isDirectory = false;
        string outFile = GetOutfile(path, out isDirectory );

        CodeWriter writer = new CodeWriter(outFile);
        string[] vmFiles;

        if (isDirectory)
        {
            Console.WriteLine("");
            Console.WriteLine("VM Translating directory: " + path);
            vmFiles = Directory.GetFiles(path, "*.vm");

            /*
            // Only write the bootstrap code if Sys.vm is present
            foreach (string file in vmFiles)
            {
                string name = FilePathToName(file).ToLower();
                if (name == "sys")
                {
                    writer.WriteInit();
                    break;
                }
            }
            */

            // Write bootstrap code for any directory translate
            writer.WriteInit();
        }
        else
        {
            Console.WriteLine("");
            Console.WriteLine("VM Translating file: " + path);
            vmFiles = new string[1] { path };
        }

        for ( int i = 0; i < vmFiles.Length; i++ )
        {
            Parser parser = new Parser( vmFiles[i] );

            writer.SetVMFileName( FilePathToName( vmFiles[i] ) );

            Console.WriteLine( "  " + vmFiles[i]);

            while (parser.HasMoreCommands())
            {
                if (parser.Advance())
                {
                    writer.WriteComment(parser.mLineString);

                    switch (parser.mCommandType)
                    {
                        case CommandType.C_ARITHMETIC:
                            writer.WriteArithmetic(parser.mArg1);
                            break;

                        case CommandType.C_PUSH:
                        case CommandType.C_POP:
                            writer.WritePushPop(parser.mCommandType, parser.mArg1, parser.mArg2);
                            break;

                        case CommandType.C_LABEL:
                            writer.WriteLabel(parser.mArg1);
                            break;

                        case CommandType.C_GOTO:
                            writer.WriteGoto(parser.mArg1);
                            break;

                        case CommandType.C_IF:
                            writer.WriteIf(parser.mArg1);
                            break;

                        case CommandType.C_FUNCTION:
                            writer.WriteFunction(parser.mArg1, parser.mArg2);
                            break;

                        case CommandType.C_CALL:
                            writer.WriteCall(parser.mArg1, parser.mArg2);
                            break;

                        case CommandType.C_RETURN:
                            writer.WriteReturn();
                            break;
                    }

                    if (writer.mError != "")
                    {
                        Console.WriteLine("ERROR: Line " + parser.mLine + " - " + writer.mError);
                    }
                }
            }
        }

        writer.Close();
    }

    public static string GetOutfile(string fileOrDirectory, out bool isDirectory )
    {
        isDirectory = false;

        FileAttributes attrib = File.GetAttributes(fileOrDirectory);
        if (attrib.HasFlag(FileAttributes.Directory))
        {
            // Directory is name of output file
            isDirectory = true;
            return fileOrDirectory + "/" + FilePathToName( fileOrDirectory ) + ".asm";
        }
        else
        {
            // Single file .asm is outfile file
            return SwitchExtension(fileOrDirectory, "asm");
        }
    }

    public static string SwitchExtension(string file, string extension)
    {
        string[] fileParts = file.Split(new char[1] { '.' });
        string outFile = "";
        for (int i = 0; i < fileParts.Length; i++)
        {
            if (i < fileParts.Length - 1)
                outFile = outFile + fileParts[i] + ".";
            else
                outFile = outFile + extension;
        }

        return outFile;
    }

    public static string FilePathToName(string file)
    {
        FileAttributes attrib = File.GetAttributes(file);
        string[] fileParts = file.Split(new char[3] { '.', '/', '\\' } );
        if (fileParts.Length > 1)
        {
            if (attrib.HasFlag(FileAttributes.Directory))
                return fileParts[fileParts.Length - 1];

            return fileParts[fileParts.Length - 2];
        }
        return "";
    }
}

public enum CommandType
{ 
    C_ARITHMETIC,
    C_PUSH,
    C_POP,
    C_LABEL,
    C_GOTO,
    C_IF,
    C_FUNCTION,
    C_RETURN,
    C_CALL
}

public class Parser
{
    public CommandType  mCommandType;
    public string       mArg1;
    public int          mArg2;
    public int          mLine;
    public string       mLineString;

    public System.IO.StreamReader   mFile;

    public Parser( string fileName )
    {
        mFile = new System.IO.StreamReader( fileName );
        mLine = 0;
    }

    ~Parser()
    {
        Close();
    }

    public void Close()
    {
        mFile.Close();
    }

    public bool HasMoreCommands()
    {
        return !mFile.EndOfStream;
    }

    public bool Advance()
    {
        bool readCommand = false;

        while (!readCommand && HasMoreCommands())
        {
            mLineString = mFile.ReadLine();
            mLine++;

            string[] words = mLineString.Split(new char[2] { ' ', '\t' });

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string lower = word;

                if (word == "//" || word == "" )
                {
                    break;
                }

                switch (i)
                {
                    case 0:
                        // Command (enum)
                        readCommand = true;
                        if (lower == "push")
                        {
                            mCommandType = CommandType.C_PUSH;
                        }
                        else if (lower == "pop")
                        {
                            mCommandType = CommandType.C_POP;
                        }
                        else if (lower == "label")
                        {
                            mCommandType = CommandType.C_LABEL;
                        }
                        else if (lower == "goto")
                        {
                            mCommandType = CommandType.C_GOTO;
                        }
                        else if (lower == "if-goto")
                        {
                            mCommandType = CommandType.C_IF;
                        }
                        else if (lower == "function")
                        {
                            mCommandType = CommandType.C_FUNCTION;
                        }
                        else if (lower == "call")
                        {
                            mCommandType = CommandType.C_CALL;
                        }
                        else if (lower == "return")
                        {
                            mCommandType = CommandType.C_RETURN;
                        }
                        else
                        {
                            mCommandType = CommandType.C_ARITHMETIC;
                            mArg1 = word;
                        }
                        break;

                    case 1:
                        // Argument 1 (string)
                        mArg1 = word;
                        break;

                    case 2:
                        // Argument 2 (int)
                        mArg2 = int.Parse( word );
                        break;
                }
            }
        }

        return readCommand;
    }
}

class CodeWriter
{
    public System.IO.StreamWriter mFile;
    public string mVMFileName;
    public string mError;

    protected int mCompareIndex = 0;
    protected int mCallIndex = 0;
    protected bool mUsingSysInit = false;

    protected bool mComments = false;
    protected bool mVerbose = false;

    // FIXME - improve this to handle the same thing for any segment when classUnique is true
    protected int mStaticMax = 0; // max static index while processing a single class
    protected int mStaticOffset = 0; // static index offset while processing a single class

    public CodeWriter(string fileName)
    {
        mFile = new System.IO.StreamWriter(fileName);
        mFile.AutoFlush = true;
        mError = "";
    }

    ~CodeWriter()
    {
    }

    public void Close()
    {
        if ( !mUsingSysInit )
        {
            WriteComment("INFINITE LOOP");
            FileWriteLine("(_END)");
            FileWriteLine("@_END");
            FileWriteLine("0;JMP");
        }

        mFile.Close();
    }

    public void SegmentInfo( string segment, ref int index, out string address, out bool isPointer, out bool isConstant )
    {
        segment = segment.ToLower();

        address = "0";
        isPointer = false;
        isConstant = false;

        object[] segments =
        {
            // segment  address         isPointer   isConstant  fileUnique
            "local",    "LCL",  /*1*/   true,       false,      false,
            "argument", "ARG",  /*2*/   true,       false,      false,
            "this",     "THIS", /*3*/   true,       false,      false,
            "that",     "THAT", /*4*/   true,       false,      false,
            "temp",     5,              false,      false,      false,
            "static",   16,             false,      false,      true,
            "constant", 0,              false,      true,       false,
            "pointer",  "THIS", /*3*/   false,      false,      false,
        };

        for (int i = 0; i < segments.Length; i = i + 5)
        {
            if (segment == (string)segments[i])
            {
                if ((bool)segments[i + 4])
                {
                    // FIXME - improve this to handle the same thing for any segment when fileUnique is true
                    // index is unique per file
                    mStaticMax = Math.Max( mStaticMax, index + 1 );
                    index += mStaticOffset;
                }

                address = "" + segments[i + 1];
                isPointer = (bool)segments[i + 2];
                isConstant = (bool)segments[i + 3];
                return;
            }
        }

        mError = "Invalid segment '" + segment + "'";
    }

    public void SetVMFileName(string vmFileName)
    {
        if (mVMFileName != vmFileName)
        {
            mStaticOffset += mStaticMax;
            mStaticMax = 0;
        }

        mVMFileName = vmFileName;
    }

    public void FileWriteLine(string line)
    {
        mFile.WriteLine(line);

        if ( mVerbose )
        {
            Console.WriteLine(line);
        }
    }

    public void WriteInit()
    {
        WriteComment("Bootstrap Code");
        Console.WriteLine("  " + "Bootstrap Code" );

        // Set SP to 256
        FileWriteLine("@256"); // @256
        FileWriteLine("D=A"); // @256
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=D"); // @SP

        // call Sys.init
        this.SetVMFileName("Boot");
        WriteComment("call Sys.init 0");
        WriteCall("Sys.init", 0);

        mUsingSysInit = true;
    }

    protected void WriteCompare( string jumpTest )
    {
        // Compare two values using the provided jumpTest
        //  ( JEQ, JLT, JGT, ... )
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M-D"); // D=M-D
        FileWriteLine("@_COMPARE_TRUE"+ mCompareIndex);
        FileWriteLine("D;" + jumpTest ); // D;<jumpTest>
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=0"); // M=0
        FileWriteLine("@_COMPARE_FINISHED" + mCompareIndex);
        FileWriteLine("0;JMP");
        FileWriteLine("(_COMPARE_TRUE" + mCompareIndex + ")");
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=-1"); // M=-1
        FileWriteLine("(_COMPARE_FINISHED" + mCompareIndex + ")" );
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        mCompareIndex++;
    }

    protected void WriteMathXY(string operation)
    {
        // operation is operated on M in terms of M and D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("M=" + operation); // D=<operation>
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1
    }

    protected void WriteMathX(string operation)
    {
        // operation is operated on M in terms of M
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("M=" + operation); // M=<operation>
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1
    }

    public void WriteLabel(string label)
    {
        FileWriteLine("(" + label + ")" ); // (<label>)
    }

    public void WriteGoto(string label)
    {
        FileWriteLine("@" + label);   // @<label>
        FileWriteLine("0;JMP");       // 0;JUMP
    }

    public void WriteIf(string label)
    {
        // If pop stack != 0
        FileWriteLine("@SP");         // @SP
        FileWriteLine("AM=M-1");      // AM=M-1
        FileWriteLine("D=M");         // D=M
        FileWriteLine("@" + label);   // @<label>
        FileWriteLine("D;JNE");       // 0;JNE
    }

    public void WriteFunction(string functionName, int localCount )
    {
        FileWriteLine("(" + functionName + ")"); // (functionName)

        if (localCount > 0)
        {
            // repeat nVars times:
            FileWriteLine("@SP"); // @SP
            FileWriteLine("A=M"); // A=M
            for (int i = 0; i < localCount; i++)
            {
                // push 0
                FileWriteLine("M=0"); // M=0
                FileWriteLine("@SP"); // @SP
                FileWriteLine("AM=M+1"); // AM=M+1
            }
        }
    }

    public void WriteCall(string functionName, int argumentCount)
    {
        string returnAddress = mVMFileName + "." + functionName + "$ret." + mCallIndex++;

        // push returnAddress (label below)
        FileWriteLine("@" + returnAddress); // @functionName$ret.N
        FileWriteLine("D=A"); // D=A
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        // push LCL pointer value
        FileWriteLine("@LCL"); // @LCL
        FileWriteLine("D=M"); // D=A
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        // push ARG pointer value
        FileWriteLine("@ARG"); // @ARG
        FileWriteLine("D=M"); // D=A
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        // push THIS pointer value
        FileWriteLine("@THIS"); // @THIS
        FileWriteLine("D=M"); // D=A
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        // push THAT pointer value
        FileWriteLine("@THAT"); // @THAT
        FileWriteLine("D=M"); // D=A
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M+1"); // M=M+1

        // ARG = SP-5-nArgs
        int backSteps = 5 + argumentCount - 1; // -1 to account for D=M-1
        FileWriteLine("@SP"); // @SP
        FileWriteLine("D=M-1"); // D=M
        for ( int i = 0; i < backSteps; i++ )
            FileWriteLine("D=D-1"); // D=D-1
        FileWriteLine("@ARG"); // @ARG
        FileWriteLine("M=D"); // M=D

        // LCL = SP
        FileWriteLine("@SP"); // @SP
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@LCL"); // @LCL
        FileWriteLine("M=D"); // M=D

        // goto functionName
        WriteGoto(functionName);

        // (returnAddress)
        FileWriteLine("(" + returnAddress + ")" );
    }

    public void WriteReturn()
    {
        // endFrame <R13> = LCL 
        FileWriteLine("@LCL"); // @LCL
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@R13"); // @R13
        FileWriteLine("M=D"); // M=D

        // retAddr <R14> = *(endFrame-5)
        for( int i = 0; i < 5; i++ )
            FileWriteLine("D=D-1"); // D=D-1
        FileWriteLine("A=D"); // A=D
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@R14"); // @R14
        FileWriteLine("M=D"); // M=D

        // *ARG = pop()
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M-1"); // A=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@ARG"); // @ARG
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=M-1"); // M=M-1

        // SP=ARG+1
        FileWriteLine("@ARG"); // @ARG
        FileWriteLine("D=M+1"); // D=M+1
        FileWriteLine("@SP"); // @SP
        FileWriteLine("M=D"); // M=D

        // THAT = *(endFrame-1)
        FileWriteLine("@R13"); // @R13
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@THAT"); // @THAT
        FileWriteLine("M=D"); // D=M

        // THIS = *(endFrame-2)
        FileWriteLine("@R13"); // @R13
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@THIS"); // @THIS
        FileWriteLine("M=D"); // D=M

        // ARG = *(endFrame-3)
        FileWriteLine("@R13"); // @R13
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@ARG"); // @ARG
        FileWriteLine("M=D"); // D=M

        // LCL = *(endFrame-4)
        FileWriteLine("@R13"); // @R13
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=M"); // D=M
        FileWriteLine("@LCL"); // @LCL
        FileWriteLine("M=D"); // D=M

        // goto retAddr <R14>
        FileWriteLine("@R14"); // @R14
        FileWriteLine("A=M"); // A=M
        FileWriteLine("0;JMP"); // 0;JMP
    }

    public void WriteArithmetic(string command)
    {
        mError = "";
        command = command.ToLower();

        if (command == "add")
        {
            WriteMathXY("D+M");
        }
        else if ( command == "sub" )
        {
            WriteMathXY("M-D");
        }
        else if (command == "or")
        {
            WriteMathXY("M|D");
        }
        else if (command == "and")
        {
            WriteMathXY("M&D");
        }
        else if (command == "neg")
        {
            WriteMathX("-M");
        }
        else if (command == "not")
        {
            WriteMathX("!M");
        }
        else if (command == "eq")
        {
            WriteCompare( "JEQ" );
        }
        else if (command == "lt")
        {
            WriteCompare("JLT");
        }
        else if (command == "gt")
        {
            WriteCompare("JGT");
        }
        else
        {
            mError = "Unrecognized command '" + command + "'";
        }
    }

    public void WritePushPop(CommandType command, string segment, int index)
    {
        mError = "";
        segment = segment.ToLower();

        bool isPointer = false;
        bool isConstant = false;
        string address = "0";

        SegmentInfo(segment, ref index, out address, out isPointer, out isConstant);

        switch (command)
        {
            case CommandType.C_PUSH:
                FileWriteLine("@" + index); // @index
                FileWriteLine("D=A"); // D=A
                if ( !isConstant )
                {
                    FileWriteLine("@" + address); // @segment pointer
                    if ( !isPointer )
                    {
                        FileWriteLine("A=D+A"); // A=D+A
                    }
                    else 
                    {
                        FileWriteLine("A=D+M"); // A=D+M
                    }
                    FileWriteLine("D=M"); // D=M
                }
                FileWriteLine("@SP"); // @SP
                FileWriteLine("M=M+1"); // M=M+1
                FileWriteLine("A=M-1"); // A=M
                FileWriteLine("M=D"); // M=D
                break;

            case CommandType.C_POP:
                // Store target address at current stackpointer
                FileWriteLine("@" + index); // @index
                FileWriteLine("D=A"); // D=A
                FileWriteLine("@" + address ); // @segment pointer
                if ( !isPointer )
                {
                    FileWriteLine("D=D+A"); // A=D+A
                }
                else
                {
                    FileWriteLine("D=D+M"); // A=D+M
                }
                FileWriteLine("@SP"); // @SP
                FileWriteLine("A=M"); // M=D
                FileWriteLine("M=D"); // M=D

                // Fetch stack pointer - 1 value
                FileWriteLine("@SP"); // @SP
                FileWriteLine("AM=M-1"); // AM=M-1
                FileWriteLine("D=M"); // D=M

                // Write it to stored address
                FileWriteLine("A=A+1"); // A=A+1
                FileWriteLine("A=M"); // A=M
                FileWriteLine("M=D"); // M=D
                break;
        }
    }

    public void WriteComment(string comment)
    {
        if ( mComments )
        {
            FileWriteLine("// " + comment + " //");
        }
    }
}
