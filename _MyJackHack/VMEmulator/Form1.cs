using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VMEmulator
{
    public partial class Form1 : Form
    {
        VM mVM = new VM();

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
            UpdateMemory();
        }

        public void Compile()
        {
            MemoryStream vmcommands = new MemoryStream();
            VMWriter writer = new VMWriter(vmcommands);
            Compile(writer);

            string vmStr = "";
            vmcommands.Seek(0, SeekOrigin.Begin);
            StreamReader vmreader = new StreamReader(vmcommands);
            while (!vmreader.EndOfStream)
            {
                vmStr = vmStr + vmreader.ReadLine() + "\r\n";
            }
            textVM.Text = vmStr;

            string byteStr = "";
            VMByteConvert convert = new VMByteConvert();
            MemoryStream byteCode = new MemoryStream();
            convert.ConvertVMToByteCode(vmcommands, byteCode);
            byteCode.Seek(0, SeekOrigin.Begin);
            while (byteCode.Position < byteCode.Length)
            {
                int command = 0;
                StreamExtensions.Read(byteCode, out command);
                string hexStr = Convert.ToString(command, 16);
                while (hexStr.Length < 8)
                    hexStr = "0" + hexStr;
                byteStr = byteStr + "0x" + hexStr.ToUpper() + "\r\n";
            }
            textByteCode.Text = byteStr;

            byteCode.Seek(0, SeekOrigin.Begin);

            mVM.Reset();
            mVM.Load(byteCode);
            UpdateMemory();
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
                if (i == mVM.mMemory[0])
                    memStr = memStr + " < SP";
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
