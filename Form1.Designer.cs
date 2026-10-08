#nullable disable
namespace AutoDark;

partial class Form1
{
    private System.ComponentModel.IContainer components;
    private Label titleLabel;
    private Button toggleButton;
    private Label statusLabel;
    private Label nextLabel;
    private Label attentionLabel;
    private LinkLabel locationLink;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
        components = new System.ComponentModel.Container();
        titleLabel = new Label();
        toggleButton = new Button();
        statusLabel = new Label();
        nextLabel = new Label();
        attentionLabel = new Label();
        locationLink = new LinkLabel();
        SuspendLayout();
        titleLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        titleLabel.Location = new Point(24, 20);
        titleLabel.Size = new Size(352, 44);
        titleLabel.Text = "AutoDark";
        toggleButton.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        toggleButton.Location = new Point(24, 84);
        toggleButton.Size = new Size(352, 64);
        toggleButton.Text = "OFF";
        toggleButton.TabIndex = 0;
        toggleButton.UseVisualStyleBackColor = true;
        toggleButton.AccessibleName = "Automatic theme switching ON or OFF";
        toggleButton.Click += ToggleButton_Click;
        statusLabel.Location = new Point(24, 162);
        statusLabel.Size = new Size(352, 26);
        statusLabel.Text = "Automatic switching disabled";
        nextLabel.Location = new Point(24, 195);
        nextLabel.Size = new Size(352, 48);
        nextLabel.Text = "Light at sunrise. Dark at sunset.";
        attentionLabel.Location = new Point(24, 250);
        attentionLabel.Size = new Size(352, 80);
        locationLink.Location = new Point(24, 339);
        locationLink.Size = new Size(352, 26);
        locationLink.Text = "Location";
        locationLink.TabIndex = 1;
        locationLink.LinkClicked += LocationLink_LinkClicked;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(400, 382);
        Controls.Add(titleLabel);
        Controls.Add(toggleButton);
        Controls.Add(statusLabel);
        Controls.Add(nextLabel);
        Controls.Add(attentionLabel);
        Controls.Add(locationLink);
        Font = new Font("Segoe UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Name = "Form1";
        Text = "AutoDark";
        Icon = (Icon)resources.GetObject("$this.Icon");
        ResumeLayout(false);
    }
    #endregion
}
