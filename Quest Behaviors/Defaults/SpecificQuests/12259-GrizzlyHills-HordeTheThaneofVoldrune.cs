using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

	/* This behavior is for killing Thane noobface in Grizzly Hills (Horde 12259 and Alliance 12255) 
		This behavior was developed by Kickazz006
		Code was taken from Shak
		How I used it in this behavior was chop each in half and take the bits that I needed
		Feel free to re-use the code to your liking (anyone else)
	*/


namespace Styx.Bot.Quest_Behaviors
{
    public class q12259 : CustomForcedBehavior
    {
		public q12259(Dictionary<string, string> args)
					: base(args)
				{
					QuestId =  12259;
					Location = WoWPoint.Empty;
					Endloc = WoWPoint.Empty;
					QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
					QuestRequirementInLog    = QuestInLogRequirement.InLog;
				}
			
        public WoWPoint Location { get; private set; }
		public WoWPoint Endloc { get; private set; }
        public int QuestId { get; set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public static LocalPlayer me = StyxWoW.Me;
		static public bool Obj1Done { get { return Lua.GetReturnVal<int>("a,b,c=GetQuestLogLeaderBoard(1,GetQuestLogIndexByID(12259));if c==1 then return 1 else return 0 end", 0) == 1; } }
		static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') or not(GetBonusBarOffset()==0) then return 1 else return 0 end", 0) == 1; } }
		WoWPoint endloc = new WoWPoint(2798.203, -2510.08, 99.77123);
		WoWPoint startloc = new WoWPoint(2939.488, -2525.839, 127.3586);
		WoWPoint flyloc = new WoWPoint(2788.155, -2508.851, 56.05595);
		
        #region Overrides of CustomForcedBehavior
		public List<WoWUnit> objmob
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 27377 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
		public List<WoWUnit> flylist
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 27292 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
					
					
                    new Decorator(ret => me.QuestLog.GetQuestById(12259) !=null && me.QuestLog.GetQuestById(12259).IsCompleted,
						new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                            new WaitContinue(120,
                            new Action(delegate
                            {
                                _isDone = true;
                                return RunStatus.Success;
                            })))),
					new Decorator(ret => !InVehicle,
						new Action(ret =>
						{
							if (flylist.Count == 0)
							{
								Navigator.MoveTo(flyloc);
								Thread.Sleep(1000);
							}
							if (flylist.Count > 0 && flylist[0].Location.Distance(me.Location) > 5)
							{
								Navigator.MoveTo(flylist[0].Location);
								Thread.Sleep(1000);
							}
							if (flylist.Count > 0 && flylist[0].Location.Distance(me.Location) <= 5)
							{
								WoWMovement.MoveStop();
								flylist[0].Interact();
								Thread.Sleep(1000);
								Lua.DoString("SelectGossipOption(1)");
                                Thread.Sleep(1000);
							}
						})),
					new Decorator(ret => InVehicle,
						new Action(ret =>
						{
							if (!InVehicle)
								return RunStatus.Success;
							if (me.QuestLog.GetQuestById(12259).IsCompleted)
							{
								if (me.Location.Distance(endloc) > 15)
								{
									WoWMovement.ClickToMove(endloc);
									Thread.Sleep(5000);
								}
								Lua.DoString("VehicleExit()");
								return RunStatus.Success;
							}
							if (objmob.Count == 0)
							{
								WoWMovement.ClickToMove(startloc);
								Thread.Sleep(1000);
							}
							if (objmob.Count > 0)
							{
								objmob[0].Target();
                                objmob[0].Face();
								//WoWMovement.ClickToMove(objmob[0].Location);
                                if (me.IsMoving || me.CharmedUnit.IsMoving)
                                    WoWMovement.ClickToMove(me.CharmedUnit.Location);
								//Thread.Sleep(100);
								//Lua.DoString("UseAction(122, 'target', 'LeftButton')");
								//Lua.DoString("UseAction(121, 'target', 'LeftButton')");
                                Lua.DoString("CastPetAction(1)");
                                Lua.DoString("CastPetAction(2)");
                                Lua.DoString("CastPetAction(3)");
							}
							return RunStatus.Running;
						}
					)),
					
					new DecoratorContinue(ret => !Obj1Done && objmob[0].Location.Distance(me.Location) <= 20,
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "PWNing " +objmob[0].Name),
							new Action(ret => Lua.DoString("VehicleMenuBarActionButton2:Click()")),
							//new Action(ret => Thread.Sleep(1500)),
							//new Action(ret => Lua.DoString("VehicleMenuBarActionButton3:Click()")),
							new Action(ret => Lua.DoString("VehicleMenuBarActionButton1:Click()")),
							new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
							new Action(ret => StyxWoW.SleepForLagDuration()),
							new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards)),
							new Action(ret => StyxWoW.SleepForLagDuration()),
							new Action(ret => objmob[0].Face()),
							new Action(ret => Thread.Sleep(500))
						)
					)
                )
			);
        }
        
        

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}