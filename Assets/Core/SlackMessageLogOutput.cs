using System;
using System.IO;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;
using ArtCom.Logging.Internal;

namespace ArtCom.Logging {

    /// <summary>
    /// Forwards log messages to a Slack webhook integration endpoint.
    /// </summary>
    public class SlackMessageLogOutput : ILogOutput
    {
        private string _webhookUrl = null;
        private string _machineId = null;
        private string _targetChannel = null;
        private LogMessageTypeFilter _severityFilter = LogMessageTypeFilter.All;
        private object _writerLock = new object();
        private UnityWebRequestScheduler _requestHandler = null;

        /// <summary>
        /// [GET / SET] The machine ID that is displayed below the Slack message.
        /// Defaults to the machine / device name as retrieved from the system.
        /// </summary>
        public string MachineId
        {
            get { return _machineId; }
            set { _machineId = value; }
        }
        /// <summary>
        /// [GET / SET] The channel or user to send messages to. Prefix with '#' or '@'
        /// as if mentioning it in a Slack message.
        /// </summary>
        public string TargetChannel
        {
            get { return _targetChannel; }
            set { _targetChannel = value; }
        }
        /// <summary>
        /// [GET / SET] An optional filter that a message needs to match in order to be sent.
        /// This can be used to attach a Slack output to a common shared log, but only send
        /// error messages, or similar.
        /// </summary>
        /// <value>The severity filter.</value>
        public LogMessageTypeFilter SeverityFilter
        {
            get { return _severityFilter; }
            set { _severityFilter = value; }
        }

        public SlackMessageLogOutput(string webhookUrl)
        {
            _webhookUrl = webhookUrl;
            _machineId = SystemInfo.deviceName;
        }

        public virtual void Write(Log source, LogEntry entry, object context)
        {
            // Skip messages that don't match our output severity filter
            if (!_severityFilter.Matches(entry.Type)) return;

            lock (_writerLock)
            {
                SlackMessageData message = new SlackMessageData();
                message.channel = _targetChannel;
                message.attachments = new SlackAttachmentData[1];
                message.attachments[0].text = EscapeMessageText(entry.Message);
                message.attachments[0].fallback = message.attachments[0].text;
                message.attachments[0].ts = GetUnixTime(entry.TimeStamp);
                message.attachments[0].footer = string.Format("{0} on {1}", source.Name, _machineId);

                switch (entry.Type)
                {
                case LogMessageType.Error:
                case LogMessageType.Fatal:
                    message.attachments[0].color = "danger";
                    break;
                case LogMessageType.Warning:
                    message.attachments[0].color = "warning";
                    break;
                }

                string payload = JsonUtility.ToJson(message, true);
                byte[] payloadData = Encoding.UTF8.GetBytes(payload);

                UnityWebRequest postRequest = new UnityWebRequest(_webhookUrl, "POST");
                postRequest.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                
                UploadHandlerRaw rawUpload = new UploadHandlerRaw(payloadData);
                postRequest.uploadHandler = rawUpload;
                postRequest.disposeUploadHandlerOnDispose = true;

                if (_requestHandler == null)
                    _requestHandler = UnityWebRequestScheduler.Create();
                
                _requestHandler.Schedule(postRequest);
            }
        }
        public void PushIndent() {}
        public void PopIndent() {}

        private static long GetUnixTime(DateTime utcTimestamp)
        {
            return (long)(utcTimestamp.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }
        private static string EscapeMessageText(string text)
        {
            // Escape according to Slack API docs. These are all the
            // characters that need to be encoded. Don't HTML encode the
            // entire message.
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        [Serializable]
        private struct SlackMessageData
        {
            public string text;
            public string channel;
            public SlackAttachmentData[] attachments;
        }
        [Serializable]
        private struct SlackAttachmentData
        {
            public string fallback;
            public string text;
            public string footer;
            public string color;
            public long ts;
        }
    }

}