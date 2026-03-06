using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    public class RuntimeConsoleWindow : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField]
        private bool openOnStart;

        [SerializeField]
        private KeyCode toggleKey = KeyCode.BackQuote;

        [SerializeField]
        private Rect windowRect = new Rect(20f, 20f, 1000f, 620f);

        [Header("Log behavior")]
        [SerializeField]
        [Min(10)]
        private int maxEntries = 2000;

        [SerializeField]
        private bool collapseDuplicates = true;

        [SerializeField]
        private bool autoOpenOnError = true;

        [SerializeField]
        private bool pauseOnError;

        [SerializeField]
        private bool includeStackTraceInCopy = true;

        [Header("Filters")]
        [SerializeField]
        private bool showLogs = true;

        [SerializeField]
        private bool showWarnings = true;

        [SerializeField]
        private bool showErrors = true;

        [Header("Appearance")]
        [SerializeField]
        [Min(8)]
        private int traceFontSize = 13;

        [SerializeField]
        private bool autoScaleWithScreen = true;

        [SerializeField]
        private Vector2Int referenceResolution = new Vector2Int(1920, 1080);

        [SerializeField]
        [Range(0.4f, 2f)]
        private float minUiScale = 0.7f;

        [SerializeField]
        [Range(0.5f, 3f)]
        private float maxUiScale = 1.6f;

        private readonly List<LogEntry> _entries = new List<LogEntry>(256);
        private readonly Queue<PendingLog> _pendingLogs = new Queue<PendingLog>(128);
        private readonly List<PendingLog> _flushBuffer = new List<PendingLog>(128);
        private readonly object _pendingLock = new object();

        private bool _isOpen;
        private bool _isLeftMouseDown;
        private Vector2 _scrollPosition;
        private bool _scrollToBottom;
        private float _lastScrollViewHeight;
        private float _lastContentHeight;
        private float _uiScale = 1f;
        private int _windowId;
        private int _nextEntryId;
        private int _selectedEntryId = -1;

        private GUIStyle _windowStyle;
        private GUIStyle _toolbarLabelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _activeButtonStyle;
        private GUIStyle _copyButtonStyle;
        private GUIStyle _countStyle;
        private GUIStyle _logStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _stackTraceStyle;

        private Texture2D _windowBackgroundTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverOverlayTexture;
        private Texture2D _buttonPressedOverlayTexture;
        private Texture2D _activeButtonTexture;
        private Texture2D _copyButtonTexture;
        private Texture2D _rowEvenTexture;
        private Texture2D _rowOddTexture;

        private float _rowHeight;

        private void Awake()
        {
            _windowId = GetInstanceID();
            _isOpen = openOnStart;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            SafeDestroyTexture(ref _windowBackgroundTexture);
            SafeDestroyTexture(ref _buttonTexture);
            SafeDestroyTexture(ref _buttonHoverOverlayTexture);
            SafeDestroyTexture(ref _buttonPressedOverlayTexture);
            SafeDestroyTexture(ref _activeButtonTexture);
            SafeDestroyTexture(ref _copyButtonTexture);
            SafeDestroyTexture(ref _rowEvenTexture);
            SafeDestroyTexture(ref _rowOddTexture);
        }

        private void Update()
        {
            FlushPendingLogs();
        }

        private void OnGUI()
        {
            HandleToggleHotkeyFromGuiEvent();
            UpdatePointerStateFromGuiEvent();

            if (!_isOpen)
            {
                return;
            }

            EnsureStyles();
            UpdateUiScale();

            var previousMatrix = GUI.matrix;
            try
            {
                if (!Mathf.Approximately(_uiScale, 1f))
                {
                    GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));
                }

                windowRect = GUILayout.Window(_windowId, windowRect, DrawWindow, GUIContent.none, _windowStyle);
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }

            ClampWindowToVisibleArea();
        }

        private void HandleToggleHotkeyFromGuiEvent()
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode != toggleKey)
            {
                return;
            }

            Toggle();
            currentEvent.Use();
        }

        private void UpdatePointerStateFromGuiEvent()
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                _isLeftMouseDown = true;
                return;
            }

            if ((currentEvent.type == EventType.MouseUp && currentEvent.button == 0) || currentEvent.type == EventType.MouseLeaveWindow)
            {
                _isLeftMouseDown = false;
            }
        }

        public void Open()
        {
            _isOpen = true;
        }

        public void Close()
        {
            _isOpen = false;
        }

        public void Toggle()
        {
            _isOpen = !_isOpen;
        }

        public void ClearLogs()
        {
            _entries.Clear();
            _selectedEntryId = -1;
            _scrollPosition = Vector2.zero;
            _lastScrollViewHeight = 0f;
            _lastContentHeight = 0f;
        }

        private void DrawWindow(int windowId)
        {
            DrawTitleBar();
            DrawControlsRow();
            DrawLogList();
            DrawBottomBar();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 44f, 24f));
        }

        private void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Debug log", _toolbarLabelStyle, GUILayout.Height(22f));
            GUILayout.FlexibleSpace();

            if (DrawToolbarButton("X", _buttonStyle, GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                Close();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawControlsRow()
        {
            GUILayout.BeginHorizontal();

            if (DrawToolbarButton("Clear", _buttonStyle, GUILayout.Height(22f)))
            {
                ClearLogs();
            }

            if (DrawToolbarButton($"Auto-open is {GetOnOff(autoOpenOnError)}", autoOpenOnError ? _activeButtonStyle : _buttonStyle, GUILayout.Height(22f)))
            {
                autoOpenOnError = !autoOpenOnError;
            }

            if (DrawToolbarButton($"Pause on error is {GetOnOff(pauseOnError)}", pauseOnError ? _activeButtonStyle : _buttonStyle, GUILayout.Height(22f)))
            {
                pauseOnError = !pauseOnError;
            }

            GUILayout.Label("|", _toolbarLabelStyle, GUILayout.Width(8f), GUILayout.Height(22f));

            DrawFilterToggle(ref showLogs, "I", Color.white);
            DrawFilterToggle(ref showWarnings, "W", new Color(1f, 0.82f, 0.2f));
            DrawFilterToggle(ref showErrors, "E", new Color(1f, 0.33f, 0.33f));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBottomBar()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (DrawToolbarButton("Copy to clipboard", _copyButtonStyle, GUILayout.Width(150f), GUILayout.Height(24f)))
            {
                GUIUtility.systemCopyBuffer = BuildClipboardText();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFilterToggle(ref bool value, string label, Color color)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = color;

            if (DrawToolbarButton(label, value ? _activeButtonStyle : _buttonStyle, GUILayout.Width(24f), GUILayout.Height(22f)))
            {
                value = !value;
            }

            GUI.contentColor = previousColor;
        }

        private bool DrawToolbarButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var clicked = GUILayout.Button(text, style, options);
            DrawToolbarButtonFeedbackOverlay();
            return clicked;
        }

        private void DrawToolbarButtonFeedbackOverlay()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var rect = GUILayoutUtility.GetLastRect();
            if (!rect.Contains(Event.current.mousePosition))
            {
                return;
            }

            GUI.DrawTexture(rect, _isLeftMouseDown ? _buttonPressedOverlayTexture : _buttonHoverOverlayTexture);
        }

        private void DrawLogList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            var contentHeight = 0f;
            var rowIndex = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!ShouldShow(entry.Type))
                {
                    continue;
                }

                contentHeight += DrawLogRow(entry, rowIndex);
                rowIndex++;
            }

            GUILayout.EndScrollView();
            _lastContentHeight = contentHeight;

            var scrollViewRect = GUILayoutUtility.GetLastRect();
            _lastScrollViewHeight = scrollViewRect.height;

            if (_scrollToBottom)
            {
                _scrollPosition.y = Mathf.Max(0f, _lastContentHeight - _lastScrollViewHeight);
                _scrollToBottom = false;
            }
        }

        private float DrawLogRow(LogEntry entry, int rowIndex)
        {
            var messageStyle = GetStyleForType(entry.Type);
            var isExpanded = _selectedEntryId == entry.Id;
            var rowHeight = GetRowHeight(entry, messageStyle, isExpanded);
            var rect = GUILayoutUtility.GetRect(10f, rowHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (CanShowStackTrace(entry))
                {
                    _selectedEntryId = isExpanded ? -1 : entry.Id;
                }
                else
                {
                    _selectedEntryId = -1;
                }

                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, rowIndex % 2 == 0 ? _rowEvenTexture : _rowOddTexture);
            }

            var messageWidth = rect.width - 34f;
            var messageHeight = messageStyle.CalcHeight(new GUIContent(entry.DisplayLine), Mathf.Max(120f, messageWidth));

            var countRect = new Rect(rect.x + 4f, rect.y + 2f, 24f, rowHeight - 4f);
            GUI.Label(countRect, entry.Count.ToString(), _countStyle);

            var messageRect = new Rect(rect.x + 30f, rect.y + 2f, messageWidth, messageHeight);
            GUI.Label(messageRect, entry.DisplayLine, messageStyle);

            if (isExpanded && CanShowStackTrace(entry))
            {
                var dividerY = messageRect.yMax + 2f;
                GUI.DrawTexture(new Rect(messageRect.x, dividerY, messageRect.width, 1f), _buttonTexture);

                var stackTraceRect = new Rect(messageRect.x, dividerY + 4f, messageRect.width, rowHeight - (dividerY - rect.y) - 6f);
                GUI.Label(stackTraceRect, NormalizeLineBreaks(entry.Stacktrace), _stackTraceStyle);
            }

            return rect.height;
        }

        private float GetRowHeight(LogEntry entry, GUIStyle messageStyle, bool isExpanded)
        {
            // Use the window width as an estimate before layout reserves exact row rect.
            var estimatedMessageWidth = Mathf.Max(120f, windowRect.width - 80f);
            var messageHeight = messageStyle.CalcHeight(new GUIContent(entry.DisplayLine), estimatedMessageWidth);
            var contentHeight = messageHeight + 6f;

            if (isExpanded && CanShowStackTrace(entry))
            {
                var stackTraceHeight = _stackTraceStyle.CalcHeight(new GUIContent(NormalizeLineBreaks(entry.Stacktrace)), estimatedMessageWidth);
                contentHeight += stackTraceHeight + 8f;
            }

            return Mathf.Max(_rowHeight, contentHeight);
        }

        private static bool CanShowStackTrace(LogEntry entry)
        {
            return IsErrorType(entry.Type) && !string.IsNullOrWhiteSpace(entry.Stacktrace);
        }

        private void SetTraceFontSize(int size)
        {
            _rowHeight = Mathf.Max(20f, size + 8f);

            if (_countStyle != null)
            {
                _countStyle.fontSize = size;
                _logStyle.fontSize = size;
                _warningStyle.fontSize = size;
                _errorStyle.fontSize = size;
                _stackTraceStyle.fontSize = Mathf.Max(9, size - 1);
            }
        }

        private void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            lock (_pendingLock)
            {
                _pendingLogs.Enqueue(new PendingLog(condition, stacktrace, type));
            }
        }

        private void FlushPendingLogs()
        {
            var shouldStickToBottom = IsScrollAtBottom();

            _flushBuffer.Clear();

            lock (_pendingLock)
            {
                while (_pendingLogs.Count > 0)
                {
                    _flushBuffer.Add(_pendingLogs.Dequeue());
                }
            }

            if (_flushBuffer.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _flushBuffer.Count; i++)
            {
                var pendingLog = _flushBuffer[i];
                var isError = IsErrorType(pendingLog.Type);

                AddLog(pendingLog.Condition, pendingLog.Stacktrace, pendingLog.Type);

                if (autoOpenOnError && isError)
                {
                    _isOpen = true;
                }

                if (pauseOnError && isError && Application.isPlaying)
                {
                    Time.timeScale = 0f;
                }
            }

            if (_isOpen && shouldStickToBottom)
            {
                _scrollToBottom = true;
            }
        }

        private bool IsScrollAtBottom()
        {
            if (_lastScrollViewHeight <= 0f)
            {
                return true;
            }

            if (_lastContentHeight <= _lastScrollViewHeight + 1f)
            {
                return true;
            }

            var bottomPosition = _scrollPosition.y + _lastScrollViewHeight;
            return bottomPosition >= _lastContentHeight - 8f;
        }

        private void UpdateUiScale()
        {
            if (!autoScaleWithScreen)
            {
                _uiScale = 1f;
                return;
            }

            var refWidth = Mathf.Max(1, referenceResolution.x);
            var refHeight = Mathf.Max(1, referenceResolution.y);

            var widthScale = Screen.width / (float)refWidth;
            var heightScale = Screen.height / (float)refHeight;
            var resolvedScale = Mathf.Min(widthScale, heightScale);

            if (maxUiScale < minUiScale)
            {
                maxUiScale = minUiScale;
            }

            _uiScale = Mathf.Clamp(resolvedScale, minUiScale, maxUiScale);
        }

        private void ClampWindowToVisibleArea()
        {
            var safeScale = Mathf.Max(0.001f, _uiScale);
            var visibleWidth = Screen.width / safeScale;
            var visibleHeight = Screen.height / safeScale;

            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, visibleWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, visibleHeight - windowRect.height));
        }

        private void AddLog(string condition, string stacktrace, LogType type)
        {
            if (collapseDuplicates && _entries.Count > 0)
            {
                var lastIndex = _entries.Count - 1;
                var last = _entries[lastIndex];
                if (last.Type == type && last.Condition == condition && last.Stacktrace == stacktrace)
                {
                    last.Count++;
                    _entries[lastIndex] = last;
                    return;
                }
            }

            if (_entries.Count >= maxEntries)
            {
                if (_selectedEntryId == _entries[0].Id)
                {
                    _selectedEntryId = -1;
                }

                _entries.RemoveAt(0);
            }

            _entries.Add(new LogEntry(_nextEntryId++, DateTime.Now, condition, stacktrace, type));
        }

        private bool ShouldShow(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return showWarnings;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return showErrors;
                default:
                    return showLogs;
            }
        }

        private GUIStyle GetStyleForType(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return _warningStyle;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return _errorStyle;
                default:
                    return _logStyle;
            }
        }

        private string BuildClipboardText()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!ShouldShow(entry.Type))
                {
                    continue;
                }

                builder.Append(entry.Count);
                builder.Append(' ');
                builder.Append(entry.DisplayLine);
                builder.Append('\n');

                if (includeStackTraceInCopy && !string.IsNullOrWhiteSpace(entry.Stacktrace))
                {
                    builder.AppendLine(entry.Stacktrace.TrimEnd());
                }
            }

            return builder.ToString();
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowBackgroundTexture = CreateSolidTexture(new Color(0.06f, 0.08f, 0.11f, 0.98f));
            _buttonTexture = CreateSolidTexture(new Color(0.24f, 0.24f, 0.24f, 1f));
            _buttonHoverOverlayTexture = CreateSolidTexture(new Color(1f, 1f, 1f, 0.08f));
            _buttonPressedOverlayTexture = CreateSolidTexture(new Color(0f, 0f, 0f, 0.18f));
            _activeButtonTexture = CreateSolidTexture(new Color(0.28f, 0.36f, 0.48f, 1f));
            _copyButtonTexture = CreateSolidTexture(new Color(0.2f, 0.42f, 0.24f, 1f));
            _rowEvenTexture = CreateSolidTexture(new Color(0.11f, 0.13f, 0.16f, 1f));
            _rowOddTexture = CreateSolidTexture(new Color(0.07f, 0.09f, 0.12f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(8, 8, 8, 8),
                border = new RectOffset(1, 1, 1, 1)
            };
            ApplyBackgroundToAllStates(_windowStyle, _windowBackgroundTexture, Color.white);

            _toolbarLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _buttonTexture, textColor = Color.white },
                hover = { background = _buttonTexture, textColor = Color.white },
                active = { background = _buttonTexture, textColor = Color.white },
                focused = { background = _buttonTexture, textColor = Color.white },
                fontSize = 12,
                margin = new RectOffset(2, 2, 0, 0),
                padding = new RectOffset(6, 6, 1, 1),
                alignment = TextAnchor.MiddleCenter
            };

            _activeButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { background = _activeButtonTexture, textColor = Color.white },
                hover = { background = _activeButtonTexture, textColor = Color.white },
                active = { background = _activeButtonTexture, textColor = Color.white },
                focused = { background = _activeButtonTexture, textColor = Color.white }
            };

            _copyButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { background = _copyButtonTexture, textColor = Color.white },
                hover = { background = _copyButtonTexture, textColor = Color.white },
                active = { background = _copyButtonTexture, textColor = Color.white },
                focused = { background = _copyButtonTexture, textColor = Color.white }
            };

            _countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) },
                wordWrap = true,
                richText = true,
                clipping = TextClipping.Clip
            };

            _warningStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(1f, 0.82f, 0.2f, 1f) }
            };

            _errorStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(1f, 0.39f, 0.39f, 1f) }
            };

            _stackTraceStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(0.74f, 0.74f, 0.74f, 1f) },
                richText = true
            };

            SetTraceFontSize(traceFontSize);
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void ApplyBackgroundToAllStates(GUIStyle style, Texture2D background, Color textColor)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;

            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
        }

        private static void SafeDestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }

            texture = null;
        }

        private static bool IsErrorType(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return true;
                default:
                    return false;
            }
        }

        private static string GetOnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        private static string NormalizeLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private readonly struct PendingLog
        {
            public readonly string Condition;
            public readonly string Stacktrace;
            public readonly LogType Type;

            public PendingLog(string condition, string stacktrace, LogType type)
            {
                Condition = condition;
                Stacktrace = stacktrace;
                Type = type;
            }
        }

        private struct LogEntry
        {
            public readonly int Id;
            public readonly string Condition;
            public readonly string Stacktrace;
            public readonly LogType Type;
            public readonly string DisplayLine;
            public int Count;

            public LogEntry(int id, DateTime timestamp, string condition, string stacktrace, LogType type)
            {
                Id = id;
                Condition = condition ?? string.Empty;
                Stacktrace = stacktrace ?? string.Empty;
                Type = type;
                Count = 1;
                DisplayLine = $"[{timestamp:HH:mm:ss}] {NormalizeLineBreaks(Condition)}";
            }
        }
    }
}
