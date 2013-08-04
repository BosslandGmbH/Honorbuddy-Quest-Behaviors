using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Bots.Quest;
using Bots.Quest.QuestOrder;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.Hooks
{
    [CustomBehaviorFileName(@"Hooks\MountHook")]
    public class MountHook : CustomForcedBehavior
    {
        public MountHook(Dictionary<string, string> args)
            : base(args)
        {

            QuestId = 0;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            //True = hook running, false = hook stopped
            state = GetAttributeAsNullable<bool>("state", true, null, null) ?? false;

        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;


        private bool state;

        private Composite _root;

        public override bool IsDone
        {
            get
            {
                return inserted;
            }
        }

        private bool inserted = false;
        private static LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        private const int ApprenticeRiding = 33388;//20
        private const int JourneyManRiding = 33391;//40
        private const int ExpertRiding = 34090;//60
        private const int ColdWeatherFlying = 54197;//68
        private const int SuperFlying = 90265;//60
        private const int FlightMastersLic = 90267;//60


        #region Trainers
        //Hellfire
        private const int HordeFlight = 35093;
        private const int AllianceFlight = 35100;

        //Stormwind
        private const int AllianceLowbie = 43769;
        //Orgimar
        private const int HordieLowbie = 44919;
        #endregion


        private static int FlightLevel
        {
            get
            {


                if (SpellManager.HasSpell(ColdWeatherFlying))
                    return 3;

                if (SpellManager.HasSpell(ExpertRiding) || SpellManager.HasSpell(SuperFlying))
                    return 2;

                if (SpellManager.HasSpell(ApprenticeRiding) || SpellManager.HasSpell(JourneyManRiding))
                    return 1;

                return 0;

            }

        }

        private static bool Hellfire
        {
            get
            {
                return StyxWoW.Me.MapId == 530;
            }
        }

        private static bool OldWorld
        {
            get
            {
                return (StyxWoW.Me.MapId == 0 || StyxWoW.Me.MapId == 1);
            }
        }

        //Return the trainer we want based on faction and location and skill.
        private static int TrainerId
        {
            get
            {
                if (OldWorld)
                {
                    return Me.IsAlliance ? AllianceLowbie : HordieLowbie;
                }
                if (Hellfire)
                {
                    return Me.IsAlliance ? AllianceFlight : HordeFlight;
                }


                return 0;
            }
        }


        public static Boolean setupQO;
        public static QuestOrder customQuestOrder;







        private static Composite QOrunner;

        private static void CleanupQuestOrder()
        {
            Logging.Write("Custom quest order complete, cleaning up.");
            TreeHooks.Instance.RemoveHook("GoobyHook2", QOrunner);
            customQuestOrder = null;

        }

        private static void SetupQuestLowbieOrder()
        {
            Logging.Write("Starting ground quest order.");
            
            //Replace with memorystream once profile is finalized    
            //var reader = new StreamReader(File.OpenRead(@"C:\Users\Dennis\Desktop\New folder\hbx\GroundTraining.XML"));
            var reader = new StreamReader(new MemoryStream(Convert.FromBase64String(GroundMounts)));
            XElement xml = XElement.Parse(reader.ReadToEnd());
            var Profile = new Profile(xml, null);
            QuestState.Instance.Order.CurrentBehavior = null;
            QuestState.Instance.Order.Nodes.InsertRange(0, Profile.QuestOrder);
            QuestState.Instance.Order.UpdateNodes();
            setupQO = true;
        }

        private static void SetupQuestFlyingOrder()
        {
            Logging.Write("Starting flying quest order.");
            //var reader = new StreamReader(File.OpenRead(@"C:\Users\Dennis\Desktop\New folder\hbx\GroundTraining.XML"));
            var reader = new StreamReader(new MemoryStream(Convert.FromBase64String(FlyingMounts)));
            XElement xml = XElement.Parse(reader.ReadToEnd());
            var Profile = new Profile(xml, null);
            QuestState.Instance.Order.CurrentBehavior = null;
            QuestState.Instance.Order.Nodes.InsertRange(0, Profile.QuestOrder);
            QuestState.Instance.Order.UpdateNodes();
            setupQO = true;


        }

        //Races that give us problems:
        //Horde
        //Blood elf -> Silvermoon
        //Undead -> Undercity
        //Alliance
        //Goats -> Goat land
        //Night elf ->Darnassus
        //Worgen -> ???


        private static Composite PurchaseMount
        {

            get
            {
                return new PrioritySelector(
                    new Decorator(r => FlightLevel == 1 && !Mount.GroundMounts.Any() && !setupQO, new Action(r => SetupQuestLowbieOrder())),
                    new Decorator(r => FlightLevel == 1 && Mount.GroundMounts.Any() && setupQO, new Action(r => setupQO = false)),
                    new Decorator(r => FlightLevel == 2 && !Mount.FlyingMounts.Any() && !setupQO, new Action(r => SetupQuestFlyingOrder())),
                    new Decorator(r => FlightLevel == 2 && Mount.FlyingMounts.Any() && setupQO, new Action(r => setupQO = false))
                    );
            }
        }

        private static void SetupTrainer()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine, "Creating ForceTrainRiding object");
            var args = new Dictionary<string, string> { { "MobId", TrainerId.ToString() } };

            gooby = new ForceTrainRiding(args);
            gooby.OnStart();
            TreeHooks.Instance.ReplaceHook("GoobyHook", gooby.Branch);
        }

        private static void CleanUpCustomForcedBehavior()
        {

            if (gooby.GetType() == typeof(ForceTrainRiding))
            {
                Logging.Write(System.Windows.Media.Colors.Aquamarine, "Cleaningup ForceTrainRiding object");
            }
            else
            {
                Logging.Write(System.Windows.Media.Colors.Aquamarine, "Cleaningup InteractWith object");
            }

            TreeHooks.Instance.RemoveHook("GoobyHook", gooby.Branch);
            gooby.Dispose();
            gooby = null;
        }


        //Composites
        private static CustomForcedBehavior gooby = null;
        private static Composite runOtherComposite
        {
            get
            {
                return new Decorator(r => gooby != null,
                new PrioritySelector(
                    new Decorator(r => gooby.IsDone, new Action(r => CleanUpCustomForcedBehavior())),
                    new Decorator(r => !gooby.IsDone, new HookExecutor("GoobyHook"))
            ));
            }
        }

        private static Composite HellfireComposite
        {
            get
            {
                return new Decorator(r => Hellfire && Me.Level >= 60 && Me.Gold >= 268 && FlightLevel < 2, new Action(r => SetupTrainer()));

            }
        }

        private static Composite OldWordComposite
        {
            get
            {
                return new Decorator(r => OldWorld && ((Me.Level >= 20 && Me.Gold >= 5 && FlightLevel < 1) || (Me.Level >= 60 && Me.Gold >= 268 && FlightLevel < 2)), new Action(r => SetupTrainer()));

            }
        }

        public static Composite _myHook;
        public static Composite myHook
        {
            get
            {
                if (_myHook == null)
                {
                    _myHook = new Decorator(r => !Me.Combat, new PrioritySelector(runOtherComposite, PurchaseMount, OldWordComposite, HellfireComposite));
                    return _myHook;
                }
                return _myHook;
            }
            set
            {
                _myHook = value;
            }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            Logging.Write("MountHook:{0}  - zxbca", state);
            if (state == true)
            {
                if (_myHook == null)
                {
                    Logging.Write("MountHook:Inserting hook - zxbca");
                    TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
                }
                else
                {
                    Logging.Write("MountHook:Insert was requested, but was already present - zxbca");
                }


            }
            else
            {
                if (_myHook != null)
                {
                    Logging.Write("MountHook:Removing hook - zxbca");
                    TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
                    myHook = null;
                }
                else
                {
                    Logging.Write("MountHook:Remove was requested, but hook was not present - zxbca");
                }

            }

            inserted = true;

        }


        #region Profiles

        private const string GroundMounts = "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjxIQlByb2ZpbGU+DQogICA8TmFtZT5NYXN0YWhnIE1vdW50IFRyYWluaW5nPC9OYW1lPg0KICAgPE1pbkxldmVsPjE8L01pbkxldmVsPg0KICAgPE1heExldmVsPjEwMTwvTWF4TGV2ZWw+DQogICA8TWluRHVyYWJpbGl0eT4wLjM8L01pbkR1cmFiaWxpdHk+DQogICA8TWluRnJlZUJhZ1Nsb3RzPjM8L01pbkZyZWVCYWdTbG90cz4NCiAgIDxNYWlsR3JleT5GYWxzZTwvTWFpbEdyZXk+DQogICA8TWFpbFdoaXRlPkZhbHNlPC9NYWlsV2hpdGU+DQogICA8TWFpbEdyZWVuPlRydWU8L01haWxHcmVlbj4NCiAgIDxNYWlsQmx1ZT5UcnVlPC9NYWlsQmx1ZT4NCiAgIDxNYWlsUHVycGxlPlRydWU8L01haWxQdXJwbGU+DQogICA8U2VsbEdyZXk+VHJ1ZTwvU2VsbEdyZXk+DQogICA8U2VsbFdoaXRlPlRydWU8L1NlbGxXaGl0ZT4NCiAgIDxTZWxsR3JlZW4+VHJ1ZTwvU2VsbEdyZWVuPg0KICAgPFNlbGxCbHVlPlRydWU8L1NlbGxCbHVlPg0KICAgPFNlbGxQdXJwbGU+RmFsc2U8L1NlbGxQdXJwbGU+DQogICA8TWFpbGJveGVzPg0KICAgICAgPCEtLSBFbXB0eSBvbiBQdXJwb3NlIC0tPg0KICAgPC9NYWlsYm94ZXM+DQogICA8QmxhY2tzcG90cyAvPg0KICAgPFF1ZXN0T3JkZXIgSWdub3JlQ2hlY2tQb2ludHM9ImZhbHNlIj4NCiAgICAgIDxJZiBDb25kaXRpb249IiFNZS5Jc0hvcmRlIj4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJDb21waWxpbmcgQWxsaWFuY2UgTW91bnQiIExvZ0NvbG9yPSJPcmFuZ2UiIC8+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5IZWFydGhzdG9uZUFyZWFJZCAhPSA1MTQ4Ij4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJNb3ZpbmcgdG8gc2V0IGhlYXJ0aCB0byBTVyBJbm5rZWVwZXIiIExvZ0NvbG9yPSJSZWQiIC8+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNjc0MCIgR29zc2lwT3B0aW9ucz0iMSIgWD0iLTg4NjcuNzg2IiBZPSI2NzMuNjcyOSIgWj0iOTcuOTAzMjQiIC8+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLkh1bWFuIj4NCiAgICAgICAgICAgIDxSdW5UbyBYPSItOTQ0Mi43NDIiIFk9Ii0xMzkwLjY2NiIgWj0iNDYuODcwNDUiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNRdWVzdCgzMjYxOCkiPg0KICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIHRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjE4IiBUdXJuSW5OYW1lPSJSYW5kYWwgSHVudGVyIiBUdXJuSW5JZD0iNDczMiIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMzg0IiBCdXlJdGVtSWQ9IjI0MTEiIFdhaXRUaW1lPSI1MDAwIiBDb2xsZWN0aW9uRGlzdGFuY2U9IjUwIiBYPSItOTQ1NS4zNjUiIFk9Ii0xMzg1LjMyNyIgWj0iNDcuMTI4MTgiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoSGFzSXRlbSgyNDExKSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMjQxMSkiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5QYW5kYSI+DQogICAgICAgICAgICA8UnVuVG8gWD0iLTgyMTIuMjIxIiBZPSI1NDcuNTY5IiBaPSIxMTcuMTk0NyIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249Ikhhc1F1ZXN0KDMyNjY1KSI+DQogICAgICAgICAgICAgICA8VHVybkluIFF1ZXN0TmFtZT0iTGVhcm4gdG8gUmlkZSIgUXVlc3RJZD0iMzI2NjUiIFR1cm5Jbk5hbWU9Ik1laSBMaW4iIFR1cm5JbklkPSI3MDI5NiIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNjUwNjgiIEJ1eUl0ZW1JZD0iODc3OTUiIFdhaXRUaW1lPSI1MDAwIiBDb2xsZWN0aW9uRGlzdGFuY2U9IjUwIiBYPSItODIwOS4zNzkiIFk9IjU0Ni4wMjYxIiBaPSIxMTcuNzY4NCIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihIYXNJdGVtKDg3Nzk1KSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoODc3OTUpIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuR25vbWUiPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZXJTZXR0aW5ncyIgTG9vdE1vYnM9IlRydWUiIFVzZUZsaWdodFBhdGhzPSJUcnVlIiBQdWxsRGlzdGFuY2U9IjI1IiAvPg0KICAgICAgICAgICAgPFJ1blRvIFg9Ii01NDU0LjE3MSIgWT0iLTYyMS4wNDgiIFo9IjM5My4zOTY4IiAvPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzUXVlc3QoMzI2NjMpIj4NCiAgICAgICAgICAgICAgIDxUdXJuSW4gUXVlc3ROYW1lPSJMZWFybiB0byBSaWRlIiBRdWVzdElkPSIzMjY2MyIgVHVybkluTmFtZT0iQmluankgRmVhdGhlcndoaXN0bGUiIFR1cm5JbklkPSI3OTU0IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI3OTU1IiBCdXlJdGVtSWQ9Ijg1OTUiIFdhaXRUaW1lPSI1MDAwIiBYPSItNTQ1NC4xNzEiIFk9Ii02MjEuMDQ4IiBaPSIzOTMuMzk2OCIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihIYXNJdGVtKDg1OTUpKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg4NTk1KSIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLkR3YXJmIj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJVc2VyU2V0dGluZ3MiIExvb3RNb2JzPSJUcnVlIiBVc2VGbGlnaHRQYXRocz0iVHJ1ZSIgUHVsbERpc3RhbmNlPSIyNSIgLz4NCiAgICAgICAgICAgIDxSdW5UbyBYPSItNTUyNC4zNTQiIFk9Ii0xMzQ5Ljg2OCIgWj0iMzk4LjY2NDEiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNRdWVzdCgzMjY2MikiPg0KICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIHRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjYyIiBUdXJuSW5OYW1lPSJVbHRoYW0gSXJvbmhvcm4iIFR1cm5JbklkPSI0NzcyIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSIxMjYxIiBCdXlJdGVtSWQ9IjU4NzMiIFdhaXRUaW1lPSI1MDAwIiBYPSItNTUzOS41NSIgWT0iLTEzMjIuNTUiIFo9IjM5OC44NjUzIiAvPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKEhhc0l0ZW0oNTg3MykpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNaXNjXFJ1bkx1YSIgTHVhPSJVc2VJdGVtQnlOYW1lKDU4NzMpIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuTmlnaHRFbGYiPg0KICAgICAgICAgICAgPCEtLSBHZXQgb24gYXQgU1csIG9mZiBhdCBSdXQndGhlcmFuIFZpbGxhZ2UgKERhcm5hc3N1cykgLS0+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMCkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ikhvb2tzXFVzZVRyYW5zcG9ydDIiIFRyYW5zcG9ydElkPSIxNzYzMTAiIFdhaXRBdFg9Ii04NjQwLjU1NiIgV2FpdEF0WT0iMTMzMC44MjkiIFdhaXRBdFo9IjUuMjMzMjA3IiBHZXRPZmZYPSI4MTc3LjU0IiBHZXRPZmZZPSIxMDAzLjA3OSIgR2V0T2ZmWj0iNi42NDYxNjQiIFN0YW5kT25YPSItODY0NC45NTIiIFN0YW5kT25ZPSIxMzQ4LjExIiBTdGFuZE9uWj0iNi4xNDMwOTQiIFRyYW5zcG9ydFN0YXJ0WD0iLTg2NTAuNzE5IiBUcmFuc3BvcnRTdGFydFk9IjEzNDYuMDUxIiBUcmFuc3BvcnRTdGFydFo9Ii0wLjAzODIzMzQiIFRyYW5zcG9ydEVuZFg9IjgxNjIuNTg3IiBUcmFuc3BvcnRFbmRZPSIxMDA1LjM2NSIgVHJhbnNwb3J0RW5kWj0iMC4wNDc0MDIzIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDwhLS0gVG8gZ2V0IGluc2lkZSBvZiBEYXJuYXNzdXMgLS0+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMSkiPg0KICAgICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iKE1lLlogJmx0OyAxMDApIj4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSI4Mzc1LjU3OSIgWT0iOTk3LjY1MTciIFo9IjI3LjQ1NzY4IiAvPg0KICAgICAgICAgICAgICAgICAgPCEtLSBSZWQgcG9ydGFsIHVwIHRvIERhcm5hc3N1cyAtLT4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSI4Mzg2Ljk0MyIgWT0iOTk5LjYyNTYiIFo9IjI5LjgwMTE0IiAvPg0KICAgICAgICAgICAgICAgICAgPCEtLSBJbnNpZGUgcG9ydGFsIC0tPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjUwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciBwb3J0IHVwIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuWiAmZ3Q7IDEwMDApIj4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSIxMDEyOS43OCIgWT0iMjUyNi41OTUiIFo9IjEzMjQuODI4IiAvPg0KICAgICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzUXVlc3QoMzI2NjQpIj4NCiAgICAgICAgICAgICAgICAgICAgIDxUdXJuSW4gUXVlc3ROYW1lPSJMZWFybiB0byBSaWRlIiBRdWVzdElkPSIzMjY2NCIgVHVybkluTmFtZT0iSmFydHNhbSIgVHVybkluSWQ9IjQ3NTMiIC8+DQogICAgICAgICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjQ3MzAiIEJ1eUl0ZW1JZD0iODYyOSIgV2FpdFRpbWU9IjUwMDAiIFg9IjEwMTI5LjkxIiBZPSIyNTMzLjI0NSIgWj0iMTMyMy4yNzEiIC8+DQogICAgICAgICAgICAgICAgICA8IS0tPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkZvcmNlVHJhaW5SaWRpbmciIE1vYklkPSI0NzUzIiAvPiAtLT4NCiAgICAgICAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihIYXNJdGVtKDg2MjkpKSI+DQogICAgICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg4NjI5KSIgLz4NCiAgICAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuRHJhZW5laSI+DQogICAgICAgICAgICA8IS0tIEdldCBvbiBhdCBTVywgb2ZmIGF0IFJ1dCd0aGVyYW4gVmlsbGFnZSAoRGFybmFzc3VzKSAtLT4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5NYXBJZCA9PSAwKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSG9va3NcVXNlVHJhbnNwb3J0MiIgVHJhbnNwb3J0SWQ9IjE3NjMxMCIgV2FpdEF0WD0iLTg2NDAuNTU2IiBXYWl0QXRZPSIxMzMwLjgyOSIgV2FpdEF0Wj0iNS4yMzMyMDciIEdldE9mZlg9IjgxNzcuNTQiIEdldE9mZlk9IjEwMDMuMDc5IiBHZXRPZmZaPSI2LjY0NjE2NCIgU3RhbmRPblg9Ii04NjQ0Ljk1MiIgU3RhbmRPblk9IjEzNDguMTEiIFN0YW5kT25aPSI2LjE0MzA5NCIgVHJhbnNwb3J0U3RhcnRYPSItODY1MC43MTkiIFRyYW5zcG9ydFN0YXJ0WT0iMTM0Ni4wNTEiIFRyYW5zcG9ydFN0YXJ0Wj0iLTAuMDM4MjMzNCIgVHJhbnNwb3J0RW5kWD0iODE2Mi41ODciIFRyYW5zcG9ydEVuZFk9IjEwMDUuMzY1IiBUcmFuc3BvcnRFbmRaPSIwLjA0NzQwMjMiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPCEtLSBUbyBnZXQgaW5zaWRlIG9mIERhcm5hc3N1cyAtLT4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5NYXBJZCA9PSAxKSI+DQogICAgICAgICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSIoTWUuWiAmbHQ7IDEwMCkgJmFtcDsmYW1wOyAoTWUuWiAmZ3Q7IDApIj4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSI4Mzc1LjU3OSIgWT0iOTk3LjY1MTciIFo9IjI3LjQ1NzY4IiAvPg0KICAgICAgICAgICAgICAgICAgPCEtLSBSZWQgcG9ydGFsIHVwIHRvIERhcm5hc3N1cyAtLT4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSI4Mzg2Ljk0MyIgWT0iOTk5LjYyNTYiIFo9IjI5LjgwMTE0IiAvPg0KICAgICAgICAgICAgICAgICAgPCEtLSBJbnNpZGUgcG9ydGFsIC0tPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjUwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciBwb3J0IHVwIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5ab25lSWQgPT0gMTY1NyI+DQogICAgICAgICAgICAgICAgICA8IS0tIERhcm5hc3N1cyAtLT4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSI5NjU1LjI1MiIgWT0iMjUwOS4zMyIgWj0iMTMzMS41OTgiIC8+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMjA3OTk1IiBPYmplY3RUeXBlPSJHYW1lT2JqZWN0IiBSYW5nZT0iNSIgWD0iOTY1NS4yNTIiIFk9IjI1MDkuMzMiIFo9IjEzMzEuNTk4IiAvPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjgwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlpvbmVJZCA9PSAzNTU3Ij4NCiAgICAgICAgICAgICAgICAgIDwhLS0gRXhvZGFyIC0tPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IkxlYXJuaW5nIERyYWVuZWkgTW91bnQiIExvZ0NvbG9yPSJPcmFuZ2UiIC8+DQogICAgICAgICAgICAgICAgICA8UnVuVG8gWD0iLTM5ODEuNzY5IiBZPSItMTE5MjkuMTQiIFo9Ii0wLjI0MTk0MTIiIC8+DQogICAgICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoKEhhc1F1ZXN0KDMyNjYxKSkgJmFtcDsmYW1wOyAoSXNRdWVzdENvbXBsZXRlZCgzMjY2MSkpKSI+DQogICAgICAgICAgICAgICAgICAgICA8VHVybkluIFF1ZXN0TmFtZT0iTGVhcm4gVG8gUmlkZSIgUXVlc3RJZD0iMzI2NjEiIFR1cm5Jbk5hbWU9IkFhbHVuIiBUdXJuSW5JZD0iMjA5MTQiIFg9Ii0zOTgxLjc2OSIgWT0iLTExOTI5LjE0IiBaPSItMC4yNDE5NDEyIiAvPg0KICAgICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgICAgICAgIDxJZiBDb25kaXRpb249IigoSGFzUXVlc3QoMTQwODIpKSAmYW1wOyZhbXA7IChJc1F1ZXN0Q29tcGxldGVkKDE0MDgyKSkpIj4NCiAgICAgICAgICAgICAgICAgICAgIDxUdXJuSW4gUXVlc3ROYW1lPSJMZWFybiBUbyBSaWRlIiBRdWVzdElkPSIxNDA4MiIgVHVybkluTmFtZT0iQWFsdW4iIFR1cm5JbklkPSIyMDkxNCIgWD0iLTM5ODEuNzY5IiBZPSItMTE5MjkuMTQiIFo9Ii0wLjI0MTk0MTIiIC8+DQogICAgICAgICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgICAgICAgPFJ1blRvIFg9Ii0zOTgxLjc2OSIgWT0iLTExOTI5LjE0IiBaPSItMC4yNDE5NDEyIiAvPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjE3NTg0IiBCdXlJdGVtSWQ9IjI4NDgxIiBXYWl0VGltZT0iNTAwMCIgQ29sbGVjdGlvbkRpc3RhbmNlPSI1MCIgWD0iLTM5ODEuNzY5IiBZPSItMTE5MjkuMTQiIFo9Ii0wLjI0MTk0MTIiIC8+DQogICAgICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoSGFzSXRlbSgyODQ4MSkpIj4NCiAgICAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNaXNjXFJ1bkx1YSIgTHVhPSJVc2VJdGVtQnlOYW1lKDI4NDgxKSIgLz4NCiAgICAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iVXNlclNldHRpbmdzIiBVc2VNb3VudD0iVHJ1ZSIgTG9vdE1vYnM9IlRydWUiIFB1bGxEaXN0YW5jZT0iMjUiIC8+DQogICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWVzc2FnZSIgVGV4dD0iVXNpbmcgSGVhcnRoc3RvbmUiIExvZ0NvbG9yPSJPcmFuZ2UiIC8+DQoJCTxDdXN0b21CZWhhdmlvciBGaWxlPSJVc2VIZWFydGhzdG9uZSIgV2FpdEZvckNEPSJ0cnVlIiAvPg0KICAgICAgPC9JZj4NCiAgICAgIDxJZiBDb25kaXRpb249Ik1lLklzSG9yZGUiPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IkNvbXBpbGluZyBIb3JkZSBNb3VudCIgTG9nQ29sb3I9Ik9yYW5nZSIgLz4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLkhlYXJ0aHN0b25lQXJlYUlkICE9IDUxNzAiPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9Ik1vdmluZyB0byBzZXQgaGVhcnRoIHRvIE9yZyBJbm5rZWVwZXIiIExvZ0NvbG9yPSJSZWQiIC8+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNjkyOSIgR29zc2lwT3B0aW9ucz0iMSIgWD0iMTU3My4yNjYiIFk9Ii00NDM5LjE1OCIgWj0iMTYuMDU2MzEiIC8+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLk9yYyI+DQogICAgICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSIhSGFzSXRlbSg1NjY1KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMzM2MiIgQnV5SXRlbUlkPSI1NjY1IiBHb3NzaXBPcHRpb25zPSIxIiBXYWl0VGltZT0iNTAwMCIgWD0iMjA3Ni42MDIiIFk9Ii00NTY4LjYzMiIgWj0iNDkuMjUzMTkiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSg1NjY1KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg1NjY1KSIgV2FpdFRpbWU9IjEwMDAiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5Hb2JsaW4iPg0KICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iIUhhc0l0ZW0oNjI0NjEpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI0ODUxMCIgQnV5SXRlbUlkPSI2MjQ2MSIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9IjE0NzUuMzIiIFk9Ii00MTQwLjk4IiBaPSI1Mi41MSIgLz4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI1MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNJdGVtKDYyNDYxKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg2MjQ2MSkiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuVHJvbGwiPg0KICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iIUhhc0l0ZW0oODU4OCkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9Ijc5NTIiIEJ1eUl0ZW1JZD0iODU4OCIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9Ii04NTIuNzgiIFk9Ii00ODg1LjQwIiBaPSIyMi4wMyIgLz4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI1MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNJdGVtKDg1ODgpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNaXNjXFJ1bkx1YSIgTHVhPSJVc2VJdGVtQnlOYW1lKDg1ODgpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLlRhdXJlbiI+DQogICAgICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSIhSGFzSXRlbSgxNTI3NykiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjM2ODUiIEJ1eUl0ZW1JZD0iMTUyNzciIEdvc3NpcE9wdGlvbnM9IjEiIFdhaXRUaW1lPSI1MDAwIiBSYW5nZT0iMiIgWD0iLTIyNzkuNzk2IiBZPSItMzkyLjA2OTciIFo9Ii05LjM5Njg2MyIgLz4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI1MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNJdGVtKDE1Mjc3KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSgxNTI3NykiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjUwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuVW5kZWFkIj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5NYXBJZCA9PSAxKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSG9va3NcVXNlVHJhbnNwb3J0MiIgVHJhbnNwb3J0SWQ9IjE2NDg3MSIgV2FpdEF0WD0iMTg0NS4xODciIFdhaXRBdFk9Ii00Mzk1LjU1NSIgV2FpdEF0Wj0iMTM1LjIzMDYiIFRyYW5zcG9ydFN0YXJ0WD0iMTgzMy41MDkiIFRyYW5zcG9ydFN0YXJ0WT0iLTQzOTEuNTQzIiBUcmFuc3BvcnRTdGFydFo9IjE1Mi43Njc5IiBUcmFuc3BvcnRFbmRYPSIyMDYyLjM3NiIgVHJhbnNwb3J0RW5kWT0iMjkyLjk5OCIgVHJhbnNwb3J0RW5kWj0iMTE0Ljk3MyIgU3RhbmRPblg9IjE4MzUuNTA5IiBTdGFuZE9uWT0iLTQzODUuNzg1IiBTdGFuZE9uWj0iMTM1LjA0MzYiIEdldE9mZlg9IjIwNjUuMDQ5IiBHZXRPZmZZPSIyODMuMTM4MSIgR2V0T2ZmWj0iOTcuMDMxNTYiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKE1lLk1hcElkID09IDApIj4NCiAgICAgICAgICAgICAgIDxXaGlsZSBDb25kaXRpb249IiFIYXNJdGVtKDQ2MzA4KSI+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNDczMSIgQnV5SXRlbUlkPSI0NjMwOCIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9IjIyNzUuMDgiIFk9IjIzNy4wMCIgWj0iMzMuNjkiIC8+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSg0NjMwOCkiPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoNDYzMDgpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5CbG9vZEVsZiI+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ikhvb2tzXFVzZVRyYW5zcG9ydDIiIFRyYW5zcG9ydElkPSIxNjQ4NzEiIFdhaXRBdFg9IjE4NDUuMTg3IiBXYWl0QXRZPSItNDM5NS41NTUiIFdhaXRBdFo9IjEzNS4yMzA2IiBUcmFuc3BvcnRTdGFydFg9IjE4MzMuNTA5IiBUcmFuc3BvcnRTdGFydFk9Ii00MzkxLjU0MyIgVHJhbnNwb3J0U3RhcnRaPSIxNTIuNzY3OSIgVHJhbnNwb3J0RW5kWD0iMjA2Mi4zNzYiIFRyYW5zcG9ydEVuZFk9IjI5Mi45OTgiIFRyYW5zcG9ydEVuZFo9IjExNC45NzMiIFN0YW5kT25YPSIxODM1LjUwOSIgU3RhbmRPblk9Ii00Mzg1Ljc4NSIgU3RhbmRPblo9IjEzNS4wNDM2IiBHZXRPZmZYPSIyMDY1LjA0OSIgR2V0T2ZmWT0iMjgzLjEzODEiIEdldE9mZlo9Ijk3LjAzMTU2IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5NYXBJZCA9PSAwKSI+DQogICAgICAgICAgICAgICA8UnVuVG8gWD0iMTgwNS44NzciIFk9IjM0NS4wMDA2IiBaPSI3MC43OTAwMiIgLz4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSIyMDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgICAgIDxXaGlsZSBDb25kaXRpb249IihNZS5ab25lSWQgPT0gMTQ5NykiPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjE4NDUwMyIgT2JqZWN0VHlwZT0iR2FtZU9iamVjdCIgUHJlSW50ZXJhY3RNb3VudFN0cmF0ZWd5PSJEaXNtb3VudCIgUmFuZ2U9IjgiIFdhaXRUaW1lPSI1MDAwIiBYPSIxODA1Ljg3NyIgWT0iMzQ1LjAwMDYiIFo9IjcwLjc5MDAyIiAvPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjEwMDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgICAgIDwvV2hpbGU+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMTYyNjQiIEJ1eUl0ZW1JZD0iMjkyMjEiIEdvc3NpcE9wdGlvbnM9IjEiIFdhaXRUaW1lPSI1MDAwIiBYPSI5MjQ0LjU5IiBZPSItNzQ5MS41NjYiIFo9IjM2LjkxNDAxIiAvPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjUwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSgyOTIyMSkiPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMjkyMjEpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJDb21wbGV0ZWQgdHJhaW5pbmcgc2Vzc2lvbiIgTG9nQ29sb3I9Ik9yYW5nZSIgLz4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJVc2luZyBIZWFydGhzdG9uZSIgTG9nQ29sb3I9Ik9yYW5nZSIgLz4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJVc2VIZWFydGhzdG9uZSIgV2FpdEZvckNEPSJ0cnVlIiAvPg0KICAgICAgPC9JZj4NCgkgIA0KCSAgDQogICA8L1F1ZXN0T3JkZXI+DQo8L0hCUHJvZmlsZT4NCg==";
        private const string FlyingMounts = "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjxIQlByb2ZpbGU+DQogICA8TmFtZT5NYXN0YWhnIEZseWluZyBNb3VudCBUcmFpbmluZzwvTmFtZT4NCiAgIDxNaW5MZXZlbD4xPC9NaW5MZXZlbD4NCiAgIDxNYXhMZXZlbD4xMDE8L01heExldmVsPg0KICAgPE1pbkR1cmFiaWxpdHk+MC4zPC9NaW5EdXJhYmlsaXR5Pg0KICAgPE1pbkZyZWVCYWdTbG90cz4zPC9NaW5GcmVlQmFnU2xvdHM+DQogICA8TWFpbEdyZXk+RmFsc2U8L01haWxHcmV5Pg0KICAgPE1haWxXaGl0ZT5GYWxzZTwvTWFpbFdoaXRlPg0KICAgPE1haWxHcmVlbj5UcnVlPC9NYWlsR3JlZW4+DQogICA8TWFpbEJsdWU+VHJ1ZTwvTWFpbEJsdWU+DQogICA8TWFpbFB1cnBsZT5UcnVlPC9NYWlsUHVycGxlPg0KICAgPFNlbGxHcmV5PlRydWU8L1NlbGxHcmV5Pg0KICAgPFNlbGxXaGl0ZT5UcnVlPC9TZWxsV2hpdGU+DQogICA8U2VsbEdyZWVuPlRydWU8L1NlbGxHcmVlbj4NCiAgIDxTZWxsQmx1ZT5UcnVlPC9TZWxsQmx1ZT4NCiAgIDxTZWxsUHVycGxlPkZhbHNlPC9TZWxsUHVycGxlPg0KICAgPE1haWxib3hlcz4NCiAgICAgIDwhLS0gRW1wdHkgb24gUHVycG9zZSAtLT4NCiAgIDwvTWFpbGJveGVzPg0KICAgPEJsYWNrc3BvdHMgLz4NCiAgIDxRdWVzdE9yZGVyIElnbm9yZUNoZWNrUG9pbnRzPSJmYWxzZSI+DQogICAgICA8SWYgQ29uZGl0aW9uPSIhTWUuSXNIb3JkZSI+DQogICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWVzc2FnZSIgVGV4dD0iQ29tcGlsaW5nIEFsbGlhbmNlIE1vdW50IiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iU3R5eFdvVy5NZS5NYXBJZCA9PSA1MzAiPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKCFIYXNJdGVtKDI1NDcyKSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjM1MTAxIiBCdXlJdGVtSWQ9IjI1NDcyIiBXYWl0VGltZT0iNTAwMCIgSWdub3JlTW9ic0luQmxhY2tzcG90cz0idHJ1ZSIgIFg9Ii02NzQuNDc3NCIgWT0iMjc0My4xMjgiIFo9IjkzLjkxNzMiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNDAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iU3R5eFdvVy5NZS5NYXBJZCA9PSAwIj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IighSGFzSXRlbSgyNTQ3MikpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI0Mzc2OCIgQnV5SXRlbUlkPSIyNTQ3MiIgV2FpdFRpbWU9IjUwMDAiIElnbm9yZU1vYnNJbkJsYWNrc3BvdHM9InRydWUiIFg9Ii04ODI5LjE4IiBZPSI0ODIuMzQiIFo9IjEwOS42MTYiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNDAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iSGFzSXRlbSgyNTQ3MikiPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMjU0NzIpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSIyMDAwIiBHb2FsVGV4dD0iVXNpbmcgaXRlbSB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICA8L1doaWxlPg0KICAgICAgPC9JZj4NCiAgICAgIDxJZiBDb25kaXRpb249Ik1lLklzSG9yZGUiPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IkNvbXBpbGluZyBIb3JkZSBNb3VudCIgTG9nQ29sb3I9Ik9yYW5nZSIgLz4NCiAgICAgICAgIDxJZiBDb25kaXRpb249IlN0eXhXb1cuTWUuTWFwSWQgPT0gNTMwIj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IiFIYXNJdGVtKDI1NDc0KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMzUwOTkiIEJ1eUl0ZW1JZD0iMjU0NzQiIFdhaXRUaW1lPSI0MDAwIiBYPSI0Ny43NjE1MyIgWT0iMjc0Mi4wMjIiIFo9Ijg1LjI3MTE5IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxSdW5UbyBYPSI4MC45MzgyNiIgWT0iMjcxMy4wMjkiIFo9Ijg1LjY5NzIxIiAvPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249IlN0eXhXb1cuTWUuTWFwSWQgPT0gMSI+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIhSGFzSXRlbSgyNTQ3NCkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjQ0OTE4IiBCdXlJdGVtSWQ9IjI1NDc0IiBXYWl0VGltZT0iNDAwMCIgWD0iMTgwNi45NCIgWT0iLTQzNDAuNjciIFo9IjEwMi4wNTA2IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSJIYXNJdGVtKDI1NDc0KSI+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSgyNTQ3NCkiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjIwMDAiIEdvYWxUZXh0PSJVc2luZyBpdGVtIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgIDwvV2hpbGU+DQogICAgICA8L0lmPg0KICAgPC9RdWVzdE9yZGVyPg0KPC9IQlByb2ZpbGU+DQo=";

        #endregion



    }
}
