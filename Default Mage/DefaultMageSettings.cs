using System.IO;
using Styx;
using Styx.Helpers;

namespace DefaultMage
{
    public class DefaultMageSettings : Settings
    {
        public static readonly DefaultMageSettings Instance = new DefaultMageSettings();

        public DefaultMageSettings()
            : base(Path.Combine(Logging.ApplicationPath, string.Format(@"CustomClasses/Config/DefaultMage-Settings-{0}.xml", StyxWoW.Me.Name)))
        {
        }

        #region Rest

        [Setting, DefaultValue(40)]
        public int RestHealthPercentage { get; set; }

        [Setting, DefaultValue(40)]
        public int RestManaPercentage { get; set; }

        [Setting, DefaultValue(0)]
        public int ArmorSelect { get; set; }

        [Setting, DefaultValue(true)]
        public bool DefaultMageMagic { get; set; }

        [Setting, DefaultValue(false)]
        public bool DampenMagic { get; set; }

        [Setting, DefaultValue(false)]
        public bool FocusMagicPet { get; set; }

        [Setting, DefaultValue(false)]
        public bool Use_Wand { get; set; }



        #region Spells

        [Setting, DefaultValue(true)]
        public bool Use_FireBlast { get; set; }

        [Setting, DefaultValue(30)]
        public int FireBlast_Hp_Percent { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_Fireball_Low { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_Frostbolt_Low { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_ArcaneMissles_Low { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_FrostNova_Low { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_CounterSpell { get; set; }
        #endregion


        #region Spells

        [Setting, DefaultValue(true)]
        public bool Use_ManaShield { get; set; }

        [Setting, DefaultValue(30)]
        public int ManaShield_Hp_Percent { get; set; }

        [Setting, DefaultValue(true)]
        public bool Pull_Fireball { get; set; }

        [Setting, DefaultValue(true)]
        public bool Pull_Frostbolt { get; set; }

        [Setting, DefaultValue(true)]
        public bool Pull_ArcaneMissles { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_FrostNova { get; set; }

        [Setting, DefaultValue(true)] 
        public bool Use_Polymorph { get; set; }

        [Setting, DefaultValue(30)]
        public int Evocation_MP_Percent { get; set; }

        [Setting, DefaultValue(true)]
        public bool Use_ManaGems{ get; set; }

        [Setting, DefaultValue(30)]
        public int ManaGems_MP_Percent { get; set; }

        #endregion
        #endregion


    }
}
