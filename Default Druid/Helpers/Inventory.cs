using System.Linq;
using Hera.SpellsMan;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;

namespace Hera.Helpers
{
    public static class Inventory
    {
        public static class ManaPotions
        {
            private static LocalPlayer Me { get { return ObjectManager.Me; } }

            private static WoWItem ManaPotion
            {
                get { return Me.CarriedItems.Where(item => item.Name.Contains("Mana Pot")).FirstOrDefault(); }
            }

            public static bool IsUseable
            {
                get
                {
                    if (ManaPotion == null) return false;

                    string luacode = String.Format("return GetItemCooldown(\"{0}\")", ManaPotion.Name);
                    if (Utils.LuaGetReturnValueString(luacode) != "0") return false;

                    return true;
                }
            }

            public static void Use()
            {
                if (Me.IsCasting) Spell.StopCasting();
                Utils.LagSleep();
                
                WoWItem manaPotion = ManaPotion;
                Utils.Log("We're having an 'Oh Shit' moment. Using mana potion: " + manaPotion.Name, Utils.Colour("Red"));
                ManaPotion.Interact();
            }

        }

        public static class HealthPotions
        {
            private static LocalPlayer Me { get { return ObjectManager.Me; } }

            /// <summary>
            /// WoWItem type of a suitable Healing Potion in your bags. Null if nothing is found
            /// </summary>
            private static WoWItem HealthPotion
            {
                get { return Me.CarriedItems.Where(item => item.Name.Contains("Healing Pot")).FirstOrDefault(); }
            }

            /// <summary>
            /// Checks if this item is not on cooldown and can be used. Returns TRUE is the item is ok to be used
            /// </summary>
            public static bool IsUseable
            {
                get
                {
                    if (HealthPotion == null) return false;
                    Utils.Log(string.Format("We have a health potion, {0}, lets see if its on cooldown...", HealthPotion.Name));

                    string luacode = String.Format("return GetItemCooldown(\"{0}\")", HealthPotion.Name);
                    bool result = (Utils.LuaGetReturnValueString(luacode) == "0");
                    Utils.Log("Potion is useable " + result);

                    return result;
                }
            }

            public static void Use()
            {
                if (Me.IsCasting) Spell.StopCasting();
                Utils.LagSleep();

                WoWItem healthPotion = HealthPotion;
                Utils.Log("We're having an 'Oh Shit' moment. Using health potion: " + healthPotion.Name, Utils.Colour("Red"));
                HealthPotion.Interact();
            }

        }

        
        public static void Drink(bool useSpecialityItems)
        {
            // If we're not using smart eat and drink then don't do this stuff, just sit and drink
            if (Config.Settings.SmartEatDrink.Contains("always"))
            {
                WoWItem drink = Styx.Logic.Inventory.Consumable.GetBestDrink(useSpecialityItems);
                if (drink == null) return;
                Utils.Log("Drinking " + drink.Name, Utils.Colour("Blue"));
                LevelbotSettings.Instance.DrinkName = drink.Name;
            }
            Styx.Logic.Common.Rest.Feed();
        }
        

        public static void Eat(bool useSpecialityItems)
        {
            // If we're not using smart eat and drink then don't do this stuff, just sit and eat
            if (Config.Settings.SmartEatDrink.Contains("always"))
            {
                WoWItem food = Styx.Logic.Inventory.Consumable.GetBestFood(useSpecialityItems);
                if (food == null) return;
                Utils.Log("Eating " + food.Name, Utils.Colour("Blue"));
                LevelbotSettings.Instance.FoodName = food.Name;
            }
            Styx.Logic.Common.Rest.Feed();
        }
    }
}
