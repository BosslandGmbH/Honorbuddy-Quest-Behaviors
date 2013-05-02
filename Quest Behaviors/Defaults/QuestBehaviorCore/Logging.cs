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
using System.Diagnostics;
using System.Windows.Media;
using System.Xml.Linq;

using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// <para>This is an efficent poor man's mechanism for reporting contract violations in methods.</para>
        /// <para>If the provided ISCONTRACTOKAY evaluates to true, no action is taken.
        /// If ISCONTRACTOKAY is false, a diagnostic message--given by the STRINGPROVIDERDELEGATE--is emitted to the log, along with a stack trace.</para>
        /// <para>This emitted information can then be used to locate and repair the code misusing the interface.</para>
        /// <para>For convenience, this method returns the evaluation if ISCONTRACTOKAY.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The interface is built in terms of a StringProviderDelegate,
        /// so we don't pay a performance penalty to build an error message that is not used
        /// when ISCONTRACTOKAY is true.</para></description></item>
        /// <item><description><para> * The .NET 4.0 Contract support is insufficient due to the way Buddy products
        /// dynamically compile parts of the project at run time.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="isContractOkay"></param>
        /// <param name="provideStringProviderDelegate"></param>
        /// <returns>the evaluation of the provided ISCONTRACTOKAY predicate delegate</returns>
        ///  30Jun2012-15:58UTC chinajade
        ///  NB: We could provide a second interface to ContractRequires() that is slightly more convenient for static string use.
        ///  But *please* don't!  If helps maintainers to not make mistakes if they see the use of this interface consistently
        ///  throughout the code.
        public static bool ContractRequires(bool isContractOkay, ProvideStringDelegate provideStringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string message = provideStringProviderDelegate(null) ?? "NO MESSAGE PROVIDED";
                var trace   = new StackTrace(1);

                LogError("[CONTRACT VIOLATION] {0}\nLocation:\n{1}",  message, trace.ToString());
                throw new ContractException(message);
            }

            return isContractOkay;
        }

        public static bool ContractProvides(bool isContractOkay, ProvideStringDelegate provideStringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string message = provideStringProviderDelegate(null) ?? "NO MESSAGE PROVIDED";
                var trace   = new StackTrace(1);

                LogError("[CONTRACT VIOLATION] {0}\nLocation:\n{1}",  message, trace.ToString());
                throw new ContractException(message);
            }

            return isContractOkay;
        }


        /// <summary>
        /// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
        /// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
        /// <para>This is useful when emitting log messages.</para>
        /// </summary>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
        /// Use it with caution.</para></description></item>
        /// </list></para>
        /// <returns></returns>
        ///  7Jul2012-20:26UTC chinajade
        public static string    GetMyMethodName(bool  showDeclaringType   = false)
        {
            var method  = (new StackTrace(1)).GetFrame(0).GetMethod();

            if (showDeclaringType)
                { return (method.DeclaringType + "." + method.Name); }

            return (method.Name);
        }


        // 30Apr2013-06:20UTC chinajade
        public static string BuildLogMessage(string messageType, string format, params object[] args)
        {
            var versionedBehaviorName =
                (BehaviorLoggingContext != null)
                    ? GetVersionedBehaviorName(BehaviorLoggingContext)
                    : string.Empty;
                                    
            return string.Format("[{0}({1})] {2}",
                versionedBehaviorName,
                messageType,
                BuildMessageWithContext(null, format, args));           
        }


        // 30Apr2013-06:20UTC chinajade
        public static string BuildMessageWithContext(XElement xElement, string format, params object[] args)
        {
            var context =
                (xElement != null)
                    ? string.Format("{0}    {1}", Environment.NewLine, GetXmlFileReference(xElement))
                    : string.Empty;
                                    
            return string.Format("{0}{1}", string.Format(format, args), context);           
        }
        
        
        /// <summary>
        /// <para>For DEBUG USE ONLY--don't use in production code! (Almost exclusively used by DebuggingTools methods.)</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void LogDeveloperInfo(string message, params object[] args)
        {
            Logging.WriteDiagnostic(Colors.LimeGreen, BuildLogMessage("debug", message, args));
        }
        
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void LogError(string message, params object[] args)
        {
            Logging.Write(Colors.Red, BuildLogMessage("error", message, args));
        }
           
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void LogFatal(string message, params object[] args)
        {
            Logging.Write(Colors.Red, BuildLogMessage("fatal", message, args));
            TreeRoot.Stop("Fatal error in quest behavior, or profile.");
        }
        
        
        /// <summary>
        /// <para>Normal information to keep user informed.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void LogInfo(string message, params object[] args)
        {
            Logging.Write(Colors.CornflowerBlue, BuildLogMessage("info", message, args));
        }
        
        
        /// <summary>
        /// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
        /// action a user can perform in the field for these types of errors.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public static void LogMaintenanceError(string message, params object[] args)
        {
            string formattedMessage = string.Format(message, args);
            var trace = new StackTrace(1);

            LogError("[MAINTENANCE ERROR] {0}\nFROM HERE:\n{1}", formattedMessage, trace.ToString());
        }


        /// <summary>
        /// ProfileErrors occur as a result of the profile attempting to use a behavior in a manner for which
        /// it was not intended.  Such errors also occur because behavior entry criteria are not met.
        /// For instance, the behavior was asked to use an item in the backpack on a mob, but the item is not
        /// present in the backpack.
        /// There is no corrective action a user or the behavior can perform to work around these types
        /// of errors; thus, they are always considered fatal.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public static void LogProfileError(string message, params object[] args)
        {
            var outMessage = string.Format(message, args);

            if (BehaviorLoggingContext != null)
            {
                outMessage += string.Format("{0}    {1}",
                    Environment.NewLine,
                    GetProfileReference(BehaviorLoggingContext.Element));
            }

            LogFatal("[PROFILE ERROR] {0}", outMessage);
        }
        
        
        /// <summary>
        /// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void LogWarning(string message, params object[] args)
        {
            Logging.Write(Colors.DarkOrange, BuildLogMessage("warning", message, args));
        }


        private static CustomForcedBehavior BehaviorLoggingContext { get; set; }
    }
}