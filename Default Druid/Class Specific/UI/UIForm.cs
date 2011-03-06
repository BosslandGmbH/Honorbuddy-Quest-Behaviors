using System;
using System.Windows.Forms;
using Settings = Hera.Config.Settings;

namespace Hera.UI
{
    public partial class UIForm : Form
    {
        public UIForm()
        {
            InitializeComponent();
        }

        private void UIForm_Load(object sender, EventArgs e)
        {
            // Load the settings from the XML file
            Settings.Load();
            this.Text = String.Format("{0} by {1}", Fpsware.CCName.ToUpper(), Fpsware.AuthorName);
            lblCCName.Text = Fpsware.CCName.ToUpper();

            // Populate the controls on the UI with the values from the settings
            
            // Common - Rest and Healing
            DrinkBar.Value = Settings.RestMana;
            ManaPotionBar.Value = Settings.PotionMana;
            InnervateMana.Value = Settings.InnervateMana;
            HealingTouch.SelectedItem = Settings.HealingTouch;
            HealingTouchHealth.Value = Settings.HealingTouchHealth;
            Regrowth.SelectedItem = Settings.Regrowth;
            RegrowthHealth.Value = Settings.RegrowthHealth;
            Cleanse.SelectedItem = Settings.Cleanse;

            // Balance
            PullBalance.SelectedItem = Settings.PullBalance;
            FaerieFireBalance.SelectedItem = Settings.FaerieFireBalance;
            InsectSwarm.SelectedItem = Settings.InsectSwarm;
            Moonfire.SelectedItem = Settings.Moonfire;
            Starsurge.SelectedItem = Settings.Starsurge;
            Trents.SelectedItem = Settings.ForceOfNature;
            PrimaryDPSSpell.SelectedItem = Settings.PrimaryDPSSpell;

            // Feral
            AttackEnergy.Value = Settings.AttackEnergy;
            FaerieFireFeral.SelectedItem = Settings.FaerieFireFeral;
            Rake.SelectedItem = Settings.Rake;
            SkullBash.SelectedItem = Settings.SkullBash;
            TigersFury.SelectedItem = Settings.TigersFury;
            SavageRoar.SelectedItem = Settings.SavageRoar;
            Swipe.SelectedItem = Settings.Swipe;
            Thrash.SelectedItem = Settings.Thrash;
            Rip.SelectedItem = Settings.Rip;
            Maim.SelectedItem = Settings.Maim;
            FerociousBite.SelectedItem = Settings.FerociousBite;
            PullFeral.SelectedItem = Settings.PullFeral;

            // Advanced
            HealPartyMembers.SelectedItem = Settings.HealPartyMembers;
            RAFTarget.SelectedItem = Settings.RAFTarget;
            Debug.SelectedItem = Settings.Debug;
            BearForm.SelectedItem = Settings.BearForm;
            TravelForm.SelectedItem = Settings.TravelForm;


            // Disable spells that you don't yet know
            //SettingControlCheck(Cleanse, "Purify", "... never");
            
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
        public bool SettingControlCheck(ComboBox uiControl,string spellName,string valueIfDisabled)
        {
            bool result = SpellsMan.Spell.IsKnown(spellName);

            if (result) return true;
            uiControl.Enabled = result;
            //uiControl.Items.Add("... Unknown spell setting disabled");
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







        private void DrinkBar_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                DrinkBar.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.RestMana = DrinkBar.Value;
            }
        }

        private void ManaPotionBar_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ManaPotionBar.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.PotionMana = ManaPotionBar.Value;
            }
        }

        private void InnervateMana_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                InnervateMana.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.InnervateMana = InnervateMana.Value;
            }
        }

        private void HealingTouchHealth_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                HealingTouchHealth.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.HealingTouchHealth = HealingTouchHealth.Value;
            }
        }

        private void RegrowthHealth_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                RegrowthHealth.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.RegrowthHealth = RegrowthHealth.Value;
            }
        }

        private void AttackEnergy_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                AttackEnergy.Value = MouPosValue(e.Location.X, DrinkBar.Width);
                Settings.AttackEnergy = AttackEnergy.Value;
            }
        }

        //
        // Balance settings
        //
        private void HealingTouch_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.HealingTouch = HealingTouch.SelectedItem.ToString();
        }

        private void Regrowth_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Regrowth = Regrowth.SelectedItem.ToString();
        }

        private void Cleanse_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Cleanse = Cleanse.SelectedItem.ToString();
        }

        private void FaerieFireBalance_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.FaerieFireBalance = FaerieFireBalance.SelectedItem.ToString();
        }

        private void InsectSwarm_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.InsectSwarm = InsectSwarm.SelectedItem.ToString();
        }

        private void Moonfire_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Moonfire = Moonfire.SelectedItem.ToString();
        }

        private void Starsurge_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Starsurge = Starsurge.SelectedItem.ToString();
        }

        private void PullBalance_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.PullBalance = PullBalance.SelectedItem.ToString();
        }


        //
        // Feral
        //
        private void FaerieFireFeral_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.FaerieFireFeral = FaerieFireFeral.SelectedItem.ToString();
        }

        private void Rake_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Rake = Rake.SelectedItem.ToString();
        }

        private void SkullBash_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.SkullBash = SkullBash.SelectedItem.ToString();
        }

        private void TigersFury_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.TigersFury = TigersFury.SelectedItem.ToString();
        }

        private void SavageRoar_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.SavageRoar = SavageRoar.SelectedItem.ToString();
        }

        private void Swipe_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Swipe = Swipe.SelectedItem.ToString();
        }

        private void Thrash_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Thrash = Thrash.SelectedItem.ToString();
        }

        private void Rip_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Rip = Rip.SelectedItem.ToString();
        }

        private void FerociousBite_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.FerociousBite = FerociousBite.SelectedItem.ToString();
        }

        private void Maim_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Maim = Maim.SelectedItem.ToString();
        }

        private void PullFeral_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.PullFeral = PullFeral.SelectedItem.ToString();
        }

        private void Debug_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Debug = Debug.SelectedItem.ToString();
        }

        private void RAFTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.RAFTarget = RAFTarget.SelectedItem.ToString();
        }

        private void HealPartyMembers_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.HealPartyMembers = HealPartyMembers.SelectedItem.ToString();
        }

        private void TravelForm_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.TravelForm = TravelForm.SelectedItem.ToString();
        }

        private void BearForm_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.BearForm = BearForm.SelectedItem.ToString();
        }

        private void PrimaryDPSSpell_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.PrimaryDPSSpell = PrimaryDPSSpell.SelectedItem.ToString();
        }

        private void Trents_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.ForceOfNature = Trents.SelectedItem.ToString();
        }

    }
}
