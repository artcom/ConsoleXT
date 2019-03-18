using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

// This is based on https://github.com/bbbscarter/UberLogger/blob/master/Editor/UberLoggerEditorWindow.cs
// ToDo: Clean up this code a bit and adjust to style guidelines


// Nice2Have Ideas
//
// ToDo: Introduce horizontal resize for source code area on the right
// ToDo: Display lines and files in call stack details view
// ToDo: Introduce optional "stack frame" column, showing the immediate surrounding method and class name
// ToDo: Introduce optional "thread" column, showing the name and / or managed thread id of the logging thread
// ToDo: Introduce optional "timing" column, showing the milliseconds passed since the last log entry. Make it as accurate as possible for quick perf checks
// ToDo: Smooth scrolling for enhanced visual feedback?

namespace ArtCom.Logging.Editor {

    public class LogEditorWindow : EditorWindow
    {
        // Static environment / editor data
        private static Texture2D _errorIcon;
        private static Texture2D _warningIcon;
        private static Texture2D _messageIcon;
        private static Texture2D _errorIconSmall;
        private static Texture2D _warningIconSmall;
        private static Texture2D _messageIconSmall;

        private static Texture2D ErrorIcon
        {
            get { return (_errorIcon = _errorIcon ?? EditorGUIUtility.FindTexture("d_console.erroricon")); }
        }
        private static Texture2D WarningIcon
        {
            get { return (_warningIcon = _warningIcon ?? EditorGUIUtility.FindTexture("d_console.warnicon")); }
        }
        private static Texture2D MessageIcon
        {
            get { return (_messageIcon = _messageIcon ?? EditorGUIUtility.FindTexture("d_console.infoicon")); }
        }
        private static Texture2D ErrorIconSmall
        {
            get { return (_errorIconSmall = _errorIconSmall ?? EditorGUIUtility.FindTexture("d_console.erroricon.sml")); }
        }
        private static Texture2D WarningIconSmall
        {
            get { return (_warningIconSmall = _warningIconSmall ?? EditorGUIUtility.FindTexture("d_console.warnicon.sml")); }
        }
        private static Texture2D MessageIconSmall
        {
            get { return (_messageIconSmall = _messageIconSmall ?? EditorGUIUtility.FindTexture("d_console.infoicon.sml")); }
        }

        [MenuItem("Window/Console XT")]
        public static void ShowLogWindow()
        {
            LogEditorWindow window = ScriptableObject.CreateInstance<LogEditorWindow>();
            window.Show();
            window.position = new Rect(200, 200, 400, 300);
            window.titleContent = new GUIContent("Console XT");
            window._topPaneHeight = window.position.height / 2;
        }
        private static Texture2D GetIconForLog(EditorWindowLogEntry logEntry)
        {
            if (logEntry.Log.Type == LogMessageType.Fatal  ) return ErrorIconSmall;
            if (logEntry.Log.Type == LogMessageType.Error  ) return ErrorIconSmall;
            if (logEntry.Log.Type == LogMessageType.Warning) return WarningIconSmall;
            return MessageIconSmall;
        }
        private static int GetEqualBeginChars(string a, string b)
        {
            int minLen = Math.Min(a.Length, b.Length);
            int lastBreakCount = 0;
            int i = 0;
            int j = 0;
            while (i < a.Length && j < b.Length)
            {
                // Skip whitespace / indentation
                if (a[i] == ' ') { ++i; continue; }
                if (b[j] == ' ') { ++j; lastBreakCount = j; continue; }
                
                if (a[i] != b[j])
                    return lastBreakCount;

                if (!char.IsLetterOrDigit(b[j]))
                    lastBreakCount = j + 1;
                
                ++i;
                ++j;
            }
            return minLen;
        }
        private static int GetEqualEndChars(string a, string b)
        {
            int minLen = Math.Min(a.Length, b.Length);
            int lastBreakCount = 0;
            for (int i = 0; i < minLen; i++)
            {
                if (a[a.Length - 1 - i] != b[b.Length - 1 - i])
                    return lastBreakCount;
                if (!char.IsLetterOrDigit(a[a.Length - 1 - i]))
                    lastBreakCount = i + 1;
            }
            return minLen;
        }


        // Editor settings and persistent data
        [SerializeField] private EditorWindowLogOutput _editorLogOutput; // Don't forget about logs at stop, play & compile
        [SerializeField] private bool _clearOnPlay;
        [SerializeField] private bool _pauseOnError;
        [SerializeField] private List<string> _channelFilter = new List<string>(new [] { "System" });
        [SerializeField] private bool _showErrors = true; 
        [SerializeField] private bool _showWarnings = true; 
        [SerializeField] private bool _showMessages = true; 
        [SerializeField] private bool _showTimes = true;
        [SerializeField] private bool _showSeparators = true;
        [SerializeField] private bool _greyOutRedundant = true;
        [SerializeField] private bool _showTraceDetails = true;
        [SerializeField] private float _topPaneHeight;
        
        // Styling, mostly generated on-the-fly to react to skin changes
        private bool _updateStyles = true;
        private GUISkin _baseEditorSkin;
        private GUIStyle _logTimeStampLabelStyle;
        private GUIStyle _logSourceLabelStyle;
        private GUIStyle _logTypeLabelStyle;
        private GUIStyle _logMessageLabelStyle;
        private GUIStyle _logRightAlignHeaderLabelStyle;
        private GUIStyle _logHeaderLabelStyle;
        private Color _lineColorSelect;
        private Color _lineColorEven;
        private Color _lineColorOdd;
        private Color _sizerLineColour;
        private GUIStyle _logLineStyle;
        private GUIStyle _selectedLogLineStyle;
        private GUIStyle _detailLogMessageStyle;
        private GUIStyle _detailStackTraceStyleScope;
        private GUIStyle _detailStackTraceStyleLine;
        private GUIStyle _detailSourceCodeStyleTop;
        private GUIStyle _detailSourceCodeStyleMain;
        private GUIStyle _detailSourceCodeStyleBottom;
        
        // UI state
        private float _currentTopPaneHeight;
        private int _selectedLogEntry = -1;
        private int _selectedCallStackFrame = 0;
        private bool _userSelectedCallStackFrame = false;
        
        private string _filterRegExString = null;
        private Regex _filterRegEx = null;

        private int _lastReceivedLogIndex = -1;
        private List<int> _logListFilteredIndices = new List<int>();
        private bool _logListAutoSelectPending;
        private bool _logListSelectedLast;
        private bool _logListScrolledToEnd;
        private Vector2 _logListScrollPosition;
        private int _logListScrolledToEndItemCount = 0;
        private float _logListFirstFilteredItemViewPos = 0;
        private int _logListFirstFilteredItemInView = 0;
        private int _logListLastFilteredItemInView = -1;
        private Rect _logListLastRect = new Rect();
        
