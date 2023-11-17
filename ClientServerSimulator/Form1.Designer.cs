namespace ClientServerSimulator
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.timerServer = new System.Windows.Forms.Timer(this.components);
            this.buttonStartServer = new System.Windows.Forms.Button();
            this.nudServerSpeed = new System.Windows.Forms.NumericUpDown();
            this.buttonStartClient = new System.Windows.Forms.Button();
            this.nudClientSpeed = new System.Windows.Forms.NumericUpDown();
            this.textBoxServer = new System.Windows.Forms.TextBox();
            this.textBoxClient = new System.Windows.Forms.TextBox();
            this.timerClient = new System.Windows.Forms.Timer(this.components);
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.nudServerSpeed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudClientSpeed)).BeginInit();
            this.SuspendLayout();
            // 
            // timerServer
            // 
            this.timerServer.Tick += new System.EventHandler(this.timerServer_Tick);
            // 
            // buttonStartServer
            // 
            this.buttonStartServer.Location = new System.Drawing.Point(12, 21);
            this.buttonStartServer.Name = "buttonStartServer";
            this.buttonStartServer.Size = new System.Drawing.Size(75, 23);
            this.buttonStartServer.TabIndex = 0;
            this.buttonStartServer.Text = "Start Server";
            this.buttonStartServer.UseVisualStyleBackColor = true;
            this.buttonStartServer.Click += new System.EventHandler(this.buttonStartServer_Click);
            // 
            // nudServerSpeed
            // 
            this.nudServerSpeed.Location = new System.Drawing.Point(93, 21);
            this.nudServerSpeed.Maximum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.nudServerSpeed.Name = "nudServerSpeed";
            this.nudServerSpeed.Size = new System.Drawing.Size(120, 23);
            this.nudServerSpeed.TabIndex = 1;
            this.nudServerSpeed.ValueChanged += new System.EventHandler(this.nudServerSpeed_ValueChanged);
            // 
            // buttonStartClient
            // 
            this.buttonStartClient.Location = new System.Drawing.Point(399, 23);
            this.buttonStartClient.Name = "buttonStartClient";
            this.buttonStartClient.Size = new System.Drawing.Size(75, 23);
            this.buttonStartClient.TabIndex = 2;
            this.buttonStartClient.Text = "Start Client";
            this.buttonStartClient.UseVisualStyleBackColor = true;
            this.buttonStartClient.Click += new System.EventHandler(this.buttonStartClient_Click);
            // 
            // nudClientSpeed
            // 
            this.nudClientSpeed.Location = new System.Drawing.Point(480, 23);
            this.nudClientSpeed.Maximum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.nudClientSpeed.Name = "nudClientSpeed";
            this.nudClientSpeed.Size = new System.Drawing.Size(120, 23);
            this.nudClientSpeed.TabIndex = 3;
            this.nudClientSpeed.ValueChanged += new System.EventHandler(this.nudClientSpeed_ValueChanged);
            // 
            // textBoxServer
            // 
            this.textBoxServer.Location = new System.Drawing.Point(12, 50);
            this.textBoxServer.Multiline = true;
            this.textBoxServer.Name = "textBoxServer";
            this.textBoxServer.ReadOnly = true;
            this.textBoxServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxServer.Size = new System.Drawing.Size(365, 373);
            this.textBoxServer.TabIndex = 4;
            // 
            // textBoxClient
            // 
            this.textBoxClient.Location = new System.Drawing.Point(399, 52);
            this.textBoxClient.Multiline = true;
            this.textBoxClient.Name = "textBoxClient";
            this.textBoxClient.ReadOnly = true;
            this.textBoxClient.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxClient.Size = new System.Drawing.Size(324, 371);
            this.textBoxClient.TabIndex = 5;
            // 
            // timerClient
            // 
            this.timerClient.Tick += new System.EventHandler(this.timerClient_Tick);
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.textBoxClient);
            this.Controls.Add(this.textBoxServer);
            this.Controls.Add(this.nudClientSpeed);
            this.Controls.Add(this.buttonStartClient);
            this.Controls.Add(this.nudServerSpeed);
            this.Controls.Add(this.buttonStartServer);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.nudServerSpeed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudClientSpeed)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer timerServer;
        private Button buttonStartServer;
        private NumericUpDown nudServerSpeed;
        private Button buttonStartClient;
        private NumericUpDown nudClientSpeed;
        private TextBox textBoxServer;
        private TextBox textBoxClient;
        private System.Windows.Forms.Timer timerClient;
        private System.Windows.Forms.Timer timer1;
    }
}