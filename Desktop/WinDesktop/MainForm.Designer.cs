namespace WinDesktop
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            this.productNameLabel = new System.Windows.Forms.Label();
            this.copyrightLabel = new System.Windows.Forms.Label();
            this.licenseLinkLabel = new System.Windows.Forms.LinkLabel();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.statusTimer = new System.Windows.Forms.Timer(this.components);
            this.animationTimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // productNameLabel
            // 
            this.productNameLabel.AutoSize = true;
            this.productNameLabel.Location = new System.Drawing.Point(12, 10);
            this.productNameLabel.Name = "productNameLabel";
            this.productNameLabel.Size = new System.Drawing.Size(78, 13);
            this.productNameLabel.TabIndex = 0;
            this.productNameLabel.Text = "[product name]";
            // 
            // copyrightLabel
            // 
            this.copyrightLabel.AutoSize = true;
            this.copyrightLabel.Location = new System.Drawing.Point(12, 35);
            this.copyrightLabel.Name = "copyrightLabel";
            this.copyrightLabel.Size = new System.Drawing.Size(56, 13);
            this.copyrightLabel.TabIndex = 1;
            this.copyrightLabel.Text = "[copyright]";
            // 
            // licenseLinkLabel
            // 
            this.licenseLinkLabel.AutoSize = true;
            this.licenseLinkLabel.Location = new System.Drawing.Point(13, 61);
            this.licenseLinkLabel.Name = "licenseLinkLabel";
            this.licenseLinkLabel.Size = new System.Drawing.Size(65, 13);
            this.licenseLinkLabel.TabIndex = 2;
            this.licenseLinkLabel.TabStop = true;
            this.licenseLinkLabel.Text = "[license link]";
            // 
            // notifyIcon
            // 
            this.notifyIcon.Text = "notifyIcon1";
            this.notifyIcon.Visible = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(323, 93);
            this.Controls.Add(this.licenseLinkLabel);
            this.Controls.Add(this.copyrightLabel);
            this.Controls.Add(this.productNameLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "neonKUBE Desktop";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label productNameLabel;
        private System.Windows.Forms.Label copyrightLabel;
        private System.Windows.Forms.LinkLabel licenseLinkLabel;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.Timer animationTimer;
    }
}