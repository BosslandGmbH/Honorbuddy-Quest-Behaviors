﻿//
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
using System.Numerics;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SetHearthstone
{
    [CustomBehaviorFileName(@"SetHearthstone")]
    public class SetHearthstone : CustomForcedBehavior
    {
        private readonly string _goalText;
        private bool _done;

        public SetHearthstone(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            NpcId = GetAttributeAsNullable<int>("MobId", false, ConstrainAs.MobId, new[] { "NpcId" }) ?? 0;
            AreaId = GetAttributeAsNullable<int>("AreaId", false, ConstrainAs.MobId, null) ?? 0;
            Location = GetAttributeAsNullable<Vector3>("", false, ConstrainAs.Vector3NonEmpty, null) ?? Me.Location;
            Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? "";

            if (!string.IsNullOrEmpty(Name))
            {
                _goalText = "Setting Hearthstone at " + Name;
            }
            else
            {
                _goalText = "Setting Hearthstone at NPC #" + NpcId;
            }
        }

        // DON'T EDIT THIS--it is auto-populated by Git
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id$");

        public int NpcId { get; set; }
        public Vector3 Location { get; set; }
        public string Name { get; set; }
        public int AreaId { get; set; }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        public override bool IsDone { get { return _done; } }

        private WoWUnit InnKeeper
        {
            get
            {
                if (NpcId == 0)
                {
                    return null;
                }

                return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).FirstOrDefault(u => u.IsInnkeeper && u.Entry == NpcId);
            }
        }

        public override void OnStart()
        {
            QuestBehaviorBase.UsageCheck_ScheduledForDeprecation(this, "InteractWith");

            Lua.Events.AttachEvent("CONFIRM_BINDER", HandleConfirmBinder);

            this.UpdateGoalText(0, _goalText);
        }

        private bool _confirmEventFired;
        private void HandleConfirmBinder(object sender, LuaEventArgs args)
        {
            Lua.DoString("ConfirmBinder(); StaticPopup_Hide('CONFIRM_BINDER')");
            Lua.Events.DetachEvent("CONFIRM_BINDER", HandleConfirmBinder);
            _confirmEventFired = true;
        }

        private bool GossipFrameActive()
        {
            return GossipFrame.Instance.IsVisible;
        }

        private bool StaticPopupActive()
        {
            return Lua.GetReturnVal<bool>("return StaticPopup1 and StaticPopup1:IsVisible()", 0);
        }

        private void SelectSetLocationGossipOption()
        {
            foreach (GossipEntry entry in GossipFrame.Instance.GossipOptionEntries)
            {
                if (entry.Type == GossipEntry.GossipEntryType.Binder)
                {
                    QBCLog.Info("Selecting gossip option: " + entry.Text + " - #" + entry.Index);
                    GossipFrame.Instance.SelectGossipOption(entry.Index);
                }
            }
        }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => AreaId != 0 && StyxWoW.Me.HearthstoneAreaId == AreaId,
                    new Action(ret => _done = true)),
                new Decorator(
                    ret => InnKeeper != null,
                    new PrioritySelector(
                        ctx => InnKeeper,
                        // Found the Innkeeper, but its a bit far away. Get within interact distance!
                        new Decorator(
                            ret => ((WoWUnit)ret).Distance > 4.5f,
                            new Action(ret => Navigator.MoveTo(((WoWUnit)ret).Location))),
                        // Now, we open up the gossip frame, and see if we can find a 'set my location' option
                        new Sequence(
                            // First, interact.
                            new Action(ret => ((WoWUnit)ret).Interact()),
                            // Some inn keepers offer the option to select "make this your home",
                            // while others just throw the popup at you. This should ensure both are handled!
                            new Wait(
                                5, ret => GossipFrameActive() || StaticPopupActive(),
                                new PrioritySelector(
                                    // If we even made it here, it means we have a window open.
                                    // The next part is trivial at best. FIRST, we deal with selecting
                                    // The "Make this my home" gossip option.
                                    new Decorator(
                                        ret => GossipFrameActive(),
                                        new Action(ret => SelectSetLocationGossipOption())),
                                    new Wait(
                                        5, ret => _confirmEventFired,
                                        new Action(ret => _done = true))
                                    )
                                )
                            )
                        )
                    ),
                new Action(ret => Navigator.MoveTo(Location))
                );
        }
    }
}