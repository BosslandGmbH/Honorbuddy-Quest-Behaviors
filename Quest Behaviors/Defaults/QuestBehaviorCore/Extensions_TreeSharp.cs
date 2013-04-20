// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Media;
using System.Threading;

using Styx;
using Styx.Common.Helpers;
using Styx.TreeSharp;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class CompositeThrottle : DecoratorContinue
    {
        public CompositeThrottle(TimeSpan throttleTime, Composite composite)
            : base(composite)
        {
            _throttle.WaitTime = throttleTime;
            // _throttle was created with "0" time--this makes it "good to go" 
            // on first visit to CompositeThrottle node
        }


        protected override bool CanRun(object context)
        {
            if (!_throttle.IsFinished)
                { return false; }
                
            _throttle.Reset();
            return true;
        }

        private readonly WaitTimer _throttle = new WaitTimer(TimeSpan.FromSeconds(0));
    }


    public class ExceptionCatchingWrapper : PrioritySelector
    {
        public ExceptionCatchingWrapper(QuestBehaviorBase questBehaviorBase, Composite unwrappedChild)
            : base(unwrappedChild)
        {
            _questBehaviorBase = questBehaviorBase;
        }

        private readonly QuestBehaviorBase _questBehaviorBase;


        public override RunStatus Tick(object context)
        {
            try
            {
                return base.Tick(context);
            }

            catch (Exception except)
            {
                if (except.GetType() != typeof(ThreadAbortException))
                {
                    if (QuestBehaviorCoreSettings.Instance.LogProfileContextOnExceptions)
                    {
                        QuestBehaviorBase.LogError("PROFILE EXCEPTION CONTEXT: {0}",
                            QuestBehaviorBase.GetProfileReference(_questBehaviorBase.Element));
                        SystemSounds.Asterisk.Play();
                    }
                    else
                    {
                        QuestBehaviorBase.LogDeveloperInfo("PROFILE EXCEPTION CONTEXT: {0}",
                            QuestBehaviorBase.GetProfileReference(_questBehaviorBase.Element));
                    }
                }

                throw;
            }
        }
    }


    //
    // This behavior wraps the child behaviors in a 'frame lock' which can provide
    // a big performance improvement if the child behaviors makes multiple HB API
    // calls that internally run off a frame in WoW in one CC pulse.
    //
    public class FrameLockSelector : PrioritySelector
    {
        public FrameLockSelector(params Composite[] children)
            : base(children)
        {
            // empty
        }


        public FrameLockSelector(ContextChangeHandler contextChange, params Composite[] children)
            : base(contextChange, children)
        {
            // empty
        }


        public override RunStatus Tick(object context)
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                return base.Tick(context);
            }
        }
    }
}