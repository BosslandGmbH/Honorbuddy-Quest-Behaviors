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
    public partial class QuestBehaviorBase
    {
        public static void DeprecationWarning(XElement xElement, string oldBehaviorName, string newBehaviorName, Dictionary<string, string> replacementAttributes)
        {
            string attributes =
                string.Join(" ", replacementAttributes.Select(kvp => string.Format("{0}=\"{1}\"", kvp.Key, kvp.Value)));
            const int audioDelayInMilliseconds = 150;
            string location = ((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
                ? (" @line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
                : " @unknown line";

            LogWarning("{0}/********************{0}DEPRECATED BEHAVIOR ({1}){0}"
                + "The {1} behavior has been deprecated, but will continue to function as originally designed."
                + "  Please replace the use of the {1} behavior with the {2} behavior.{0}"
                + "The affected profile is \"{3}\", and the replacement command to accomplish this task is:{0}{4}:    {5}{0}"
                + "********************/{0}",
                Environment.NewLine,
                oldBehaviorName,
                newBehaviorName,
                ProfileManager.CurrentOuterProfile.Name,
                location,
                string.Format("<CustomBehavior File=\"{0}\" {1} />", newBehaviorName, attributes));

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