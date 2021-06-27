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

namespace VMEmulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VM mVM = new VM();

        MemoryStream byteCode = new MemoryStream();
        MemoryStream vmcommands = new MemoryStream();
        VMCompiler compiler;

        int mTestCase = -1;
        int mTestCases = 0;

        DispatcherTimer dispatchStep;
        DispatcherTimer dispatchCompile;
        DispatcherTimer dispatchUpdate;

        public MainWindow()
        {
            InitializeComponent();

            TestCaseInit();
            TestCaseSet("Empty");

            Compile();
        }

        private void textCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            TimerCompileSetup();
        }

        private void buttonStep_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();

            TimerStepSetup(-1);
            TimerUpdateSetup(-1);
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
            TimerStepSetup(100);
        }

        private void buttonPlayFast_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();
            TimerStepSetup(15);
        }

        private void buttonPlayFull_Click(object sender, RoutedEventArgs e)
        {
            if (!mVM.Running())
                Reset();
            TimerStepSetup(0, false);
            TimerUpdateSetup(1000);
        }

        public void Reset()
        {
            TimerStepSetup(-1);
            TimerUpdateSetup(-1);
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

        public void TimerStepSetup( int msTickRate = 50, bool updateForm = true )
        {
            if (dispatchStep != null)
            {
                dispatchStep.Stop();
                dispatchStep = null;
                TimerUpdateSetup(0);
            }

            if ( msTickRate >= 0 )
            {
                dispatchStep = new DispatcherTimer();
                if ( updateForm )
                    dispatchStep.Tick += TimerStepTickUpdate;
                else
                    dispatchStep.Tick += TimerStepTick;
                dispatchStep.Interval = new TimeSpan(0, 0, 0, 0, msTickRate);
                dispatchStep.Start();
            }
        }

        void TimerStepTick(object sender, object e)
        {
            if (!mVM.ExecuteStep())
            {
                TimerStepSetup(-1);
                TimerUpdateSetup(-1);
                UpdateForm();
            }
        }

        void TimerStepTickUpdate(object sender, object e)
        {
            TimerStepTick(sender, e);
            UpdateForm();
        }

        void TimerUpdateTick(object sender, object e)
        {
            UpdateForm();
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
            TimerCompileSetup(-1);
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
            TimerStepSetup(-1);
            TimerUpdateSetup(-1);

            byteCode = new MemoryStream();
            vmcommands = new MemoryStream();

            VMWriter writer = new VMWriter(vmcommands);
            Compile(writer);

            VMByteCode convert = new VMByteCode();
            convert.ConvertVMText(vmcommands, byteCode);

            mVM.Reset();
            mVM.Load(byteCode);
            UpdateForm();
        }

        public void UpdateForm()
        {
            UpdateVMCommands();
            UpdateByteCode();
            UpdateSegments();
            UpdateStack();
            UpdateGlobals();
            UpdateHeap();
            UpdateErrors();
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
                if (i == (int)VM.SegPointer.SP)
                    memStr = memStr + " [SP]";
                else if (i == (int)VM.SegPointer.ARG)
                    memStr = memStr + " [ARG]";
                else if (i == (int)VM.SegPointer.GLOBAL)
                    memStr = memStr + " [GLOBAL]";
                else if (i == (int)VM.SegPointer.LOCAL)
                    memStr = memStr + " [LOCAL]";
                else if (i == (int)VM.SegPointer.THIS)
                    memStr = memStr + " [THIS]";
                else if (i == (int)VM.SegPointer.THAT)
                    memStr = memStr + " [THAT]";
                else if (i == (int)VM.SegPointer.TEMP)
                    memStr = memStr + " [TEMP]";
                memStr = memStr + "\r\n";
            }

            if (textSegmentPointers != null )
                textSegmentPointers.Text = memStr;
        }

        public void UpdateStack()
        {
            string memStr = "";
            int start = (int)VM.SegPointer.COUNT;
            int end = mVM.mMemory[(int)VM.SegPointer.SP];
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
            int start = mVM.mMemory[(int)VM.SegPointer.GLOBAL];
            int end = start + mVM.mGlobalDwords;
            for (int i = start; i < end; i++)
            {
                if ( i >= 0 && i < mVM.mMemory.Length )
                    memStr = memStr + mVM.mMemory[i] + "\r\n";
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

        public void UpdateVMCommands()
        {
            string vmStr = "";
            int codeFrame = 0;
            vmcommands.Seek(0, SeekOrigin.Begin);
            StreamReader vmreader = new StreamReader(vmcommands);
            while (!vmreader.EndOfStream)
            {
                string lineStr = vmreader.ReadLine();
                string[] elements = VMByteCode.CommandElements(lineStr);

                if (elements[0] != "label" && codeFrame == mVM.mCodeFrame)
                    lineStr = "> " + lineStr;
                else
                    lineStr = "  " + lineStr;

                if (elements[0] != "label")
                    codeFrame++;

                lineStr = lineStr + "\r\n";

                vmStr = vmStr + lineStr;
            }
            if (textVM != null)
                textVM.Text = vmStr;
        }

        public void UpdateByteCode()
        {
            string byteStr = "";
            byteCode.Seek(0, SeekOrigin.Begin);
            int i = 0;
            while (byteCode.Position < byteCode.Length)
            {
                int command = 0;
                VMStream.Read(byteCode, out command);
                string hexStr = Convert.ToString(command, 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                hexStr = "0x" + hexStr.ToUpper();
                if (i == mVM.mCodeFrame)
                    hexStr = "> " + hexStr;
                else
                    hexStr = "  " + hexStr;
                byteStr = byteStr + hexStr + "\r\n";
                i = i + 1;
            }
            if (textVMByteCode != null)
                textVMByteCode.Text = byteStr;

            byteCode.Seek(0, SeekOrigin.Begin);
        }

        public void UpdateErrors()
        {
            string errors = "";
            bool vmErrors = false;
            for (int i = 0; i < compiler.mErrors.Count; i++)
            {
                errors = errors + compiler.mErrors[i] + "\r\n";
            }
            for (int i = 0; i < mVM.mErrors.Count; i++)
            {
                vmErrors = true;
                errors = errors + mVM.mErrors[i] + "\r\n";
            }

            if ( mVM.Halted() )
                errors = errors + "PROGRAM HALTED\r\n";
            else if (mVM.Finished())
                errors = errors + "PROGRAM ENDED\r\n";

            if (textErrors != null)
                textErrors.Text = errors;
        }

        public void Compile(IVMWriter writer)
        {
            VMCompiler.ResetAll();

            string code = textCode.Text;

            byte[] byteArray = Encoding.ASCII.GetBytes(code);
            MemoryStream stream = new MemoryStream(byteArray);
            StreamReader reader = new StreamReader(stream);

            // Read all tokens into memory
            VMTokenizer tokenizer = new VMTokenizer(reader);
            tokenizer.ReadAll();

            compiler = new VMCompiler(tokenizer, writer);
            compiler.Compile();
        }
    }
}