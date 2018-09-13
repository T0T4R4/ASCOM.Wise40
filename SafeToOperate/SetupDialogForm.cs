using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;
//using ASCOM.Wise40.SafeToOperate;
using ASCOM.Wise40.Boltwood;

namespace ASCOM.Wise40SafeToOperate
{
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SafeToOperateSetupDialogForm : Form
    {
        WiseSafeToOperate wisesafetooperate = WiseSafeToOperate.Instance;

        public SafeToOperateSetupDialogForm()
        {
            InitializeComponent();
            // Initialise current values of user settings from the ASCOM Profile
            
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            bool valid = true;
            Color errorColor = Color.Red;
            int i;

            // Place any validation constraint checks here
            // Update the state variables with results from the dialogue
            i = Convert.ToInt32(textBoxAge.Text);
            if (i < 0)
            {
                textBoxAge.ForeColor = errorColor;
                valid = false;
            } else
            {
                wisesafetooperate.ageMaxSeconds = i;
            }

            i = Convert.ToInt32(textBoxRestoreSafety.Text);
            if (i < 0)
            {
                textBoxRestoreSafety.ForeColor = errorColor;
                valid = false;
            }
            else
            {
                wisesafetooperate._stabilizationPeriod = new TimeSpan(0, Convert.ToInt32(textBoxRestoreSafety.Text), 0);
            }

            wisesafetooperate.cloudsSensor.MaxAsString = textBoxCloudCoverPercent.Text;
            wisesafetooperate.rainSensor.MaxAsString = textBoxRain.Text;

            i = Convert.ToInt32(textBoxHumidity.Text);
            if (i >= 0 && i <= 100)
            {
                wisesafetooperate.humiditySensor.MaxAsString = i.ToString();
            }
            else
            {
                textBoxHumidity.ForeColor = errorColor;
                valid = false;
            }

            i = Convert.ToInt32(textBoxWind.Text);
            if (i >= 0)
            {
                wisesafetooperate.windSensor.MaxAsString = i.ToString();
            }
            else
            {
                textBoxWind.ForeColor = errorColor;
                valid = false;
            }

            double deg = Convert.ToDouble(textBoxSunElevation.Text);
            if (deg > 0 || deg < -20)
            {
                textBoxSunElevation.ForeColor = errorColor;
                valid = false;
            }
            else
                wisesafetooperate.sunSensor.MaxAsString = deg.ToString();

            if (!valid)
                return;

            foreach (Sensor s in wisesafetooperate._sensors)
                s.Stop();

            wisesafetooperate.cloudsSensor.Repeats = Convert.ToInt32(textBoxCloudRepeats.Text);
            wisesafetooperate.cloudsSensor.Interval = Convert.ToInt32(textBoxCloudIntervalSeconds.Text);
            wisesafetooperate.cloudsSensor.Enabled = checkBoxCloud.Checked;

            wisesafetooperate.windSensor.Repeats = Convert.ToInt32(textBoxWindRepeats.Text);
            wisesafetooperate.windSensor.Interval = Convert.ToInt32(textBoxWindIntervalSeconds.Text);
            wisesafetooperate.windSensor.Enabled = checkBoxWind.Checked;

            wisesafetooperate.humiditySensor.Repeats = Convert.ToInt32(textBoxHumidityRepeats.Text);
            wisesafetooperate.humiditySensor.Interval = Convert.ToInt32(textBoxHumidityIntervalSeconds.Text);
            wisesafetooperate.humiditySensor.Enabled = checkBoxHumidity.Checked;

            wisesafetooperate.sunSensor.Repeats = Convert.ToInt32(textBoxSunRepeats.Text);
            wisesafetooperate.sunSensor.Interval = Convert.ToInt32(textBoxSunIntervalSeconds.Text);
            wisesafetooperate.sunSensor.Enabled = checkBoxSun.Checked;

            wisesafetooperate.rainSensor.Repeats = Convert.ToInt32(textBoxRainRepeats.Text);            
            wisesafetooperate.rainSensor.Interval = Convert.ToInt32(textBoxRainIntervalSeconds.Text);            
            wisesafetooperate.rainSensor.Enabled = checkBoxRain.Checked;            
            
            wisesafetooperate.WriteProfile();

            foreach (Sensor s in wisesafetooperate._sensors)
                s.Start();

            Close();
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("http://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            wisesafetooperate.Connected = true;

            textBoxCloudCoverPercent.Tag = textBoxCloudCoverPercent.Text = wisesafetooperate.cloudsSensor.MaxAsString;
            textBoxRain.Tag = textBoxRain.Text = wisesafetooperate.rainSensor.MaxAsString;
            textBoxWind.Tag = textBoxWind.Text = wisesafetooperate.windSensor.MaxAsString;
            textBoxHumidity.Tag = textBoxHumidity.Text = wisesafetooperate.humiditySensor.MaxAsString;
            textBoxSunElevation.Tag = textBoxSunElevation.Text = wisesafetooperate.sunSensor.MaxAsString;

            textBoxAge.Text = wisesafetooperate.ageMaxSeconds.ToString();
            textBoxRestoreSafety.Tag = textBoxRestoreSafety.Text = wisesafetooperate._stabilizationPeriod.Minutes.ToString();

            checkBoxCloud.Tag = checkBoxCloud.Checked = wisesafetooperate.cloudsSensor.Enabled;
            checkBoxHumidity.Tag = checkBoxHumidity.Checked = wisesafetooperate.humiditySensor.Enabled;
            checkBoxRain.Tag = checkBoxRain.Checked = wisesafetooperate.rainSensor.Enabled;
            checkBoxSun.Tag = checkBoxSun.Checked = wisesafetooperate.sunSensor.Enabled;
            checkBoxWind.Tag = checkBoxWind.Checked = wisesafetooperate.windSensor.Enabled;

            textBoxCloudIntervalSeconds.Tag = textBoxCloudIntervalSeconds.Text = wisesafetooperate.cloudsSensor.Interval.ToString();
            textBoxHumidityIntervalSeconds.Tag = textBoxHumidityIntervalSeconds.Text = wisesafetooperate.humiditySensor.Interval.ToString();
            textBoxRainIntervalSeconds.Tag = textBoxRainIntervalSeconds.Text = wisesafetooperate.rainSensor.Interval.ToString();
            textBoxSunIntervalSeconds.Tag = textBoxSunIntervalSeconds.Text = wisesafetooperate.sunSensor.Interval.ToString();
            textBoxWindIntervalSeconds.Tag = textBoxWindIntervalSeconds.Text = wisesafetooperate.windSensor.Interval.ToString();

            textBoxCloudRepeats.Tag = textBoxCloudRepeats.Text = wisesafetooperate.cloudsSensor.Repeats.ToString();
            textBoxHumidityRepeats.Tag = textBoxHumidityRepeats.Text = wisesafetooperate.humiditySensor.Repeats.ToString();
            textBoxRainRepeats.Tag = textBoxRainRepeats.Text = wisesafetooperate.rainSensor.Repeats.ToString();
            textBoxSunRepeats.Tag = textBoxSunRepeats.Text = wisesafetooperate.sunSensor.Repeats.ToString();
            textBoxWindRepeats.Tag = textBoxWindRepeats.Text = wisesafetooperate.windSensor.Repeats.ToString();

            toolTip1.SetToolTip(textBoxHumidity, "0 to 100 (%)");
            toolTip1.SetToolTip(textBoxAge, "0 to use data of any age (sec)");
            toolTip1.SetToolTip(textBoxHumidity, "between 0 and 100 (%)");
            toolTip1.SetToolTip(textBoxWind, "greter than 0 (mps)");
        }

        private void textBoxWind_Validating(object sender, CancelEventArgs e)
        {
            if (int.Parse(((TextBox)sender).Text) < 0)
            {
                ((TextBox)sender).ForeColor = Color.Red;
            } else
            {
                ((TextBox)sender).ForeColor = Color.DarkOrange;
            }
            base.OnTextChanged(e);
        }

        private void textBoxHumidity_Validating(object sender, CancelEventArgs e)
        {
            int percent = int.Parse(((TextBox)sender).Text);
            if (percent < 0 || percent > 100)
            {
                ((TextBox)sender).ForeColor = Color.Red;
            }
            else
            {
                ((TextBox)sender).ForeColor = Color.DarkOrange;
            }
            base.OnTextChanged(e);
        }

        private void textBoxAge_Validating(object sender, CancelEventArgs e)
        {
            if (int.Parse(((TextBox)sender).Text) < 0)
            {
                ((TextBox)sender).ForeColor = Color.Red;
            }
            else
            {
                ((TextBox)sender).ForeColor = Color.DarkOrange;
            }
            base.OnTextChanged(e);
        }
    }
}
