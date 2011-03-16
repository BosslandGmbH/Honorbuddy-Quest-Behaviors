using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Bots.Grind;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    public class RunLikeHell : CustomForcedBehavior
    {
        /// <summary>
        /// RunLikeHell by Bobby53
        /// 
        /// Allows you to Run following a specific path.  Supports options to prevent combat 
        /// (disables CC while running), use Click-To-Move instead of Navigator, and
        /// the ability to specify a mob that when it enters the specified range, causes
        /// you to move to the nexts point.  
        /// 
        /// The difference between this and having several NoCombatMoveTo in a row is that
        /// HonorBuddy will let the CC enter combat after one has completed but before the
        /// next begins.  RunLikeHell prevents that since it stays in control.  However it
        /// takes a straight path to the point using ClickToMove so no path calculation is done.
        /// 
        /// If QuestId is non-zero, behavior will stop when quest becomes complete even if 
        /// it has not completed NumOfTimes iterations of full path specified
        /// 
        /// You can control the movement with the options below. 
        /// 
        /// ##Syntax##
        /// [Optional] QuestId: Id of the quest (default is 0)
        /// [Optional] WaitTime: ms to pause at each point (default is 0)
        /// [Optional] MobId: wait at point until mob is within Range yds (default is to move immediately)
        /// [Optional] Range: when mob is within this distance, move to next point (default is 15)
        /// [Optional] NumOfTimes: number of times to run path (default is 1)
        /// [Optional] Combat: fight back if attacked (default is true, false you keep moving)
        /// [Optional] UseCTM: use ClickToMove if true, otherwise Navigator (default is false)
        /// [Required] <Hotspot X="" Y="" Z="" /> : child elements specifying path to run
        /// 
        /// ##Examples##
        /// following will take path one time as listed
        /// <CustomBehavior File="RunLikeHell" >
        ///     <Hotspot X="4554.003" Y="-4718.743" Z="883.0464" />
        ///     <Hotspot X="4578.725" Y="-4721.257" Z="882.8724" />
        ///     <Hotspot X="4584.166" Y="-4693.487" Z="882.7331" />
        /// </CustomBehavior>
        /// 
        /// following path up to 4 times and moves to next spot only 
        /// if the mob #40434 is within 10 yds
        /// <CustomBehavior File="RunLikeHell" NumOfTimes="4" MobId="40434" Range="10">
        ///     <Hotspot X="4554.003" Y="-4718.743" Z="883.0464" />
        ///     <Hotspot X="4578.725" Y="-4721.257" Z="882.8724" />
        ///     <Hotspot X="4584.166" Y="-4693.487" Z="882.7331" />
        /// </CustomBehavior>
        /// 
        /// following follows path up to 4 times and moves to next spot only 
        /// if the mob #40434 is within 10 yds.  stops at 4 loops or when quest complete
        /// <CustomBehavior File="RunLikeHell" QuestId="25499" NumOfTimes="4" MobId="40434" Range="10">
        ///     <Hotspot X="4554.003" Y="-4718.743" Z="883.0464" />
        ///     <Hotspot X="4578.725" Y="-4721.257" Z="882.8724" />
        ///     <Hotspot X="4584.166" Y="-4693.487" Z="882.7331" />
        /// </CustomBehavior>
        /// 
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"WaitTime",null},
            {"MobId",null},
            {"NpcId",null},
            {"NumOfTimes",null},
            {"Combat",null},
            {"UseCTM", null},
            {"Range",null},
        };

        bool success = true;

        public RunLikeHell(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int waitTime = 0;
            int questId = 0;
            int mobid = 0;
            int numberoftimes = 0;
            bool allowcombat = true;
            bool usectm = false;


            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("WaitTime", false, "0", 0, int.MaxValue, out waitTime);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsBoolean("Combat", false, "true", out allowcombat);
            success = success && GetAttributeAsBoolean("UseCTM", false, "false", out usectm);

            success = success && GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobid);
            if (mobid == 0)
                success = success && GetAttributeAsInteger("MobId", false, "0", 0, int.MaxValue, out mobid);

            Range = 15;
            if (Args.ContainsKey("Range"))
            {
                int range = 15;
                success = success && GetAttributeAsInteger("Range", false, "15", 0, int.MaxValue, out range);
                Range = range != 0 ? range : 15;
            }

            if (!success)
                TreeRoot.Stop();

            QuestId = (uint)questId;
            WaitTime = waitTime;
            NumberOfTimes = numberoftimes;
            MobId = mobid;
            AllowCombat = allowcombat;
            UseCTM = usectm;

            Counter = 0;
            lineCount = 0;
            lastStateReturn = RunStatus.Success;
        }

        public int WaitTime { get; set; }
        public uint QuestId { get; set; }
        public int MobId { get; set; }
        public int NumberOfTimes { get; set; }
        public int Range { get; set; }
        public int Counter { get; set; }
        public Queue<WoWPoint> Path { get; set; }
        public RunStatus lastStateReturn { get; set; }
        public int lineCount { get; set; }
        public bool AllowCombat { get; set; }
        public bool UseCTM { get; set; }

        public static LocalPlayer Me { get { return StyxWoW.Me; } }

        public WoWUnit Mob
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                       .Where(u => u.Entry == MobId && !u.Dead)
                                       .OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        public void Log(string msg, params object[] args)
        {
            // following linecount hack is to stop dup suppression of Log window
            Logging.Write(Color.Blue, "[GreaterOfTwoEvils] " + msg + (++lineCount % 2 == 0 ? "" : " "), args);
        }

        public void DLog(string msg, params object[] args)
        {
            // following linecount hack is to stop dup suppression of Log window
            Logging.Write(Color.Blue, "(GreaterOfTwoEvils) " + msg + (++lineCount % 2 == 0 ? "" : " "), args);
        }

        private bool ParsePath()
        {
            var path = new Queue<WoWPoint>();

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Hotspot")))
                path.Enqueue(point);

            Path = path;
            return true;
        }

        public IEnumerable<WoWPoint> ParseWoWPoints(IEnumerable<XElement> elements)
        {
            var temp = new List<WoWPoint>();

            foreach (XElement element in elements)
            {
                XAttribute xAttribute, yAttribute, zAttribute;
                xAttribute = element.Attribute("X");
                yAttribute = element.Attribute("Y");
                zAttribute = element.Attribute("Z");

                float x, y, z;
                float.TryParse(xAttribute.Value, out x);
                float.TryParse(yAttribute.Value, out y);
                float.TryParse(zAttribute.Value, out z);
                temp.Add(new WoWPoint(x, y, z));
            }

            return temp;
        }


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            ParsePath();        // refresh the list of points

            if (quest != null)
            {
                TreeRoot.GoalText = "RunLikeHell - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "RunLikeHell";
            }

            if (AllowCombat)
                Log("will allow Combat");
            else
            {
                if (TreeRoot.Current == null)
                    Log("ERROR - TreeRoot.Current == null");
                else if (TreeRoot.Current.Root == null)
                    Log("ERROR - TreeRoot.Current.Root == null");
                else if (TreeRoot.Current.Root.LastStatus == RunStatus.Running)
                    Log("ERROR - TreeRoot.Current.Root.LastStatus == RunStatus.Running");
                else
                {
                    var currentRoot = TreeRoot.Current.Root;    
                    if (!(currentRoot is GroupComposite))
                        Log("ERROR - !(currentRoot is GroupComposite)");
                    else
                    {
                        if (currentRoot is Sequence)
                            lastStateReturn = RunStatus.Failure;
                        else if (currentRoot is PrioritySelector)
                            lastStateReturn = RunStatus.Success;
                        else
                        {
                            Log("unknown type of Group Composite at root");
                            lastStateReturn = RunStatus.Success;
                        }

                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                        Log("disabled Combat");
                    }
                }
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator( ret => !IsDone,
                    new PrioritySelector(
                        new Decorator(ret => !Path.Any() && Counter < NumberOfTimes ,
                            new Action(delegate
                            {
                                Counter++;
                                ParsePath();
                            })),
                        new Decorator(ret => Path.Peek().Distance(Me.Location) <= 1,
                            new PrioritySelector(
                                new Decorator(ret => Me.IsMoving && WaitTime > 0,
                                    new Action(delegate
                                    {
                                        WoWMovement.MoveStop();
                                        TreeRoot.GoalText = "RunLikeHell pausing " + WaitTime + " ms";
                                        System.Threading.Thread.Sleep(WaitTime);
                                    })),
                                new Decorator(ret => MobId != 0 && Mob.Distance > Range,
                                    new Action(delegate
                                    {
                                        TreeRoot.GoalText = "RunLikeHell wait for " + Mob.Name + " within " + Range + " yds";
                                    })),
                                new Action(delegate
                                {
                                    Path.Dequeue();
                                }))
                            ),
                        new Action(delegate
                        {
                            if ( NumberOfTimes > 1 )
                                TreeRoot.GoalText = "RunLikeHell[Lap " + Counter + "] to " + Path.Peek().ToString();
                            else
                                TreeRoot.GoalText = "RunLikeHell to " + Path.Peek().ToString();

                            if (UseCTM)
                                WoWMovement.ClickToMove(Path.Peek());
                            else
                                Navigator.MoveTo(Path.Peek());

                            return lastStateReturn;
                        })
                    )
                )
            );
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                var result = 
                    _isDone 
                    || (quest != null && quest.IsCompleted)         // quest complete
                    || quest == null                                // don't have quest
                    || Me.Dead || Me.IsGhost                        // i'm a ghost
                    || (!Path.Any() && Counter >= NumberOfTimes);   // not hotspots left and all iterations complete

                if (result)
                    _isDone = true;

                return result;
            }
        }

        #endregion
    }
}
