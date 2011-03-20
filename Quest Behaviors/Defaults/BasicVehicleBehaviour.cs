using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class BasicVehicleBehaviour : CustomForcedBehavior
    {
        public BasicVehicleBehaviour(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                WoWPoint    destcoords;
                WoWPoint    mountcoords;
                int         mobId;
                int         questId;
                int         spellId;
                int         vehicleId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "DestX",      null },
                                                    { "DestY",      null },
                                                    { "DestZ",      null },
                                                    { "MobId",      null },
                                                    { "MountX",     null },
                                                    { "MountY",     null },
                                                    { "MountZ",     null },
                                                    { "NpcId",      null },
                                                    { "QuestId",    null },
                                                    { "SpellId",    null },
                                                    { "VehicleId",  null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint("DestX", "DestY", "DestZ", true, WoWPoint.Empty, out destcoords);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint("MountX", "MountY", "MountZ", true, WoWPoint.Empty, out mountcoords);
                _isAttributesOkay &= GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobId);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsInteger("SpellId", false, "0", 0, int.MaxValue, out spellId);
                _isAttributesOkay &= GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);

                // "NpcId" is allowed for legacy purposes --
                // If it was not supplied, then its new name "MobId" is required.
                if (mobId == 0)
                    _isAttributesOkay &= GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId);


                // Weed out Profile Writer sloppiness --
                if (_isAttributesOkay)
                {
                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "MobId may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (spellId == 0)
                    {
                        UtilLogMessage("error", "SpellId may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Counter = 0;
                    IsMounted = false;
                    LocationDest = destcoords;
                    LocationMount = mountcoords;
                    MobId = mobId;
                    MountedPoint = new WoWPoint(0, 0, 0);
                    QuestId = (uint)questId;
                    SpellCastId = spellId;
                    VehicleId = vehicleId;
                }
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				_isAttributesOkay = false;
			}
        }


        public int      Counter { get; set; }
        public bool     IsMounted { get; set; }
        public WoWPoint LocationDest { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public int      MobId { get; set; }
        public WoWPoint MountedPoint { get; private set; }
        public uint     QuestId { get; set; }
        public int      SpellCastId { get; set; }
        public int      VehicleId { get; set; }

        private bool            _isAttributesOkay;
        private bool            _isBehaviorDone;
        private Composite       _root;
        private List<WoWUnit>   _vehicleList;

        private static LocalPlayer  s_me = ObjectManager.Me;


        #region Overrides of CustomForcedBehavior

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Entry == VehicleId); } }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => (QuestId != 0 && s_me.QuestLog.GetQuestById(QuestId) != null &&
                         s_me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isBehaviorDone = true)),

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => IsMounted != true && _vehicleList == null,
                                new Action(ctx =>
                                {
                                    WoWPoint destination1 = new WoWPoint(LocationMount.X, LocationMount.Y, LocationMount.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(s_me.Location, destination1);

                                    foreach (WoWPoint p1 in pathtoDest1)
                                    {
                                        while (!s_me.Dead && p1.Distance(s_me.Location) > 3)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p1);
                                        }
                                    }

                                    ObjectManager.Update();
                                    _vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(ret => (ret.Entry == VehicleId) && !ret.Dead).OrderBy(ret => ret.Location.Distance(s_me.Location)).ToList();

                                })
                                ),

                            new Decorator(ret => _vehicleList[0] != null && !_vehicleList[0].WithinInteractRange && IsMounted != true,
                                new Action(ret => Navigator.MoveTo(_vehicleList[0].Location))
                                ),

                            new Decorator(ret => StyxWoW.Me.IsMoving,
                                new Action(ret =>
                                {
                                    WoWMovement.MoveStop();
                                    StyxWoW.SleepForLagDuration();
                                })
                                ),

                            new Decorator(ret => IsMounted != true,
                                new Action(ctx =>
                                {

                                    MountedPoint = s_me.Location;
                                    _vehicleList[0].Interact();
                                    StyxWoW.SleepForLagDuration();
                                    IsMounted = true;

                                    ObjectManager.Update();
                                    _vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(ret => (ret.Entry == VehicleId) && !ret.Dead).OrderBy(ret => ret.Location.Distance(MountedPoint)).ToList();
                                    Thread.Sleep(3000);
                                })
                                ),

                            new Decorator(ret => IsMounted = true,
                                new Action(ret =>
                                {
                                    WoWPoint destination = new WoWPoint(LocationDest.X, LocationDest.Y, LocationDest.Z);
                                    WoWPoint[] pathtoDest = Styx.Logic.Pathing.Navigator.GeneratePath(_vehicleList[0].Location, destination);

                                    foreach (WoWPoint p in pathtoDest)
                                    {
                                        while (!_vehicleList[0].Dead && p.Distance(_vehicleList[0].Location) > 3)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                    }

                                    Lua.DoString("CastSpellByID(" + SpellCastId + ")");

                                    Counter++;

                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
        }


        public override void OnStart()
		{
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}
		}

        #endregion
    }
}
