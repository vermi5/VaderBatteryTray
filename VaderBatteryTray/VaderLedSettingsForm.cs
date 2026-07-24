using System;
using System.Drawing;
using System.Windows.Forms;

namespace VaderBatteryTray
{
    internal sealed class VaderLedSettingsForm : Form
    {
        private readonly TrackBar brightnessTrackBar;
        private readonly Label valueLabel;
        private readonly Action<byte> previewAction;

        public VaderLedSettingsForm(int currentBrightness, Action<byte> previewAction)
        {
            this.previewAction = previewAction;
            Text = "Controller lighting";
            ClientSize = new Size(360, 155);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;

            Label promptLabel = new Label();
            promptLabel.AutoSize = true;
            promptLabel.Location = new Point(16, 16);
            promptLabel.Text = "Brightness";

            valueLabel = new Label();
            valueLabel.AutoSize = false;
            valueLabel.Location = new Point(286, 16);
            valueLabel.Size = new Size(58, 18);
            valueLabel.TextAlign = ContentAlignment.TopRight;

            brightnessTrackBar = new TrackBar();
            brightnessTrackBar.Location = new Point(12, 40);
            brightnessTrackBar.Size = new Size(336, 45);
            brightnessTrackBar.Minimum = 0;
            brightnessTrackBar.Maximum = 100;
            brightnessTrackBar.TickFrequency = 10;
            brightnessTrackBar.SmallChange = 1;
            brightnessTrackBar.LargeChange = 10;
            brightnessTrackBar.Value = Math.Max(0, Math.Min(100, currentBrightness));
            brightnessTrackBar.ValueChanged += delegate
            {
                UpdateValueLabel();
            };
            brightnessTrackBar.MouseUp += delegate { PreviewBrightness(); };
            brightnessTrackBar.KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (IsBrightnessAdjustmentKey(e.KeyCode))
                {
                    PreviewBrightness();
                }
            };
            Label explanationLabel = new Label();
            explanationLabel.AutoSize = true;
            explanationLabel.Location = new Point(16, 86);
            explanationLabel.Text = "Preview is sent when the slider is released.";

            Button saveButton = new Button();
            saveButton.Location = new Point(188, 116);
            saveButton.Size = new Size(75, 27);
            saveButton.Text = "Save";
            saveButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Location = new Point(269, 116);
            cancelButton.Size = new Size(75, 27);
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(promptLabel);
            Controls.Add(valueLabel);
            Controls.Add(brightnessTrackBar);
            Controls.Add(explanationLabel);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            UpdateValueLabel();
        }

        public byte BrightnessPercent
        {
            get { return (byte)brightnessTrackBar.Value; }
        }

        private void UpdateValueLabel()
        {
            valueLabel.Text = brightnessTrackBar.Value.ToString() + "%";
        }

        private void PreviewBrightness()
        {
            if (previewAction != null)
            {
                previewAction(BrightnessPercent);
            }
        }

        private static bool IsBrightnessAdjustmentKey(Keys keyCode)
        {
            return keyCode == Keys.Left ||
                keyCode == Keys.Right ||
                keyCode == Keys.Up ||
                keyCode == Keys.Down ||
                keyCode == Keys.PageUp ||
                keyCode == Keys.PageDown ||
                keyCode == Keys.Home ||
                keyCode == Keys.End;
        }
    }
}
