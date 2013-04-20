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
using System.Xml;
using System.Xml.Linq;

using Styx.CommonBot.Profiles;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        // 19Apr2013-05:58UTC chinajade
        public static void DeprecationWarning_Behavior(XElement xElement, string oldBehaviorName, string newBehaviorName, Dictionary<string, string> replacementAttributes)
        {
            if (QuestBehaviorCoreSettings.Instance.LogNotifyOn_OnDeprecatedBehaviorUse)
            {
                string attributes =
                    string.Join(" ", replacementAttributes.Select(kvp => string.Format("{0}=\"{1}\"", kvp.Key, kvp.Value)));

                LogWarning(
                    "{0}/********************{0}DEPRECATED BEHAVIOR ({1}){0}"
                    + "The {1} behavior has been deprecated, but will continue to function as originally designed."
                    + "  Please replace the use of the {1} behavior with the {2} behavior.{0}"
                    + "The affected profile is \"{3}\", and the replacement command to accomplish this task is:{0}{4}:    {5}{0}"
                    + "********************/{0}",
                    Environment.NewLine,
                    oldBehaviorName,
                    newBehaviorName,
                    GetProfileName(),
                    GetProfileLineNumber(xElement),
                    string.Format("<CustomBehavior File=\"{0}\" {1} />", newBehaviorName, attributes));

                if (QuestBehaviorCoreSettings.Instance.AudibleNotify_OnDeprecatedBehaviorUse)
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


        // 19Apr2013-05:46UTC chinajade
        public static string GetProfileLineNumber(XElement xElement)
        {
            return ((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
                ? ("@line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
                : "@unknown line";            
        }


        // 19Apr2013-05:58UTC chinajade
        public static string GetProfileName()
        {
            return ProfileManager.CurrentOuterProfile.Name;
        }

        // 20Apr2013-01:23UTC chinajade
        public static string GetProfileReference(XElement xElement)
        {
            return string.Format("[Ref: \"{0}\" {1}]", GetProfileName(), GetProfileLineNumber(xElement));
        }

        
        // 19Apr2013-05:58UTC chinajade
        public void UsageCheck_DeprecatedAttribute(XElement xElement, bool deprecatedAttributeEncounteredPredicate,
                                                    string attributeName, ProvideStringDelegate messageDelegate)
        {
            if (QuestBehaviorCoreSettings.Instance.LogNotifyOn_OnDeprecatedAttributeUse)
            {
                if (deprecatedAttributeEncounteredPredicate)
                {
                    LogWarning("DEPRECATED ATTRIBUTE ({1}):{0}{2}{0}{3}{0}",
                        Environment.NewLine, attributeName, messageDelegate(null), GetProfileReference(xElement));

                    if (QuestBehaviorCoreSettings.Instance.AudibleNotify_OnDeprecatedAttributeUse)
                    {
                        const int audioDelayInMilliseconds = 150;
                        SystemSounds.Asterisk.Play();
                        Thread.Sleep(audioDelayInMilliseconds);
                        SystemSounds.Asterisk.Play();
                    }
                }
            }
        }        

        
        // 19Apr2013-05:58UTC chinajade
        public void UsageCheck_SemanticCoherency(XElement xElement, bool incoherencyPredicate, ProvideStringDelegate messageDelegate)
        {
            if (incoherencyPredicate)
            {
                LogError("PROFILE ERROR: {1}{0}{2}{0}",
                    Environment.NewLine, messageDelegate(null), GetProfileReference(xElement));
                IsAttributeProblem = true;

                if (QuestBehaviorCoreSettings.Instance.AudibleNotify_OnSemanticIncoherency)
                {
                    const int audioDelayInMilliseconds = 150;
                    SystemSounds.Asterisk.Play();
                    Thread.Sleep(audioDelayInMilliseconds);
                    SystemSounds.Asterisk.Play();
                }
            }
        }
    }
}