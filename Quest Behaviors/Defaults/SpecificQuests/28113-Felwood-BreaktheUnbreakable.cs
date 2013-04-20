using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BreaktheUnbreakable
{
    [CustomBehaviorFileName(@"SpecificQuests\28113-Felwood-BreaktheUnbreakable")]
    public class _28113 : CustomForcedBehavior
    {
        public _28113(Dictionary<string, string> args)
            : base(args)
        {
            QuestId = 28113;
            touchdown = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
        }

        public int QuestId { get; set; }
        private bool IsAttached;
        private bool IsBehaviorDone;
        private WoWPoint wp = new WoWPoint(-8361.689, 1726.248, 39.94792);
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        private Composite _root;
        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;


        //	206625	Jadefire Barrier	5.1960515975952148	4.75	<4576.11, -384.74, 303.044>	<4576.11, -384.74, 303.044>	10111		0		32		0	0	0		Ready	Door	Styx.WoWInternals.WoWObjects.WoWDoor	None	0	255	False	False	False	False	False	None	False	False	spells\sunwell_fire_barrier_ext_center.mdx		False	641637240	True	GameObject	Object, GameObject	17371271210385544193	0	22.5625	False	False	17371271210385544193	4576.11	-384.74	303.044	0	0	26.998952419497073	5.0990185737609863	25.9999897480011	False	<4576.11, -384.74, 303.044>	None	TaxiNotEligible	False	False	True	False	False	False


        private WoWItem ClawThing
        {
            get { return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63031); }
        }

        private bool touched;
        //Handin <Vendor Name="dsada" Entry="0" Type="Repair" X="4567.456" Y="-406.0457" Z="305.6446" />
        private WoWPoint touchdown;// = new WoWPoint(4566.125,-402.532,305.2783);
        //kill <Vendor Name="dsada" Entry="0" Type="Repair" X="4584.85" Y="-359.2484" Z="301.6123" />
        //private WoWPoint touchdown = new WoWPoint(4566.125, -402.532, 305.2783);

        protected override Composite CreateBehavior()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,
                //new Decorator(r => HookDone, new Action(r => TreeHooks.Instance.RemoveHook("Combat_OOC", Hook))),
                new Decorator(r => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
                new Decorator(r => Firewall != null && Firewall.Distance < 10, new Action(r => { Navigator.PlayerMover.MoveStop();ClawThing.Use(); })),
                new Decorator(r => !touched && touchdown.Distance(StyxWoW.Me.Location) < 5, new Action(r => touched = true)),
                new Decorator(r => !touched, new Action(r => WoWMovement.ClickToMove(touchdown)))



                )));

        }

        private WoWGameObject Firewall
        {
            get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(r => r.Entry == 206625 && r.FlagsUint == 32); }
        }

        private bool QuestComplete
        {
            get
            {

                var Completed = StyxWoW.Me.QuestLog.GetQuestById(28113);

                if (Completed == null)
                {
                    return false;
                }
                return Completed.IsCompleted;
            }
        }


        //<Vendor Name="dasa" Entry="0" Type="Repair" X="" />

        private bool HookDone
        {
            get
            {

                var Completed = StyxWoW.Me.QuestLog.GetCompletedQuests().FirstOrDefault(r => r == 28113);

                return Completed != 0;
            }
        }



        public void Dispose(bool isExplicitlyInitiatedDispose)
        {



            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => touched, new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        public override bool IsDone
        {
            get
            {
                return _isBehaviorDone;
            }
        }


        public override void OnStart()
        {


            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();
            //TreeHooks.Instance.InsertHook("Combat_OOC", 0, Hook);
            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {




                //CharacterSettings.Instance.UseMount = false;

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }

                //TreeRoot.TicksPerSecond = 30;
                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }


    }
}
