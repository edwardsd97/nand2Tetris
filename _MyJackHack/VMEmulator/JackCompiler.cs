using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

class JackCompiler
{
    // Options - default false
    static public bool mComments = false;
    static public bool mVerbose = false;
    static public bool mRecursiveFolders = false;

    // Options - default true
    static public bool mStaticStrings = true;
    static public bool mOSClasses = true;
    static public bool mInvertedConditions = true;

    static string HACK_OS = ".HackOS.";

    static public Dictionary<string, Tokenizer> mTokenizerDic = new Dictionary<string, Tokenizer>();

    static void OldMain(string[] args)
    {
        List<string> paths = new List<string>();

        foreach (string arg in args)
        {
            string lwrArg = arg.ToLower();
            if (lwrArg == "-c")
                JackCompiler.mComments = !JackCompiler.mComments;
            else if (lwrArg == "-v")
                JackCompiler.mVerbose = !JackCompiler.mVerbose;
            else if (lwrArg == "-r")
                JackCompiler.mRecursiveFolders = !JackCompiler.mRecursiveFolders;
            else if (lwrArg == "-s")
                JackCompiler.mStaticStrings = !JackCompiler.mStaticStrings;
            else if (lwrArg == "-o")
                JackCompiler.mOSClasses = !JackCompiler.mOSClasses;
            else if (lwrArg == "-f")
                JackCompiler.mInvertedConditions = !JackCompiler.mInvertedConditions;
            else
                paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            Console.WriteLine("No filename or directory provided.");
            return;
        }

        // Convert directories to actual files recursively
        for (int i = 0; i < paths.Count; i++)
        {
            FileAttributes attrib = File.GetAttributes(paths[i]);
            if (attrib.HasFlag(FileAttributes.Directory))
            {
                string[] files = Directory.GetFiles(paths[i], "*.jack");
                foreach (string file in files)
                {
                    if ( !paths.Contains( file ) && ( JackCompiler.mRecursiveFolders || !File.GetAttributes(file).HasFlag(FileAttributes.Directory) ) )
                        paths.Add(file);
                }
                paths.RemoveAt(i--);
            }
        }

        // Setup global scope symbol table
        SymbolTable.ScopePush( "global" );

        // Pre-process in 2 phases - this allows the compiler to realize all possible class types before compiling 
        Assembly asm = Assembly.GetExecutingAssembly();
        for ( int phase = 0; phase < 2; phase++ )
        {
            // Pre-process the operating system classes that are part of the compiler itself
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(HACK_OS))
                    continue;
                if (ClassInList(osName, paths))
                    continue;
                Stream resourceStream = asm.GetManifestResourceStream(osName);
                if (resourceStream != null)
                {
                    if (phase == 0)
                        Console.WriteLine("Preprocessing... " + osName);
                    StreamReader sRdr = new StreamReader(resourceStream);
                    PreProcessFile(osName, sRdr, phase);
                }
            }

            // Pre-process the target files
            for (int i = 0; i < paths.Count; i++)
            {
                if ( phase == 0 )
                    Console.WriteLine("Preprocessing... " + paths[i]);
                StreamReader sRdr = new StreamReader(paths[i]);
                PreProcessFile(paths[i], sRdr, phase);
            }
        }

        // Compile the files
        List<string> destFolders = new List<string>();
        for (int i = 0; i < paths.Count; i++)
        {
            Console.WriteLine("Compiling... " + paths[i]);
            CompileFile(paths[i], GetOutBasefile(paths[i]));
            string destFolder = FileToPath( paths[i] );
            if ( !destFolders.Contains(destFolder))
            {
                destFolders.Add(destFolder);
            }
        }

        // Finally compile any OS classes that were referenced and any other OS classes those reference as well
        bool doneOS = false;
        Dictionary<string, bool> compiled = new Dictionary<string, bool>();
        while ( !doneOS && JackCompiler.mOSClasses )
        {
            doneOS = true;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(HACK_OS))
                    continue;
                if (ClassInList(osName, paths))
                    continue;
                foreach ( Compiler.FuncSpec funcSpec in Compiler.mFunctions.Values )
                {
                    if ( compiled.ContainsKey( osName ) )
                        break;
                    if ( funcSpec.filePath == osName && funcSpec.referenced && !funcSpec.compiled )
                    {
                        Console.WriteLine("Compiling... " + osName);
                        foreach ( string destFolder in destFolders )
                        {
                            CompileFile(osName, destFolder + FileToName(osName));
                            compiled.Add(osName, true);
                        }
                        doneOS = false;
                    }
                }
            }
        }
    }

    static void PreProcessFile(string filePath, StreamReader streamReader, int phase )
    {
        Tokenizer tokenizer;

        if (mTokenizerDic.ContainsKey(filePath))
        {
            tokenizer = mTokenizerDic[filePath];
        }
        else
        {
            tokenizer = new Tokenizer(streamReader);

            // Read all tokens into memory
            while (tokenizer.HasMoreTokens())
            {
                tokenizer.Advance();
            }
            tokenizer.Close();
            tokenizer.Reset();

            mTokenizerDic.Add(filePath, tokenizer);
        }

        Compiler compiler = new Compiler(tokenizer);
        compiler.CompilePrePass();
    }

    static void CompileFile( string srcPath, string destPath )
    {
        Tokenizer tokenizer;
        if (mTokenizerDic.TryGetValue(srcPath, out tokenizer))
        {
            Compiler compiler = new Compiler( tokenizer, new VMWriter( destPath + ".vm" ) );

            // Compile the tokens into output file
            compiler.Reset();
            compiler.CompileClass();
        }
        else
        {
            Console.WriteLine("Tokens not found for " + srcPath);
        }
    }

    public static string GetOutTokenfile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name + "T.xml";
        return outFile;
    }

    public static string GetOutBasefile(string file)
    {
        string name = FileToName(file);
        string path = FileToPath(file);
        string outFile = path + name;
        return outFile;
    }

    public static string FileToName(string file)
    {
        string[] fileParts = file.Split(new char[3] { '.', '/', '\\' });
        if (fileParts.Length > 1)
        {
            return fileParts[fileParts.Length - 2];
        }
        return "";
    }

    public static string FileToPath(string file)
    {
        string[] fileParts = file.Split(new char[3] { '.', '/', '\\' });
        string result = "";
        if (fileParts.Length > 1)
        {
            for (int i = 0; i < fileParts.Length - 2; i++)
                result = result + fileParts[i] + "/";
        }
        return result;
    }

    public static bool ClassInList(string classFile, List<string> files)
    {
        string fileName = FileToName(classFile).ToLower();
        foreach (string file in files)
        {
            if ( fileName == FileToName(file).ToLower() )
                return true;
        }
        return false;
    }
}

