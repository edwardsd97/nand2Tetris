using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VMEmulator
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();

            Compile();
        }

        private void textCode_TextChanged(object sender, EventArgs e)
        {
            Compile();
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
            compiler.CompilePrePass("", 0);
            compiler.Reset();
            compiler.CompilePrePass("", 1);
            compiler.SetWriter(writer);
            compiler.Reset();
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
