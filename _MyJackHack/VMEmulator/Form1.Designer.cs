
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.textCode = new System.Windows.Forms.TextBox();
            this.textCompile = new System.Windows.Forms.TextBox();
            this.textVM = new System.Windows.Forms.TextBox();
            this.textByteCode = new System.Windows.Forms.TextBox();
            this.textMemory = new System.Windows.Forms.TextBox();
            this.buttonStep = new System.Windows.Forms.Button();
            this.buttonReset = new System.Windows.Forms.Button();
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
            this.textCode.Text = resources.GetString("textCode.Text");
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
            this.textCompile.Size = new System.Drawing.Size(1043, 74);
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
            // textByteCode
            // 
            this.textByteCode.AcceptsReturn = true;
            this.textByteCode.AcceptsTab = true;
            this.textByteCode.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textByteCode.Location = new System.Drawing.Point(911, 12);
            this.textByteCode.Multiline = true;
            this.textByteCode.Name = "textByteCode";
            this.textByteCode.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textByteCode.Size = new System.Drawing.Size(227, 492);
            this.textByteCode.TabIndex = 3;
            this.textByteCode.Text = "Byte code";
            // 
            // textMemory
            // 
            this.textMemory.AcceptsReturn = true;
            this.textMemory.AcceptsTab = true;
            this.textMemory.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textMemory.Location = new System.Drawing.Point(1144, 12);
            this.textMemory.Multiline = true;
            this.textMemory.Name = "textMemory";
            this.textMemory.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textMemory.Size = new System.Drawing.Size(227, 572);
            this.textMemory.TabIndex = 4;
            this.textMemory.Text = "Memory";
            // 
            // buttonStep
            // 
            this.buttonStep.Location = new System.Drawing.Point(1063, 511);
            this.buttonStep.Name = "buttonStep";
            this.buttonStep.Size = new System.Drawing.Size(75, 23);
            this.buttonStep.TabIndex = 5;
            this.buttonStep.Text = "Step";
            this.buttonStep.UseVisualStyleBackColor = true;
            this.buttonStep.Click += new System.EventHandler(this.buttonStep_Click);
            // 
            // buttonReset
            // 
            this.buttonReset.Location = new System.Drawing.Point(1063, 540);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(75, 23);
            this.buttonReset.TabIndex = 6;
            this.buttonReset.Text = "Reset";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1383, 596);
            this.Controls.Add(this.buttonReset);
            this.Controls.Add(this.buttonStep);
            this.Controls.Add(this.textMemory);
            this.Controls.Add(this.textByteCode);
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
        private System.Windows.Forms.TextBox textByteCode;
        private System.Windows.Forms.TextBox textMemory;
        private System.Windows.Forms.Button buttonStep;
        private System.Windows.Forms.Button buttonReset;
    }
}

