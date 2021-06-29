﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reflection;
using System.Windows.Threading;
using VM;

namespace VMEmulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Emulator: Super tiny limits for easier viewing and tooling
        Emulator mVM = new Emulator( 256, 172, 64 );

        MemoryStream Instructions = new MemoryStream();
        Compiler compiler;

        int mTestCase = -1;
        int mTestCases = 0;
        bool mStringsMode = false;
        bool mJustCompiled = false;

        DispatcherTimer dispatchCompile;
        DispatcherTimer dispatchUpdate;

        Debugger mDebugger;

        public MainWindow()
        {
            InitializeComponent();

            TestCaseInit();
            TestCaseSet("Empty");

            Compile();
        }

        private void buttonStrings_Click(object sender, RoutedEventArgs e)
        {
            mStringsMode = !mStringsMode;
            UpdateForm();
        }

        private void textCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            TimerCompileSetup();
        }

        private void textCode_SelectChanged(object sender, RoutedEventArgs e)
        {
            if (textValue == null)
                return;

            textValue.Content = "";
            if ( mDebugger != null )
            {
                string select = textCode.Text.Substring(textCode.SelectionStart, textCode.SelectionLength);
                select = select.Trim();
                if ( select != "" )
                {
                    // this is ugly
                    int lineNum = 1;
                    int charNum = 0;
                    int c = 0;
                    string text = textCode.Text;

                    while (c <= textCode.SelectionStart)
                    {
                        if (text[c] == '\n')
                        {
                            lineNum++;
                            charNum = 0;
                        }

                        charNum++;
                        c++;
                    }

                    int value;
                    if ( mDebugger.GetSymbolValue( mVM, "", lineNum, charNum, select, out value ) )
                    {
                        textValue.Content = select + " " + value;
                    }
                }
            }
        }

        private void buttonStep_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();

            TimerUpdateSetup(-1);
            mVM.ExecuteThread(false);
            mVM.ExecuteStep();
            UpdateForm();
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void buttonTestCase_Click(object sender, RoutedEventArgs e)
        {
            TestCaseCycle(1);
        }

        private void buttonTestCasePrev_Click(object sender, RoutedEventArgs e)
        {
            TestCaseCycle(-1);
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();
            mVM.ExecuteThread(true, 100000);
            TimerUpdateSetup(100);
        }

        private void buttonPlayFast_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();
            mVM.ExecuteThread(true, 15000);
            TimerUpdateSetup(15);
        }

        private void buttonPlayFull_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();
            mVM.ExecuteThread(true);
            TimerUpdateSetup(50);
        }

        public void Reset()
        {
            TimerUpdateSetup(-1);
            mVM.ExecuteThread(false);
            mVM.Reset();
            UpdateForm();
        }

        public void TimerUpdateSetup(int msTickRate = 50, bool updateForm = true)
        {
            if (dispatchUpdate != null)
            {
                dispatchUpdate.Stop();
                dispatchUpdate = null;
            }

            if (msTickRate >= 0)
            {
                dispatchUpdate = new DispatcherTimer();
                dispatchUpdate.Tick += TimerUpdateTick;
                dispatchUpdate.Interval = new TimeSpan(0, 0, 0, 0, msTickRate);
                dispatchUpdate.Start();
            }
        }

        void TimerUpdateTick(object sender, object e)
        {
            UpdateForm();
            if (!mVM.Running())
            {
                TimerUpdateSetup(-1);
                UpdateForm();
            }
        }

        public void TimerCompileSetup(int msTickRate = 750)
        {
            if (dispatchCompile != null)
            {
                dispatchCompile.Stop();
                dispatchCompile = null;
            }

            if (msTickRate >= 0)
            {
                dispatchCompile = new DispatcherTimer();
                dispatchCompile.Tick += TimerCompileTick;
                dispatchCompile.Interval = new TimeSpan(0, 0, 0, 0, msTickRate);
                dispatchCompile.Start();
            }
        }

        void TimerCompileTick(object sender, object e)
        {
            Compile();
        }

        public void TestCaseInit()
        {
            mTestCases = 0;

            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".TestCases."))
                    continue;
                mTestCases++;
            }
        }

        public void TestCaseCycle( int offset )
        {
            mTestCase = mTestCase + offset;
            if (mTestCase > mTestCases - 1)
                mTestCase = 0;
            if (mTestCase < 0 )
                mTestCase = mTestCases - 1;
            TestCaseUpdate();
        }

        public void TestCaseUpdate()
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            int testCaseIndex = -1;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".TestCases."))
                    continue;

                testCaseIndex++;

                if ( mTestCase == testCaseIndex )
                {
                    Stream resourceStream = asm.GetManifestResourceStream(osName);
                    if (resourceStream != null)
                    {
                        StreamReader sRdr = new StreamReader(resourceStream);
                        textCode.Text = sRdr.ReadToEnd();
                        Compile();
                        return;
                    }
                }
            }
        }

        public void TestCaseSet( string subString )
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            int testCaseIndex = -1;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".TestCases."))
                    continue;

                testCaseIndex++;

                if (osName.Contains(subString))
                    break;
            }

            mTestCase = testCaseIndex;
            TestCaseUpdate();
        }

        public void Compile()
        {
            mVM.ExecuteThread(false);
            TimerUpdateSetup(-1);
            TimerCompileSetup(-1);

            MemoryStream byteCode = new MemoryStream();

            Instructions = new MemoryStream();

            Writer writer = new Writer(Instructions);
            Compile(writer);

            ByteCode convert = new ByteCode();
            convert.ConvertVMText(Instructions, byteCode, compiler);

            mVM.ResetAll();
            mVM.Load(byteCode);
            UpdateForm();
        }

        public void UpdateForm()
        {
            UpdateCodeHighlight();
            UpdateInstructions();
            UpdateByteCode();
            UpdateSegments();
            UpdateStack();
            UpdateGlobals();
            UpdateHeap();
            UpdateErrors();

            mJustCompiled = false;
        }

        public void UpdateCodeHighlight()
        {
            textCode.Focus();

            if ( mVM.Running() && mVM.mCodeFrame > 0 )
            {
                int selectStart = -1;
                int selectLength = -1;

                Debugger.DebugCommand dbgCmd;
                if (mDebugger.mCommandMap.TryGetValue(mVM.mCodeFrame, out dbgCmd))
                {
                    int line = 1;
                    int c = 0;
                    string text = textCode.Text;

                    while (c < text.Length)
                    {
                        if (selectStart < 0 && line == dbgCmd.mSourceLine)
                            selectStart = c;

                        if (text[c] == '\n')
                        {
                            if (selectStart >= 0)
                            {
                                selectLength = c - selectStart;
                                break;
                            }
                            line++;
                        }

                        c++;
                    }
                }

                if ( selectStart > -1 && selectLength > -1 )
                    textCode.Select(selectStart, selectLength);
            }
            else
            {
                textCode.Select(0, 0);
            }
        }

        public void UpdateSegments()
        {
            string memStr = "";
            for (int i = 0; i < mVM.mMemoryDwords && i < 7; i++)
            {
                string hexStr = Convert.ToString(mVM.mMemory[i], 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                memStr = memStr + "0x" + hexStr.ToUpper();
                if (i == (int)SegPointer.SP)
                    memStr = memStr + " [SP]";
                else if (i == (int)SegPointer.ARG)
                    memStr = memStr + " [ARG]";
                else if (i == (int)SegPointer.GLOBAL)
                    memStr = memStr + " [GLOBAL]";
                else if (i == (int)SegPointer.LOCAL)
                    memStr = memStr + " [LOCAL]";
                else if (i == (int)SegPointer.THIS)
                    memStr = memStr + " [THIS]";
                else if (i == (int)SegPointer.THAT)
                    memStr = memStr + " [THAT]";
                else if (i == (int)SegPointer.TEMP)
                    memStr = memStr + " [TEMP]";
                memStr = memStr + "\r\n";
            }

            if (textSegmentPointers != null )
                textSegmentPointers.Text = memStr;
        }

        public void UpdateStack()
        {
            string memStr = "";
            int start = (int)SegPointer.COUNT;
            int end = mVM.mMemory[(int)SegPointer.SP];
            for (int i = start; i < end; i++)
            {
                if (i >= 0 && i < mVM.mMemory.Length)
                    memStr = memStr + mVM.mMemory[i] + "\r\n";
            }

            if (textStack != null)
                textStack.Text = memStr;
        }

        public void UpdateGlobals()
        {
            string memStr = "";
            int start = mVM.mMemory[(int)SegPointer.GLOBAL];
            int end = start + mVM.mGlobalDwords;
            for (int i = start; i < end; i++)
            {
                if (i >= 0 && i < mVM.mMemory.Length)
                {
                    if (mDebugger != null)
                    {
                        int offset = i - start;
                        string varName = mDebugger.GetSegmentSymbol(SymbolTable.Kind.GLOBAL, offset, "", 0, 0 );
                        if (varName != "")
                            memStr = memStr + varName + " ";
                    }
                    memStr = memStr + mVM.mMemory[i] + "\r\n";
                }
            }

            if (textGlobals != null)
                textGlobals.Text = memStr;
        }

        public void UpdateHeap()
        {
            string memStr = "";

            for (int i = mVM.mMemoryDwords - mVM.mHeapDwords; i < mVM.mMemory.Length; i++)
            {
                string hexStr = Convert.ToString(mVM.mMemory[i], 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                memStr = memStr + "0x" + hexStr.ToUpper() + "\r\n";
            }

            if (textHeap != null)
                textHeap.Text = memStr;
        }

        public void UpdateInstructions()
        {
            string vmStr = "";

            if (mStringsMode)
            {
                labelVM.Content = "String Table";
                buttonStrings.Content = "Emulator Cmds";

                vmStr = vmStr + "Statics:\r\n";

                foreach (int key in mVM.mStrings.Keys)
                {
                    string idStr = "" + key;
                    if (key == mVM.mStringsStatic)
                    {
                        vmStr = vmStr + "\r\n";
                        vmStr = vmStr + "Modifiable:\r\n";
                    }
                    while (idStr.Length < 4)
                        idStr = " " + idStr;
                    string lineStr = idStr + " \"" + mVM.mStrings[key].mString + "\"";
                    lineStr = lineStr + "\r\n";
                    vmStr = vmStr + lineStr;
                }
            }
            else
            {
                labelVM.Content = "Emulator Commands";
                buttonStrings.Content = "Strings";
                int codeFrame = 0;
                Instructions.Seek(0, SeekOrigin.Begin);
                StreamReader vmreader = new StreamReader(Instructions);
                while (!vmreader.EndOfStream)
                {
                    string lineStr = vmreader.ReadLine();
                    string[] elements = ByteCode.CommandElements(lineStr);

                    lineStr = " " + lineStr;

                    if (elements[0] != "label")
                    {
                        if ( codeFrame == mVM.mCodeFrame )
                            lineStr = ">" + lineStr;

                        if (elements[0] != "label")
                            codeFrame++;
                    }

                    lineStr = lineStr + "\r\n";

                    vmStr = vmStr + lineStr;
                }
            }

            if (textVM != null)
                textVM.Text = vmStr;
        }

        public void UpdateByteCode()
        {
            string byteStr = "";

            for ( int i = 0; i < mVM.mCode.Length; i++ )
            {
                int command = ByteCode.Translate( mVM.mCode[i] );
                string hexStr = Convert.ToString(command, 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                hexStr = "0x" + hexStr.ToUpper();
                if (i == mVM.mCodeFrame)
                    hexStr = ">" + hexStr;
                else
                    hexStr = " " + hexStr;
                byteStr = byteStr + hexStr + "\r\n";
            }
            if (textVMByteCode != null)
                textVMByteCode.Text = byteStr;
        }

        public void UpdateErrors()
        {
            string errors = "";
            for (int i = 0; i < compiler.mErrors.Count; i++)
            {
                errors = errors + compiler.mErrors[i] + "\r\n";
            }
            for (int i = 0; i < mVM.mErrors.Count; i++)
            {
                errors = errors + mVM.mErrors[i] + "\r\n";
            }

            if ( mVM.Halted() )
                errors = errors + "PROGRAM HALTED\r\n";
            else if (mVM.Finished())
                errors = errors + "Program ended\r\n";

            if (textErrors != null)
                textErrors.Text = errors;
        }

        public void Compile(IWriter writer)
        {
            string code = textCode.Text;

            byte[] byteArray = Encoding.ASCII.GetBytes(code);
            MemoryStream stream = new MemoryStream(byteArray);
            StreamReader reader = new StreamReader(stream);

            List<Tokenizer> tokenizers = new List<Tokenizer>();

            // Include the operating system class declarations
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".OSVM."))
                    continue;

                Stream resourceStream = asm.GetManifestResourceStream(osName);
                if (resourceStream != null)
                {
                    StreamReader sRdr = new StreamReader(resourceStream);
                    Tokenizer tokens = new Tokenizer( sRdr, osName );
                    tokens.ReadAll();
                    tokenizers.Add(tokens);
                }
            }

            Tokenizer tokenizer = new Tokenizer(reader);
            tokenizer.ReadAll();
            tokenizers.Add(tokenizer);

            mDebugger = new Debugger();
            compiler = new Compiler(tokenizers, writer, mDebugger);
            compiler.Compile();

            mJustCompiled = true;
        }
    }
}