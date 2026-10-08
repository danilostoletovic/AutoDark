#nullable disable
namespace AutoDark;

partial class LocationForm
{
    private System.ComponentModel.IContainer components;
    private CheckBox automaticBox;
    private NumericUpDown latitudeInput;
    private NumericUpDown longitudeInput;
    private Label latitudeLabel;
    private Label longitudeLabel;
    private Label helpLabel;
    private Button saveButton;
    private Button cancelButton;
    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        automaticBox = new CheckBox();
        latitudeInput = new NumericUpDown();
        longitudeInput = new NumericUpDown();
        latitudeLabel = new Label();
        longitudeLabel = new Label();
        helpLabel = new Label();
        saveButton = new Button();
        cancelButton = new Button();
        ((System.ComponentModel.ISupportInitialize)latitudeInput).BeginInit();
        ((System.ComponentModel.ISupportInitialize)longitudeInput).BeginInit();
        SuspendLayout();
        automaticBox.Location = new Point(20, 20);
        automaticBox.Size = new Size(340, 28);
        automaticBox.Text = "Use Windows location (with your permission)";
        automaticBox.CheckedChanged += AutomaticBox_CheckedChanged;
        latitudeLabel.Location = new Point(20, 68);
        latitudeLabel.Size = new Size(110, 28);
        latitudeLabel.Text = "Latitude";
        latitudeInput.Location = new Point(140, 65);
        latitudeInput.Size = new Size(210, 28);
        latitudeInput.Minimum = -90;
        latitudeInput.Maximum = 90;
        latitudeInput.DecimalPlaces = 4;
        latitudeInput.Increment = 0.01M;
        latitudeInput.AccessibleName = "Latitude in degrees, positive north";
        longitudeLabel.Location = new Point(20, 108);
        longitudeLabel.Size = new Size(110, 28);
        longitudeLabel.Text = "Longitude";
        longitudeInput.Location = new Point(140, 105);
        longitudeInput.Size = new Size(210, 28);
        longitudeInput.Minimum = -180;
        longitudeInput.Maximum = 180;
        longitudeInput.DecimalPlaces = 4;
        longitudeInput.Increment = 0.01M;
        longitudeInput.AccessibleName = "Longitude in degrees, positive east";
        helpLabel.Location = new Point(20, 150);
        helpLabel.Size = new Size(330, 75);
        helpLabel.Text = "Manual coordinates stay on this PC. North and east are positive; south and west are negative. Update them when you travel.";
        saveButton.Location = new Point(160, 239);
        saveButton.Size = new Size(90, 34);
        saveButton.Text = "Save";
        saveButton.Click += SaveButton_Click;
        cancelButton.Location = new Point(260, 239);
        cancelButton.Size = new Size(90, 34);
        cancelButton.Text = "Cancel";
        cancelButton.DialogResult = DialogResult.Cancel;
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(370, 293);
        Font = new Font("Segoe UI", 9F);
        Controls.Add(automaticBox);
        Controls.Add(latitudeLabel);
        Controls.Add(latitudeInput);
        Controls.Add(longitudeLabel);
        Controls.Add(longitudeInput);
        Controls.Add(helpLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "AutoDark · Location";
        ((System.ComponentModel.ISupportInitialize)latitudeInput).EndInit();
        ((System.ComponentModel.ISupportInitialize)longitudeInput).EndInit();
        ResumeLayout(false);
    }
}


