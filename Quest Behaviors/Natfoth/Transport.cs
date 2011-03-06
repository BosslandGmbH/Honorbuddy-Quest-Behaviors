using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class Transport : CustomForcedBehavior
    {
        

        /// <summary>
        /// Transport by Natfoth
        /// Allows you to use Transports.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ObjectId: ID of the transport.
        /// TransGetOn: Location on the transport to move to.
        /// TransGetOff: Location of where the transport will be when you want to get off.
        /// GetOn: Where you wish to get on at.
        /// GetOff: Where you wish to end up at
        /// X,Y,Z: The Location while waiting for the transport
        /// TransStand: Where you want to stand on the transport
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ObjectId",null},
            {"QuestId",null},
            {"TransGetOnX",null},
            {"TransGetOnY",null},
            {"TransGetOnZ",null},
            {"TransGetOffX",null},
            {"TransGetOffY",null},
            {"TransGetOffZ",null},
            {"GetOnX",null},
            {"GetOnY",null},
            {"GetOnZ",null},
            {"GetOffX",null},
            {"GetOffY",null},
            {"GetOffZ",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"TransStandX",null},
            {"TransStandY",null},
            {"TransStandZ",null},

        };

        bool success = true;

        public Transport(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint getOnlocation = new WoWPoint(0, 0, 0);
            WoWPoint getOfflocation = new WoWPoint(0, 0, 0);
            WoWPoint reachedlocation = new WoWPoint(0, 0, 0);
            WoWPoint endlocation = new WoWPoint(0, 0, 0);
            WoWPoint docklocation = new WoWPoint(0, 0, 0);
            WoWPoint shipstand = new WoWPoint(0, 0, 0);
            int questId = 0;
            int objectId = 0;

            success = success && GetAttributeAsInteger("ObjectId", true, "1", 0, int.MaxValue, out objectId);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("TransGetOnX", "TransGetOnY", "TransGetOnZ", true, new WoWPoint(0, 0, 0), out reachedlocation);
            success = success && GetXYZAttributeAsWoWPoint("TransGetOffX", "TransGetOffY", "TransGetOffZ", true, new WoWPoint(0, 0, 0), out endlocation);
            success = success && GetXYZAttributeAsWoWPoint("GetOnX", "GetOnY", "GetOnZ", true, new WoWPoint(0, 0, 0), out getOnlocation);
            success = success && GetXYZAttributeAsWoWPoint("GetOffX", "GetOffY", "GetOffZ", true, new WoWPoint(0, 0, 0), out getOfflocation);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", false, new WoWPoint(0, 0, 0), out docklocation);
            success = success && GetXYZAttributeAsWoWPoint("TransStandX", "TransStandY", "TransStandZ", false, new WoWPoint(0, 0, 0), out shipstand);

            QuestId = (uint)questId;
            ObjectID = objectId;

            GetOnLocation = getOnlocation;
            GetOffLocation = getOfflocation;
            ReachedLocation = reachedlocation;
            EndLocation = endlocation;
            Location = docklocation;
            ShipStandLocation = shipstand;

            Counter = 0;
        }

        public WoWPoint GetOnLocation { get; private set; }
        public WoWPoint GetOffLocation { get; private set; }
        public WoWPoint ReachedLocation { get; private set; }
        public WoWPoint EndLocation { get; private set; }
        public WoWPoint Location { get; private set; }
        public WoWPoint ShipStandLocation { get; private set; }
        public int Counter { get; set; }
        public bool MovedOnShip = false;
        public bool OnShip = false;
        public bool MovedToTarget = false;
        public int ObjectID { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWGameObject> objectList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>()
                            .Where(u => u.Entry == ObjectID)
                            .OrderBy(u => u.Distance).ToList();
            }
        }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "Transport - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "Transport: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => !MovedToTarget,
                                new Sequence(
                                    new DecoratorContinue(ret => Location.Distance(me.Location) <= 3,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "At Location, Waiting for Transport - " + ObjectID),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => MovedToTarget = true)
                                            )
                                    ),
                                    new DecoratorContinue(ret => Location.Distance(me.Location) > 3,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X:" + Location.X + " Y: " + Location.Y+ " Z: " + Location.Z),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(50))
                                            ))
                                    )),

                           new Decorator(ret => MovedToTarget && !MovedOnShip,
                                new Sequence(
                                    new DecoratorContinue(ret => objectList.Count > 0,
                                        new Sequence(
                                            new DecoratorContinue(ret => transportLocation.Distance(me.Location) < 20,
                                                new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Transport is here, Moving onto"),
                                                new Action(ret => Thread.Sleep(3000)),
                                                new Action(ret => WoWMovement.ClickToMove(GetOnLocation)),
                                                new Action(ret => Thread.Sleep(500)),
                                                new Action(ret => OnShip = true)
                                                )
                                            ),

                                            new DecoratorContinue(ret => (OnShip && ShipStandLocation.X > 0) && ShipStandLocation.Distance(me.Location) > 3,
                                                new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Moving To Stand Point"),
                                                new Action(ret => WoWMovement.ClickToMove(ShipStandLocation)),
                                                new Action(ret => Thread.Sleep(300)),
                                                new Action(ret => TreeRoot.StatusText = "Waiting On Transport To Reach Location"),
                                                new Action(ret => MovedOnShip = true)
                                                )
                                            ),

                                            new DecoratorContinue(ret => OnShip && ShipStandLocation.X == 0,
                                                new Sequence(
                                                    new Action(ret => TreeRoot.StatusText = "Waiting On Transport To Reach Location"),
                                                    new Action(ret => MovedOnShip = true)
                                                ))
                                            )),

                                   new DecoratorContinue(ret => objectList.Count == 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Waiting for Transport"),
                                            new Action(ret => Thread.Sleep(100))
                                          ))
                                    )),

                           new Decorator(ret => MovedOnShip,
                                new Sequence(
                                    new DecoratorContinue(ret => transportLocation.Distance(EndLocation) <= 3,
                                        new Sequence(
                                            new DecoratorContinue(ret => GetOffLocation.Distance(me.Location) <= 3,
                                                new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "At End Location"),
                                                new Action(ret => Counter++)
                                                )
                                            ),

                                            new DecoratorContinue(ret => GetOffLocation.Distance(me.Location) > 3,
                                                new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Moving off Ship"),
                                                new Action(ret => WoWMovement.ClickToMove(GetOffLocation)),
                                                new Action(ret => Thread.Sleep(300))
                                                ))
                                            ))
                            ))

                    ));
        }

        WoWPoint transportLocation
        {
            get
            {
                Tripper.Tools.Math.Matrix m = objectList[0].GetWorldMatrix();

                return new WoWPoint(m.M41, m.M42, m.M43);
            }

        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

