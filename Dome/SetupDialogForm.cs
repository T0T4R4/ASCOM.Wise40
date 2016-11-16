using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;

using ASCOM.Wise40.Common;

namespace ASCOM.Wise40
{
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        private WiseDome wisedome = WiseDome.Instance;

        public SetupDialogForm()
        {
            InitializeComponent();

            wisedome.ReadProfile();
            checkBoxAutoCalibrate.Checked = wisedome._autoCalibrate;
            checkBoxBypassSafety.Checked = wisedome._bypassSafety;
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            wisedome.WriteProfile();
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

        private void checkBoxAutoCalibrate_CheckedChanged(object sender, EventArgs e)
        {
            wisedome._autoCalibrate = (sender as CheckBox).Checked;
        }

        private void checkBoxBypassSafety_CheckedChanged(object sender, EventArgs e)
        {
            wisedome._bypassSafety = (sender as CheckBox).Checked;
        }
    }
}