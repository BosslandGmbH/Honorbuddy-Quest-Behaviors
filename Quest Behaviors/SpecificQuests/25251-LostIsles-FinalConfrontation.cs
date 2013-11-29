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
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FinalConfrontation
{
    [CustomBehaviorFileName(@"SpecificQuests\25251-LostIsles-FinalConfrontation")]
    public class q25251 : CustomForcedBehavior
	{
		public q25251(Dictionary<string, string> args)
            : base(args)
		{
            QBCLog.BehaviorLoggingContext = this;
		}
    
        
        public static LocalPlayer Me = StyxWoW.Me;
		static public bool OnCooldown1 { get { return Lua.GetReturnVal<int>("a,b,c=GetActionCooldown(121);if b==0 then return 1 else return 0 end", 0) == 0; } }
		static public bool OnCooldown2 { get { return Lua.GetReturnVal<int>("a,b,c=GetActionCooldown(122);if b==0 then return 1 else return 0 end", 0) == 0; } }
		static public bool OnCooldown3 { get { return Lua.GetReturnVal<int>("a,b,c=GetActionCooldown(123);if b==0 then return 1 else return 0 end", 0) == 0; } }
		WoWPoint startloc = new WoWPoint(2298.823, 2433.5, 26.45126);
		WoWPoint flyloc = new WoWPoint(2120.643, 2402.012, 49.6927);
		WoWPoint temploc = new WoWPoint(2400.707, 2532.421, 4.890985);
        private bool locreached;
		public List<WoWUnit> objmob
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 39582 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
		public List<WoWUnit> flylist
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 39592 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(


                    new Decorator(ret => Me.QuestLog.GetQuestById(25251) != null && Me.QuestLog.GetQuestById(25251).IsCompleted,
						new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                            new WaitContinue(120,
                            new Action(delegate
                            {
                                _isDone = true;
                                return RunStatus.Success;
                            })))),
                    new Decorator(ret => !Query.IsInVehicle(),
						new Action(ret =>
						{
							if (flylist.Count == 0)
							{
								Navigator.MoveTo(flyloc);
								StyxWoW.Sleep(1000);
							}
                            if (flylist.Count > 0 && flylist[0].Location.Distance(Me.Location) > 5)
							{
								Navigator.MoveTo(flylist[0].Location);
								StyxWoW.Sleep(1000);
							}
                            if (flylist.Count > 0 && flylist[0].Location.Distance(Me.Location) <= 5)
							{
								WoWMovement.MoveStop();
								flylist[0].Interact();
								StyxWoW.Sleep(1000);
							}
						})),
                    new Decorator(ret => Query.IsInVehicle(),
						new Action(ret =>
						{
                            if (!Query.IsInVehicle())
								return RunStatus.Success;
                            if (Me.QuestLog.GetQuestById(25251).IsCompleted)
							{
                                while (Me.Location.Distance(flyloc) > 10)
								{
									Navigator.MoveTo(flyloc);
									StyxWoW.Sleep(1000);
								}
								Lua.DoString("VehicleExit()");
								return RunStatus.Success;
							}
							if (objmob.Count == 0)
							{
                                if (Me.Location.Distance(temploc) <= 7)
									locreached = true;
								if (!locreached)
								{
									Navigator.MoveTo(temploc);
									StyxWoW.Sleep(1000);
								}
								else
								Navigator.MoveTo(startloc);
								StyxWoW.Sleep(1000);
							}
                            if (objmob.Count > 0 && (objmob[0].Location.Distance(Me.Location) > 40 || !objmob[0].InLineOfSight))
							{
								
								objmob[0].Target();
                                if (Me.Location.Distance(temploc) <= 7)
									locreached = true;
								if (!locreached)
								{
									Navigator.MoveTo(temploc);
									StyxWoW.Sleep(1000);
								}
								if (locreached)
								{
									Navigator.MoveTo(objmob[0].Location);
									StyxWoW.Sleep(1000);
								}
								StyxWoW.Sleep(1000);
							}
                            if (objmob.Count > 0 && objmob[0].Location.Distance(Me.Location) <= 40 && objmob[0].InLineOfSight)
							{
								WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
								WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
								objmob[0].Target();
								objmob[0].Face();
								if (!OnCooldown3)
								Lua.DoString("UseAction(123, 'target', 'LeftButton')");
								if (!OnCooldown2)
								Lua.DoString("UseAction(122, 'target', 'LeftButton')");
                                if (!OnCooldown1 && objmob[0].Location.Distance(Me.Location) <= 10)
								Lua.DoString("UseAction(121, 'target', 'LeftButton')");
							}
							return RunStatus.Running;
						}
					))
                )
			);
        }


        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }


        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                this.UpdateGoalText(0);
            }
        }
    }
}

