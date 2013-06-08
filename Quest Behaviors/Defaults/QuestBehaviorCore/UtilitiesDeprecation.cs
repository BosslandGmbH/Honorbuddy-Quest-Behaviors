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
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Threading;
using System.Xml.Linq;

using Styx.CommonBot.Profiles;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        // 19Apr2013-05:58UTC chinajade
        public static void DeprecationWarning_Behavior(CustomForcedBehavior cfb, string newBehaviorName, Dictionary<string, string> replacementAttributes)
        {
            if (QuestBehaviorCoreSettings.Instance.LogNotifyOn_OnDeprecatedBehaviorUse)
            {
                var oldLoggingContext = QBCLog.BehaviorLoggingContext;

                QBCLog.BehaviorLoggingContext = cfb;

                string attributes =
                    string.Join(" ", replacementAttributes.Select(kvp => string.Format("{0}=\"{1}\"", kvp.Key, kvp.Value)));

                QBCLog.Warning(QBCLog.BuildMessageWithContext(cfb.Element,
                    "{0}/********************{0}DEPRECATED BEHAVIOR ({1}){0}"
                    + "The {1} behavior has been deprecated, but will continue to function as originally designed."
                    + "  Please replace the use of the {1} behavior with the {2} behavior.{0}"
                    + "The replacement command to accomplish this task is:{0}    {3}",
                    Environment.NewLine,
                    cfb.GetType().Name,
                    newBehaviorName,
                    string.Format("<CustomBehavior File=\"{0}\" {1} />", newBehaviorName, attributes))
                    + Environment.NewLine
                    + "********************/"
                    + Environment.NewLine);

                AudibleNotifyOn(QuestBehaviorCoreSettings.Instance.AudibleNotify_OnDeprecatedBehaviorUse);
                QBCLog.BehaviorLoggingContext = oldLoggingContext;
            }
        }


        // 19Apr2013-05:58UTC chinajade
        public void UsageCheck_DeprecatedAttribute(XElement xElement, bool deprecatedAttributeEncounteredPredicate,
                                                    string attributeName, ProvideStringDelegate messageDelegate)
        {
            if (QuestBehaviorCoreSettings.Instance.LogNotifyOn_OnDeprecatedAttributeUse)
            {
                if (deprecatedAttributeEncounteredPredicate)
                {
                    QBCLog.Warning(QBCLog.BuildMessageWithContext(xElement,
                        "DEPRECATED ATTRIBUTE ({1}):{0}{2}{0}",
                        Environment.NewLine,
                        attributeName,
                        messageDelegate(null)));

                    AudibleNotifyOn(QuestBehaviorCoreSettings.Instance.AudibleNotify_OnDeprecatedAttributeUse);
                }
            }
        }        

        
        // 19Apr2013-05:58UTC chinajade
        public void UsageCheck_SemanticCoherency(XElement xElement, bool incoherencyPredicate, ProvideStringDelegate messageDelegate)
        {
            if (incoherencyPredicate)
            {
                QBCLog.Error(QBCLog.BuildMessageWithContext(xElement,
                    "PROFILE ERROR: {1}{0}",
                    Environment.NewLine,
                    messageDelegate(null)));
                IsAttributeProblem = true;

                AudibleNotifyOn(QuestBehaviorCoreSettings.Instance.AudibleNotify_OnSemanticIncoherency);
            }
        }


        // 19Apr2013-05:58UTC chinajade
        public static void UsageCheck_ScheduledForDeprecation(CustomForcedBehavior cfb, string replacementBehaviorName)
        {
            if (QuestBehaviorCoreSettings.Instance.LogNotifyOn_OnScheduledForDeprecation)
            {
                var oldLoggingContext = QBCLog.BehaviorLoggingContext;

                QBCLog.BehaviorLoggingContext = cfb;
                QBCLog.Warning(QBCLog.BuildMessageWithContext(cfb.Element,
                    "SCHEDULED FOR DEPRECATION ({1}){0}"
                    + "Please replace the behavior with \"{2}\"",
                    Environment.NewLine,    
                    cfb.GetType().Name,
                    replacementBehaviorName));

                AudibleNotifyOn(true);
                QBCLog.BehaviorLoggingContext = oldLoggingContext;
            }
        }


        //  1May2013-01:51UTC chinajade
        private static void AudibleNotifyOn(bool doNotifyPredicate)
        {
            if (doNotifyPredicate)
            {
                const int audioDelayInMilliseconds = 150;
                SystemSounds.Asterisk.Play();
                Thread.Sleep(audioDelayInMilliseconds);
                SystemSounds.Asterisk.Play();
                Thread.Sleep(audioDelayInMilliseconds);
                SystemSounds.Asterisk.Play();
                Thread.Sleep(audioDelayInMilliseconds);
                SystemSounds.Asterisk.Play();                
            }
        }
    }
}