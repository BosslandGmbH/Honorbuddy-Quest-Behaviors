//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Bots.Quest;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Hooks
{
    [CustomBehaviorFileName(@"Hooks\MountHook")]
    public class MountHook : CustomForcedBehavior
    {
        public MountHook(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            //True = hook running, false = hook stopped
            _state = GetAttributeAsNullable<bool>("state", true, null, null) ?? false;

        }

        private bool _inserted;
        private bool _state;


        public override bool IsDone
        {
            get
            {
                return _inserted;
            }
        }


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


        private int FlightLevel
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


        private bool Hellfire
        {
            get
            {
                return StyxWoW.Me.MapId == 530;
            }
        }


        private bool OldWorld
        {
            get
            {
                return (StyxWoW.Me.MapId == 0 || StyxWoW.Me.MapId == 1);
            }
        }


        //Return the trainer we want based on faction and location and skill.
        private int TrainerId
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


        private Boolean setupQO;

        private void SetupQuestLowbieOrder()
        {
            QBCLog.Info(this, "Starting ground quest order.");
            
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


        private void SetupQuestFlyingOrder()
        {
            QBCLog.Info(this, "Starting flying quest order.");
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


        private Composite PurchaseMount
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => FlightLevel == 1 && !Mount.GroundMounts.Any() && !setupQO,
                        new Action(r => SetupQuestLowbieOrder())),
                    new Decorator(r => FlightLevel == 1 && Mount.GroundMounts.Any() && setupQO,
                        new Action(r => setupQO = false)),
                    new Decorator(r => FlightLevel == 2 && !Mount.FlyingMounts.Any() && !setupQO,
                        new Action(r => SetupQuestFlyingOrder())),
                    new Decorator(r => FlightLevel == 2 && Mount.FlyingMounts.Any() && setupQO,
                        new Action(r => setupQO = false))
                    );
            }
        }


        private void SetupTrainer()
        {
            QBCLog.Info(this, "Creating ForceTrainRiding object");
            var args = new Dictionary<string, string> { { "MobId", TrainerId.ToString() } };

            gooby = new ForceTrainRiding(args);
            gooby.OnStart();
            TreeHooks.Instance.ReplaceHook("GoobyHook", gooby.Branch);
        }


        private void CleanUpCustomForcedBehavior()
        {

            if (gooby.GetType() == typeof(ForceTrainRiding))
            {
                QBCLog.Info(this, "Cleaning up ForceTrainRiding object");
            }
            else
            {
                QBCLog.Info(this, "Cleaning up InteractWith object");
            }

            TreeHooks.Instance.RemoveHook("GoobyHook", gooby.Branch);
            gooby.Dispose();
            gooby = null;
        }


        //Composites
        private static CustomForcedBehavior gooby = null;
        private Composite RunOtherComposite
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


        private Composite HellfireComposite
        {
            get
            {
                return new Decorator(r => Hellfire && Me.Level >= 60 && Me.Gold >= 278 && FlightLevel < 2,
                    new Action(r => SetupTrainer()));
            }
        }


        private Composite OldWordComposite
        {
            get
            {
                return new Decorator(r => OldWorld && ((Me.Level >= 20 && Me.Gold >= 5 && FlightLevel < 1) || (Me.Level >= 60 && Me.Gold >= 278 && FlightLevel < 2)),
                    new Action(r => SetupTrainer()));
            }
        }


        public static Composite _myHook;
        public Composite CreateHook()
        {
            return new Decorator(r => !Me.Combat,
                new PrioritySelector(
                    RunOtherComposite,
                    PurchaseMount,
                    OldWordComposite,
                    HellfireComposite));
        }


        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            if (_state == true)
            {
                if (_myHook == null)
                {
                    QBCLog.Info("Inserting hook");
                    _myHook = CreateHook();
                    TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
					BotEvents.OnBotStarted += BotEvents_OnBotStarted;
                }
                else
                {
                    QBCLog.Info("Insert was requested, but was already present");
                }

                _inserted = true;
            }
            else
            {
                if (_myHook != null)
                {
                    QBCLog.Info("Removing hook");
                    TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
					BotEvents.OnBotStarted -= BotEvents_OnBotStarted;
                    _myHook = null;
                }
                else
                {
                    QBCLog.Info("Remove was requested, but hook was not present");
                }

                _inserted = false;
            }
        }

		void BotEvents_OnBotStarted(EventArgs args)
		{
			// we need to set this to false on bot start so the mount buy questorder is re-inserted when needed.
			setupQO = false;
		}


        #region Profiles
		// Use http://www.freeformatter.com/base64-encoder.html for simple base64 decode/encode
		private const string GroundMounts = "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjxIQlByb2ZpbGU+DQogICA8TmFtZT5NYXN0YWhnIE1vdW50IFRyYWluaW5nPC9OYW1lPg0KICAgPE1pbkxldmVsPjE8L01pbkxldmVsPg0KICAgPE1heExldmVsPjEwMTwvTWF4TGV2ZWw+DQogICA8TWluRHVyYWJpbGl0eT4wLjM8L01pbkR1cmFiaWxpdHk+DQogICA8TWluRnJlZUJhZ1Nsb3RzPjM8L01pbkZyZWVCYWdTbG90cz4NCiAgIDxNYWlsR3JleT5GYWxzZTwvTWFpbEdyZXk+DQogICA8TWFpbFdoaXRlPkZhbHNlPC9NYWlsV2hpdGU+DQogICA8TWFpbEdyZWVuPlRydWU8L01haWxHcmVlbj4NCiAgIDxNYWlsQmx1ZT5UcnVlPC9NYWlsQmx1ZT4NCiAgIDxNYWlsUHVycGxlPlRydWU8L01haWxQdXJwbGU+DQogICA8U2VsbEdyZXk+VHJ1ZTwvU2VsbEdyZXk+DQogICA8U2VsbFdoaXRlPlRydWU8L1NlbGxXaGl0ZT4NCiAgIDxTZWxsR3JlZW4+VHJ1ZTwvU2VsbEdyZWVuPg0KICAgPFNlbGxCbHVlPlRydWU8L1NlbGxCbHVlPg0KICAgPFNlbGxQdXJwbGU+RmFsc2U8L1NlbGxQdXJwbGU+DQogICA8TWFpbGJveGVzPg0KICAgICAgPCEtLSBFbXB0eSBvbiBQdXJwb3NlIC0tPg0KICAgPC9NYWlsYm94ZXM+DQogICA8QmxhY2tzcG90cyAvPg0KICAgPFF1ZXN0T3JkZXIgSWdub3JlQ2hlY2tQb2ludHM9ImZhbHNlIj4NCiAgICAgIDxJZiBDb25kaXRpb249IiFNZS5Jc0hvcmRlIj4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJDb21waWxpbmcgQWxsaWFuY2UgTW91bnQiIExvZ0NvbG9yPSJPcmFuZ2UiIC8+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5IZWFydGhzdG9uZUFyZWFJZCAhPSA1MTQ4Ij4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJNb3ZpbmcgdG8gc2V0IGhlYXJ0aCB0byBTVyBJbm5rZWVwZXIiIExvZ0NvbG9yPSJSZWQiIC8+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNjc0MCIgR29zc2lwT3B0aW9ucz0iMSIgWD0iLTg4NjcuNzg2IiBZPSI2NzMuNjcyOSIgWj0iOTcuOTAzMjQiIC8+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLkh1bWFuIj4NCiAgICAgICAgICAgIDxSdW5UbyBYPSItOTQ0Mi43NDIiIFk9Ii0xMzkwLjY2NiIgWj0iNDYuODcwNDUiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNRdWVzdCgzMjYxOCkiPg0KICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIHRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjE4IiBUdXJuSW5OYW1lPSJSYW5kYWwgSHVudGVyIiBUdXJuSW5JZD0iNDczMiIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMzg0IiBCdXlJdGVtSWQ9IjI0MTQiIFdhaXRUaW1lPSI1MDAwIiBDb2xsZWN0aW9uRGlzdGFuY2U9IjUwIiBYPSItOTQ1NS4zNjUiIFk9Ii0xMzg1LjMyNyIgWj0iNDcuMTI4MTgiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoSGFzSXRlbSgyNDE0KSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMjQxNCkiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5QYW5kYXJlbiI+DQogICAgICAgICAgICA8UnVuVG8gWD0iLTgyMTIuMjIxIiBZPSI1NDcuNTY5IiBaPSIxMTcuMTk0NyIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249Ikhhc1F1ZXN0KDMyNjY1KSI+DQogICAgICAgICAgICAgICA8VHVybkluIFF1ZXN0TmFtZT0iTGVhcm4gdG8gUmlkZSIgUXVlc3RJZD0iMzI2NjUiIFR1cm5Jbk5hbWU9Ik1laSBMaW4iIFR1cm5JbklkPSI3MDI5NiIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNjUwNjgiIEJ1eUl0ZW1JZD0iODc3OTUiIFdhaXRUaW1lPSI1MDAwIiBDb2xsZWN0aW9uRGlzdGFuY2U9IjUwIiBYPSItODIwOS4zNzkiIFk9IjU0Ni4wMjYxIiBaPSIxMTcuNzY4NCIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihIYXNJdGVtKDg3Nzk1KSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoODc3OTUpIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuR25vbWUiPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZXJTZXR0aW5ncyIgTG9vdE1vYnM9IlRydWUiIFVzZUZsaWdodFBhdGhzPSJUcnVlIiBQdWxsRGlzdGFuY2U9IjI1IiAvPg0KICAgICAgICAgICAgPFJ1blRvIFg9Ii01NDU0LjE3MSIgWT0iLTYyMS4wNDgiIFo9IjM5My4zOTY4IiAvPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzUXVlc3QoMzI2NjMpIj4NCiAgICAgICAgICAgICAgIDxUdXJuSW4gUXVlc3ROYW1lPSJMZWFybiB0byBSaWRlIiBRdWVzdElkPSIzMjY2MyIgVHVybkluTmFtZT0iQmluankgRmVhdGhlcndoaXN0bGUiIFR1cm5JbklkPSI3OTU0IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI3OTU1IiBCdXlJdGVtSWQ9Ijg1OTUiIFdhaXRUaW1lPSI1MDAwIiBYPSItNTQ1NC4xNzEiIFk9Ii02MjEuMDQ4IiBaPSIzOTMuMzk2OCIgLz4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihIYXNJdGVtKDg1OTUpKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg4NTk1KSIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLkR3YXJmIj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJVc2VyU2V0dGluZ3MiIExvb3RNb2JzPSJUcnVlIiBVc2VGbGlnaHRQYXRocz0iVHJ1ZSIgUHVsbERpc3RhbmNlPSIyNSIgLz4NCiAgICAgICAgICAgIDxSdW5UbyBYPSItNTUyNC4zNTQiIFk9Ii0xMzQ5Ljg2OCIgWj0iMzk4LjY2NDEiIC8+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNRdWVzdCgzMjY2MikiPg0KICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIHRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjYyIiBUdXJuSW5OYW1lPSJVbHRoYW0gSXJvbmhvcm4iIFR1cm5JbklkPSI0NzcyIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSIxMjYxIiBCdXlJdGVtSWQ9IjU4NzMiIFdhaXRUaW1lPSI1MDAwIiBYPSItNTUzOS41NSIgWT0iLTEzMjIuNTUiIFo9IjM5OC44NjUzIiAvPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKEhhc0l0ZW0oNTg3MykpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNaXNjXFJ1bkx1YSIgTHVhPSJVc2VJdGVtQnlOYW1lKDU4NzMpIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuTmlnaHRFbGYiPg0KICAgICAgICAgICAgPCEtLSBHZXQgb24gYXQgU1csIG9mZiBhdCBSdXQndGhlcmFuIFZpbGxhZ2UgKERhcm5hc3N1cykgLS0+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMCkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZVRyYW5zcG9ydCIgVHJhbnNwb3J0SWQ9IjE3NjMxMCIgV2FpdEF0WD0iLTg2NDAuNTU2IiBXYWl0QXRZPSIxMzMwLjgyOSIgV2FpdEF0Wj0iNS4yMzMyMDciIEdldE9mZlg9IjgxNzcuNTQiIEdldE9mZlk9IjEwMDMuMDc5IiBHZXRPZmZaPSI2LjY0NjE2NCIgU3RhbmRPblg9Ii04NjQ0Ljk1MiIgU3RhbmRPblk9IjEzNDguMTEiIFN0YW5kT25aPSI2LjE0MzA5NCIgVHJhbnNwb3J0U3RhcnRYPSItODY1MC43MTkiIFRyYW5zcG9ydFN0YXJ0WT0iMTM0Ni4wNTEiIFRyYW5zcG9ydFN0YXJ0Wj0iLTAuMDM4MjMzNCIgVHJhbnNwb3J0RW5kWD0iODE2Mi41ODciIFRyYW5zcG9ydEVuZFk9IjEwMDUuMzY1IiBUcmFuc3BvcnRFbmRaPSIwLjA0NzQwMjMiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPCEtLSBUbyBnZXQgaW5zaWRlIG9mIERhcm5hc3N1cyAtLT4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5NYXBJZCA9PSAxKSI+DQogICAgICAgICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSIoTWUuWiAmbHQ7IDEwMCkiPg0KICAgICAgICAgICAgICAgICAgPFJ1blRvIFg9IjgzNzUuNTc5IiBZPSI5OTcuNjUxNyIgWj0iMjcuNDU3NjgiIC8+DQogICAgICAgICAgICAgICAgICA8IS0tIFJlZCBwb3J0YWwgdXAgdG8gRGFybmFzc3VzIC0tPg0KICAgICAgICAgICAgICAgICAgPFJ1blRvIFg9IjgzODYuOTQzIiBZPSI5OTkuNjI1NiIgWj0iMjkuODAxMTQiIC8+DQogICAgICAgICAgICAgICAgICA8IS0tIEluc2lkZSBwb3J0YWwgLS0+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yIHBvcnQgdXAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgICAgPC9XaGlsZT4NCiAgICAgICAgICAgICAgIDxJZiBDb25kaXRpb249IihNZS5aICZndDsgMTAwMCkiPg0KICAgICAgICAgICAgICAgICAgPFJ1blRvIFg9IjEwMTI5Ljc4IiBZPSIyNTI2LjU5NSIgWj0iMTMyNC44MjgiIC8+DQogICAgICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNRdWVzdCgzMjY2NCkiPg0KICAgICAgICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIHRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjY0IiBUdXJuSW5OYW1lPSJKYXJ0c2FtIiBUdXJuSW5JZD0iNDc1MyIgLz4NCiAgICAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNDczMCIgQnV5SXRlbUlkPSI4NjI5IiBXYWl0VGltZT0iNTAwMCIgWD0iMTAxMjkuOTEiIFk9IjI1MzMuMjQ1IiBaPSIxMzIzLjI3MSIgLz4NCiAgICAgICAgICAgICAgICAgIDwhLS08Q3VzdG9tQmVoYXZpb3IgRmlsZT0iRm9yY2VUcmFpblJpZGluZyIgTW9iSWQ9IjQ3NTMiIC8+IC0tPg0KICAgICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKEhhc0l0ZW0oODYyOSkpIj4NCiAgICAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNaXNjXFJ1bkx1YSIgTHVhPSJVc2VJdGVtQnlOYW1lKDg2MjkpIiAvPg0KICAgICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5EcmFlbmVpIj4NCiAgICAgICAgICAgIDwhLS0gR2V0IG9uIGF0IFNXLCBvZmYgYXQgUnV0J3RoZXJhbiBWaWxsYWdlIChEYXJuYXNzdXMpIC0tPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKE1lLk1hcElkID09IDApIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJVc2VUcmFuc3BvcnQiIFRyYW5zcG9ydElkPSIxNzYzMTAiIFdhaXRBdFg9Ii04NjQwLjU1NiIgV2FpdEF0WT0iMTMzMC44MjkiIFdhaXRBdFo9IjUuMjMzMjA3IiBHZXRPZmZYPSI4MTc3LjU0IiBHZXRPZmZZPSIxMDAzLjA3OSIgR2V0T2ZmWj0iNi42NDYxNjQiIFN0YW5kT25YPSItODY0NC45NTIiIFN0YW5kT25ZPSIxMzQ4LjExIiBTdGFuZE9uWj0iNi4xNDMwOTQiIFRyYW5zcG9ydFN0YXJ0WD0iLTg2NTAuNzE5IiBUcmFuc3BvcnRTdGFydFk9IjEzNDYuMDUxIiBUcmFuc3BvcnRTdGFydFo9Ii0wLjAzODIzMzQiIFRyYW5zcG9ydEVuZFg9IjgxNjIuNTg3IiBUcmFuc3BvcnRFbmRZPSIxMDA1LjM2NSIgVHJhbnNwb3J0RW5kWj0iMC4wNDc0MDIzIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDwhLS0gVG8gZ2V0IGluc2lkZSBvZiBEYXJuYXNzdXMgLS0+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMSkiPg0KICAgICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iKE1lLlogJmx0OyAxMDApICZhbXA7JmFtcDsgKE1lLlogJmd0OyAwKSI+DQogICAgICAgICAgICAgICAgICA8UnVuVG8gWD0iODM3NS41NzkiIFk9Ijk5Ny42NTE3IiBaPSIyNy40NTc2OCIgLz4NCiAgICAgICAgICAgICAgICAgIDwhLS0gUmVkIHBvcnRhbCB1cCB0byBEYXJuYXNzdXMgLS0+DQogICAgICAgICAgICAgICAgICA8UnVuVG8gWD0iODM4Ni45NDMiIFk9Ijk5OS42MjU2IiBaPSIyOS44MDExNCIgLz4NCiAgICAgICAgICAgICAgICAgIDwhLS0gSW5zaWRlIHBvcnRhbCAtLT4NCiAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI1MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgcG9ydCB1cCB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuWm9uZUlkID09IDE2NTciPg0KICAgICAgICAgICAgICAgICAgPCEtLSBEYXJuYXNzdXMgLS0+DQogICAgICAgICAgICAgICAgICA8UnVuVG8gWD0iOTY1NS4yNTIiIFk9IjI1MDkuMzMiIFo9IjEzMzEuNTk4IiAvPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjIwNzk5NSIgT2JqZWN0VHlwZT0iR2FtZU9iamVjdCIgUmFuZ2U9IjUiIFg9Ijk2NTUuMjUyIiBZPSIyNTA5LjMzIiBaPSIxMzMxLjU5OCIgLz4NCiAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI4MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5ab25lSWQgPT0gMzU1NyI+DQogICAgICAgICAgICAgICAgICA8IS0tIEV4b2RhciAtLT4NCiAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJMZWFybmluZyBEcmFlbmVpIE1vdW50IiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KICAgICAgICAgICAgICAgICAgPFJ1blRvIFg9Ii0zOTgxLjc2OSIgWT0iLTExOTI5LjE0IiBaPSItMC4yNDE5NDEyIiAvPg0KICAgICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKChIYXNRdWVzdCgzMjY2MSkpICZhbXA7JmFtcDsgKElzUXVlc3RDb21wbGV0ZWQoMzI2NjEpKSkiPg0KICAgICAgICAgICAgICAgICAgICAgPFR1cm5JbiBRdWVzdE5hbWU9IkxlYXJuIFRvIFJpZGUiIFF1ZXN0SWQ9IjMyNjYxIiBUdXJuSW5OYW1lPSJBYWx1biIgVHVybkluSWQ9IjIwOTE0IiBYPSItMzk4MS43NjkiIFk9Ii0xMTkyOS4xNCIgWj0iLTAuMjQxOTQxMiIgLz4NCiAgICAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoKEhhc1F1ZXN0KDE0MDgyKSkgJmFtcDsmYW1wOyAoSXNRdWVzdENvbXBsZXRlZCgxNDA4MikpKSI+DQogICAgICAgICAgICAgICAgICAgICA8VHVybkluIFF1ZXN0TmFtZT0iTGVhcm4gVG8gUmlkZSIgUXVlc3RJZD0iMTQwODIiIFR1cm5Jbk5hbWU9IkFhbHVuIiBUdXJuSW5JZD0iMjA5MTQiIFg9Ii0zOTgxLjc2OSIgWT0iLTExOTI5LjE0IiBaPSItMC4yNDE5NDEyIiAvPg0KICAgICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgICAgICAgIDxSdW5UbyBYPSItMzk4MS43NjkiIFk9Ii0xMTkyOS4xNCIgWj0iLTAuMjQxOTQxMiIgLz4NCiAgICAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSIxNzU4NCIgQnV5SXRlbUlkPSIyODQ4MSIgV2FpdFRpbWU9IjUwMDAiIENvbGxlY3Rpb25EaXN0YW5jZT0iNTAiIFg9Ii0zOTgxLjc2OSIgWT0iLTExOTI5LjE0IiBaPSItMC4yNDE5NDEyIiAvPg0KICAgICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKEhhc0l0ZW0oMjg0ODEpKSI+DQogICAgICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSgyODQ4MSkiIC8+DQogICAgICAgICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZXJTZXR0aW5ncyIgVXNlTW91bnQ9IlRydWUiIExvb3RNb2JzPSJUcnVlIiBQdWxsRGlzdGFuY2U9IjI1IiAvPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IlVzaW5nIEhlYXJ0aHN0b25lIiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KCQk8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iVXNlSGVhcnRoc3RvbmUiIFdhaXRGb3JDRD0idHJ1ZSIgLz4NCiAgICAgIDwvSWY+DQogICAgICA8SWYgQ29uZGl0aW9uPSJNZS5Jc0hvcmRlIj4NCiAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJDb21waWxpbmcgSG9yZGUgTW91bnQiIExvZ0NvbG9yPSJPcmFuZ2UiIC8+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5IZWFydGhzdG9uZUFyZWFJZCAhPSA1MTcwIj4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJNZXNzYWdlIiBUZXh0PSJNb3ZpbmcgdG8gc2V0IGhlYXJ0aCB0byBPcmcgSW5ua2VlcGVyIiBMb2dDb2xvcj0iUmVkIiAvPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjY5MjkiIEdvc3NpcE9wdGlvbnM9IjEiIFg9IjE1NzMuMjY2IiBZPSItNDQzOS4xNTgiIFo9IjE2LjA1NjMxIiAvPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5PcmMiPg0KICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iIUhhc0l0ZW0oNTY2NSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjMzNjIiIEJ1eUl0ZW1JZD0iNTY2NSIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9IjIwNzYuNjAyIiBZPSItNDU2OC42MzIiIFo9IjQ5LjI1MzE5IiAvPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjUwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgPC9XaGlsZT4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249Ikhhc0l0ZW0oNTY2NSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoNTY2NSkiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8SWYgQ29uZGl0aW9uPSJNZS5SYWNlID09IFdvV1JhY2UuR29ibGluIj4NCiAgICAgICAgICAgIDxXaGlsZSBDb25kaXRpb249IiFIYXNJdGVtKDYyNDYxKSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNDg1MTAiIEJ1eUl0ZW1JZD0iNjI0NjEiIEdvc3NpcE9wdGlvbnM9IjEiIFdhaXRUaW1lPSI1MDAwIiBYPSIxNDc1LjMyIiBZPSItNDE0MC45OCIgWj0iNTIuNTEiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSg2MjQ2MSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoNjI0NjEpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLlRyb2xsIj4NCiAgICAgICAgICAgIDxXaGlsZSBDb25kaXRpb249IiFIYXNJdGVtKDg1ODgpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI3OTUyIiBCdXlJdGVtSWQ9Ijg1ODgiIEdvc3NpcE9wdGlvbnM9IjEiIFdhaXRUaW1lPSI1MDAwIiBYPSItODUyLjc4IiBZPSItNDg4NS40MCIgWj0iMjIuMDMiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSg4NTg4KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSg4NTg4KSIgV2FpdFRpbWU9IjEwMDAiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5UYXVyZW4iPg0KICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iIUhhc0l0ZW0oMTUyNzcpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSIzNjg1IiBCdXlJdGVtSWQ9IjE1Mjc3IiBHb3NzaXBPcHRpb25zPSIxIiBXYWl0VGltZT0iNTAwMCIgUmFuZ2U9IjIiIFg9Ii0yMjc5Ljc5NiIgWT0iLTM5Mi4wNjk3IiBaPSItOS4zOTY4NjMiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSgxNTI3NykiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMTUyNzcpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSI1MDAwIiBHb2FsVGV4dD0iV2FpdGluZyBmb3IgIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iTWUuUmFjZSA9PSBXb1dSYWNlLlVuZGVhZCI+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZVRyYW5zcG9ydCIgVHJhbnNwb3J0SWQ9IjE2NDg3MSIgV2FpdEF0WD0iMTg0NS4xODciIFdhaXRBdFk9Ii00Mzk1LjU1NSIgV2FpdEF0Wj0iMTM1LjIzMDYiIFRyYW5zcG9ydFN0YXJ0WD0iMTgzMy41MDkiIFRyYW5zcG9ydFN0YXJ0WT0iLTQzOTEuNTQzIiBUcmFuc3BvcnRTdGFydFo9IjE1Mi43Njc5IiBUcmFuc3BvcnRFbmRYPSIyMDYyLjM3NiIgVHJhbnNwb3J0RW5kWT0iMjkyLjk5OCIgVHJhbnNwb3J0RW5kWj0iMTE0Ljk3MyIgU3RhbmRPblg9IjE4MzUuNTA5IiBTdGFuZE9uWT0iLTQzODUuNzg1IiBTdGFuZE9uWj0iMTM1LjA0MzYiIEdldE9mZlg9IjIwNjUuMDQ5IiBHZXRPZmZZPSIyODMuMTM4MSIgR2V0T2ZmWj0iOTcuMDMxNTYiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKE1lLk1hcElkID09IDApIj4NCiAgICAgICAgICAgICAgIDxXaGlsZSBDb25kaXRpb249IiFIYXNJdGVtKDQ2MzA4KSI+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iNDczMSIgQnV5SXRlbUlkPSI0NjMwOCIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9IjIyNzUuMDgiIFk9IjIzNy4wMCIgWj0iMzMuNjkiIC8+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICAgICA8L1doaWxlPg0KICAgICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iSGFzSXRlbSg0NjMwOCkiPg0KICAgICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoNDYzMDgpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgICAgIDwvSWY+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249Ik1lLlJhY2UgPT0gV29XUmFjZS5CbG9vZEVsZiI+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIoTWUuTWFwSWQgPT0gMSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZVRyYW5zcG9ydCIgVHJhbnNwb3J0SWQ9IjE2NDg3MSIgV2FpdEF0WD0iMTg0NS4xODciIFdhaXRBdFk9Ii00Mzk1LjU1NSIgV2FpdEF0Wj0iMTM1LjIzMDYiIFRyYW5zcG9ydFN0YXJ0WD0iMTgzMy41MDkiIFRyYW5zcG9ydFN0YXJ0WT0iLTQzOTEuNTQzIiBUcmFuc3BvcnRTdGFydFo9IjE1Mi43Njc5IiBUcmFuc3BvcnRFbmRYPSIyMDYyLjM3NiIgVHJhbnNwb3J0RW5kWT0iMjkyLjk5OCIgVHJhbnNwb3J0RW5kWj0iMTE0Ljk3MyIgU3RhbmRPblg9IjE4MzUuNTA5IiBTdGFuZE9uWT0iLTQzODUuNzg1IiBTdGFuZE9uWj0iMTM1LjA0MzYiIEdldE9mZlg9IjIwNjUuMDQ5IiBHZXRPZmZZPSIyODMuMTM4MSIgR2V0T2ZmWj0iOTcuMDMxNTYiIC8+DQogICAgICAgICAgICA8L0lmPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKE1lLk1hcElkID09IDApIj4NCiAgICAgICAgICAgICAgIDxSdW5UbyBYPSIxODA1Ljg3NyIgWT0iMzQ1LjAwMDYiIFo9IjcwLjc5MDAyIiAvPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjIwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iKE1lLlpvbmVJZCA9PSAxNDk3KSI+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMTg0NTAzIiBPYmplY3RUeXBlPSJHYW1lT2JqZWN0IiBQcmVJbnRlcmFjdE1vdW50U3RyYXRlZ3k9IkRpc21vdW50IiBSYW5nZT0iOCIgV2FpdFRpbWU9IjUwMDAiIFg9IjE4MDUuODc3IiBZPSIzNDUuMDAwNiIgWj0iNzAuNzkwMDIiIC8+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iMTAwMDAiIEdvYWxUZXh0PSJXYWl0aW5nIGZvciAge1RpbWVSZW1haW5pbmd9IiAvPg0KICAgICAgICAgICAgICAgPC9XaGlsZT4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSIxNjI2NCIgQnV5SXRlbUlkPSIyOTIyMSIgR29zc2lwT3B0aW9ucz0iMSIgV2FpdFRpbWU9IjUwMDAiIFg9IjkyNDQuNTkiIFk9Ii03NDkxLjU2NiIgWj0iMzYuOTE0MDEiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNTAwMCIgR29hbFRleHQ9IldhaXRpbmcgZm9yICB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSJIYXNJdGVtKDI5MjIxKSI+DQogICAgICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSgyOTIyMSkiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IkNvbXBsZXRlZCB0cmFpbmluZyBzZXNzaW9uIiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IlVzaW5nIEhlYXJ0aHN0b25lIiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IlVzZUhlYXJ0aHN0b25lIiBXYWl0Rm9yQ0Q9InRydWUiIC8+DQogICAgICA8L0lmPg0KICAgPC9RdWVzdE9yZGVyPg0KPC9IQlByb2ZpbGU+DQo=";
        private const string FlyingMounts = "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjxIQlByb2ZpbGU+DQogICA8TmFtZT5NYXN0YWhnIEZseWluZyBNb3VudCBUcmFpbmluZzwvTmFtZT4NCiAgIDxNaW5MZXZlbD4xPC9NaW5MZXZlbD4NCiAgIDxNYXhMZXZlbD4xMDE8L01heExldmVsPg0KICAgPE1pbkR1cmFiaWxpdHk+MC4zPC9NaW5EdXJhYmlsaXR5Pg0KICAgPE1pbkZyZWVCYWdTbG90cz4zPC9NaW5GcmVlQmFnU2xvdHM+DQogICA8TWFpbEdyZXk+RmFsc2U8L01haWxHcmV5Pg0KICAgPE1haWxXaGl0ZT5GYWxzZTwvTWFpbFdoaXRlPg0KICAgPE1haWxHcmVlbj5UcnVlPC9NYWlsR3JlZW4+DQogICA8TWFpbEJsdWU+VHJ1ZTwvTWFpbEJsdWU+DQogICA8TWFpbFB1cnBsZT5UcnVlPC9NYWlsUHVycGxlPg0KICAgPFNlbGxHcmV5PlRydWU8L1NlbGxHcmV5Pg0KICAgPFNlbGxXaGl0ZT5UcnVlPC9TZWxsV2hpdGU+DQogICA8U2VsbEdyZWVuPlRydWU8L1NlbGxHcmVlbj4NCiAgIDxTZWxsQmx1ZT5UcnVlPC9TZWxsQmx1ZT4NCiAgIDxTZWxsUHVycGxlPkZhbHNlPC9TZWxsUHVycGxlPg0KICAgPE1haWxib3hlcz4NCiAgICAgIDwhLS0gRW1wdHkgb24gUHVycG9zZSAtLT4NCiAgIDwvTWFpbGJveGVzPg0KICAgPEJsYWNrc3BvdHMgLz4NCiAgIDxRdWVzdE9yZGVyIElnbm9yZUNoZWNrUG9pbnRzPSJmYWxzZSI+DQogICAgICA8SWYgQ29uZGl0aW9uPSIhTWUuSXNIb3JkZSI+DQogICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWVzc2FnZSIgVGV4dD0iQ29tcGlsaW5nIEFsbGlhbmNlIE1vdW50IiBMb2dDb2xvcj0iT3JhbmdlIiAvPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iU3R5eFdvVy5NZS5NYXBJZCA9PSA1MzAiPg0KICAgICAgICAgICAgPElmIENvbmRpdGlvbj0iKCFIYXNJdGVtKDI1NDcyKSkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjM1MTAxIiBCdXlJdGVtSWQ9IjI1NDcyIiBXYWl0VGltZT0iNTAwMCIgSWdub3JlTW9ic0luQmxhY2tzcG90cz0idHJ1ZSIgIFg9Ii02NzQuNDc3NCIgWT0iMjc0My4xMjgiIFo9IjkzLjkxNzMiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNDAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPElmIENvbmRpdGlvbj0iU3R5eFdvVy5NZS5NYXBJZCA9PSAwIj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IighSGFzSXRlbSgyNTQ3MikpIj4NCiAgICAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJJbnRlcmFjdFdpdGgiIE1vYklkPSI0Mzc2OCIgQnV5SXRlbUlkPSIyNTQ3MiIgV2FpdFRpbWU9IjUwMDAiIElnbm9yZU1vYnNJbkJsYWNrc3BvdHM9InRydWUiIFg9Ii04ODI5LjE4IiBZPSI0ODIuMzQiIFo9IjEwOS42MTYiIC8+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iV2FpdFRpbWVyIiBXYWl0VGltZT0iNDAwMCIgLz4NCiAgICAgICAgICAgIDwvSWY+DQogICAgICAgICA8L0lmPg0KICAgICAgICAgPFdoaWxlIENvbmRpdGlvbj0iSGFzSXRlbSgyNTQ3MikiPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1pc2NcUnVuTHVhIiBMdWE9IlVzZUl0ZW1CeU5hbWUoMjU0NzIpIiBXYWl0VGltZT0iMTAwMCIgLz4NCiAgICAgICAgICAgIDxDdXN0b21CZWhhdmlvciBGaWxlPSJXYWl0VGltZXIiIFdhaXRUaW1lPSIyMDAwIiBHb2FsVGV4dD0iVXNpbmcgaXRlbSB7VGltZVJlbWFpbmluZ30iIC8+DQogICAgICAgICA8L1doaWxlPg0KICAgICAgPC9JZj4NCiAgICAgIDxJZiBDb25kaXRpb249Ik1lLklzSG9yZGUiPg0KICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9Ik1lc3NhZ2UiIFRleHQ9IkNvbXBpbGluZyBIb3JkZSBNb3VudCIgTG9nQ29sb3I9Ik9yYW5nZSIgLz4NCiAgICAgICAgIDxJZiBDb25kaXRpb249IlN0eXhXb1cuTWUuTWFwSWQgPT0gNTMwIj4NCiAgICAgICAgICAgIDxJZiBDb25kaXRpb249IiFIYXNJdGVtKDI1NDc0KSI+DQogICAgICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iSW50ZXJhY3RXaXRoIiBNb2JJZD0iMzUwOTkiIEJ1eUl0ZW1JZD0iMjU0NzQiIFdhaXRUaW1lPSI0MDAwIiBYPSI0Ny43NjE1MyIgWT0iMjc0Mi4wMjIiIFo9Ijg1LjI3MTE5IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgICAgIDxSdW5UbyBYPSI4MC45MzgyNiIgWT0iMjcxMy4wMjkiIFo9Ijg1LjY5NzIxIiAvPg0KICAgICAgICAgPC9JZj4NCiAgICAgICAgIDxJZiBDb25kaXRpb249IlN0eXhXb1cuTWUuTWFwSWQgPT0gMSI+DQogICAgICAgICAgICA8SWYgQ29uZGl0aW9uPSIhSGFzSXRlbSgyNTQ3NCkiPg0KICAgICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IkludGVyYWN0V2l0aCIgTW9iSWQ9IjQ0OTE4IiBCdXlJdGVtSWQ9IjI1NDc0IiBXYWl0VGltZT0iNDAwMCIgWD0iMTgwNi45NCIgWT0iLTQzNDAuNjciIFo9IjEwMi4wNTA2IiAvPg0KICAgICAgICAgICAgPC9JZj4NCiAgICAgICAgIDwvSWY+DQogICAgICAgICA8V2hpbGUgQ29uZGl0aW9uPSJIYXNJdGVtKDI1NDc0KSI+DQogICAgICAgICAgICA8Q3VzdG9tQmVoYXZpb3IgRmlsZT0iTWlzY1xSdW5MdWEiIEx1YT0iVXNlSXRlbUJ5TmFtZSgyNTQ3NCkiIFdhaXRUaW1lPSIxMDAwIiAvPg0KICAgICAgICAgICAgPEN1c3RvbUJlaGF2aW9yIEZpbGU9IldhaXRUaW1lciIgV2FpdFRpbWU9IjIwMDAiIEdvYWxUZXh0PSJVc2luZyBpdGVtIHtUaW1lUmVtYWluaW5nfSIgLz4NCiAgICAgICAgIDwvV2hpbGU+DQogICAgICA8L0lmPg0KICAgPC9RdWVzdE9yZGVyPg0KPC9IQlByb2ZpbGU+DQo=";

        #endregion
    }
}
