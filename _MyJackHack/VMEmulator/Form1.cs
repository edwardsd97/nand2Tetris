using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VMEmulator
{
    public partial class Form1 : Form
    {
        VM mVM = new VM();

        MemoryStream byteCode = new MemoryStream();
        MemoryStream vmcommands = new MemoryStream();

        public Form1()
        {
            InitializeComponent();

            Compile();
        }

        private void textCode_TextChanged(object sender, EventArgs e)
        {
            Compile();
        }

        private void buttonStep_Click(object sender, EventArgs e)
        {
            mVM.ExecuteStep();
            UpdateForm();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            mVM.Reset();
            UpdateForm();
        }

        public void Compile()
        {
            vmcommands.Seek(0, SeekOrigin.Begin);

            VMWriter writer = new VMWriter(vmcommands);
            Compile(writer);

            VMByteConvert convert = new VMByteConvert();
            convert.ConvertVMToByteCode(vmcommands, byteCode);

            mVM.Reset();
            mVM.Load(byteCode);
            UpdateForm();
        }

        public void UpdateForm()
        {
            UpdateVMCommands();
            UpdateMemory();
            UpdateByteCode();
        }

        public void UpdateMemory()
        {
            string memStr = "";
            for (int i = 0; i < mVM.mMemoryDwords && i < 20; i++)
            {
                string hexStr = Convert.ToString(mVM.mMemory[i], 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                memStr = memStr + "0x" + hexStr.ToUpper();
                if (i == mVM.mMemory[(int)VM.SegPointer.SP])
                    memStr = memStr + " < SP";
                else if (i == (int)VM.SegPointer.SP)
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

            memStr = memStr + "Globals:\r\n";

            for (int i = 0; i < mVM.mMemoryDwords && i < 8; i++)
            {
                string hexStr = Convert.ToString(mVM.mMemory[mVM.mMemory[(int)VM.SegPointer.GLOBAL]+i], 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                memStr = memStr + "0x" + hexStr.ToUpper() + "\r\n";
            }

            textMemory.Text = memStr;
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
                string[] elements = VMByteConvert.CommandElements(lineStr);

                if (elements[0] != "label" && codeFrame == mVM.mCodeFrame)
                    lineStr = lineStr + " <-";

                if (elements[0] != "label")
                    codeFrame++;

                lineStr = lineStr + "\r\n";

                vmStr = vmStr + lineStr;
            }
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
                StreamExtensions.Read(byteCode, out command);
                string hexStr = Convert.ToString(command, 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                byteStr = byteStr + "0x" + hexStr.ToUpper();
                if (i == mVM.mCodeFrame)
                    byteStr = byteStr + " <-";
                byteStr = byteStr + "\r\n";
                i = i + 1;
            }
            textByteCode.Text = byteStr;

            byteCode.Seek(0, SeekOrigin.Begin);
        }

        public void Compile( IVMWriter writer )
        {
            Compiler.ResetAll();

            string code = textCode.Text;

            Tokenizer tokenizer;
            byte[] byteArray = Encoding.ASCII.GetBytes(code);
            MemoryStream stream = new MemoryStream(byteArray);
            StreamReader reader = new StreamReader(stream);

            // Read all tokens into memory
            tokenizer = new Tokenizer(reader);
            tokenizer.ReadAll();

            Compiler compiler = new Compiler(tokenizer);
            compiler.SetWriter(writer);
            compiler.Compile();

            string errors = "";
            for (int i = 0; i < compiler.mErrors.Count; i++)
            {
                errors = errors + compiler.mErrors[i] + "\r\n";
            }
            textCompile.Text = errors;
        }
    }
}
