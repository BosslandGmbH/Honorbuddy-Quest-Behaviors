using System;
using System.Reflection;
using System.Windows.Forms;
using Settings = Hera.Config.Settings;

namespace Hera.UI
{
    public partial class UIForm : Form
    {
        private bool _isLoading = true;
        private bool _dirtyData;
        private string _tempEnvironment = "";

        public UIForm()
        {
            InitializeComponent();
        }

        private void UIForm_Load(object sender, EventArgs e)
        {
            this.Text = String.Format("{0} by {1}", Fpsware.CCName.ToUpper(), Fpsware.AuthorName);
            lblCCName.Text = Fpsware.CCName.ToUpper();
            LoadSettings();

            // Disable some controls due to unknown spells
            if (!SpellsMan.Spell.IsKnown("Lifeblood")) { LifebloodHealth.Enabled = false; LifebloodHealth.Value = 0; lifebloodLabel.Enabled = false; }
        }

        public void LoadSettings()
        {
            // Load the settings from the XML file
            if (_tempEnvironment != "") Settings.Environment = _tempEnvironment;
            Settings.LoadEnvironment();
            Settings.Load();

            if (_tempEnvironment == "") Environment.SelectedItem = Settings.Environment;
            EnvironmentLoading.SelectedItem = Settings.EnvironmentLoading;

            _isLoading = true;

            // Populate the controls on the UI with the values from the settings
            // Through the wonders of Reflection this is an automated process
            Type type = typeof(Settings);
            PropertyInfo[] props = type.GetProperties();
            foreach (var p in props)
            {
                if (p.Name == "DirtyData") continue;
                if (p.Name == "Environment") continue;
                if (p.Name == "EnvironmentLoading") continue;

                PropertyInfo pInfo = type.GetProperty(p.Name);
                object propValue = pInfo.GetValue(p.Name, null);

                foreach (TabPage tab in tabControl1.Controls)
                {
                    foreach (GroupBox gb in tab.Controls)
                    {
                        foreach (object obj in gb.Controls)
                        {
                            // Populate all combo boxes, this will be the most common
                            if (obj is ComboBox)
                            {
                                ComboBox cbox = (ComboBox)obj;
                                if (cbox.Name == p.Name)
                                {
                                    cbox.SelectedItem = propValue.ToString();
                                    break;
                                }
                            }

                                // Populate all progress bars, mostly for health or mana 
                            else if (obj is ProgressBar)
                            {
                                ProgressBar pbar = (ProgressBar)obj;
                                if (pbar.Name == p.Name)
                                {
                                    pbar.Value = (int)propValue;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            _isLoading = false;
        }

        private void SaveSettings_Click(object sender, EventArgs e)
        {
            // Save the settings to the XML file
            Settings.Save();

            // DirtyData tells the CC the data has possibly changed and it needs to reload it
            Settings.DirtyData = true;
            Close();
        }


        /// <summary>
        /// Changed the enabled/disabled status of a combobox dependant on the associated spell being known
        /// </summary>
        /// <param name="uiControl"></param>
        /// <param name="spellName">Spell name to check</param>
        /// <param name="valueIfDisabled">Default selected item if the setting is disabled</param>
        /// <returns>TRUE is the spell is know and the control is usable. FALSE if the spell is not known and the control is disabled.</returns>
        public bool SettingControlCheck(ComboBox uiControl, string spellName, string valueIfDisabled)
        {
            bool result = SpellsMan.Spell.IsKnown(spellName);

            if (result) return true;
            uiControl.Enabled = result;
            uiControl.SelectedItem = valueIfDisabled;

            return false;
        }


        // Neat little function to use a Progress bar like a slider control by simply clicking on it or dragging
        // REALLY NEAT! Took 3 seconds to come up with the idea and 30 minutes to figure out how to get it to work
        public int MouPosValue(double mousePosition, double progressBarWidth)
        {
            if (mousePosition < 0) mousePosition = 0;
            if (mousePosition > progressBarWidth) mousePosition = progressBarWidth;

            double ratio = mousePosition / progressBarWidth;
            double value = ratio * 100;

            if (value > 100) value = 100;
            if (value < 0) value = 0;

            return (int)Math.Ceiling(value);
        }

        private void CommonProgressBar_MouseAction(object sender, MouseEventArgs e)
        {
            if (_isLoading) return;

            if (e.Button == MouseButtons.Left)
            {
                ProgressBar pbar = (ProgressBar)sender;
                string pbarName = pbar.Name;
                int value;

                pbar.Value = MouPosValue(e.Location.X, pbar.Width);
                value = pbar.Value;

                Type type = typeof(Settings);
                PropertyInfo[] props = type.GetProperties();
                foreach (var p in props)
                {
                    if (p.Name != pbarName) continue;

                    p.SetValue(type, value, null);
                    _dirtyData = true;
                    break;
                }
            }

        }

        private void CommonDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            ComboBox cbox = (ComboBox)sender;
            string cboxName = cbox.Name;
            string value = cbox.SelectedItem.ToString();

            Type type = typeof(Settings); PropertyInfo[] props = type.GetProperties();
            foreach (var p in props)
            {
                if (p.Name != cboxName) continue;

                p.SetValue(type, value, null);
                _dirtyData = true;
                break;
            }
        }

        private void Environment_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (_dirtyData)
            {
                DialogResult manswer = MessageBox.Show("Some settings have been changed, do you want to save your changes?", "Save changes", MessageBoxButtons.YesNo);
                if (manswer == DialogResult.Yes)
                {
                    Settings.Save();
                }
                _dirtyData = false;
            }


            _tempEnvironment = Environment.SelectedItem.ToString();
            Settings.Environment = _tempEnvironment;
            Settings.SaveEnvironment();
            LoadSettings();
        }

        private void EnvironmentLoading_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            Settings.EnvironmentLoading = EnvironmentLoading.SelectedItem.ToString();
            Settings.SaveEnvironment();
        }


    }
}
