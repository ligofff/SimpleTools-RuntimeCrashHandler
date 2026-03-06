using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Theme")]
        [SerializeField]
        private RuntimeConsoleTheme theme;

        private readonly List<LogEntry> _entries = new List<LogEntry>(256);
        private readonly Queue<PendingLog> _pendingLogs = new Queue<PendingLog>(128);
        private readonly List<PendingLog> _flushBuffer = new List<PendingLog>(128);
        private readonly Dictionary<int, ButtonVisualState> _buttonVisualStates = new Dictionary<int, ButtonVisualState>(32);
        private readonly List<int> _buttonStateKeyBuffer = new List<int>(32);
        private readonly object _pendingLock = new object();

        private bool _isOpen;
        private bool _isLeftMouseDown;
        private bool _pausedByConsole;
        private Vector2 _scrollPosition;
        private bool _scrollToBottom;
        private float _lastScrollViewHeight;
        private float _lastContentHeight;
        private float _copyToastTimeLeft;
        private float _timeScaleBeforePause = 1f;
        private float _uiScale = 1f;
        private int _windowId;
        private int _buttonDrawIndex;
        private int _nextEntryId;
        private int _selectedEntryId = -1;

        private GUIStyle _windowStyle;
        private GUIStyle _toolbarLabelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _activeButtonStyle;
        private GUIStyle _copyButtonStyle;
        private GUIStyle _copyErrorsButtonStyle;
        private GUIStyle _copyToastStyle;
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
        private Texture2D _copyErrorsButtonTexture;
        private Texture2D _rowEvenTexture;
        private Texture2D _rowOddTexture;

        private float _rowHeight;

        private Canvas _uiBlockerCanvas;
        private RectTransform _uiBlockerRect;
        private Image _uiBlockerImage;
        private RuntimeConsoleTheme _appliedTheme;
        private bool _logSubscriptionActive;
        private bool _hasLoggedMissingThemeError;

        private void Awake()
        {
            _windowId = GetInstanceID();
            _isOpen = openOnStart;

            if (!EnsureThemeAssigned())
            {
                return;
            }

            EnsureUiBlocker();
            SetUiBlockerActive(_isOpen);
        }

        private void OnDestroy()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();
            DestroyUiBlocker();
            ReleaseStyleResources();
        }

        private void OnDisable()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();

            if (_uiBlockerCanvas != null)
            {
                _uiBlockerCanvas.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!EnsureThemeAssigned())
            {
                return;
            }

            SubscribeToLogEvents();
            EnsureUiBlocker();
            SetUiBlockerActive(_isOpen);
        }

        private void OnValidate()
        {
            ReleaseStyleResources();

            if (!Application.isPlaying || !enabled)
            {
                return;
            }

            EnsureThemeAssigned();
        }

        public void SetTheme(RuntimeConsoleTheme runtimeConsoleTheme)
        {
            theme = runtimeConsoleTheme;
            _hasLoggedMissingThemeError = false;
            ReleaseStyleResources();

            if (Application.isPlaying && enabled)
            {
                EnsureThemeAssigned();
            }
        }

        private bool EnsureThemeAssigned()
        {
            if (theme != null)
            {
                _hasLoggedMissingThemeError = false;
                return true;
            }

            if (!_hasLoggedMissingThemeError)
            {
                Debug.LogException(
                    new InvalidOperationException(
                        $"{nameof(RuntimeConsoleWindow)} requires a {nameof(RuntimeConsoleTheme)} asset. Assign it in the inspector."),
                    this);
                _hasLoggedMissingThemeError = true;
            }

            _isOpen = false;
            if (_uiBlockerCanvas != null)
            {
                _uiBlockerCanvas.gameObject.SetActive(false);
            }

            enabled = false;
            return false;
        }

        private void SubscribeToLogEvents()
        {
            if (_logSubscriptionActive)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            _logSubscriptionActive = true;
        }

        private void UnsubscribeFromLogEvents()
        {
            if (!_logSubscriptionActive)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            _logSubscriptionActive = false;
        }

        private void Update()
        {
            if (theme == null)
            {
                return;
            }

            FlushPendingLogs();
            AnimateButtonStates();

            if (_copyToastTimeLeft > 0f)
            {
                _copyToastTimeLeft = Mathf.Max(0f, _copyToastTimeLeft - Time.unscaledDeltaTime);
            }
        }

        private void OnGUI()
        {
            if (theme == null)
            {
                return;
            }

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
            SyncUiBlockerToWindow();
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
            SetUiBlockerActive(true);
        }

        public void Close()
        {
            _isOpen = false;
            RestoreTimeScaleIfNeeded();
            SetUiBlockerActive(false);
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
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
            _buttonDrawIndex = 0;
            var activeTheme = theme;
            DrawControlsRow();
            GUILayout.Space(activeTheme.ControlsToEntriesSpacing);
            DrawLogList();
            DrawBottomBar();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 44f, 24f));
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

                if (!pauseOnError)
                {
                    RestoreTimeScaleIfNeeded();
                }
            }

            GUILayout.Label("|", _toolbarLabelStyle, GUILayout.Width(8f), GUILayout.Height(22f));

            DrawFilterToggle(ref showLogs, "I", theme.InfoFilterColor);
            DrawFilterToggle(ref showWarnings, "W", theme.WarningFilterColor);
            DrawFilterToggle(ref showErrors, "E", theme.ErrorFilterColor);

            GUILayout.FlexibleSpace();

            if (DrawToolbarButton("X", _buttonStyle, GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                Close();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawBottomBar()
        {
            GUILayout.Space(theme.BottomBarTopSpacing);
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            DrawCopyToastLabel();

            if (DrawToolbarButton("Copy latest 3 errors", _copyErrorsButtonStyle, GUILayout.Width(170f), GUILayout.Height(24f)))
            {
                GUIUtility.systemCopyBuffer = BuildLatestErrorsClipboardText(3);
                NotifyCopiedToClipboard();
            }

            GUILayout.Space(theme.BottomButtonsSpacing);

            if (DrawToolbarButton("Copy ALL", _copyButtonStyle, GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                GUIUtility.systemCopyBuffer = BuildClipboardText();
                NotifyCopiedToClipboard();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCopyToastLabel()
        {
            var alpha = GetCopyToastAlpha();
            if (alpha <= 0.001f)
            {
                return;
            }

            var previousColor = GUI.color;
            var toastColor = theme.CopyToastTextColor;
            GUI.color = new Color(toastColor.r, toastColor.g, toastColor.b, toastColor.a * alpha);
            GUILayout.Label("Copied to clipboard!", _copyToastStyle, GUILayout.Height(24f));
            GUI.color = previousColor;

            GUILayout.Space(theme.CopyToastRightSpacing);
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
            var content = new GUIContent(text);
            var layoutRect = GUILayoutUtility.GetRect(content, style, options);
            var buttonId = _buttonDrawIndex++;

            var isHovered = layoutRect.Contains(Event.current.mousePosition);
            var isPressed = isHovered && _isLeftMouseDown;
            var state = GetButtonVisualState(buttonId);

            if (Event.current.type == EventType.Repaint)
            {
                state.HoverAmount = Mathf.MoveTowards(state.HoverAmount, isHovered ? 1f : 0f, Time.unscaledDeltaTime * theme.HoverFadeSpeed);
            }

            var drawRect = layoutRect;
            var pulseScale = 1f + Mathf.Sin((1f - state.ClickPulse) * Mathf.PI) * theme.ClickPulseScale * state.ClickPulse;
            drawRect = ScaleRectAroundCenter(drawRect, pulseScale);
            if (isPressed)
            {
                // Tiny shrink + down-right offset to simulate physical button depth.
                drawRect = ScaleRectAroundCenter(drawRect, theme.PressedScale);
                drawRect.x += theme.PressedOffset.x;
                drawRect.y += theme.PressedOffset.y;
            }

            var clicked = GUI.Button(drawRect, content, style);
            if (clicked)
            {
                state.ClickPulse = 1f;
            }

            state.LastSeenFrame = Time.frameCount;
            _buttonVisualStates[buttonId] = state;

            DrawToolbarButtonFeedbackOverlay(drawRect, isHovered, isPressed, state);
            return clicked;
        }

        private void DrawToolbarButtonFeedbackOverlay(Rect drawRect, bool isHovered, bool isPressed, ButtonVisualState state)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var hoverAlpha = theme.HoverOverlayAlphaMultiplier * state.HoverAmount;
            if (hoverAlpha > 0.001f)
            {
                DrawTextureWithAlpha(drawRect, _buttonHoverOverlayTexture, hoverAlpha);
            }

            if (isPressed)
            {
                DrawTextureWithAlpha(drawRect, _buttonPressedOverlayTexture, theme.PressedOverlayAlphaMultiplier);
            }

            var clickFlashAlpha = state.ClickPulse * theme.ClickFlashAlphaMultiplier;
            if (clickFlashAlpha > 0.001f)
            {
                DrawTextureWithAlpha(drawRect, _buttonHoverOverlayTexture, clickFlashAlpha);
            }

            if (isHovered)
            {
                var borderAlpha = Mathf.Clamp01(
                    state.HoverAmount * theme.HoverBorderHoverContribution +
                    state.ClickPulse * theme.HoverBorderClickContribution);

                var borderColor = isPressed
                    ? theme.ButtonPressedBorderColor
                    : WithAlpha(theme.ButtonHoverBorderColor, theme.HoverBorderBaseAlpha + borderAlpha * theme.HoverBorderBoostAlpha);

                DrawRectBorder(
                    drawRect,
                    borderColor,
                    theme.ButtonBorderThickness);
            }
        }

        private void DrawTextureWithAlpha(Rect rect, Texture2D texture, float alpha)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture);
            GUI.color = previousColor;
        }

        private static void DrawRectBorder(Rect rect, Color color, float thickness = 1f)
        {
            var prevColor = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

            GUI.color = prevColor;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private void AnimateButtonStates()
        {
            if (_buttonVisualStates.Count == 0)
            {
                return;
            }

            _buttonStateKeyBuffer.Clear();
            foreach (var pair in _buttonVisualStates)
            {
                _buttonStateKeyBuffer.Add(pair.Key);
            }

            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _buttonStateKeyBuffer.Count; i++)
            {
                var key = _buttonStateKeyBuffer[i];
                var state = _buttonVisualStates[key];
                state.ClickPulse = Mathf.MoveTowards(state.ClickPulse, 0f, dt * theme.ClickPulseDecaySpeed);

                var framesWithoutUse = Time.frameCount - state.LastSeenFrame;
                if (framesWithoutUse > theme.ButtonStateRetentionFrames && state.ClickPulse <= 0.001f && state.HoverAmount <= 0.001f)
                {
                    _buttonVisualStates.Remove(key);
                    continue;
                }

                _buttonVisualStates[key] = state;
            }
        }

        private ButtonVisualState GetButtonVisualState(int buttonId)
        {
            if (_buttonVisualStates.TryGetValue(buttonId, out var state))
            {
                return state;
            }

            return default;
        }

        private static Rect ScaleRectAroundCenter(Rect rect, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                return rect;
            }

            var width = rect.width * scale;
            var height = rect.height * scale;
            return new Rect(
                rect.center.x - width * 0.5f,
                rect.center.y - height * 0.5f,
                width,
                height);
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

        private void SetTraceFontSize(int size, int stackTraceFontOffset, int stackTraceMinFontSize)
        {
            size = Mathf.Max(8, size);
            _rowHeight = Mathf.Max(20f, size + 8f);

            if (_countStyle != null)
            {
                _countStyle.fontSize = size;
                _logStyle.fontSize = size;
                _warningStyle.fontSize = size;
                _errorStyle.fontSize = size;
                _stackTraceStyle.fontSize = Mathf.Max(stackTraceMinFontSize, size + stackTraceFontOffset);
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
                    Open();
                }

                if (pauseOnError && isError)
                {
                    PauseTimeScaleIfNeeded();
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
            if (!theme.AutoScaleWithScreen)
            {
                _uiScale = 1f;
                return;
            }

            var refWidth = Mathf.Max(1, theme.ReferenceResolution.x);
            var refHeight = Mathf.Max(1, theme.ReferenceResolution.y);

            var widthScale = Screen.width / (float)refWidth;
            var heightScale = Screen.height / (float)refHeight;
            var resolvedScale = Mathf.Min(widthScale, heightScale);

            _uiScale = Mathf.Clamp(resolvedScale, theme.MinUiScale, theme.MaxUiScale);
        }

        private void ClampWindowToVisibleArea()
        {
            var safeScale = Mathf.Max(0.001f, _uiScale);
            var visibleWidth = Screen.width / safeScale;
            var visibleHeight = Screen.height / safeScale;

            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, visibleWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, visibleHeight - windowRect.height));
        }

        private void EnsureUiBlocker()
        {
            if (_uiBlockerCanvas != null && _uiBlockerRect != null && _uiBlockerImage != null)
            {
                return;
            }

            var canvasObject = new GameObject($"RuntimeConsoleUiBlockerCanvas_{GetInstanceID()}");
            _uiBlockerCanvas = canvasObject.AddComponent<Canvas>();
            _uiBlockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiBlockerCanvas.sortingOrder = short.MaxValue;
            canvasObject.AddComponent<GraphicRaycaster>();

            var blockerObject = new GameObject("WindowBlocker");
            blockerObject.transform.SetParent(canvasObject.transform, false);
            _uiBlockerRect = blockerObject.AddComponent<RectTransform>();
            _uiBlockerRect.anchorMin = Vector2.zero;
            _uiBlockerRect.anchorMax = Vector2.zero;
            _uiBlockerRect.pivot = Vector2.zero;

            _uiBlockerImage = blockerObject.AddComponent<Image>();
            _uiBlockerImage.color = new Color(0f, 0f, 0f, 0f);
            _uiBlockerImage.raycastTarget = true;
        }

        private void SetUiBlockerActive(bool isActive)
        {
            if (!isActive && _uiBlockerCanvas == null)
            {
                return;
            }

            EnsureUiBlocker();

            if (_uiBlockerCanvas != null)
            {
                _uiBlockerCanvas.gameObject.SetActive(isActive);
            }
        }

        private void SyncUiBlockerToWindow()
        {
            if (!_isOpen)
            {
                return;
            }

            EnsureUiBlocker();
            if (_uiBlockerRect == null)
            {
                return;
            }

            var scaledRect = new Rect(
                windowRect.x * _uiScale,
                windowRect.y * _uiScale,
                windowRect.width * _uiScale,
                windowRect.height * _uiScale);

            var width = Mathf.Max(0f, scaledRect.width);
            var height = Mathf.Max(0f, scaledRect.height);
            var bottom = Screen.height - scaledRect.y - height;

            _uiBlockerRect.sizeDelta = new Vector2(width, height);
            _uiBlockerRect.anchoredPosition = new Vector2(
                Mathf.Clamp(scaledRect.x, 0f, Mathf.Max(0f, Screen.width - width)),
                Mathf.Clamp(bottom, 0f, Mathf.Max(0f, Screen.height - height)));
        }

        private void DestroyUiBlocker()
        {
            if (_uiBlockerCanvas == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_uiBlockerCanvas.gameObject);
            }
            else
            {
                DestroyImmediate(_uiBlockerCanvas.gameObject);
            }

            _uiBlockerCanvas = null;
            _uiBlockerRect = null;
            _uiBlockerImage = null;
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

        private void PauseTimeScaleIfNeeded()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_pausedByConsole)
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _pausedByConsole = true;
        }

        private void RestoreTimeScaleIfNeeded()
        {
            if (!_pausedByConsole)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _pausedByConsole = false;
        }

        private void NotifyCopiedToClipboard()
        {
            _copyToastTimeLeft = theme.CopyToastDuration;
        }

        private float GetCopyToastAlpha()
        {
            if (_copyToastTimeLeft <= 0f)
            {
                return 0f;
            }

            var duration = theme.CopyToastDuration;
            var t = Mathf.Clamp01(_copyToastTimeLeft / duration);
            // Slightly slower fade at the beginning so it remains readable.
            return Mathf.Pow(t, 0.7f);
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
            RuntimeConsoleCopyHeaderData.AppendHeader(builder, "All visible entries");
            var copiedCount = 0;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!ShouldShow(entry.Type))
                {
                    continue;
                }

                AppendEntryToClipboard(builder, entry);
                copiedCount++;
            }

            if (copiedCount == 0)
            {
                builder.AppendLine("No entries found for current filters.");
            }

            return builder.ToString();
        }

        private string BuildLatestErrorsClipboardText(int maxErrors)
        {
            if (maxErrors <= 0)
            {
                maxErrors = 1;
            }

            var builder = new StringBuilder();
            RuntimeConsoleCopyHeaderData.AppendHeader(builder, $"Latest {maxErrors} errors");
            var copiedCount = 0;

            for (var i = _entries.Count - 1; i >= 0 && copiedCount < maxErrors; i--)
            {
                var entry = _entries[i];
                if (!IsErrorType(entry.Type))
                {
                    continue;
                }

                AppendEntryToClipboard(builder, entry);
                copiedCount++;
            }

            if (copiedCount == 0)
            {
                builder.AppendLine("No error entries found.");
            }

            return builder.ToString();
        }

        private void AppendEntryToClipboard(StringBuilder builder, LogEntry entry)
        {
            builder.Append(entry.Count);
            builder.Append(' ');
            builder.Append(entry.DisplayLine);
            builder.Append('\n');

            if (includeStackTraceInCopy && !string.IsNullOrWhiteSpace(entry.Stacktrace))
            {
                builder.AppendLine(entry.Stacktrace.TrimEnd());
            }

            builder.AppendLine();
        }

        private void ReleaseStyleResources()
        {
            _windowStyle = null;
            _toolbarLabelStyle = null;
            _buttonStyle = null;
            _activeButtonStyle = null;
            _copyButtonStyle = null;
            _copyErrorsButtonStyle = null;
            _copyToastStyle = null;
            _countStyle = null;
            _logStyle = null;
            _warningStyle = null;
            _errorStyle = null;
            _stackTraceStyle = null;
            _appliedTheme = null;

            SafeDestroyTexture(ref _windowBackgroundTexture);
            SafeDestroyTexture(ref _buttonTexture);
            SafeDestroyTexture(ref _buttonHoverOverlayTexture);
            SafeDestroyTexture(ref _buttonPressedOverlayTexture);
            SafeDestroyTexture(ref _activeButtonTexture);
            SafeDestroyTexture(ref _copyButtonTexture);
            SafeDestroyTexture(ref _copyErrorsButtonTexture);
            SafeDestroyTexture(ref _rowEvenTexture);
            SafeDestroyTexture(ref _rowOddTexture);
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null && ReferenceEquals(_appliedTheme, theme))
            {
                return;
            }

            ReleaseStyleResources();
            _appliedTheme = theme;

            _windowBackgroundTexture = CreateSolidTexture(theme.WindowBackgroundColor);
            _buttonTexture = CreateSolidTexture(theme.ButtonColor);
            _buttonHoverOverlayTexture = CreateSolidTexture(theme.ButtonHoverOverlayColor);
            _buttonPressedOverlayTexture = CreateSolidTexture(theme.ButtonPressedOverlayColor);
            _activeButtonTexture = CreateSolidTexture(theme.ActiveButtonColor);
            _copyButtonTexture = CreateSolidTexture(theme.CopyAllButtonColor);
            _copyErrorsButtonTexture = CreateSolidTexture(theme.CopyErrorsButtonColor);
            _rowEvenTexture = CreateSolidTexture(theme.RowEvenColor);
            _rowOddTexture = CreateSolidTexture(theme.RowOddColor);

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = CopyRectOffset(theme.WindowPadding, 8, 8, 12, 8),
                border = CopyRectOffset(theme.WindowBorder, 1, 1, 1, 1)
            };
            ApplyBackgroundToAllStates(_windowStyle, _windowBackgroundTexture, Color.white);

            _toolbarLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = theme.ToolbarLabelFontSize,
                normal = { textColor = theme.ToolbarLabelTextColor },
                alignment = TextAnchor.MiddleLeft
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _buttonTexture, textColor = Color.white },
                hover = { background = _buttonTexture, textColor = Color.white },
                active = { background = _buttonTexture, textColor = Color.white },
                focused = { background = _buttonTexture, textColor = Color.white },
                fontSize = theme.ButtonFontSize,
                margin = CopyRectOffset(theme.ButtonMargin, 2, 2, 0, 0),
                padding = CopyRectOffset(theme.ButtonPadding, 6, 6, 1, 1),
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

            _copyErrorsButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { background = _copyErrorsButtonTexture, textColor = Color.white },
                hover = { background = _copyErrorsButtonTexture, textColor = Color.white },
                active = { background = _copyErrorsButtonTexture, textColor = Color.white },
                focused = { background = _copyErrorsButtonTexture, textColor = Color.white }
            };

            _copyToastStyle = new GUIStyle(_toolbarLabelStyle)
            {
                fontSize = theme.CopyToastFontSize,
                alignment = TextAnchor.MiddleRight
            };

            _countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = theme.CountTextColor },
                clipping = TextClipping.Clip
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = theme.LogTextColor },
                wordWrap = true,
                richText = theme.EnableRichText,
                clipping = TextClipping.Clip
            };

            _warningStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = theme.WarningTextColor }
            };

            _errorStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = theme.ErrorTextColor }
            };

            _stackTraceStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = theme.StackTraceTextColor },
                richText = theme.EnableRichText
            };

            SetTraceFontSize(theme.TraceFontSize, theme.StackTraceFontOffset, theme.StackTraceMinFontSize);
        }

        private static RectOffset CopyRectOffset(RectOffset source, int defaultLeft, int defaultRight, int defaultTop, int defaultBottom)
        {
            if (source == null)
            {
                return new RectOffset(defaultLeft, defaultRight, defaultTop, defaultBottom);
            }

            return new RectOffset(source.left, source.right, source.top, source.bottom);
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

        private struct ButtonVisualState
        {
            public float HoverAmount;
            public float ClickPulse;
            public int LastSeenFrame;
        }
    }
}