        private EditorWindowLogEntry _displayedDetailLogEntry;
        private bool[] _displayedDetailSourceAvailable;
        private Vector2 _logDetailsMessageScrollPosition;
        private bool _displayDetailStackTrace;
        private Vector2 _logDetailsStackTraceScrollPosition;
        
        private bool _resizeInProgress = false;
        private Rect _resizeCursorRect;
        
        private double _lastMessageClickTime = 0;
        private double _lastFrameClickTime = 0;
        
        private int _lastErrorCount = 0;
        private bool _wasPlaying = false;
        
        // Cache for GetSourceForFrame
        private string[] _displayedSourceCode = null;
        private StackFrame _sourceLinesFrame;


        private float LogItemHeight
        {
            get
            {
                return 
                    _logMessageLabelStyle.CalcSize(new GUIContent("Test")).y + 
                    _logMessageLabelStyle.padding.vertical + 
                    _logMessageLabelStyle.margin.vertical + 
                    _logMessageLabelStyle.border.vertical;
            }
        }
        private int SelectedLogEntry
        {
            get { return _selectedLogEntry; }
            set
            {
                if (_selectedLogEntry != value)
                {
                    _selectedLogEntry = value;
                    OnSelectedLogEntryChanged();
                    OnSelectedCallStackFrameChanged();
                }
            }
        }
        private int SelectedCallStackFrame
        {
            get { return _selectedCallStackFrame; }
            set
            {
                if (_selectedCallStackFrame != value)
                {
                    _selectedCallStackFrame = value;
                    OnSelectedCallStackFrameChanged();
                }
            }
        }
        private string FilterRegEx
        {
            get { return _filterRegExString; }
            set
            {
                if (_filterRegExString != value)
                {
                    _filterRegExString = value;
                    if (!string.IsNullOrEmpty(_filterRegExString))
                        _filterRegEx = new Regex(_filterRegExString, RegexOptions.IgnoreCase);
                    else
                        _filterRegEx = null;
                    OnFilterRegExChanged();
                }
            }
        }


        private void OnEnable()
        {
            minSize = new Vector2(400, 200);
            _updateStyles = true;

            if (_editorLogOutput == null)
                _editorLogOutput = new EditorWindowLogOutput();

            _editorLogOutput.LogReceived += EditorLogOutput_LogReceived;
            Logs.AddGlobalOutput(_editorLogOutput);

            RebuildFilteredIndexList();
            
            EditorApplication.playmodeStateChanged += EditorApplication_playmodeStateChanged;
        }
        private void OnDisable()
        {
            _editorLogOutput.LogReceived -= EditorLogOutput_LogReceived;
            EditorApplication.playmodeStateChanged -= EditorApplication_playmodeStateChanged;
            Logs.RemoveGlobalOutput(_editorLogOutput); 
        }
        private void OnGUI()
        {
            // Perform pending auto-select
            if (_logListAutoSelectPending)
            {
                SelectedLogEntry = _logListFilteredIndices.Count - 1;
                _logListAutoSelectPending = false;
            }

            if (_updateStyles)
            {
                UpdateGUIStyles();
            }

            GUILayout.BeginVertical(GUILayout.Height(_currentTopPaneHeight));
            HandleGUIToolbar();
            HandleGUILogList();
            GUILayout.EndVertical();
            HandleGUIVerticalSplitter();

            HandleGUILogDetails();
        }
        private void Update()
        {
            _editorLogOutput.PollMessages();
        }

