
namespace CyberArkAuthApp
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.txtTenant = new System.Windows.Forms.TextBox();
            this.txtPamTenant = new System.Windows.Forms.TextBox();
            this.txtEmail = new System.Windows.Forms.TextBox();
            this.cmbMode = new System.Windows.Forms.ComboBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.chkUseToken = new System.Windows.Forms.CheckBox();
            this.chkUseCookies = new System.Windows.Forms.CheckBox();
            this.txtLogs = new System.Windows.Forms.TextBox();
            this.dgvAccounts = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAccounts)).BeginInit();
            this.SuspendLayout();

            // label1 - ID Tenant
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 15);
            this.label1.Text = "ID Tenant";
            // txtTenant
            this.txtTenant.Location = new System.Drawing.Point(82, 12);
            this.txtTenant.Name = "txtTenant";
            this.txtTenant.Size = new System.Drawing.Size(90, 23);
            this.txtTenant.Text = "ace4189";

            // label4 - PAM Tenant
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(182, 15);
            this.label4.Text = "PAM Tenant";
            // txtPamTenant
            this.txtPamTenant.Location = new System.Drawing.Point(260, 12);
            this.txtPamTenant.Name = "txtPamTenant";
            this.txtPamTenant.Size = new System.Drawing.Size(100, 23);
            this.txtPamTenant.Text = "rocketsoftware";

            // label2 - Email
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(370, 15);
            this.label2.Text = "Email";
            // txtEmail
            this.txtEmail.Location = new System.Drawing.Point(415, 12);
            this.txtEmail.Name = "txtEmail";
            this.txtEmail.Size = new System.Drawing.Size(140, 23);
            this.txtEmail.Text = "testuser@rocketpowersystems.onmicrosoft.com";

            // label3 - Mode
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(565, 15);
            this.label3.Text = "Mode";
            // cmbMode
            this.cmbMode.FormattingEnabled = true;
            this.cmbMode.Location = new System.Drawing.Point(605, 12);
            this.cmbMode.Name = "cmbMode";
            this.cmbMode.Size = new System.Drawing.Size(65, 23);

            // Toggles
            this.chkUseToken.AutoSize = true;
            this.chkUseToken.Location = new System.Drawing.Point(12, 45);
            this.chkUseToken.Text = "Use Token";
            this.chkUseToken.Checked = true;

            this.chkUseCookies.AutoSize = true;
            this.chkUseCookies.Location = new System.Drawing.Point(100, 45);
            this.chkUseCookies.Text = "Use Cookies";
            this.chkUseCookies.Checked = false;

            // btnStart
            this.btnStart.Location = new System.Drawing.Point(680, 11);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(108, 26);
            this.btnStart.Text = "Connect";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);

            // txtLogs
            this.txtLogs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogs.Location = new System.Drawing.Point(12, 80);
            this.txtLogs.Multiline = true;
            this.txtLogs.Name = "txtLogs";
            this.txtLogs.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLogs.Size = new System.Drawing.Size(776, 280);

            // dgvAccounts
            this.dgvAccounts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvAccounts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAccounts.Location = new System.Drawing.Point(12, 380);
            this.dgvAccounts.Name = "dgvAccounts";
            this.dgvAccounts.Size = new System.Drawing.Size(776, 160);

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 560);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtTenant);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtPamTenant);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtEmail);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cmbMode);
            this.Controls.Add(this.chkUseToken);
            this.Controls.Add(this.chkUseCookies);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.txtLogs);
            this.Controls.Add(this.dgvAccounts);
            this.Name = "MainForm";
            this.Text = "CyberArk Authentication App";
            ((System.ComponentModel.ISupportInitialize)(this.dgvAccounts)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox txtTenant;
        private System.Windows.Forms.TextBox txtPamTenant;
        private System.Windows.Forms.TextBox txtEmail;
        private System.Windows.Forms.ComboBox cmbMode;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.CheckBox chkUseToken;
        private System.Windows.Forms.CheckBox chkUseCookies;
        private System.Windows.Forms.TextBox txtLogs;
        private System.Windows.Forms.DataGridView dgvAccounts;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
    }
}
