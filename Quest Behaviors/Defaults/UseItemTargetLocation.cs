using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Logic.Combat;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;
using CommonBehaviors.Actions;
using Styx.Logic.BehaviorTree;

namespace Styx.Bot.Quest_Behaviors
{
    public class UseItemTargetLocation : CustomForcedBehavior
    {
        /// <summary>
        /// UseItemTargetLocation by raphus
        /// Allows you to use item on an object or at a location
        /// ##Syntax##
        /// [Optional] QuestId: Id of the quest. If specified the QB will run until the quest is completed
        /// [Optional] ObjectId: Id of the object/npc that the item will be used on
        /// ItemId: Id of the item that will be used
        /// [Optional]WaitTime: Time to wait after using the item 
        /// UseType: PointToObject (from X,Y,Z to an object's location)
        ///          PointToPoint  (from X,Y,Z to ClickToX,ClickToY,ClickToZ)
        ///          ToObject      (from range of an object to object's location)
        ///          Default is PointToPoint
        /// X,Y,Z: If the UseType is AtLocation, QB will move to that location before using item. Otherwise it will move towards that point to search for objects
        /// [Optional]ClickToX,ClickToY,ClickToZ: If the UseType is PoinToPoint, this location will be used to remote click 
        /// [Optional]Range: If the UseType is ToObject, QB will move to that range of an object/npc before using item. (default 4)
        /// </summary>
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"ObjectId",null},
            {"ItemId", null},
            {"WaitTime",null},
            {"UseType",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"ClickToX",null},
            {"ClickToY",null},
            {"ClickToZ",null},
            {"Range",null},
         
        };

        public enum QBType
        {
            PointToPoint = 0,
            PointToObject = 1,
            ToObject = 2
        }

        bool success = true;


        public UseItemTargetLocation(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int questId = 0;
            int objectId = 0;
            int itemId = 0;
            int waitTime = 0;
            string useType = "";
            WoWPoint moveToLocation = WoWPoint.Empty;
            WoWPoint clickToLocation = WoWPoint.Empty;
            int range = 0;

            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("ObjectId", false, "0", 0, int.MaxValue, out objectId);
            success = success && GetAttributeAsInteger("ItemId", true, "0", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("WaitTime", false, "0", 0, int.MaxValue, out waitTime);
            success = success && GetAttributeAsString("UseType", false, "PointToPoint", out useType);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out moveToLocation);
            success = success && GetXYZAttributeAsWoWPoint("ClickToX", "ClickToY", "ClickToZ", false, WoWPoint.Empty, out clickToLocation);
            success = success && GetAttributeAsInteger("Range", false, "4", 0, int.MaxValue, out range);


            QuestId = (uint)questId;
            ObjectId = objectId;
            ItemId = itemId;
            WaitTime = waitTime;
            UseType = (QBType)Enum.Parse(typeof(QBType), useType, true);
            MoveToLocation = moveToLocation;
            ClickToLocation = clickToLocation;
            Range = range;
        }

        public uint QuestId { get; set; }
        public int ObjectId { get; set; }
        public int ItemId { get; set; }
        public int WaitTime { get; set; }
        public QBType UseType { get; set; }
        public WoWPoint MoveToLocation { get; set; }
        public WoWPoint ClickToLocation { get; set; }
        public int Range { get; set; }

        public LocalPlayer Me { get { return StyxWoW.Me; } }

        private WoWItem Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId && i.Cooldown == 0); } }

        private WoWObject UseObject 
        { 
            get 
            {
                return ObjectManager.GetObjectsOfType<WoWObject>(true, false).
                    Where(o => o.Entry == ObjectId).OrderBy(o => o.Distance).FirstOrDefault();
            }
        }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => Item == null,
                        new ActionAlwaysSucceed()),

                    new Decorator(
                        ret => UseType == QBType.PointToPoint,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation)))),
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Using Item"),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                new Action(ret => Item.UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                new Action(ret => LegacySpellManager.ClickRemoteLocation(ClickToLocation)),
                                new Action(ret => Thread.Sleep(WaitTime)),
                                new DecoratorContinue(
                                    ret => QuestId == 0,
                                    new Action(ret => _isDone = true)))
                            )),

                    new Decorator(
                        ret => UseType == QBType.PointToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation)))),
                            new Decorator(
                                ret => UseObject != null,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Using Item"),
                                    new Action(ret => Navigator.PlayerMover.MoveStop()),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new Action(ret => Item.UseContainerItem()),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new Action(ret => LegacySpellManager.ClickRemoteLocation(UseObject.Location)),
                                    new Action(ret => Thread.Sleep(WaitTime)),
                                    new DecoratorContinue(
                                        ret => QuestId == 0,
                                        new Action(ret => _isDone = true)))),
                            new Action(ret => TreeRoot.StatusText = "No objects around. Waiting")
                            )),

                    new Decorator(
                        ret => UseType == QBType.ToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => UseObject != null,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => UseObject.Distance > Range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to object's range"),
                                            new Action(ret => Navigator.MoveTo(UseObject.Location)))),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Item"),
                                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => Item.UseContainerItem()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => LegacySpellManager.ClickRemoteLocation(UseObject.Location)),
                                        new Action(ret => Thread.Sleep(WaitTime)),
                                        new DecoratorContinue(
                                            ret => QuestId == 0,
                                            new Action(ret => _isDone = true))))),
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation))))
                        ))
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get 
            {
                if (QuestId != 0)
                {
                    var quest = Me.QuestLog.GetQuestById(QuestId);

                    if (quest != null)
                        return quest.IsCompleted;
                }

                return _isDone; 
            }
        }

        #endregion
    }
}
