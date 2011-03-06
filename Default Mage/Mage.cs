//Default Mage - Frost - Written by CodenameGamma - 10/21/10 - For WoW 4.0.1 and up
//Special Thanks - Nesox, Apoc, Hawker, Raphus, Pios and Wired.
using System;
using System.Threading;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Tripper.XNAMath.Graphics;
using Action = TreeSharp.Action;


namespace DefaultMage
{
    public partial class DefaultMage : CombatRoutine
    {
        private readonly Version _version = new Version(1, 0, 1);
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public override string Name { get { return "Default Mage " + _version; } }
        public override WoWClass Class { get { return WoWClass.Mage; } }
        public static int RestHealthPercentage = 50;
        public static int RestManaPercentage = 50;
        public bool Use_Wand = true;


        public new double? PullDistance
        {
            get
            {
                if (SpellManager.CanCast("Arcane Missiles") && Me.Auras.ContainsKey("Arcane Missiles!"))
                {
                    return SpellManager.Spells["Arcane Missiles"].MaxRange;
                }
                else
                if (!SpellManager.HasSpell("Frostbolt"))
                {
                    return SpellManager.Spells["Fireball"].MaxRange;
                }
                else
                if (SpellManager.HasSpell("Frostbolt"))
                {
                    return SpellManager.Spells["Frostbolt"].MaxRange;
                }
                else
                

               return 30;
 
            }
        }

     

        public override bool WantButton { get { return true; } }
    
        //private Form _configForm;
        public override void OnButtonPress()
        {
            
        }
      
    }

    public partial class DefaultMage
    {
        private static string _logSpam;
        private static void Log(string format, params object[] args)
        {
            string s = Utilities.FormatString(format, args);

            if (s != _logSpam)
            {
                Logging.Write("[Default Mage]: {0}", string.Format(format, args));
                _logSpam = s;
            }
        }

        private static void Log(string format)
        {
            Log(format, new object());
        }

        #region Behavior Tree Composite Helpers

        public delegate WoWUnit UnitSelectDelegate(object context);
        public Composite CreateBuffCheckAndCast(string name)
        {
            return new Decorator(ret => SpellManager.CanBuff(name),
                                 new Action(ret => SpellManager.Buff(name)));
        }

        public Composite CreateBuffCheckAndCast(string name, UnitSelectDelegate onUnit)
        {
            return new Decorator(ret => SpellManager.CanBuff(name, onUnit(ret)),
                                 new Action(ret => SpellManager.Buff(name, onUnit(ret))));
        }

        public Composite CreateBuffCheckAndCast(string name, CanRunDecoratorDelegate extra)
        {
            return new Decorator(ret => extra(ret) && SpellManager.CanBuff(name),
                                 new Action(ret => SpellManager.Buff(name)));
        }
        public Composite CreateBuffCheckAndCast(string name, bool OnMe)
        {
            return new Decorator(ret => !Me.Auras.ContainsKey(name),
                                  new Action(ret => SpellManager.Buff(name)));
        }
        public Composite CreateBuffCheckAndCast(string name, UnitSelectDelegate onUnit, CanRunDecoratorDelegate extra)
        {
            return CreateBuffCheckAndCast(name, onUnit, extra, false);
        }

        public Composite CreateBuffCheckAndCast(string name, UnitSelectDelegate onUnit, CanRunDecoratorDelegate extra, bool targetLast)
        {
            return new Decorator(ret => extra(ret) && SpellManager.CanBuff(name, onUnit(ret)),
                                 new Action(ret => SpellManager.Buff(name, onUnit(ret))));
        }

        public static Composite CreateSpellCheckAndCast(string name)
        {
            return new Decorator(ret => SpellManager.CanCast(name),
                                 new Action(ret => SpellManager.Cast(name))
                                 );
        }

        public Composite CreateSpellCheckAndCast(string name, WoWUnit onUnit)
        {
            return new Decorator(ret => SpellManager.CanCast(name),
                                 new Action(ret => SpellManager.Cast(name, onUnit)));
        }

        public Composite CreateSpellCheckAndCast(string name, CanRunDecoratorDelegate extra)
        {
            return new Decorator(ret => extra(ret) && SpellManager.CanCast(name),
                                 new Action(ret => SpellManager.Cast(name)));
        }

        public Composite CreateSpellCheckAndCast(string name, bool checkRange)
        {
            return new Decorator(ret => SpellManager.CanCast(name, checkRange),
                                 new Action(ret => SpellManager.Cast(name)));
        }

        public Composite CreateSpellCheckAndCast(string name, CanRunDecoratorDelegate extra, ActionDelegate extraAction)
        {
            return new Decorator(ret => extra(ret) && SpellManager.CanCast(name),
                                 new Action(delegate(object ctx)
                                 {
                                     SpellManager.Cast(name);
                                     extraAction(ctx);
                                     return RunStatus.Success;
                                 }));
        }
        public Composite CreateSpellCheckAndCast(string name, string Buffname)
        {
        
            return new Decorator(ret => SpellManager.CanCast(name),
                                 new Action(delegate
                                 {
                                     SpellManager.Cast(name);
                                     StyxWoW.SleepForLagDuration();
                                     if (Me.CurrentTarget.Auras.ContainsKey(Buffname))
                                     {
                                         SpellManager.StopCasting();
                                     }
                                     return RunStatus.Success;
                                 })
                                 );
        }
        public Composite CreateSpellCheckAndCast(string name, CanRunDecoratorDelegate extra, ActionDelegate extraAction, WoWUnit onUnit)
        {
            return new Decorator(ret => extra(ret) && SpellManager.CanCast(name),
                                 new Action(delegate(object ctx)
                                 {
                                     SpellManager.Cast(name, onUnit);
                                     extraAction(ctx);
                                     return RunStatus.Success;
                                 }));
        }

        public Composite SummonPet(string PetSpellName)
        {

            return new Decorator(ret => SpellManager.CanCast(PetSpellName) && !Me.GotAlivePet,
                                 new Action(delegate
                                 {
                                     SpellManager.Cast(PetSpellName);
                                     StyxWoW.SleepForLagDuration();
                                     if (Me.GotAlivePet)
                                     {
                                         SpellManager.StopCasting();
                                     }
                                     return RunStatus.Success;
                                 })
                                 );
        }

        #endregion
    }
}
