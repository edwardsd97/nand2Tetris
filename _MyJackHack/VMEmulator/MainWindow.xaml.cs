using System;
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

        bool mStringsMode = false;
        int mFirstTestCase = -1;

        DispatcherTimer dispatchCompile;
        DispatcherTimer dispatchUpdate;

        Debugger mDebugger;

        public MainWindow()
        {
            InitializeComponent();

            // Include the operating system class declarations
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".OSVM."))
                    continue;

                string[] parts = osName.Split(new char[3] { '/', '\\', '.' });
                if (parts.Length >= 2)
                {
                    comboTest.Items.Add(parts[parts.Length - 2]);
                }
            }
            comboTest.Items.Add("--------");
            mFirstTestCase = comboTest.Items.Count;
            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains(".TestCases."))
                    continue;

                string[] parts = osName.Split(new char[3] { '/', '\\', '.' });
                if (parts.Length >= 2)
                    comboTest.Items.Add(parts[parts.Length - 2]);
            }

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
        
        private void comboTest_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboTest.SelectedIndex >= 0 && comboTest.SelectedIndex < comboTest.Items.Count )
                TestCaseSet( comboTest.SelectedItem.ToString() );
        }

        private void buttonStep_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();

            if (mDebugger == null)
                return;

            TimerUpdateSetup(-1);
            mVM.ExecuteThread(false);
            mDebugger.StepSingle(mVM);
            UpdateForm();
        }

        private void buttonStepOver_Click(object sender, RoutedEventArgs e)
        {
            TimerUpdateSetup(-1);
            mVM.ExecuteThread(false);

            if (!mVM.Running())
                Reset();

            if (mDebugger == null)
                return;

            mDebugger.StepOver( mVM );

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
            Compile();
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

        public void TestCaseCycle( int offset )
        {
            if (comboTest.SelectedIndex == comboTest.Items.Count - 1 && offset > 0 )
                comboTest.SelectedIndex = mFirstTestCase;
            else if (comboTest.SelectedIndex == mFirstTestCase && offset < 0)
                comboTest.SelectedIndex = comboTest.Items.Count - 1;
            else 
                comboTest.SelectedIndex += offset;

            if (((string)comboTest.SelectedValue).Contains("---"))
                TestCaseCycle(offset);

            TestCaseUpdate();
        }

        public void TestCaseUpdate()
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            foreach (string osName in asm.GetManifestResourceNames())
            {
                if (!osName.Contains("." + comboTest.SelectedValue + "." ) )
                    continue;

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

        public void TestCaseSet( string subString )
        {
            for (int i = 0; i < comboTest.Items.Count; i++)
            {
                if ((string)comboTest.Items[i] == subString)
                    comboTest.SelectedIndex = i;
            }

            TestCaseUpdate();
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
        }

        public void UpdateCodeHighlight()
        {
            textCode.Focus();

            if ( mVM.Running() )
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
            int codeLine = 0;
            int codeChar = 0;
            Debugger.DebugCommand cmd;
            if (mDebugger.mCommandMap.TryGetValue(mVM.mCodeFrame, out cmd))
            {
                codeLine = cmd.mSourceLine;
                codeChar = cmd.mSourceChar;
            }
            for (int i = start; i < end; i++)
            {
                string entry = "" + mVM.mMemory[i];
                while (entry.Length < 4)
                    entry = entry + " ";

                if (i >= 0 && i < mVM.mMemory.Length)
                    memStr = memStr + entry;

                string stackVarName = mDebugger.GetStackSymbol(mVM, i, "", codeLine, codeChar);
                if (stackVarName != "")
                    memStr = memStr + " " + stackVarName;

                memStr = memStr + "\r\n";
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
                    string entry = "" + mVM.mMemory[i];
                    while (entry.Length < 4)
                        entry = entry + " ";

                    memStr = memStr + entry;

                    if (mDebugger != null)
                    {
                        int offset = i - start;
                        string varName = mDebugger.GetSegmentSymbol( mVM, SymbolTable.Kind.GLOBAL, offset, "", 0, 0 );
                        if (varName != "")
                            memStr = memStr + " " + varName;
                    }
                    memStr = memStr + "\r\n";
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
                buttonStrings.Content = "VM Cmds";

                foreach (int key in mVM.mObjects.mObjects["string"].Keys)
                {
                    if ( vmStr == "" )
                        vmStr = vmStr + "Statics:\r\n";

                    string idStr = "" + key;
                    if (key == mVM.mStringsStatic + 1)
                    {
                        vmStr = vmStr + "\r\n";
                        vmStr = vmStr + "Modifiable:\r\n";
                    }
                    while (idStr.Length < 4)
                        idStr = " " + idStr;
                    string lineStr = idStr + " \"" + (string) mVM.mObjects.mObjects["string"][key].mObject + "\"";
                    lineStr = lineStr + "\r\n";
                    vmStr = vmStr + lineStr;
                }

                if (vmStr == "")
                    vmStr = "[No strings in use]";
            }
            else
            {
                labelVM.Content = "VM Commands";
                buttonStrings.Content = "Strings";
                int codeFrame = 0;
                Instructions.Seek(0, SeekOrigin.Begin);
                StreamReader vmreader = new StreamReader(Instructions);
                while (!vmreader.EndOfStream)
                {
                    string lineStr = vmreader.ReadLine();
                    string[] elements = ByteCode.CommandElements(lineStr);

                    if (elements[0] != "label" && codeFrame == mVM.mCodeFrame)
                    {
                        lineStr = ">" + lineStr;
                    }
                    else
                    {
                        lineStr = " " + lineStr;
                    }

                    if (elements[0] != "label")
                        codeFrame++;

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

            // Setup the VM so that it uses fake heap memory for emulated objects like strings or other game objects
            mVM.ResetAll();
            mVM.OptionSet(Emulator.Option.FAKE_HEAP_OBJECTS, true);
            mVM.mObjects.RegisterType("string", 2);

            mVM.Load(byteCode);
            UpdateForm();
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

                if (osName.Contains((string)comboTest.SelectedItem))
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
        }
    }
}