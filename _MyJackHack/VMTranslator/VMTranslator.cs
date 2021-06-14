using System;
using System.IO;
using System.Collections;

class VMTranslator
{
    static void Main(string[] args)
    {
        ArrayList paths = new ArrayList();

        Console.WriteLine("VM Translator Arguments:");
        foreach (string arg in args)
        {
            Console.WriteLine("  " + arg );
            string lwrArg = arg.ToLower();
            if (lwrArg == "-c")
                CodeWriter.mComments = true;
            else if (lwrArg == "-v")
                CodeWriter.mVerbose = false;
            else paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            Console.WriteLine("No filename or directory provided.");
        }

        for (int i = 0; i < paths.Count; i++)
        {
            ProcessPath((string) paths[i]);
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
            foreach (string file in vmFiles)
            {
                string lower = file.ToLower();
                if ( lower.Contains( "sys.vm" ) )
                {
                    writer.WriteBootstrapSysInit(outFile);
                    break;
                }
            }
        }
        else
        {
            Console.WriteLine("");
            Console.WriteLine("VM Translating file: " + path);
            vmFiles = new string[1] { path };
        }

        Parser.Command command = null;
        Parser.Command commandNext = null;

        for ( int i = 0; i < vmFiles.Length; i++ )
        {
            Parser parser = new Parser( vmFiles[i] );

            string outVMName = FilePathToName(vmFiles[i]);
            writer.SetVMFileName(outVMName);

            Console.WriteLine( "  " + vmFiles[i] + " -> " + outFile );

            while ( parser.HasMoreCommands() || commandNext != null )
            {
                command = commandNext;
                commandNext = null;

                if ( parser.HasMoreCommands() )
                {
                    commandNext = parser.Advance();
                }

                if ( command != null )
                {
                    // If the command after a PUSH is a POP write PUSHPOP that writes directly to the destination rather than using the stack
                    if ( commandNext != null && command.mCommandType == CommandType.C_PUSH && commandNext.mCommandType == CommandType.C_POP )
                    {
                        writer.WriteComment(command.mLineString + " AND " + commandNext.mLineString );
                        writer.WritePushPop( command.mArg1, command.mArg2, commandNext.mArg1, commandNext.mArg2 );
                        commandNext = null;
                        if (parser.HasMoreCommands())
                        {
                            commandNext = parser.Advance();
                        }
                        if (writer.mError != "")
                        {
                            Console.WriteLine("ERROR: Line " + parser.mLine + " - " + writer.mError);
                        }
                        continue;
                    }                    

                    writer.WriteComment(command.mLineString);

                    switch (command.mCommandType)
                    {
                        case CommandType.C_ARITHMETIC:
                            writer.WriteArithmetic(command.mArg1);
                            break;

                        case CommandType.C_PUSH:
                            writer.WritePush(command.mArg1, command.mArg2);
                            break;

                        case CommandType.C_POP:
                            writer.WritePop(command.mArg1, command.mArg2);
                            break;

                        case CommandType.C_LABEL:
                            writer.WriteLabel(command.mArg1);
                            break;

                        case CommandType.C_GOTO:
                            writer.WriteGoto(command.mArg1);
                            break;

                        case CommandType.C_IF:
                            writer.WriteIf(command.mArg1);
                            break;

                        case CommandType.C_FUNCTION:
                            writer.WriteFunction(command.mArg1, command.mArg2);
                            break;

                        case CommandType.C_CALL:
                            writer.WriteCall(command.mArg1, command.mArg2);
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
            string filePathToName = FilePathToName(fileOrDirectory);
            string result = fileOrDirectory;

            if (filePathToName != "")
            {
                // Directory is a path to a folder
                result = result + "/" + filePathToName;
            }
            else
            {
                // Directory is a single folder name in current folder
                result = result + "/" + fileOrDirectory;
            }

            return result + ".asm";
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
    public class Command
    {
        public CommandType  mCommandType;
        public string       mArg1;
        public int          mArg2;

        public int mLine;
        public string mLineString;

        public Command(string commandStr, string lineString, int line)
        {
            mLineString = lineString;
            mLine = line;
            mArg1 = commandStr;

            string lower = commandStr.ToLower();

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
            }
        }
    };

    public System.IO.StreamReader   mFile;
    public int mLine;
    public string mLineString;

    public Parser( string fileName )
    {
        mFile = new System.IO.StreamReader( fileName );
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

    public Command Advance()
    {
        Command command = null;

        while (command == null && HasMoreCommands())
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
                        // Create new command
                        command = new Command( word, mLineString, mLine );
                        break;

                    case 1:
                        // Argument 1 (string)
                        command.mArg1 = word;
                        break;

                    case 2:
                        // Argument 2 (int)
                        command.mArg2 = int.Parse( word );
                        break;
                }
            }
        }

        return command;
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

    protected static object[] mSegments = 
    { 
        // segment  address         isPointer   isConstant  fileUnique
        "local",    "LCL",  /*1*/   1,          0,          0,
        "argument", "ARG",  /*2*/   1,          0,          0,
        "this",     "THIS", /*3*/   1,          0,          0,
        "that",     "THAT", /*4*/   1,          0,          0,
        "temp",     5,              0,          0,          0,
        "static",   16,             0,          0,          1,
        "constant", 0,              0,          1,          0,
        "pointer",  "THIS", /*3*/   0,          0,          0,
    };

    static public bool mComments = false;
    static public bool mVerbose = false;

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

        for (int i = 0; i < mSegments.Length; i = i + 5)
        {
            if (segment == (string)mSegments[i])
            {
                if ( (int)mSegments[i + 4] != 0 )
                {
                    // FIXME - improve this to handle the same thing for any segment when fileUnique is true
                    // index is unique per file
                    mStaticMax = Math.Max( mStaticMax, index + 1 );
                    index += mStaticOffset;
                }

                address = "" + mSegments[i + 1];
                isPointer = (int)mSegments[i + 2] != 0;
                isConstant = (int)mSegments[i + 3] != 0;
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

    public void WriteBootstrapSysInit(string outFile)
    {
        WriteComment("Bootstrap Code");
        Console.WriteLine("  " + "Bootstrap Code -> " + outFile );

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

    public void WritePush(string segment, int index)
    {
        mError = "";
        segment = segment.ToLower();

        bool isPointer = false;
        bool isConstant = false;
        string address = "0";

        SegmentInfo(segment, ref index, out address, out isPointer, out isConstant);

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
    }

    public void WritePop(string segment, int index)
    {
        mError = "";
        segment = segment.ToLower();

        bool isPointer = false;
        bool isConstant = false;
        string address = "0";

        SegmentInfo(segment, ref index, out address, out isPointer, out isConstant);

        // Optimal - 9 instructions //

        // put addr = index + RAM[segment] into D  // 4 lines of code
        FileWriteLine("@" + index); // @index
        FileWriteLine("D=A"); // D=A
        FileWriteLine("@" + address); // @segment pointer
        if (!isPointer)
        {
            FileWriteLine("D=D+A"); // A=D+A
        }
        else
        {
            FileWriteLine("D=D+M"); // A=D+M
        }

        // add the top of the stack to D so that D holds val + addr (and //decrease stack pointer in the process // 3 lines of code
        FileWriteLine("@SP"); // @SP
        FileWriteLine("AM=M-1"); // AM=M-1
        FileWriteLine("D=D+M"); // D=D+M

        //  A = (val + addr) - val ( addr ) // 1 line of code
        FileWriteLine("A=D-M"); // A=D-M

        // RAM[addr] = (val + addr) - addr ( val ) // 1 line of code
        FileWriteLine("M=D-A"); // M=D-A

        /* Original - 13 instructions //
        // Store target address at current stackpointer
        FileWriteLine("@" + index); // @index
        FileWriteLine("D=A"); // D=A
        FileWriteLine("@" + address); // @segment pointer
        if (!isPointer)
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
        */
    }

    public void WritePushPop( string segmentPush, int indexPush, string segmentPop, int indexPop )
    {
        mError = "";
        segmentPush = segmentPush.ToLower();
        segmentPop = segmentPop.ToLower();

        bool isPointerPush = false;
        bool isConstantPush = false;
        string addressPush = "0";

        bool isPointerPop = false;
        bool isConstantPop = false;
        string addressPop = "0";

        // Push and Pop together that does not need to increment and decrement the SP

        SegmentInfo(segmentPush, ref indexPush, out addressPush, out isPointerPush, out isConstantPush);
        SegmentInfo(segmentPop, ref indexPop, out addressPop, out isPointerPop, out isConstantPop);

        // PUSH - FIXME - optimize
        FileWriteLine("@" + indexPush); // @index
        FileWriteLine("D=A"); // D=A
        if (!isConstantPush)
        {
            FileWriteLine("@" + addressPush); // @segment pointer
            if (!isPointerPush)
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
        FileWriteLine("A=M"); // A=M
        FileWriteLine("M=D"); // M=D

        // POP
        // put addr = index + RAM[segment] into D  // 4 lines of code
        FileWriteLine("@" + indexPop); // @index
        FileWriteLine("D=A"); // D=A
        FileWriteLine("@" + addressPop); // @segment pointer
        if (!isPointerPop)
        {
            FileWriteLine("D=D+A"); // A=D+A
        }
        else
        {
            FileWriteLine("D=D+M"); // A=D+M
        }

        // add the top of the stack to D so that D holds val + addr // 3 lines of code
        FileWriteLine("@SP"); // @SP
        FileWriteLine("A=M"); // A=M
        FileWriteLine("D=D+M"); // D=D+M

        //  A = (val + addr) - val ( addr ) // 1 line of code
        FileWriteLine("A=D-M"); // A=D-M

        // RAM[addr] = (val + addr) - addr ( val ) // 1 line of code
        FileWriteLine("M=D-A"); // M=D-A
    }

    public void WriteComment(string comment)
    {
        if ( mComments )
        {
            FileWriteLine("// " + comment + " //");
        }
    }
}
