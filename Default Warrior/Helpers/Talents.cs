using System;
using System.Collections.Generic;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class Talents
    {
        private static void LoadCurrentSpec() { Load(ActiveGroup); }

        private static readonly string[] TabNames = new string[4];
        private static readonly int[] TabPoints = new int[4];
        private static int _indexGroup;
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
/*
        private static int TotalPoints
        {
            get { int nPoints = 0; for (int iTab = 1; iTab <= 3; iTab++) nPoints += TabPoints[iTab]; return nPoints; }
        }
*/

       
        public static int Spec
        {
            get
            {
                LoadCurrentSpec();

                int nSpec = 0;
                if (TabPoints[1] == 0 && TabPoints[2] == 0 && TabPoints[3] == 0)
                {
                    if (Me.Level > 9)
                    { Utils.Log("*** NO TALENT POINTS HAVE BEEN SPENT YET ***"); }
                    else if (Me.Level < 10)
                    { Utils.Log("*** Below level 10 no talent points available ***"); }
                    nSpec = 0;
                    return nSpec;
                }

                if (TabPoints[1] >= (TabPoints[2] + TabPoints[3])) nSpec = 1;
                else if (TabPoints[2] >= (TabPoints[1] + TabPoints[3])) nSpec = 2;
                else if (TabPoints[3] >= (TabPoints[1] + TabPoints[2])) nSpec = 3;

                return nSpec;
            }
        }

        public static void Load(int nGroup)
        {
            int nTab;
            _indexGroup = nGroup;

            //if (ObjectManager.Me.Level <10) return;



            
            for (nTab = 1; nTab <= 3; nTab++)
            {
                try
                {
                    string luaCode = String.Format("return GetTalentTabInfo({0},false,false,{1})", nTab, _indexGroup);
                    List<string> tabInfo = Lua.GetReturnValues(luaCode, "stuff.lua");

                    TabNames[nTab] = tabInfo[1];
                    TabPoints[nTab] = Convert.ToInt32(tabInfo[4]);
                }
                catch (Exception ex) { Logging.WriteException(ex); }
            }
             
        }

/*
        private static bool IsActiveGroup() { return ActiveGroup == _indexGroup; }
*/

        private static int ActiveGroup { get { return Lua.GetReturnVal<int>("return GetActiveTalentGroup(false,false)", 0); } }

/*
        private static void ActivateGroup() { Lua.DoString("SetActiveTalentGroup(\"{0}\")", _indexGroup); }
*/

/*
        private static int GetNumGroups() { return Lua.GetReturnVal<int>("return GetNumTalentGroups(false,false)", 0); }
*/
    }
}