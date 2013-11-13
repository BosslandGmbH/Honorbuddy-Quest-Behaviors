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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.MrDsWildRide
{
    [CustomBehaviorFileName(@"SpecificQuests\26028-Stonetalons-MrDsWildRide")]
    public class q26028 : CustomForcedBehavior
    {
        
        public q26028(Dictionary<string, string> args)
            : base(args)
        {
            QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
            Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
            Location = GetAttributeAsNullable<WoWPoint>("end", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
            QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
            QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
        }

        public WoWPoint Location { get; private set; }
		public WoWPoint Endloc { get; private set; }
        public int QuestId { get; set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public static LocalPlayer Me = StyxWoW.Me;
		static public bool Obj1Done { get { return Lua.GetReturnVal<int>("a,b,c=GetQuestLogLeaderBoard(1,GetQuestLogIndexByID(26028));if c==1 then return 1 else return 0 end", 0) == 1; } }
		static public bool Obj2Done { get { return Lua.GetReturnVal<int>("a,b,c=GetQuestLogLeaderBoard(2,GetQuestLogIndexByID(26028));if c==1 then return 1 else return 0 end", 0) == 1; } }
		
        #region Overrides of CustomForcedBehavior
		public List<WoWUnit> mob1List
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 35203 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
        public List<WoWUnit> mob2List
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 35334 && !u.IsDead))
                                    .OrderBy(u => u.Distance).ToList();
            }
        }
		
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
					
                    new Decorator(ret => !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete) && Endloc.Distance(Me.Location) <= 3,
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Finished!"),
							new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarLeaveButton','0')")),
							new Action(ret => StyxWoW.SleepForLagDuration()),
                            new WaitContinue(120,
                            new Action(delegate
                            {
                                _isDone = true;
                                return RunStatus.Success;
                                }))
                    )),
					new Decorator(ret => (!Me.IsMoving && !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete)) && Endloc.Distance(Me.Location) > 3,
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Moving To Camp"),
							new Action(ret => Navigator.MoveTo(Endloc)),
							new Action(ret => StyxWoW.SleepForLagDuration())
                        )
                    ),
					new Decorator(ret => (!Me.IsMoving && (mob1List.Count == 0 || mob2List.Count == 0)),
						new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Moving To Location"),
                            new Action(ret => Navigator.MoveTo(Location)),
							new Action(ret => StyxWoW.SleepForLagDuration())
                        )
                    ),
					new Decorator(ret => mob1List.Count > 0 || mob2List.Count > 0,
						new Sequence(
							new DecoratorContinue(ret => !Me.IsMoving && !Obj1Done && mob1List[0].Location.Distance(Me.Location) > 30,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Moving to " +mob1List[0].Name),
									new Action(ret => Navigator.MoveTo(mob1List[0].Location)),
									new Action(ret => StyxWoW.SleepForLagDuration())
								)
							),
							new DecoratorContinue(ret => !Obj1Done && mob1List[0].Location.Distance(Me.Location) <= 30,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "PWNing " +mob1List[0].Name),
									new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton1','0')")),
									new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton3','0')")),
									new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
									new Action(ret => StyxWoW.SleepForLagDuration()),
									new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards)),
									new Action(ret => StyxWoW.SleepForLagDuration()),
									new Action(ret => mob1List[0].Face()),
									new Sleep(2000)
								)
							),
							new DecoratorContinue(ret => !Me.IsMoving && !Obj2Done && Obj1Done && mob2List[0].Location.Distance(Me.Location) > 30,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Moving to " +mob2List[0].Name),
									new Action(ret => Navigator.MoveTo(mob2List[0].Location)),
									new Action(ret => StyxWoW.SleepForLagDuration())
								)
							),
							new DecoratorContinue(ret => !Obj2Done && Obj1Done && mob2List[0].Location.Distance(Me.Location) <= 30,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "PWNing " +mob2List[0].Name),
									new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton1','0')")),
									new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton3','0')")),
									new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
									new Action(ret => StyxWoW.SleepForLagDuration()),
									new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards)),
									new Action(ret => StyxWoW.SleepForLagDuration()),
									new Action(ret => mob2List[0].Face()),
									new Sleep(2000)
								)
							)
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
