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

            // Only write the bootstrap code if Sys.vm is present
            if ( File.Exists(path + "/Sys.vm" ) )
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

        return "";
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
        Close();
    }

    public void Close()
    {
        WriteComment("INFINITE LOOP");

        mFile.WriteLine("(_END)");
        mFile.WriteLine("@_END");
        mFile.WriteLine("0;JMP");

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
            // segment  address         isPointer   isConstant  classUnique
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

    public void WriteInit()
    {
        WriteComment("Bootstrap Code");

        // Set SP to 256
        mFile.WriteLine("@256"); // @256
        mFile.WriteLine("D=A"); // @256
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=D"); // @SP

        // call Sys.init
        this.SetVMFileName("Boot");
        WriteComment("call Sys.init 0");
        WriteCall("Sys.init", 0);
    }

    protected void WriteCompare( string jumpTest )
    {
        // Compare two values using the provided jumpTest
        //  ( JEQ, JLT, JGT, ... )
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("D=M-D"); // D=M-D
        mFile.WriteLine("@_COMPARE_TRUE"+ mCompareIndex);
        mFile.WriteLine("D;" + jumpTest ); // D;<jumpTest>
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=0"); // M=0
        mFile.WriteLine("@_COMPARE_FINISHED" + mCompareIndex);
        mFile.WriteLine("0;JMP");
        mFile.WriteLine("(_COMPARE_TRUE" + mCompareIndex + ")");
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=-1"); // M=-1
        mFile.WriteLine("(_COMPARE_FINISHED" + mCompareIndex + ")" );
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        mCompareIndex++;
    }

    protected void WriteMathXY(string operation)
    {
        // operation is operated on M in terms of M and D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=" + operation); // D=<operation>
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1
    }

    protected void WriteMathX(string operation)
    {
        // operation is operated on M in terms of M
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=" + operation); // M=<operation>
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1
    }

    public void WriteLabel(string label)
    {
        mFile.WriteLine("(" + label + ")" ); // (<label>)
    }

    public void WriteGoto(string label)
    {
        mFile.WriteLine("@" + label);   // @<label>
        mFile.WriteLine("0;JMP");       // 0;JUMP
    }

    public void WriteIf(string label)
    {
        // If pop stack != 0
        mFile.WriteLine("@SP");         // @SP
        mFile.WriteLine("M=M-1");       // M=M-1
        mFile.WriteLine("A=M");         // A=M
        mFile.WriteLine("D=M");         // D=M
        mFile.WriteLine("@" + label);   // @<label>
        mFile.WriteLine("D;JNE");       // 0;JNE
    }

    public void WriteFunction(string functionName, int localCount )
    {
        mFile.WriteLine("(" + functionName + ")"); // (functionName)

        if (localCount > 0)
        {
            // repeat nVars times:
            mFile.WriteLine("@SP"); // @SP
            mFile.WriteLine("A=M"); // A=M
            for (int i = 0; i < localCount; i++)
            {
                // push 0
                mFile.WriteLine("M=0"); // M=0
                mFile.WriteLine("@SP"); // @SP
                mFile.WriteLine("AM=M+1"); // AM=M+1
            }
        }
    }

    public void WriteCall(string functionName, int argumentCount)
    {
        string returnAddress = mVMFileName + "." + functionName + "$ret." + mCallIndex++;

        // push returnAddress (label below)
        mFile.WriteLine("@" + returnAddress); // @functionName$ret.N
        mFile.WriteLine("D=A"); // D=A
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        // push LCL pointer value
        mFile.WriteLine("@LCL"); // @LCL
        mFile.WriteLine("D=M"); // D=A
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        // push ARG pointer value
        mFile.WriteLine("@ARG"); // @ARG
        mFile.WriteLine("D=M"); // D=A
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        // push THIS pointer value
        mFile.WriteLine("@THIS"); // @THIS
        mFile.WriteLine("D=M"); // D=A
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        // push THAT pointer value
        mFile.WriteLine("@THAT"); // @THAT
        mFile.WriteLine("D=M"); // D=A
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M+1"); // M=M+1

        // ARG = SP-5-nArgs
        int backSteps = 5 + argumentCount - 1; // -1 to account for D=M-1
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("D=M-1"); // D=M
        for ( int i = 0; i < backSteps; i++ )
            mFile.WriteLine("D=D-1"); // D=D-1
        mFile.WriteLine("@ARG"); // @ARG
        mFile.WriteLine("M=D"); // M=D

        // LCL = SP
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@LCL"); // @LCL
        mFile.WriteLine("M=D"); // M=D

        // goto functionName
        WriteGoto(functionName);

        // (returnAddress)
        mFile.WriteLine("(" + returnAddress + ")" );
    }

    public void WriteReturn()
    {
        // endFrame <R13> = LCL 
        mFile.WriteLine("@LCL"); // @LCL
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@R13"); // @R13
        mFile.WriteLine("M=D"); // M=D

        // retAddr <R14> = *(endFrame-5)
        for( int i = 0; i < 5; i++ )
            mFile.WriteLine("D=D-1"); // D=D-1
        mFile.WriteLine("A=D"); // A=D
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@R14"); // @R14
        mFile.WriteLine("M=D"); // M=D

        // *ARG = pop()
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("A=M-1"); // A=M-1
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@ARG"); // @ARG
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("M=D"); // M=D
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=M-1"); // M=M-1

        // SP=ARG+1
        mFile.WriteLine("@ARG"); // @ARG
        mFile.WriteLine("D=M+1"); // D=M+1
        mFile.WriteLine("@SP"); // @SP
        mFile.WriteLine("M=D"); // M=D

        // THAT = *(endFrame-1)
        mFile.WriteLine("@R13"); // @R13
        mFile.WriteLine("AM=M-1"); // AM=M-1
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@THAT"); // @THAT
        mFile.WriteLine("M=D"); // D=M

        // THIS = *(endFrame-2)
        mFile.WriteLine("@R13"); // @R13
        mFile.WriteLine("AM=M-1"); // AM=M-1
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@THIS"); // @THIS
        mFile.WriteLine("M=D"); // D=M

        // ARG = *(endFrame-3)
        mFile.WriteLine("@R13"); // @R13
        mFile.WriteLine("AM=M-1"); // AM=M-1
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@ARG"); // @ARG
        mFile.WriteLine("M=D"); // D=M

        // LCL = *(endFrame-4)
        mFile.WriteLine("@R13"); // @R13
        mFile.WriteLine("AM=M-1"); // AM=M-1
        mFile.WriteLine("D=M"); // D=M
        mFile.WriteLine("@LCL"); // @LCL
        mFile.WriteLine("M=D"); // D=M

        // goto retAddr <R14>
        mFile.WriteLine("@R14"); // @R14
        mFile.WriteLine("A=M"); // A=M
        mFile.WriteLine("0;JMP"); // 0;JMP
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
                mFile.WriteLine("@" + index); // @index
                mFile.WriteLine("D=A"); // D=A
                if ( !isConstant )
                {
                    mFile.WriteLine("@" + address); // @segment pointer
                    if ( !isPointer )
                    {
                        mFile.WriteLine("A=D+A"); // A=D+A
                    }
                    else 
                    {
                        mFile.WriteLine("A=D+M"); // A=D+M
                    }
                    mFile.WriteLine("D=M"); // D=M
                }
                mFile.WriteLine("@SP"); // @SP
                mFile.WriteLine("A=M"); // A=M
                mFile.WriteLine("M=D"); // M=D
                mFile.WriteLine("@SP"); // @SP
                mFile.WriteLine("M=M+1"); // M=M+1
                break;

            case CommandType.C_POP:
                // Store target address at current stackpointer
                mFile.WriteLine("@" + index); // @index
                mFile.WriteLine("D=A"); // D=A
                mFile.WriteLine("@" + address ); // @segment pointer
                if ( !isPointer )
                {
                    mFile.WriteLine("D=D+A"); // A=D+A
                }
                else
                {
                    mFile.WriteLine("D=D+M"); // A=D+M
                }
                mFile.WriteLine("@SP"); // @SP
                mFile.WriteLine("A=M"); // M=D
                mFile.WriteLine("M=D"); // M=D

                // Fetch stack pointer - 1 value
                mFile.WriteLine("@SP"); // @SP
                mFile.WriteLine("M=M-1"); // M=M-1
                mFile.WriteLine("A=M"); // A=M
                mFile.WriteLine("D=M"); // D=M

                // Write it to stored address
                mFile.WriteLine("A=A+1"); // A=A+1
                mFile.WriteLine("A=M"); // A=M
                mFile.WriteLine("M=D"); // M=D
                break;
        }
    }

    public void WriteComment(string comment)
    {
       // mFile.WriteLine("// " + comment + " //" );
    }
}
