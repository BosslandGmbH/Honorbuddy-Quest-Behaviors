// This work is part of the Buddy Wiki.  You may find it here:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Category:Honorbuddy_CustomBehavior
//
// This work is licensed under the 
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons
//      171 Second Street, Suite 300
//      San Francisco, California, 94105, USA. 
//
// Release History:
//  Version 1.1 -- Built-in error handlers (15-Feb-2011, chinajade)
//                   Converted to the new buit-in error handlers provided by
//                   CustomForcedBehavior.  Eliminated the CustomBehaviorUtils
//                   class as a consequence. Yay!
//                   Repaired a bug with IsDone processing wrt/QuestIds
//  Version 1.0 -- Initial Release to BuddyWiki (22-Jan-2011, chinajade)
//                   Includes the new 'standardized' error checking.
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

using Styx;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;

using System.Windows.Forms;


namespace BuddyWiki.CustomBehavior
{
    public class UserDialogForm : Form
    {
        private UserDialogForm()
        {
            InitializeComponent();

            this.ControlBox  = false;    // disable close box for this dialog
            this.MinimizeBox = false;    // disable minimize box for this dialog
            this.MaximizeBox = false;    // disable maximize box for this dialog
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            _windowParent = (Form)Control.FromHandle(Process.GetCurrentProcess().MainWindowHandle);
            periodicWarningTimer.Stop();
        }


        public static UserDialogForm Singleton()
        {
            if (s_singleton == null)
            {
                s_singleton = new UserDialogForm();
                s_singleton.FormClosing += new FormClosingEventHandler(Singleton_FormClosing);
            }

            return (s_singleton);
        }


        private static void Singleton_FormClosing(object sender, FormClosingEventArgs evt)
        {
            // This prevents the dialog from actually closing --
            // If we close it, the resources would be released, and we don't want this.
            // Instead, we simply Hide the dialog.
            evt.Cancel = true;

            s_singleton.Hide();
        }


        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.textBoxMessage = new System.Windows.Forms.TextBox();
            this.buttonContinueProfile = new System.Windows.Forms.Button();
            this.buttonStopBot = new System.Windows.Forms.Button();
            this.checkBoxSuppressAudio = new System.Windows.Forms.CheckBox();
            this.periodicWarningTimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // textBoxMessage
            // 
            this.textBoxMessage.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.textBoxMessage.Location = new System.Drawing.Point(12, 12);
            this.textBoxMessage.Multiline = true;
            this.textBoxMessage.Name = "textBoxMessage";
            this.textBoxMessage.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxMessage.Size = new System.Drawing.Size(393, 151);
            this.textBoxMessage.TabIndex = 0;
            // 
            // buttonContinueProfile
            // 
            this.buttonContinueProfile.Location = new System.Drawing.Point(315, 169);
            this.buttonContinueProfile.Name = "buttonContinueProfile";
            this.buttonContinueProfile.Size = new System.Drawing.Size(90, 23);
            this.buttonContinueProfile.TabIndex = 1;
            this.buttonContinueProfile.Text = "Continue Profile";
            this.buttonContinueProfile.UseVisualStyleBackColor = true;
            this.buttonContinueProfile.Click += new System.EventHandler(this.buttonContinueProfile_Click);
            // 
            // buttonStopBot
            // 
            this.buttonStopBot.Enabled = false;
            this.buttonStopBot.Location = new System.Drawing.Point(234, 169);
            this.buttonStopBot.Name = "buttonStopBot";
            this.buttonStopBot.Size = new System.Drawing.Size(75, 23);
            this.buttonStopBot.TabIndex = 2;
            this.buttonStopBot.Text = "Stop Bot";
            this.buttonStopBot.UseVisualStyleBackColor = true;
            this.buttonStopBot.Click += new System.EventHandler(this.buttonStopBot_Click);
            // 
            // checkBoxSuppressAudio
            // 
            this.checkBoxSuppressAudio.AutoSize = true;
            this.checkBoxSuppressAudio.Location = new System.Drawing.Point(12, 173);
            this.checkBoxSuppressAudio.Name = "checkBoxSuppressAudio";
            this.checkBoxSuppressAudio.Size = new System.Drawing.Size(197, 17);
            this.checkBoxSuppressAudio.TabIndex = 3;
            this.checkBoxSuppressAudio.Text = "Suppress Periodic Audible Warnings";
            this.checkBoxSuppressAudio.UseVisualStyleBackColor = true;
            // 
            // PeriodicWarningTimer
            // 
            this.periodicWarningTimer.Enabled = true;
            this.periodicWarningTimer.Interval = 5000;
            this.periodicWarningTimer.Tick += new System.EventHandler(this.periodicWarningTimer_Tick);
            // 
            // UserDialogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(417, 204);
            this.Controls.Add(this.checkBoxSuppressAudio);
            this.Controls.Add(this.buttonStopBot);
            this.Controls.Add(this.buttonContinueProfile);
            this.Controls.Add(this.textBoxMessage);
            this.Name = "UserDialogForm";
            this.Text = "UserDialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion


