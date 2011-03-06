using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Styx;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = System.Action;
using Sequence = Styx.Logic.Sequence;


namespace DefaultMage
{
    public partial class DefaultMage
    {
      
            public static bool GotSheep
            {
                get
                {
                    List<WoWUnit> sheepList =
                        (from o in ObjectManager.ObjectList
                         where o is WoWUnit
                         let p = o.ToUnit()
                         where p.Distance < 60
                               && p.HasAura("Polymorph")
                         select p).ToList();

                    return sheepList.Count > 0;
                }
            }



            public static bool NeedTocastSheep()
            {
                List<WoWUnit> AddList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
      unit.Guid != Me.Guid &&
      unit.IsTargetingMeOrPet &&
      !unit.IsFriendly &&
      !unit.Elite &&
      !unit.InLineOfSight &&
      !unit.IsPet &&
      (unit.CreatureType == WoWCreatureType.Humanoid || unit.CreatureType == WoWCreatureType.Beast) &&
      unit != Me.CurrentTarget &&
      !Styx.Logic.Blacklist.Contains(unit.Guid));
                if (AddList.Count > 0 && !GotSheep)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            //Sheep Timer Added to Stop DoubleCasting Sheeps
            static Stopwatch SheepTimer = new Stopwatch(); 
            public static void SheepLogic()
            {
                List<WoWUnit> AddList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
    unit.Guid != Me.Guid &&
    unit.IsTargetingMeOrPet &&
    !unit.IsFriendly &&
    !unit.Elite &&
    !unit.InLineOfSight &&
    !unit.IsPet &&
    (unit.CreatureType == WoWCreatureType.Humanoid || unit.CreatureType == WoWCreatureType.Beast) &&
    unit != Me.CurrentTarget &&
    !Styx.Logic.Blacklist.Contains(unit.Guid));

                if (AddList.Count > 0 && !GotSheep && (!SheepTimer.IsRunning || SheepTimer.ElapsedMilliseconds > 5000))
                {
                    Log("Got adds Lets Polymorph a Target");
                    WoWUnit SheepAdd = AddList[0].ToUnit();
                    Log("Casting Poly on {0}", SheepAdd.Name);
                    SpellManager.Cast("Polymorph", SheepAdd);
                    Thread.Sleep(700);
                    SheepTimer.Reset();
                    if (!SheepTimer.IsRunning)
                    {
                        SheepTimer.Reset();
                        SheepTimer.Start();
                    }
                }
            }

        

        public static void retargetSheep()
        {
            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);

            foreach (WoWUnit sheep in mobList)
            {
                if (sheep.Auras.ContainsKey("Polymorph"))
                {
                    sheep.Target();
                    Thread.Sleep(200);
                }
            }
        }
        public bool ColdSnapCheck()
        {
            List<int> SpellList = new List<int>();
            SpellList.Clear();
            //Check for Spells on Cooldown And Add to the List if they Are.
            if(SpellIsOnCooldown("Cone of Cold"))
            {
                SpellList.Add(1);
            }
            if (SpellIsOnCooldown("Frost Nova"))
            {
                SpellList.Add(1);
            }
            if (SpellIsOnCooldown("Icy Veins"))
            {
                SpellList.Add(1);
            }
            if (SpellIsOnCooldown("Ice Barrier"))
            {
                SpellList.Add(1);
            }
            if (SpellList.Count > 1)
            {
                return true;
            }
            else 
            {return false;}
        }
        //check For spell Cooldown
        public static bool SpellIsOnCooldown(string SpellName)
        {
            if (SpellManager.HasSpell(SpellName) && SpellManager.Spells[SpellName].Cooldown)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool IsInPartyOrRaid()
        {
            if (Me.PartyMembers.Count > 0)
                return true;

            return false;
        }
        public static List<WoWUnit> getAdds()
        {
            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
                unit.Guid != Me.Guid &&
                unit.IsTargetingMeOrPet &&
                !unit.IsFriendly &&
                !Styx.Logic.Blacklist.Contains(unit.Guid));

            return mobList;

        }
        public static bool Adds()
        {
            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
                unit.Guid != Me.Guid &&
                unit.IsTargetingMeOrPet &&
                !unit.IsFriendly &&
                !Styx.Logic.Blacklist.Contains(unit.Guid));

            if (mobList.Count > 0)
            {
                return true;
            }
            else
                return false;

        }
        public static bool HasSheeped()
        {
            List<WoWUnit> SheepedList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
unit.Guid != Me.Guid &&
unit.Auras.ContainsKey("Polymorph"));
            if (SheepedList.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
         private static bool IsNotWanding
         {
             get
             {
                 if (Lua.GetReturnVal<int>("return IsAutoRepeatSpell(\"Shoot\")", 0) == 1) { return false; }
                 if (Lua.GetReturnVal<int>("return HasWandEquipped()", 0) == 0) { return false; }
                 return true;
             }
         }



        private void BackUp()
        {
            WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
            WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
            WoWMovement.Face();
            Thread.Sleep(1500);
            WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
            WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
        }

        private void BackUpPVP()
        {
            if (SpellManager.HasSpell("Blink") && SpellManager.CanCast("Blink"))
            {
                SpellManager.Cast("Blink", null);
                Thread.Sleep(100);
                Me.CurrentTarget.Face();
            }
            else
            {
                WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                WoWMovement.Face();
                Thread.Sleep(1500);
                WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
                WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend); 
            }

        }




        public bool isItemInCooldown(WoWItem item)
        {
            if (Equals(null, item))
                return true;

            string cd_st;
            Lua.DoString("s=GetItemCooldown(" + item.Entry + ")");
            cd_st = Lua.GetLocalizedText("s", ObjectManager.Me.BaseAddress);
            if (cd_st == "0")
                return false;

            return true;

        }
      

   

        private new WoWItem ManaGem;
       public bool HaveManaGem()
       {
           foreach (WoWItem item in ObjectManager.GetObjectsOfType<WoWItem>(false))
           {
               if (item.Entry == 36799)
               {
                   ManaGem = item;
                   return true;
               }
              
           }
            return false;
           
       }
       public bool ManaGemNotCooldown()
       {
           if (ManaGem != null)
           {
               if (ManaGem.Cooldown == 0)
               {
                   return true;
               }

           }
           return false;
       }
        public void UseManaGem()
        {
            if (ManaGem != null && ManaGemNotCooldown())
            {
                Lua.DoString("UseItemByName(\"" + ManaGem.Name + "\")"); 
            }
        }
    }
}
