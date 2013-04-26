﻿// Originally contributed by Chinajade.
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
using System.Diagnostics;
using System.Media;
using System.Threading;

using Styx;
using Styx.TreeSharp;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class CompositeThrottle : Decorator
    {
        public CompositeThrottle(TimeSpan throttleTime, Composite composite)
            : this(context => true, throttleTime, composite)
        {
            // empty
        }


        public CompositeThrottle(CanRunDecoratorDelegate predicateDelegate, TimeSpan throttleTime, Composite composite)
            : base(composite)
        {
            _predicateDelegate = predicateDelegate;
            _throttleTime = throttleTime;
        }


        protected override bool CanRun(object context)
        {
            if (_predicateDelegate(context))
            {
                bool canRun = IsFinished;

                if (IsFinished)
                    { _throttle.Restart(); }

                return canRun;
            }

            if (_throttle.IsRunning)
                {_throttle.Stop(); }

            return false;
        }

        private bool IsFinished
        {
            get { return !_throttle.IsRunning || (_throttle.Elapsed > _throttleTime); }
        }

        private readonly CanRunDecoratorDelegate _predicateDelegate;
        private readonly Stopwatch _throttle = new Stopwatch();
        private readonly TimeSpan _throttleTime;
    }


    public class CompositeThrottleContinue : DecoratorContinue
    {
        public CompositeThrottleContinue(TimeSpan throttleTime, Composite composite)
            : this(context => true, throttleTime, composite)
        {
            // empty
        }

        
        public CompositeThrottleContinue(CanRunDecoratorDelegate predicateDelegate, TimeSpan throttleTime, Composite composite)
            : base(composite)
        {
            _predicateDelegate = predicateDelegate;
            _throttleTime = throttleTime;
        }


        protected override bool CanRun(object context)
        {
            if (_predicateDelegate(context))
            {
                bool canRun = IsFinished;

                if (IsFinished)
                    { _throttle.Restart(); }

                return canRun;
            }

            if (_throttle.IsRunning)
                {_throttle.Stop(); }

            return false;
        }

        private bool IsFinished
        {
            get { return !_throttle.IsRunning || (_throttle.Elapsed > _throttleTime); }
        }

        private readonly CanRunDecoratorDelegate _predicateDelegate;
        private readonly Stopwatch _throttle = new Stopwatch();
        private readonly TimeSpan _throttleTime;
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
                    var message = string.Format("{0} EXCEPTION CONTEXT: {1}",
                        _questBehaviorBase.GetVersionedBehaviorName(),
                        QuestBehaviorBase.GetProfileReference(_questBehaviorBase.Element));


                    if (QuestBehaviorCoreSettings.Instance.LogProfileContextOnExceptions)
                    {
                        QuestBehaviorBase.LogError(message);
                        SystemSounds.Asterisk.Play();
                    }
                    else
                    {
                        QuestBehaviorBase.LogDeveloperInfo(message);
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