        private void buttonContinueProfile_Click(object sender, EventArgs e)
        {
            _usersChoice = UsersChoice.BOT_CONTINUE;

            // Note that the Singleton_FormClosing handler prevents the dialog from
            // actually closing, and instead just hides it.  This prevents the
            // resources associated with the dialog from being released--which would
            // be a bad thing for a Singleton pattern.
            Close();
        }


        private void buttonStopBot_Click(object sender, EventArgs e)
        {
            _usersChoice = UsersChoice.BOT_STOP;

            // Note that the Singleton_FormClosing handler prevents the dialog from
            // actually closing, and instead just hides it.  This prevents the
            // resources associated with the dialog from being released--which would
            // be a bad thing for a Singleton pattern.
            Close();
        }


        private void periodicWarningTimer_Tick(object sender, EventArgs e)
        {
            if (!checkBoxSuppressAudio.Checked)
                { _soundCue.Play(); }
        }


        public enum UsersChoice
        {
            BOT_CONTINUE,
            BOT_STOP,
        }

        public UsersChoice        popupDialog(string                        title,
                                              string                        message,
                                              bool                          isBotStopAllowed,
                                              System.Media.SystemSound      soundCue,
                                              int                           soundPeriodInSeconds)
        {
            _usersChoice    = UsersChoice.BOT_CONTINUE;
            _soundCue        = soundCue;

            message = message.Replace("\\n", Environment.NewLine);
            message = message.Replace("\\t", "\t");

            this.Text                          = "[Honorbuddy UserDialog] " + title;
            this.textBoxMessage.Text           = message;
            this.textBoxMessage.SelectionStart = this.textBoxMessage.SelectionLength;
            this.buttonStopBot.Enabled         = isBotStopAllowed;


            // Setup the audible warnings --
            // Note: *Never* try to set the periodicWarningTimer.Interval to zero.
            // Doing so will trigger a Windoze bug that prevents the dialog from
            // opening.
            switch (soundPeriodInSeconds)
            {
              case 0:
                // Play no sound--nothing to do
                this.checkBoxSuppressAudio.Enabled = false;
                break;

              case 1:
                // Play sound once for dialog open
                soundCue.Play();
                periodicWarningTimer.Enabled = false;
                this.checkBoxSuppressAudio.Enabled = false;
                break;

              default:
                // Play sound now for dialog open, then
                // arrange to play the sound at the specified intervals
                soundCue.Play();
                periodicWarningTimer.Interval = soundPeriodInSeconds * 1000;
                periodicWarningTimer.Enabled  = true;
                this.checkBoxSuppressAudio.Enabled = true;
                break;
            }

            // Popup the window
            this.Activate();
            this.ShowDialog(_windowParent);

            // Turn the timer off --
            periodicWarningTimer.Enabled = false;

            return (_usersChoice);
        }

        // VS-generated data members
        private System.Windows.Forms.Button     buttonContinueProfile;
        private System.Windows.Forms.Button     buttonStopBot;
        private System.Windows.Forms.CheckBox   checkBoxSuppressAudio;
        private System.Windows.Forms.TextBox    textBoxMessage;
        private System.Windows.Forms.Timer      periodicWarningTimer;

