
namespace VMEmulator
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textCode = new System.Windows.Forms.TextBox();
            this.textCompile = new System.Windows.Forms.TextBox();
            this.textVM = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // textCode
            // 
            this.textCode.AcceptsReturn = true;
            this.textCode.AcceptsTab = true;
            this.textCode.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textCode.HideSelection = false;
            this.textCode.Location = new System.Drawing.Point(12, 12);
            this.textCode.Multiline = true;
            this.textCode.Name = "textCode";
            this.textCode.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textCode.Size = new System.Drawing.Size(441, 492);
            this.textCode.TabIndex = 0;
            this.textCode.Text = "class Main\r\n{\r\n   function void main()\r\n   {\r\n      int x = 5;\r\n      x = x + 2;\r" +
    "\n   }\r\n}";
            this.textCode.TextChanged += new System.EventHandler(this.textCode_TextChanged);
            // 
            // textCompile
            // 
            this.textCompile.AcceptsReturn = true;
            this.textCompile.AcceptsTab = true;
            this.textCompile.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textCompile.Location = new System.Drawing.Point(12, 510);
            this.textCompile.Multiline = true;
            this.textCompile.Name = "textCompile";
            this.textCompile.ReadOnly = true;
            this.textCompile.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textCompile.Size = new System.Drawing.Size(893, 74);
            this.textCompile.TabIndex = 1;
            this.textCompile.Text = "Compile Errors....";
            // 
            // textVM
            // 
            this.textVM.AcceptsReturn = true;
            this.textVM.AcceptsTab = true;
            this.textVM.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textVM.Location = new System.Drawing.Point(459, 12);
            this.textVM.Multiline = true;
            this.textVM.Name = "textVM";
            this.textVM.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textVM.Size = new System.Drawing.Size(446, 492);
            this.textVM.TabIndex = 2;
            this.textVM.Text = "VM commands\r\n";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(917, 596);
            this.Controls.Add(this.textVM);
            this.Controls.Add(this.textCompile);
            this.Controls.Add(this.textCode);
            this.Name = "Form1";
            this.Text = "VMEmulator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textCode;
        private System.Windows.Forms.TextBox textCompile;
        private System.Windows.Forms.TextBox textVM;
    }
}