        private void HandleGUIToolbar()
        {
            // General log view settings and actions
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    Clear();
                GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
                _clearOnPlay  = GUILayout.Toggle(_clearOnPlay, "Clear on Play", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                _pauseOnError = GUILayout.Toggle(_pauseOnError, "Error Pause", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
                _greyOutRedundant = GUILayout.Toggle(_greyOutRedundant, "Redundancy", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                _showTimes = GUILayout.Toggle(_showTimes, "Timestamps", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                _showSeparators = GUILayout.Toggle(_showSeparators, "Separators", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
                _showTraceDetails = GUILayout.Toggle(_showTraceDetails, "Trace Details", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            }

            GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));

            // Filter by custom keyword or regex
            {
                bool clearClicked = false;
                if (GUILayout.Button(string.IsNullOrEmpty(FilterRegEx) ? "Filter by:" : "Clear Filter", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    clearClicked = true;

                string newFilterRegexString = EditorGUILayout.TextArea(FilterRegEx, EditorStyles.toolbarTextField, GUILayout.Width(200));
                if (clearClicked)
                {
                    newFilterRegexString = null;
                    GUIUtility.keyboardControl = 0;
                    GUIUtility.hotControl = 0;
                }

                FilterRegEx = newFilterRegexString;
            }

            GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
            
            // Filter by channel
            {
                bool channelFilterChanged = false;

                // "All" Channels
                {
                    bool allActive = (_channelFilter.Count == 0);
                    bool newAllActive = GUILayout.Toggle(allActive, "All", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                    if (!allActive && newAllActive)
                    {
                        _channelFilter.Clear();
                        channelFilterChanged = true;
                    }
                    else if (allActive && !newAllActive)
                    {
                        _channelFilter.Clear();
                        _channelFilter.AddRange(_editorLogOutput.KnownChannels);
                        channelFilterChanged = true;
                    }
                }

                // Individual Channels
                foreach (string channel in _editorLogOutput.KnownChannels)
                {
                    bool channelActive = !_channelFilter.Contains(channel);
                    bool newChannelActive = GUILayout.Toggle(channelActive, channel, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                    if (channelActive && !newChannelActive)
                    {
                        _channelFilter.Add(channel);
                        channelFilterChanged = true;
                    }
                    else if (newChannelActive && !channelActive)
                    {
                        _channelFilter.Remove(channel);
                        channelFilterChanged = true;
                    }
                }

                if (channelFilterChanged)
                {
                    OnFilterChannelChanged();
                }
            }

            GUILayout.Label(string.Empty, EditorStyles.toolbarButton, GUILayout.Width(6));
            
            // Filter by message type
            {
                bool showMessages = GUILayout.Toggle(_showMessages, new GUIContent(_editorLogOutput.MessageCount.ToString(), MessageIconSmall), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                bool showWarnings = GUILayout.Toggle(_showWarnings, new GUIContent(_editorLogOutput.WarningCount.ToString(), WarningIconSmall), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                bool showErrors   = GUILayout.Toggle(_showErrors  , new GUIContent(_editorLogOutput.ErrorCount  .ToString(), ErrorIconSmall  ), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                
                if (showErrors != _showErrors || showWarnings != _showWarnings || showMessages != _showMessages)
                {
                    _showWarnings = showWarnings;
                    _showMessages = showMessages;
                    _showErrors = showErrors;
                    OnFilterTypeChanged();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        private void HandleGUILogList()
        {
            Color oldTextColor = GUI.contentColor;
            Color oldBackColor = GUI.backgroundColor;
            bool darkBackground = EditorGUIUtility.isProSkin;
                
            float itemHeight = LogItemHeight;

            // Handle layout events
            if (Event.current.type == EventType.Layout)
            {
                // Determine which items are visible only during the layout phase,
                // then stick to it. This is an immediate mode UI requirement.
                _logListFirstFilteredItemInView = _editorLogOutput.LogEntries.Count + 1;
                _logListLastFilteredItemInView = -1;
                _logListFirstFilteredItemViewPos = -1;

                float maxLogPanelHeight = position.height;
                float itemBufferVirtualHeight = maxLogPanelHeight;
                float itemY = 0;
                for(int i = 0; i < _logListFilteredIndices.Count; i++)
                {
                    // Skip items that are clearly out of view. Not that this is not accurate at all, since
                    // we have no way of knowing the exact available height at this point. Also, we actually
                    // do want a "buffer zone" of visible items around the actual visible are, so scrolling
                    // repaints will show elements right away, without havign to wait for the next layout.
                    if (itemY + itemHeight < _logListScrollPosition.y - itemBufferVirtualHeight || itemY > _logListScrollPosition.y + maxLogPanelHeight)
                    {
                        itemY += itemHeight;
                        continue;
                    }

                    _logListFirstFilteredItemInView = Math.Min(i, _logListFirstFilteredItemInView);
                    _logListLastFilteredItemInView = Math.Max(i, _logListLastFilteredItemInView);

                    if (_logListFirstFilteredItemViewPos < 0) _logListFirstFilteredItemViewPos = itemY;
                    
                    itemY += itemHeight;
                }
            }

            // Draw the header region
            float availMessageWidth = position.width - GUI.skin.verticalScrollbar.fixedWidth - 4 - 5;
            GUI.backgroundColor = _lineColorOdd;
            GUILayout.BeginHorizontal(_logLineStyle);
            if (_showTimes)
            {
                GUILayout.Label("Date", _logRightAlignHeaderLabelStyle, GUILayout.Width(75));
                GUILayout.Label("Time", _logRightAlignHeaderLabelStyle, GUILayout.Width(65));
                GUILayout.Label("Frame", _logRightAlignHeaderLabelStyle, GUILayout.Width(50));
                availMessageWidth -= 75 + 65 + 50;
            }
            GUILayout.Label("Source", _logRightAlignHeaderLabelStyle, GUILayout.Width(75));
            GUILayout.Space(25);
            availMessageWidth -= 75 + 25 + 15;
            GUILayout.Label("Message", _logHeaderLabelStyle);
            GUILayout.EndHorizontal();
            GUI.backgroundColor = oldBackColor;
            
            // Handle everything that is event-type agnostic
            {
                EditorGUILayout.BeginHorizontal();

                // Draw the "scrolled to end" / autoscroll indicator
                GUI.backgroundColor = 
                    _logListScrolledToEnd ? 
                        new Color(_baseEditorSkin.settings.selectionColor.r,
                                  _baseEditorSkin.settings.selectionColor.g,
                                  _baseEditorSkin.settings.selectionColor.b,
                                  _baseEditorSkin.settings.selectionColor.a * (darkBackground ? 0.5f : 0.75f)) : 
                        new Color(0, 0, 0, 0);
                GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Width(4), GUILayout.ExpandHeight(true));
                GUI.backgroundColor = oldBackColor;

                // If we were scrolled to the end and got new log entries since last time, auto-scroll
                if (_logListScrolledToEnd && _logListFilteredIndices.Count != _logListScrolledToEndItemCount)
                {
                    _logListScrolledToEndItemCount = _logListFilteredIndices.Count;
                    _logListScrollPosition.y = _logListFilteredIndices.Count * itemHeight;
                }
                Vector2 newScrollPos = EditorGUILayout.BeginScrollView(_logListScrollPosition);
                if (newScrollPos != _logListScrollPosition) Repaint();
                _logListScrollPosition = newScrollPos;
                _logListScrolledToEnd = _logListScrollPosition.y + _logListLastRect.height >= _logListFilteredIndices.Count * itemHeight;

                // Reserve virtual space that represents items outside our current view
                GUILayout.Space(_logListFirstFilteredItemViewPos);

                // Draw all the items
                float itemY = _logListFirstFilteredItemViewPos;
                int lastDisplayedFrameStamp = 0;
                string[] lastDisplayedLogLines = new string[3];
                int lastDisplayedLogLineIndex = 0;
                for (int i = _logListFirstFilteredItemInView; i <= _logListLastFilteredItemInView; i++)
                {
                    if (i >= _logListFilteredIndices.Count) break;

                    int entryIndex = _logListFilteredIndices[i];
                    EditorWindowLogEntry logEntry = _editorLogOutput.LogEntries[entryIndex];

                    // Determine the style of the entire log line
                    GUIStyle logLineStyle;
                    float messageAlpha;
                    if (i == _selectedLogEntry)
                    {
                        messageAlpha = 1.0f;
                        logLineStyle = _selectedLogLineStyle;
                        GUI.backgroundColor = _lineColorSelect;
                    }
                    else
                    {
                        messageAlpha = 0.75f;
                        logLineStyle = _logLineStyle;
                        GUI.backgroundColor = (i % 2 == 0) ? _lineColorEven : _lineColorOdd;
                    }

                    // Determine the color by message type
                    Color baseMessageColor = darkBackground ? Color.white : Color.black;
                    Color messageTypeColor = Color.white;
                    Color messageColor = new Color(baseMessageColor.r, baseMessageColor.g, baseMessageColor.b, messageAlpha);
                    Color iconColor = Color.white;
                    switch (logEntry.Log.Type) {
                    default:
                    case LogMessageType.Debug:
                        messageColor = new Color(baseMessageColor.r, baseMessageColor.g, baseMessageColor.b, messageAlpha * 0.5f);
                        iconColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
                        messageTypeColor = darkBackground ? new Color32(173, 230, 201, 255) : new Color32(0, 128, 128, 255);
                        messageTypeColor.a = messageColor.a;
                        break;
                    case LogMessageType.Message:
                        messageTypeColor = darkBackground ? new Color32(173, 216, 230, 255) : new Color32(0, 0, 128, 255);
                        messageTypeColor.a = messageColor.a;
                        break;
                    case LogMessageType.Warning:
                        messageTypeColor = darkBackground ? new Color32(255, 204, 102, 255) : new Color32(128, 64, 0, 255);
                        messageTypeColor.a = messageColor.a;
                        break;
                    case LogMessageType.Error:
                        messageTypeColor = darkBackground ? new Color32(255, 102, 102, 255) : new Color32(128, 0, 0, 255);
                        messageTypeColor.a = messageColor.a;
                        break;
                    case LogMessageType.Fatal:
                        messageColor = baseMessageColor;
                        messageTypeColor = darkBackground ? new Color32(255, 51, 51, 255) : new Color32(204, 0, 0, 255);
                        messageTypeColor.a = messageColor.a;
                        break;
                    }
                    Color framestampColor = new Color(messageColor.r, messageColor.g, messageColor.b, messageColor.a * 0.75f);
                    Color timestampColor = new Color(messageColor.r, messageColor.g, messageColor.b, messageColor.a * 0.5f);

                    string displayedMessage = logEntry.SingleLineMessage ?? string.Empty;
                    
                    // If we're writing the same kind of text again, "grey out" the repeating parts
                    int beginGreyLength = 0;
                    int endGreyLength   = 0;
                    if (_greyOutRedundant)
                    {
                        for (int lastLogIndex = 0; lastLogIndex < lastDisplayedLogLines.Length; lastLogIndex++)
                        {
                            string lastLogLine = lastDisplayedLogLines[lastLogIndex] ?? string.Empty;
                            beginGreyLength = Math.Max(beginGreyLength, GetEqualBeginChars(lastLogLine, displayedMessage));
                            endGreyLength   = Math.Max(endGreyLength  , GetEqualEndChars  (lastLogLine, displayedMessage));
                        }
                        if (beginGreyLength == displayedMessage.Length)
                            endGreyLength = 0;
                        if (beginGreyLength + endGreyLength >= displayedMessage.Length)
                        {
                            beginGreyLength = displayedMessage.Length;
                            endGreyLength = 0;
                        }
                    }

                    GUILayout.BeginHorizontal(logLineStyle, GUILayout.Height(itemHeight));
                    if (_showTimes)
                    {
                        DateTime localTime = logEntry.Log.TimeStamp.ToLocalTime();
                        GUI.contentColor = timestampColor;
                        GUILayout.Label(localTime.ToString(@"yyyy-MM-dd"), _logTimeStampLabelStyle, GUILayout.Width(75), GUILayout.ExpandHeight(true));
                        GUILayout.Label(localTime.ToString(@"HH\:mm\:ss"), _logTimeStampLabelStyle, GUILayout.Width(65), GUILayout.ExpandHeight(true));
                        GUI.contentColor = framestampColor;
                        GUILayout.Label(logEntry.Log.FrameStamp.ToString(), _logTimeStampLabelStyle, GUILayout.Width(50), GUILayout.ExpandHeight(true));
                        GUI.contentColor = oldTextColor;
                    }
                    GUI.contentColor = messageTypeColor;
                    GUILayout.Label(logEntry.SourceName, _logSourceLabelStyle, GUILayout.Width(75), GUILayout.ExpandHeight(true));
                    GUI.contentColor = iconColor;
                    GUILayout.Label(GetIconForLog(logEntry), _logTypeLabelStyle, GUILayout.Width(25));
                    GUILayout.Space(Math.Max(0, logEntry.Indent) * 15);
                    if (beginGreyLength == 0 && endGreyLength == 0)
                    {
                        GUI.contentColor = messageColor;
                        GUILayout.Label(displayedMessage, _logMessageLabelStyle, GUILayout.ExpandHeight(true), GUILayout.MaxWidth(availMessageWidth));
                    }
                    else
                    {
                        string start = displayedMessage.Substring(0, beginGreyLength);
                        string mid = displayedMessage.Substring(beginGreyLength, displayedMessage.Length - beginGreyLength - endGreyLength);
                        string end = displayedMessage.Substring(displayedMessage.Length - endGreyLength, endGreyLength);

                        float startSize = _logMessageLabelStyle.CalcSize(new GUIContent(start)).x;
                        float midSize = _logMessageLabelStyle.CalcSize(new GUIContent(mid)).x;
                        float endSize = _logMessageLabelStyle.CalcSize(new GUIContent(end)).x;
                        
                        GUI.contentColor = messageColor;
                        GUILayout.Label(displayedMessage, _logMessageLabelStyle, GUILayout.ExpandHeight(true), GUILayout.MaxWidth(availMessageWidth));
                        Rect labelRect = GUILayoutUtility.GetLastRect();

                        Color lineBackColor = GUI.backgroundColor * GUI.color;
                        Color oldColorTint = GUI.color;
                        GUI.color = new Color(lineBackColor.r, lineBackColor.g, lineBackColor.b, 0.5f);
                        GUI.DrawTexture(new Rect(labelRect.x, labelRect.y, startSize - 2, labelRect.height), EditorGUIUtility.whiteTexture);
                        GUI.DrawTexture(new Rect(labelRect.x + startSize + midSize - 2, labelRect.y, endSize + 2, labelRect.height), EditorGUIUtility.whiteTexture);
                        GUI.color = oldColorTint;
                    }
                    GUI.contentColor = messageColor;
                    GUILayout.Label(logEntry.Log.Message.Length > logEntry.SingleLineMessage.Length ? "..." : string.Empty, _logMessageLabelStyle, GUILayout.Width(15));
                    GUI.contentColor = oldTextColor;
                    GUILayout.EndHorizontal();

                    // Draw a faint separator line on frame changes
                    if (_showSeparators && logEntry.Log.FrameStamp != lastDisplayedFrameStamp)
                    {
                        Color oldColorTint = GUI.color;
                        GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.1f) * GUI.color;
                        GUI.DrawTexture(new Rect(0, itemY, position.width, 1), EditorGUIUtility.whiteTexture);
                        GUI.color = oldColorTint;
                    }

                    // Mind the last lines we displayed, so we can grey out similarities / redundancy
                    lastDisplayedLogLines[lastDisplayedLogLineIndex] = displayedMessage;
                    lastDisplayedLogLineIndex = (lastDisplayedLogLineIndex + 1) % lastDisplayedLogLines.Length;
                    lastDisplayedFrameStamp = logEntry.Log.FrameStamp;

                    // Handle mouse events on a per-item basis
                    Rect itemRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                    {
                        // Select a message, or jump to source if it's double-clicked
                        if (i == _selectedLogEntry)
                        {
                            if (EditorApplication.timeSinceStartup - _lastMessageClickTime < 0.3f)
                            {
                                _lastMessageClickTime = 0;
                                if (logEntry.CallStack.IsAvailable)
                                {
                                    JumpToSource(logEntry.CallStack.Frames[0]);
                                }
                            }
                            else
                            {
                                _lastMessageClickTime = EditorApplication.timeSinceStartup;
                            }
                        }
                        else
                        {
                            SelectedLogEntry = i;
                        }
                    }

                    itemY += itemHeight;
                }

                // Draw the end of log marker
                GUI.backgroundColor = new Color(_lineColorEven.r * 1.25f, _lineColorEven.g * 1.25f, _lineColorEven.b * 1.25f);
                GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Height(1));
                GUI.backgroundColor = oldBackColor;

                // Reserve virtual space that represents items outside our current view
                GUILayout.Space((_logListFilteredIndices.Count * itemHeight) - itemY + itemHeight * 0.25f);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndHorizontal();
            }

            // Handle Ctrl+C command to copy selected entry
            if (Event.current.type == EventType.ValidateCommand)
            {
                if (_selectedLogEntry != -1 && Event.current.commandName == "Copy")
                    Event.current.Use();
            } 
            else if (Event.current.type == EventType.ExecuteCommand)
            {
                if (_selectedLogEntry != -1 && Event.current.commandName == "Copy")
                {
                    if (_logListFilteredIndices.Count > _selectedLogEntry)
                    {
                        int entryIndex = _logListFilteredIndices[_selectedLogEntry];
                        EditorWindowLogEntry logEntry = _editorLogOutput.LogEntries[entryIndex];
                        EditorGUIUtility.systemCopyBuffer = logEntry.Log.Message;
                    }
                }
            }

            // Handle arrow key navigation for selected entries
            if (Event.current.type == EventType.KeyDown)
            {
                if (_logListFilteredIndices.Count > 0)
                {
                    if (Event.current.keyCode == KeyCode.DownArrow)
                    {
                        Event.current.Use();
                        if (_selectedLogEntry == -1)
                            SelectedLogEntry = _logListFilteredIndices.Count - 1;
                        else
                            SelectedLogEntry = Math.Min(_logListFilteredIndices.Count - 1, _selectedLogEntry + 1);
                        ScrollDownToIndex(SelectedLogEntry);
                    }
                    else if (Event.current.keyCode == KeyCode.UpArrow)
                    {
                        Event.current.Use();
                        if (_selectedLogEntry == -1)
                            SelectedLogEntry = 0;
                        else
                            SelectedLogEntry = Math.Max(0, _selectedLogEntry - 1);
                        ScrollUpToIndex(SelectedLogEntry);
                    }
                    else if (Event.current.keyCode == KeyCode.PageUp)
                    {
                        Event.current.Use();
                        SelectedLogEntry = 0;
                        ScrollUpToIndex(SelectedLogEntry);
                    }
                    else if (Event.current.keyCode == KeyCode.PageDown)
                    {
                        Event.current.Use();
                        SelectedLogEntry = _logListFilteredIndices.Count - 1;
                        ScrollDownToIndex(SelectedLogEntry);
                    }
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                _logListLastRect = GUILayoutUtility.GetLastRect();
            }
            
            GUI.backgroundColor = oldBackColor;
        }
        private void HandleGUIVerticalSplitter()
        {
            float lastTopPaneHeight = _currentTopPaneHeight;
            if (_selectedLogEntry == -1)
            {
                _currentTopPaneHeight = position.height;
                
                // Keep around the same number ob UI objects to not confuse the layout
                GUILayout.Space(0);
                GUILayout.Space(0);
            }
            else
            {
                //Set up the resize collision rect
                _resizeCursorRect = new Rect(0, _topPaneHeight, position.width, 5);
                Rect expandedCursorChangeRect = new Rect(
                    _resizeCursorRect.x,
                    _resizeCursorRect.y - 3,
                    _resizeCursorRect.width,
                    _resizeCursorRect.height + 6);
                
                var oldColor = GUI.color;
                GUI.color = _sizerLineColour; 
                GUI.DrawTexture(_resizeCursorRect, EditorGUIUtility.whiteTexture);
                GUI.color = oldColor;
                
                // Create a matching gap so the resize handle isn't overwritten
                GUILayout.Space(5);
                EditorGUIUtility.AddCursorRect(expandedCursorChangeRect, MouseCursor.ResizeVertical);
                
                if (Event.current.type == EventType.MouseDown && expandedCursorChangeRect.Contains(Event.current.mousePosition))
                {
                    _resizeInProgress = true;
                }
                
                if (_resizeInProgress)
                {
                    _topPaneHeight = Event.current.mousePosition.y;
                    _resizeCursorRect.Set(_resizeCursorRect.x,_topPaneHeight,_resizeCursorRect.width,_resizeCursorRect.height);
                }
                
                // If we release the mouse while not hovering Unity, we won't get the MouseUp event
                if (Event.current.type == EventType.MouseUp)
                    _resizeInProgress = false;
                
                _topPaneHeight = Mathf.Clamp(_topPaneHeight, 150, position.height - 50);
                _currentTopPaneHeight = _topPaneHeight;
            }
            
            if (lastTopPaneHeight != _currentTopPaneHeight)
                OnDetailSplitterMoved();
        }
        private void HandleGUILogDetails()
        {
            bool darkBackground = EditorGUIUtility.isProSkin;
            float availableHeight = position.height - _currentTopPaneHeight - 5;

            // Retreive the current log entry to display in the details area. This can only happen during
            // layout events, because it will change the UI layout that we build here.
            if (Event.current.type == EventType.Layout)
            {
                _displayedDetailSourceAvailable = null;
                _displayedDetailLogEntry = default(EditorWindowLogEntry);
                _displayDetailStackTrace = false;
                if (_selectedLogEntry != -1 && _logListFilteredIndices.Count > _selectedLogEntry)
                {
                    int entryIndex = _logListFilteredIndices[_selectedLogEntry];
                    EditorWindowLogEntry logEntry = _editorLogOutput.LogEntries[entryIndex];
                    _displayedDetailLogEntry = logEntry;
                    _displayDetailStackTrace = _showTraceDetails && (availableHeight > 60 && logEntry.CallStack.IsAvailable);
                    _displayedDetailSourceAvailable = new bool[logEntry.CallStack.Frames.Length];
                    for (int i = 0; i < _displayedDetailSourceAvailable.Length; i++)
                    {
                        _displayedDetailSourceAvailable[i] = 
                            !string.IsNullOrEmpty(logEntry.CallStack.Frames[i].FileName) &&
                            File.Exists(logEntry.CallStack.Frames[i].FileName);
                    }
                }
            }

            // Early-out, if we have not log entry selected for details
            if (_displayedDetailLogEntry.Log.Message == null) return;
            
            float codePaneWidth = Mathf.Min(600, position.width * 0.5f);
            float stackPaneWidth = position.width - codePaneWidth - 2 - 5 - _baseEditorSkin.verticalScrollbar.fixedWidth;

            Color oldBackColor = GUI.backgroundColor;
            Color oldTextColor = GUI.contentColor;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Display the full, multi-line log entry
            {
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = 
                    darkBackground ?
                    new Color(0.6f, 1.0f, 0.2f, 0.25f) :
                    new Color(0.0f, 0.45f, 0.0f, 0.5f);
                GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Width(4), GUILayout.ExpandHeight(true));
                GUI.backgroundColor = oldBackColor;

                _logDetailsMessageScrollPosition = EditorGUILayout.BeginScrollView(_logDetailsMessageScrollPosition);
                GUIContent multiLineMessage = new GUIContent(_displayedDetailLogEntry.Log.Message);
                float multiLineMessageHeight = _detailLogMessageStyle.CalcHeight(multiLineMessage, position.width - 50);
                GUI.backgroundColor = _lineColorEven;
                GUILayout.Label(_displayedDetailLogEntry.Log.Message, _detailLogMessageStyle, GUILayout.Height(multiLineMessageHeight));
                GUI.backgroundColor = oldBackColor;
                EditorGUILayout.EndScrollView();

                GUILayout.EndHorizontal();
            }

            GUI.backgroundColor = _sizerLineColour;
            GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Height(2));
            GUI.backgroundColor = oldBackColor;
            
            // Display the stack trace of the log entry
            if (_displayDetailStackTrace)
            {
                GUILayout.BeginHorizontal();
                
                GUI.backgroundColor = darkBackground ?
                    new Color(0.8f, 0.2f, 1.0f, 0.25f) :
                    new Color(0.45f, 0.0f, 0.75f, 0.5f);
                GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Width(4), GUILayout.ExpandHeight(true));
                GUI.backgroundColor = oldBackColor;

                LogEntryStrackTrace stackTrace = _displayedDetailLogEntry.CallStack;
                LogEntryStrackFrame[] stackFrames = stackTrace.Frames;

                _logDetailsStackTraceScrollPosition = EditorGUILayout.BeginScrollView(_logDetailsStackTraceScrollPosition);

                // Display all stack frames
                for (int entryIndex = 0; entryIndex < stackFrames.Length; entryIndex++)
                {
                    string frameDesc = stackFrames[entryIndex].ToString();
                    if (string.IsNullOrEmpty(frameDesc)) continue;

                    // Determine style values for this frame
                    bool sourceAvailable = 
                        _displayedDetailSourceAvailable != null &&
                        _displayedDetailSourceAvailable.Length > entryIndex &&
                        _displayedDetailSourceAvailable[entryIndex];
                    Color scopeColor = sourceAvailable ? oldTextColor : new Color(oldTextColor.r, oldTextColor.g, oldTextColor.b, oldTextColor.a * 0.5f);
                    if (_userSelectedCallStackFrame && entryIndex == _selectedCallStackFrame)
                    {
                        GUI.backgroundColor = _lineColorSelect;
                        scopeColor = Color.white;
                    }
                    else
                    {
                        GUI.backgroundColor = _lineColorEven;
                    }
                    Color lineFileColor = new Color(scopeColor.r, scopeColor.g, scopeColor.b, scopeColor.a * 0.5f);
                    
                    EditorGUILayout.BeginHorizontal(_logLineStyle);

                    // Gather displayable stack frame data
                    string fileName = Path.GetFileName(stackFrames[entryIndex].FileName);
                    string lineNumber = stackFrames[entryIndex].LineNumber > 0 ? stackFrames[entryIndex].LineNumber.ToString() : "-";

                    // Display stack frame elements
                    GUI.contentColor = scopeColor;
                    GUILayout.Label(frameDesc, _detailStackTraceStyleScope, GUILayout.Width(stackPaneWidth - 150 - 50));
                    GUI.contentColor = lineFileColor;
                    GUILayout.Label(fileName, _detailStackTraceStyleScope, GUILayout.Width(150));
                    GUILayout.Label(lineNumber, _detailStackTraceStyleLine, GUILayout.Width(50));

                    EditorGUILayout.EndHorizontal();
                    
                    // Handle clicks on the stack frame
                    Rect itemRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                    {
                        Repaint();
                        _userSelectedCallStackFrame = true;
                        if (entryIndex == _selectedCallStackFrame)
                        {
                            if (EditorApplication.timeSinceStartup - _lastFrameClickTime < 0.3f)
                            {
                                _lastFrameClickTime = 0;
                                JumpToSource(stackFrames[entryIndex]);
                            }
                            else
                            {
                                _lastFrameClickTime = EditorApplication.timeSinceStartup;
                            }
                        }
                        else
                        {
                            SelectedCallStackFrame = entryIndex;
                        }
                    }
                }
                GUI.backgroundColor = oldBackColor;
                GUI.contentColor = oldTextColor;
                
                EditorGUILayout.EndScrollView();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUI.backgroundColor = _sizerLineColour;
            GUILayout.Label(string.Empty, _logLineStyle, GUILayout.Width(2), GUILayout.ExpandHeight(true));
            GUI.backgroundColor = oldBackColor;

            // Display the surrounding source code of the selected stack frame
            if (_displayDetailStackTrace)
            {
                HandleGUIFrameSource(_displayedSourceCode, codePaneWidth);
            }

            GUILayout.EndHorizontal();
            GUI.backgroundColor = oldBackColor;
        }
        private void HandleGUIFrameSource(string[] source, float codePaneWidth)
        {
            if (source == null || source.Length < 3) source = new string[3];

            Color oldBackColor = GUI.backgroundColor;
            Color oldTextColor = GUI.contentColor;
            Color greyedOutTextColor = new Color(
                oldTextColor.r,
                oldTextColor.g,
                oldTextColor.b,
                oldTextColor.a * 0.5f);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(false), GUILayout.Width(codePaneWidth), GUILayout.MinHeight(0), GUILayout.ExpandHeight(true));
            GUI.backgroundColor = _lineColorOdd;

            GUI.contentColor = greyedOutTextColor;
            GUILayout.Label(source[0], _detailSourceCodeStyleTop, GUILayout.Width(codePaneWidth), GUILayout.MinHeight(0));
            GUI.contentColor = oldTextColor;
            GUILayout.Label(source[1], _detailSourceCodeStyleMain, GUILayout.Width(codePaneWidth));
            GUI.contentColor = greyedOutTextColor;
            GUILayout.Label(source[2], _detailSourceCodeStyleBottom, GUILayout.Width(codePaneWidth), GUILayout.MinHeight(0), GUILayout.ExpandHeight(true));

            GUI.backgroundColor = oldBackColor;
            GUI.contentColor = oldTextColor;
            GUILayout.EndVertical();
        }

        private void UpdateGUIStyles()
        {
            // Set up the basic style, based on the Unity defaults.
            // A bit hacky, but means we don't have to ship an editor 
            // guistyle and can fit in to pro and free skins.
            _baseEditorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            Color defaultLineColor = EditorGUIUtility.isProSkin
                ? (Color)new Color32(56, 56, 56, 255)
                    : (Color)new Color32(194, 194, 194, 255);
            
            _selectedLogLineStyle = new GUIStyle(EditorStyles.label);
            _selectedLogLineStyle.margin = new RectOffset(0, 0, 0, 0);
            _selectedLogLineStyle.padding = new RectOffset(0, 0, 0, 0);
            _selectedLogLineStyle.normal.background = EditorGUIUtility.whiteTexture;
            _selectedLogLineStyle.active = _selectedLogLineStyle.normal;
            _selectedLogLineStyle.hover = _selectedLogLineStyle.normal;
            _selectedLogLineStyle.focused = _selectedLogLineStyle.normal;
            
            _logLineStyle = new GUIStyle(EditorStyles.label);
            _logLineStyle.margin = new RectOffset(0, 0, 0, 0);
            _logLineStyle.padding = new RectOffset(0, 0, 0, 0);
            _logLineStyle.normal.background = EditorGUIUtility.whiteTexture;
            _logLineStyle.active = _logLineStyle.normal;
            _logLineStyle.hover = _logLineStyle.normal;
            _logLineStyle.focused = _logLineStyle.normal;
            
            _detailLogMessageStyle = new GUIStyle(EditorStyles.label);
            _detailLogMessageStyle.margin = new RectOffset(0, 0, 0, 0);
            _detailLogMessageStyle.padding = new RectOffset(4, 4, 4, 4);
            _detailLogMessageStyle.normal.background = EditorGUIUtility.whiteTexture;
            _detailLogMessageStyle.active = _logLineStyle.normal;
            _detailLogMessageStyle.hover = _logLineStyle.normal;
            _detailLogMessageStyle.focused = _logLineStyle.normal;
            
            _detailStackTraceStyleScope = new GUIStyle(EditorStyles.label);
            _detailStackTraceStyleScope.margin = new RectOffset(0, 0, 0, 0);
            _detailStackTraceStyleScope.padding = new RectOffset(4, 2, 2, 4);
            _detailStackTraceStyleScope.normal.background = EditorGUIUtility.whiteTexture;
            _detailStackTraceStyleScope.active = _logLineStyle.normal;
            _detailStackTraceStyleScope.hover = _logLineStyle.normal;
            _detailStackTraceStyleScope.focused = _logLineStyle.normal;

            _detailStackTraceStyleLine = new GUIStyle(_detailStackTraceStyleScope);
            _detailStackTraceStyleLine.alignment = TextAnchor.MiddleRight;

            _detailSourceCodeStyleMain = new GUIStyle(EditorStyles.label);
            _detailSourceCodeStyleMain.margin = new RectOffset(0, 0, 0, 0);
            _detailSourceCodeStyleMain.padding = new RectOffset(4, 0, 0, 4);
            _detailSourceCodeStyleMain.normal.background = EditorGUIUtility.whiteTexture;
            _detailSourceCodeStyleMain.active = _logLineStyle.normal;
            _detailSourceCodeStyleMain.hover = _logLineStyle.normal;
            _detailSourceCodeStyleMain.focused = _logLineStyle.normal;
            _detailSourceCodeStyleMain.clipping = TextClipping.Clip;
            _detailSourceCodeStyleMain.wordWrap = false;
            _detailSourceCodeStyleMain.alignment = TextAnchor.MiddleLeft;
            
            _detailSourceCodeStyleTop = new GUIStyle(_detailSourceCodeStyleMain);
            _detailSourceCodeStyleTop.alignment = TextAnchor.LowerLeft;
            _detailSourceCodeStyleTop.padding = new RectOffset(4, 0, 2, 4);
            
            _detailSourceCodeStyleBottom = new GUIStyle(_detailSourceCodeStyleMain);
            _detailSourceCodeStyleBottom.alignment = TextAnchor.UpperLeft;
            _detailSourceCodeStyleTop.padding = new RectOffset(4, 2, 0, 4);
            
            _logHeaderLabelStyle = new GUIStyle(EditorStyles.label);
            _logHeaderLabelStyle.alignment = TextAnchor.MiddleCenter;
            _logHeaderLabelStyle.margin = new RectOffset(0, 0, 2, 2);
            _logHeaderLabelStyle.fontStyle = FontStyle.Bold;
            
            _logRightAlignHeaderLabelStyle = new GUIStyle(_logHeaderLabelStyle);
            _logRightAlignHeaderLabelStyle.alignment = TextAnchor.MiddleRight;
            
            _logMessageLabelStyle = new GUIStyle(EditorStyles.label);
            _logMessageLabelStyle.stretchHeight = true;
            _logMessageLabelStyle.clipping = TextClipping.Clip;
            _logMessageLabelStyle.margin = _logHeaderLabelStyle.margin;
            _logMessageLabelStyle.padding = _logHeaderLabelStyle.padding;
            _logMessageLabelStyle.alignment = TextAnchor.MiddleLeft;
            _logMessageLabelStyle.normal.textColor = Color.white;
            
            _logTimeStampLabelStyle = new GUIStyle(_logMessageLabelStyle);
            _logTimeStampLabelStyle.alignment = TextAnchor.MiddleRight;
            
            _logSourceLabelStyle = new GUIStyle(_logMessageLabelStyle);
            _logSourceLabelStyle.alignment = TextAnchor.MiddleRight;
            _logSourceLabelStyle.fontStyle = FontStyle.Bold;
            
            _logTypeLabelStyle = new GUIStyle(_logMessageLabelStyle);
            _logTypeLabelStyle.alignment = TextAnchor.MiddleCenter;
            
            _lineColorEven = defaultLineColor;
            _lineColorOdd = new Color(defaultLineColor.r * 0.9f, defaultLineColor.g * 0.9f, defaultLineColor.b * 0.9f);
            _sizerLineColour = new Color(defaultLineColor.r * 0.5f, defaultLineColor.g * 0.5f, defaultLineColor.b * 0.5f);
            
            // We can't allow transparent back colors due to redundancy overdraw, so we'll have to figure out
            // a solid replacement for the partiall transparent selection color
            _lineColorSelect = _baseEditorSkin.settings.selectionColor;
            _lineColorSelect = new Color(_lineColorSelect.r, _lineColorSelect.g, _lineColorSelect.b, 1.0f);
            _lineColorSelect = Color.Lerp(_lineColorSelect, _lineColorEven, _baseEditorSkin.settings.selectionColor.a);
        }
        
        private void JumpToSource(LogEntryStrackFrame frame)
        {
            if (frame.FileName == null) return;

            string filename = Path.Combine(Directory.GetCurrentDirectory(), frame.FileName);
            if (File.Exists(filename))
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(frame.FileName, frame.LineNumber);
            }
        }

        private void RebuildFilteredIndexList()
        {
            _lastReceivedLogIndex = -1;
            _logListFilteredIndices.Clear();
            AppendLogEntriesToFilteredList(0);
            Repaint();
        }
        private void AppendLogEntriesToFilteredList(int startIndex)
        {
            for(int entryIndex = startIndex; entryIndex < _editorLogOutput.LogEntries.Count; entryIndex++)
            {
                EditorWindowLogEntry logEntry = _editorLogOutput.LogEntries[entryIndex];
                if (!IsLogFilterMatch(_filterRegEx, logEntry)) continue;
                _logListFilteredIndices.Add(entryIndex);
            }
            
            _lastReceivedLogIndex = _editorLogOutput.LogEntries.Count - 1;
            Repaint();
        }
        private bool IsLogFilterMatch(Regex regex, EditorWindowLogEntry logEntry)
        {
            // Channel filter
            if (_channelFilter.Contains(logEntry.SourceName)) return false;
            
            // Type filter
            if (!_showMessages && logEntry.Log.Type == LogMessageType.Debug  ) return false;
            if (!_showMessages && logEntry.Log.Type == LogMessageType.Message) return false;
            if (!_showWarnings && logEntry.Log.Type == LogMessageType.Warning) return false;
            if (!_showErrors   && logEntry.Log.Type == LogMessageType.Error  ) return false;
            if (!_showErrors   && logEntry.Log.Type == LogMessageType.Fatal  ) return false;
            
            // Regex filter
            if (regex != null && !regex.IsMatch(logEntry.Log.Message)) return false;
            
            return true;
        }
        private void Clear()
        {
            // Reset indentation to get rid of those left over by uncaught
            // exceptions leaving a stack frame without calling PopIndent,
            // as well as misused of Push / Pop indent commands.
            _editorLogOutput.ResetIndent();
            _editorLogOutput.Clear();
            RebuildFilteredIndexList();
            SelectedLogEntry = -1;
            Repaint();
        }

        private float GetLogItemYOffset(int index)
        {
            return index * LogItemHeight;
        }
        private void ScrollDownToIndex(int index)
        {
            float itemTopScrollY = GetLogItemYOffset(_selectedLogEntry);
            float itemBottomScrollY = itemTopScrollY + (LogItemHeight + 10) + _logListLastRect.y - _currentTopPaneHeight;
            _logListScrollPosition.y = Mathf.Max(_logListScrollPosition.y, itemBottomScrollY);

            if (index == _logListFilteredIndices.Count - 1)
                _logListScrolledToEnd = true;
        }
        private void ScrollUpToIndex(int index)
        {
            float itemTopScrollY = GetLogItemYOffset(_selectedLogEntry);
            float itemBottomScrollY = itemTopScrollY - 10 + _logListLastRect.y - _logListLastRect.y;
            _logListScrollPosition.y = Mathf.Min(_logListScrollPosition.y, itemBottomScrollY);
        }
        
        private void UpdateDisplayedSourceCode()
        {
            string[] newDisplayedSourceCode = null;
            if (_selectedLogEntry != -1 && _selectedCallStackFrame != -1)
            {
                int entryIndex = _logListFilteredIndices[_selectedLogEntry];
                EditorWindowLogEntry entry = _editorLogOutput.LogEntries[entryIndex];
                if (entry.CallStack.Frames.Length > _selectedCallStackFrame)
                    newDisplayedSourceCode = GetSourceForFrame(entry.CallStack.Frames[_selectedCallStackFrame]);
            }
            _displayedSourceCode = newDisplayedSourceCode;
        }
        private string[] GetSourceForFrame(LogEntryStrackFrame frame)
        {
            if (string.IsNullOrEmpty(frame.FileName))
                return null;
            
            if (!File.Exists(frame.FileName))
                return null;
            
            string[] sourceCode = new string[3];
            int lineNumber = frame.LineNumber - 1;
            int linesAround = 8;
            string[] lines = File.ReadAllLines(frame.FileName);
            int firstLine = Mathf.Max(lineNumber - linesAround, 0);
            int lastLine = Mathf.Min(lineNumber + linesAround + 1, lines.Length);

            if (lineNumber >= lines.Length)
                return null;
            
            sourceCode[0] = "";
            for (int i = firstLine; i < lineNumber; i++)
            {
                sourceCode[0] += lines[i];
                if (i < lineNumber - 1)
                    sourceCode[0] += "\n";
            }
            sourceCode[1] = lines[lineNumber];
            sourceCode[2] = "";
            for (int i = lineNumber + 1; i < lastLine; i++)
            {
                sourceCode[2] += lines[i] + "\n";
            }
            
            return sourceCode;
        }

        private void OnDetailSplitterMoved()
        {
            // Make sure our selected item is still visible / scrolled to since we now have less space at the bottom
            if (_selectedLogEntry != -1)
            {
                ScrollDownToIndex(_selectedLogEntry);
            }

            Repaint();
        }
        private void OnSelectedLogEntryChanged()
        {
            _logListSelectedLast = (_selectedLogEntry != -1 && (_selectedLogEntry >= _logListFilteredIndices.Count - 1));

            // Determine the newly selected entry
            EditorWindowLogEntry entry = default(EditorWindowLogEntry);
            if (_selectedLogEntry != -1 && _logListFilteredIndices.Count > _selectedLogEntry)
            {
                int entryIndex = _logListFilteredIndices[_selectedLogEntry];
                entry = _editorLogOutput.LogEntries[entryIndex];
            }

            // Update the stack frame selection
            int newSelectedCallstackFrame = -1;
            if (entry.CallStack.IsAvailable)
                newSelectedCallstackFrame = 0;
            _userSelectedCallStackFrame = false;
            SelectedCallStackFrame = newSelectedCallstackFrame;
            
            // Highlight the context object that is the source of this message
            if (entry.Context.UnityObject)
                EditorGUIUtility.PingObject(entry.Context.UnityObject);

            Repaint();
        }
        private void OnSelectedCallStackFrameChanged()
        {
            UpdateDisplayedSourceCode();
            Repaint();
        }
        private void OnFilterRegExChanged()
        {
            SelectedLogEntry = -1;
            RebuildFilteredIndexList();
            Repaint();
        }
        private void OnFilterChannelChanged()
        {
            SelectedLogEntry = -1;
            RebuildFilteredIndexList();
            Repaint();
        }
        private void OnFilterTypeChanged()
        {
            SelectedLogEntry = -1;
            RebuildFilteredIndexList();
            Repaint();
        }

        private void EditorApplication_playmodeStateChanged()
        {
            if (!_wasPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (_clearOnPlay && _editorLogOutput != null)
                    Clear();
            }
            _wasPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            _updateStyles = true;
        }
        private void EditorLogOutput_LogReceived(object sender, EventArgs e)
        {
            // Add newly received log entries to the filtered list
            AppendLogEntriesToFilteredList(_lastReceivedLogIndex + 1);

            // Auto-select, if we selected the last message before
            if (_logListSelectedLast)
                _logListAutoSelectPending = true;

            // Pause on error
            if (_pauseOnError && _editorLogOutput.ErrorCount > _lastErrorCount)
            {
                UnityEngine.Debug.Break();
            }
            _lastErrorCount = _editorLogOutput.ErrorCount;

            Repaint();
        }
    }

}