        // Hand-written data members
        private System.Media.SystemSound        _soundCue;
        private UsersChoice                     _usersChoice;
        private Form                            _windowParent;

        private static UserDialogForm           s_singleton;
    }


    public class UserDialog : CustomForcedBehavior
    {
        public UserDialog(Dictionary<string, string> args)
            : base(args)
        {
            String        soundCueName  = "";

            _isBehaviorDone = false;

            CheckForUnrecognizedAttributes(s_recognizedAttributeNames);

            _isAttributesOkay = true;
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out _questId);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("Title", false, "Attention Required...", out _dialogTitle);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("Text", true, "", out _dialogMessage);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsBoolean("AllowBotStop", false, "false", out _isBotStopAllowed);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsSpecificString("SoundCue", false, "Asterisk", s_soundsAllowed, out soundCueName);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("SoundCueInterval", false, "60", 0, int.MaxValue, out _soundCueIntervalInSeconds);

            if (_isAttributesOkay)
                { _soundCue = (System.Media.SystemSound)s_soundsAllowed[soundCueName]; }
        }


        private void UtilLogMessage(string messageType,
                                    string message)
        {
            string  behaviorName = this.GetType().Name;
            Color   messageColor = Color.Black;

            if (messageType == "error")
                messageColor = Color.Red;
            else if (messageType == "warning")
                messageColor = Color.DarkOrange;
            else if (messageType == "info")
                messageColor = Color.Navy;

            Logging.Write(messageColor, String.Format("[Behavior: {0}({1})]: {2}", behaviorName, messageType, message));
        }


        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                PlayerQuest        quest = StyxWoW.Me.QuestLog.GetQuestById((uint)_questId);

                // Note that a _questId of zero is never complete (by definition), it requires the behavior to complete...
                return (_isBehaviorDone                                                         // normal completion
                        ||  ((_questId != 0) && (quest == null))                                // quest not in our log
                        ||  ((_questId != 0) && (quest != null) && quest.IsCompleted));         // quest is done
            }
        }


        public override void OnStart()
        {
            if (!_isAttributesOkay)
            {
                UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");
                TreeRoot.Stop();
            }

            else if (!IsDone)
            {
                UserDialogForm.UsersChoice    usersChoice;
                UserDialogForm                userDialogForm    = UserDialogForm.Singleton();

                TreeRoot.GoalText   = "User Attention Required...";
                TreeRoot.StatusText = "Waiting for user confirmation of dialog";
                usersChoice = userDialogForm.popupDialog(_dialogTitle,
                                                         _dialogMessage,
                                                         _isBotStopAllowed,
                                                         _soundCue,
                                                         _soundCueIntervalInSeconds);
                TreeRoot.GoalText   = "";
                TreeRoot.StatusText = "";

                if (usersChoice == UserDialogForm.UsersChoice.BOT_STOP)
                {
                    TreeRoot.StatusText = "Honorbuddy stopped at User's request";
                    UtilLogMessage("user response", "Stopping Bot at User request.");
                    TreeRoot.Stop();
                }
               
                _isBehaviorDone = true;
            }
        }

        #endregion



        private string                      _dialogTitle;
        private string                      _dialogMessage;
        private bool                        _isAttributesOkay;
        private bool                        _isBehaviorDone;
        private bool                        _isBotStopAllowed;
        private int                         _questId;
        private System.Media.SystemSound    _soundCue;
        private int                         _soundCueIntervalInSeconds;

        private static Dictionary<String, Object>    s_soundsAllowed  = new Dictionary<string, object>()
                       {
                            { "Asterisk",        System.Media.SystemSounds.Asterisk },
                            { "Beep",            System.Media.SystemSounds.Beep },
                            { "Exclamation",     System.Media.SystemSounds.Exclamation },
                            { "Hand",            System.Media.SystemSounds.Hand },
                            { "Question",        System.Media.SystemSounds.Question },
                       };

        private static Dictionary<String, Object>    s_recognizedAttributeNames = new Dictionary<string, object>()
                       {
                            { "AllowBotStop",        null },
                            { "QuestId",             null },
                            { "Title",               null },
                            { "Text",                null },
                            { "SoundCue",            null },
                            { "SoundCueInterval",    null },
                       };
    }
}
