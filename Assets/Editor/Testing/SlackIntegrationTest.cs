using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

using UnityEngine;
using UnityEngine.Networking;

using ArtCom.Logging;

namespace LogTests
{
    public class SlackIntegrationTest : MonoBehaviour
    {
        [SerializeField] private string _webhookUrl = "https://hooks.slack.com/services/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";
        private Log _privateSlackLog = null;

        private void Awake()
        {
            // For the purpose of this test, we'll create a private log, so you
            // can have multiple slack integration test behaviours doing different
            // things without interfering with each other.
            //
            // Normally, you would define a custom log info and add the slack output
            // in its init method - or just attach it to an existing log channel.
            //
            _privateSlackLog = new Log("SlackLog");
            foreach (ILogOutput globalOutput in Logs.GlobalLogOutput)
            {
                _privateSlackLog.AddOutput(globalOutput);
            }

            SlackMessageLogOutput slackOut = new SlackMessageLogOutput(_webhookUrl);
            //
            // Use this to only send messages of a certain severity:
            //
            //slackOut.SeverityFilter = LogMessageTypeFilter.Irregular;
            //

            // Set the target channel or user to message using the TargetChannel property.
            // When not set, it will be sent to whatever channel or user is set in the webhook,
            // likely some test or temp channel. You can configure this here:
            // https://artcom.slack.com/apps/A0F7XDUAZ-incoming-webhooks
            //
            //slackOut.TargetChannel = "@yourusername";
            //slackOut.TargetChannel = "#some-channel";
            //
            _privateSlackLog.AddOutput(slackOut);
        }
        private IEnumerator Start()
        {
            _privateSlackLog.Write("Hello World!");

            yield return new WaitForSeconds(1.0f);

            _privateSlackLog.Write("Emoji Test: \ud83c\udf0a");
            
            yield return new WaitForSeconds(1.0f);
            
            _privateSlackLog.Write("Encoding Test: Ampersand '&'");
            _privateSlackLog.Write("Encoding Test: Less Than '<'");
            _privateSlackLog.Write("Encoding Test: Greater Than '>'");
            
            yield return new WaitForSeconds(1.0f);
            
            _privateSlackLog.Write("Link Test: https://api.slack.com/docs/message-formatting");
            
            yield return new WaitForSeconds(1.0f);
            
            _privateSlackLog.Write("This is a multi-line text." + Environment.NewLine + "I'm the second line.");
            _privateSlackLog.WriteWarning("This is a warning.");
            _privateSlackLog.WriteError("This is an error.");
            _privateSlackLog.WriteFatal("This is a fatal error.");
            _privateSlackLog.WriteDebug("This is a debug message.");
        }
    }
}
