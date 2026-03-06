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
        protected bool openOnStart;

        [SerializeField]
        protected KeyCode toggleKey = KeyCode.BackQuote;

        [SerializeField]
        protected Rect windowRect = new Rect(20f, 20f, 1000f, 620f);

        [Header("Log behavior")]
        [SerializeField]
        [Min(10)]
        protected int maxEntries = 2000;

        [SerializeField]
        protected bool collapseDuplicates = true;

        [SerializeField]
        protected bool autoOpenOnError = true;

        [SerializeField]
        protected bool pauseOnError;

        [SerializeField]
        protected bool includeStackTraceInCopy = true;

        [Header("Filters")]
        [SerializeField]
        protected bool showLogs = true;

        [SerializeField]
        protected bool showWarnings = true;

        [SerializeField]
        protected bool showErrors = true;

        [Header("Theme")]
        [SerializeField]
        protected RuntimeConsoleTheme theme;

        protected readonly List<LogEntry> _entries = new List<LogEntry>(256);
        protected readonly Queue<PendingLog> _pendingLogs = new Queue<PendingLog>(128);
        protected readonly List<PendingLog> _flushBuffer = new List<PendingLog>(128);
        protected readonly Dictionary<int, ButtonVisualState> _buttonVisualStates = new Dictionary<int, ButtonVisualState>(32);
        protected readonly List<int> _buttonStateKeyBuffer = new List<int>(32);
        protected readonly List<BottomActionButton> _bottomCenterActions = new List<BottomActionButton>(8);
        protected readonly List<VirtualizedRow> _virtualizedRows = new List<VirtualizedRow>(128);
        protected readonly Dictionary<int, float> _collapsedRowHeights = new Dictionary<int, float>(512);
        protected readonly GUIContent _reusableContent = new GUIContent();
        protected readonly object _pendingLock = new object();

        protected bool _isOpen;
        protected bool _isLeftMouseDown;
        protected bool _pausedByConsole;
        protected Vector2 _scrollPosition;
        protected bool _scrollToBottom;
        protected float _lastScrollViewHeight;
        protected float _lastContentHeight;
        protected float _copyToastTimeLeft;
        protected float _timeScaleBeforePause = 1f;
        protected float _uiScale = 1f;
        protected float _rowHeightCacheWidth = -1f;
        protected int _windowId;
        protected int _buttonDrawIndex;
        protected int _nextEntryId;
        protected int _nextBottomActionId;
        protected int _selectedEntryId = -1;

        protected GUIStyle _windowStyle;
        protected GUIStyle _toolbarLabelStyle;
        protected GUIStyle _buttonStyle;
        protected GUIStyle _activeButtonStyle;
        protected GUIStyle _copyButtonStyle;
        protected GUIStyle _copyErrorsButtonStyle;
        protected GUIStyle _copyToastStyle;
        protected GUIStyle _countStyle;
        protected GUIStyle _logStyle;
        protected GUIStyle _warningStyle;
        protected GUIStyle _errorStyle;
        protected GUIStyle _stackTraceStyle;

        protected Texture2D _windowBackgroundTexture;
        protected Texture2D _headerBackgroundTexture;
        protected Texture2D _buttonTexture;
        protected Texture2D _buttonHoverOverlayTexture;
        protected Texture2D _buttonPressedOverlayTexture;
        protected Texture2D _activeButtonTexture;
        protected Texture2D _copyButtonTexture;
        protected Texture2D _copyErrorsButtonTexture;
        protected Texture2D _rowEvenTexture;
        protected Texture2D _rowOddTexture;

        protected float _rowHeight;

        protected Canvas _uiBlockerCanvas;
        protected RectTransform _uiBlockerRect;
        protected Image _uiBlockerImage;
        protected RuntimeConsoleTheme _appliedTheme;
        protected bool _logSubscriptionActive;
        protected bool _hasLoggedMissingThemeError;

        public bool IsOpen => _isOpen;
        public bool IsPausedByConsole => _pausedByConsole;
        public float CurrentUiScale => _uiScale;
        public int EntryCount => _entries.Count;
        public Vector2 ScrollPosition => _scrollPosition;
        public int SelectedEntryId => _selectedEntryId;
        public bool OpenOnStart
        {
            get => openOnStart;
            set => openOnStart = value;
        }

        public KeyCode ToggleKey
        {
            get => toggleKey;
            set => toggleKey = value;
        }

        public int MaxEntries
        {
            get => maxEntries;
            set
            {
                maxEntries = Mathf.Max(10, value);
                TrimEntriesToCapacity();
            }
        }

        public bool CollapseDuplicates
        {
            get => collapseDuplicates;
            set => collapseDuplicates = value;
        }

        public bool AutoOpenOnError
        {
            get => autoOpenOnError;
            set => autoOpenOnError = value;
        }

        public bool PauseOnError
        {
            get => pauseOnError;
            set
            {
                pauseOnError = value;
                if (!pauseOnError)
                {
                    RestoreTimeScaleIfNeeded();
                }
            }
        }

        public bool IncludeStackTraceInCopy
        {
            get => includeStackTraceInCopy;
            set => includeStackTraceInCopy = value;
        }

        public bool ShowLogsFilter
        {
            get => showLogs;
            set => showLogs = value;
        }

        public bool ShowWarningsFilter
        {
            get => showWarnings;
            set => showWarnings = value;
        }

        public bool ShowErrorsFilter
        {
            get => showErrors;
            set => showErrors = value;
        }

        public Rect WindowRect
        {
            get => windowRect;
            set => windowRect = value;
        }

        public RuntimeConsoleTheme Theme => theme;
        public IReadOnlyList<LogEntry> Entries => _entries;
        protected int CurrentSelectedEntryId
        {
            get => _selectedEntryId;
            set => _selectedEntryId = value;
        }

        protected Vector2 MutableScrollPosition
        {
            get => _scrollPosition;
            set => _scrollPosition = value;
        }

        protected virtual float UnscaledDeltaTime => Time.unscaledDeltaTime;
        protected virtual DateTime GetLogTimestamp() => DateTime.Now;
        protected virtual float GetCurrentTimeScale() => Time.timeScale;
        protected virtual void SetCurrentTimeScale(float value) => Time.timeScale = value;
        protected virtual string BuildDisplayLine(DateTime timestamp, string condition)
        {
            return $"[{timestamp:HH:mm:ss}] {NormalizeLineBreaks(condition)}";
        }

        protected virtual bool CanCollapseWithPrevious(LogEntry previousEntry, string condition, string stacktrace, LogType type)
        {
            return previousEntry.Type == type && previousEntry.Condition == condition && previousEntry.Stacktrace == stacktrace;
        }

        protected virtual void TrimEntriesToCapacity()
        {
            while (_entries.Count > maxEntries)
            {
                var removedEntry = _entries[0];
                if (_selectedEntryId == removedEntry.Id)
                {
                    _selectedEntryId = -1;
                }

                _collapsedRowHeights.Remove(removedEntry.Id);
                _entries.RemoveAt(0);
            }
        }

        protected virtual void OnConsoleOpened()
        {
        }

        protected virtual void OnConsoleClosed()
        {
        }

        protected virtual void OnLogsCleared()
        {
        }

        protected virtual void Awake()
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

        protected virtual void OnDestroy()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();
            DestroyUiBlocker();
            ReleaseStyleResources();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();

            if (_uiBlockerCanvas != null)
            {
                _uiBlockerCanvas.gameObject.SetActive(false);
            }
        }

        protected virtual void OnEnable()
        {
            if (!EnsureThemeAssigned())
            {
                return;
            }

            SubscribeToLogEvents();
            EnsureUiBlocker();
            SetUiBlockerActive(_isOpen);
        }

        protected virtual void OnValidate()
        {
            ReleaseStyleResources();

            if (!Application.isPlaying || !enabled)
            {
                return;
            }

            EnsureThemeAssigned();
        }

        public virtual void SetTheme(RuntimeConsoleTheme runtimeConsoleTheme)
        {
            theme = runtimeConsoleTheme;
            _hasLoggedMissingThemeError = false;
            ReleaseStyleResources();

            if (Application.isPlaying && enabled)
            {
                EnsureThemeAssigned();
            }
        }

        protected virtual bool EnsureThemeAssigned()
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

        protected virtual void SubscribeToLogEvents()
        {
            if (_logSubscriptionActive)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            _logSubscriptionActive = true;
        }

        protected virtual void UnsubscribeFromLogEvents()
        {
            if (!_logSubscriptionActive)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            _logSubscriptionActive = false;
        }

        protected virtual void Update()
        {
            if (theme == null)
            {
                return;
            }

            FlushPendingLogs();
            AnimateButtonStates();

            if (_copyToastTimeLeft > 0f)
            {
                _copyToastTimeLeft = Mathf.Max(0f, _copyToastTimeLeft - UnscaledDeltaTime);
            }
        }

        protected virtual void OnGUI()
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

        protected virtual void HandleToggleHotkeyFromGuiEvent()
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

        protected virtual void UpdatePointerStateFromGuiEvent()
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

        public virtual void Open()
        {
            _isOpen = true;
            SetUiBlockerActive(true);
            OnConsoleOpened();
        }

        public virtual void Close()
        {
            _isOpen = false;
            RestoreTimeScaleIfNeeded();
            SetUiBlockerActive(false);
            OnConsoleClosed();
        }

        public virtual void Toggle()
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

        public virtual string AddBottomCenterAction(string label, Action callback)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Action label cannot be null or whitespace.", nameof(label));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var id = $"bottom_action_{_nextBottomActionId++}";
            _bottomCenterActions.Add(new BottomActionButton(id, label.Trim(), callback));
            return id;
        }

        public virtual bool RemoveBottomCenterAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            for (var i = 0; i < _bottomCenterActions.Count; i++)
            {
                if (!string.Equals(_bottomCenterActions[i].Id, actionId, StringComparison.Ordinal))
                {
                    continue;
                }

                _bottomCenterActions.RemoveAt(i);
                return true;
            }

            return false;
        }

        public virtual void ClearBottomCenterActions()
        {
            _bottomCenterActions.Clear();
        }

        public virtual void ScrollToBottom()
        {
            _scrollToBottom = true;
        }

        public virtual void ClearLogs()
        {
            _entries.Clear();
            _collapsedRowHeights.Clear();
            _selectedEntryId = -1;
            _scrollPosition = Vector2.zero;
            _lastScrollViewHeight = 0f;
            _lastContentHeight = 0f;
            OnLogsCleared();
        }

        protected virtual void DrawWindow(int windowId)
        {
            _buttonDrawIndex = 0;
            var activeTheme = theme;
            DrawHeaderBackground();
            DrawControlsRow();
            GUILayout.Space(activeTheme.ControlsToEntriesSpacing);
            DrawLogList();
            DrawBottomBar();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 44f, 24f));
        }

        protected virtual void DrawHeaderBackground()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.DrawTexture(new Rect(0f, 0f, windowRect.width, 36f), _headerBackgroundTexture);
        }

        protected virtual void DrawControlsRow()
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

        protected virtual void DrawBottomBar()
        {
            GUILayout.Space(theme.BottomBarTopSpacing);
            GUILayout.BeginHorizontal();

            if (DrawToolbarButton("Go down", _buttonStyle, GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                ScrollToBottom();
            }

            GUILayout.FlexibleSpace();
            DrawBottomCenterActions();
            GUILayout.FlexibleSpace();
            DrawCopyToastLabel();

            if (DrawToolbarButton("Copy latest 3 errors", _copyErrorsButtonStyle, GUILayout.Width(170f), GUILayout.Height(24f)))
            {
                CopyLatestErrorsToClipboard(3);
            }

            GUILayout.Space(theme.BottomButtonsSpacing);

            if (DrawToolbarButton("Copy ALL", _copyButtonStyle, GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                CopyAllVisibleEntriesToClipboard();
            }
            GUILayout.EndHorizontal();
        }

        protected virtual void DrawBottomCenterActions()
        {
            for (var i = 0; i < _bottomCenterActions.Count; i++)
            {
                var actionButton = _bottomCenterActions[i];
                if (DrawToolbarButton(actionButton.Label, _buttonStyle, GUILayout.Height(24f)))
                {
                    InvokeBottomCenterAction(actionButton);
                }

                if (i < _bottomCenterActions.Count - 1)
                {
                    GUILayout.Space(theme.BottomButtonsSpacing);
                }
            }
        }

        protected virtual void InvokeBottomCenterAction(BottomActionButton actionButton)
        {
            try
            {
                actionButton.Callback.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        protected virtual void CopyLatestErrorsToClipboard(int maxErrors)
        {
            GUIUtility.systemCopyBuffer = BuildLatestErrorsClipboardText(maxErrors);
            NotifyCopiedToClipboard();
        }

        protected virtual void CopyAllVisibleEntriesToClipboard()
        {
            GUIUtility.systemCopyBuffer = BuildClipboardText();
            NotifyCopiedToClipboard();
        }

        protected virtual void DrawCopyToastLabel()
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

        protected virtual void DrawFilterToggle(ref bool value, string label, Color color)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = color;

            if (DrawToolbarButton(label, value ? _activeButtonStyle : _buttonStyle, GUILayout.Width(24f), GUILayout.Height(22f)))
            {
                value = !value;
            }

            GUI.contentColor = previousColor;
        }

        protected virtual bool DrawToolbarButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            var layoutRect = GUILayoutUtility.GetRect(content, style, options);
            var buttonId = _buttonDrawIndex++;

            var isHovered = layoutRect.Contains(Event.current.mousePosition);
            var isPressed = isHovered && _isLeftMouseDown;
            var state = GetButtonVisualState(buttonId);

            if (Event.current.type == EventType.Repaint)
            {
                state.HoverAmount = Mathf.MoveTowards(state.HoverAmount, isHovered ? 1f : 0f, UnscaledDeltaTime * theme.HoverFadeSpeed);
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

        protected virtual void DrawToolbarButtonFeedbackOverlay(Rect drawRect, bool isHovered, bool isPressed, ButtonVisualState state)
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

        protected virtual void DrawTextureWithAlpha(Rect rect, Texture2D texture, float alpha)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture);
            GUI.color = previousColor;
        }

        protected static void DrawRectBorder(Rect rect, Color color, float thickness = 1f)
        {
            var prevColor = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

            GUI.color = prevColor;
        }

        protected static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        protected virtual void AnimateButtonStates()
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

            var dt = UnscaledDeltaTime;
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

        protected virtual ButtonVisualState GetButtonVisualState(int buttonId)
        {
            if (_buttonVisualStates.TryGetValue(buttonId, out var state))
            {
                return state;
            }

            return default;
        }

        protected static Rect ScaleRectAroundCenter(Rect rect, float scale)
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

        protected virtual void DrawLogList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            _virtualizedRows.Clear();
            var contentHeight = 0f;
            var rowIndex = 0;
            var canCull = _lastScrollViewHeight > 1f;
            var viewportHeight = Mathf.Max(_lastScrollViewHeight, 1f);
            var viewportTop = _scrollPosition.y;
            var viewportBottom = viewportTop + viewportHeight;
            var overscanHeight = Mathf.Max(_rowHeight * 2f, 48f);
            var cullTop = canCull ? viewportTop - overscanHeight : float.NegativeInfinity;
            var cullBottom = canCull ? viewportBottom + overscanHeight : float.PositiveInfinity;
            var estimatedMessageWidth = Mathf.Max(120f, windowRect.width - 80f);

            EnsureRowHeightCacheForWidth(estimatedMessageWidth);

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!ShouldShow(entry.Type))
                {
                    continue;
                }

                var isExpanded = _selectedEntryId == entry.Id;
                var rowHeight = GetRowHeight(entry, GetStyleForType(entry.Type), isExpanded, estimatedMessageWidth);
                var rowTop = contentHeight;
                var rowBottom = rowTop + rowHeight;

                if (rowBottom >= cullTop && rowTop <= cullBottom)
                {
                    _virtualizedRows.Add(new VirtualizedRow(i, rowIndex, rowTop, rowHeight));
                }

                contentHeight = rowBottom;
                rowIndex++;
            }

            // Reserve full content height once, then draw virtualized rows manually.
            var contentRect = GUILayoutUtility.GetRect(0f, 10000f, contentHeight, contentHeight, GUILayout.ExpandWidth(true));

            if (_virtualizedRows.Count > 0)
            {
                for (var i = 0; i < _virtualizedRows.Count; i++)
                {
                    var row = _virtualizedRows[i];
                    var rowRect = new Rect(contentRect.x, contentRect.y + row.Top, contentRect.width, row.Height);
                    DrawLogRow(_entries[row.EntryIndex], row.RowIndex, rowRect);
                }
            }

            GUILayout.EndScrollView();
            _lastContentHeight = contentHeight;

            var scrollViewRect = GUILayoutUtility.GetLastRect();
            _lastScrollViewHeight = scrollViewRect.height;

            if (_scrollToBottom)
            {
                _scrollPosition.y = float.MaxValue;
                _scrollToBottom = false;
            }
        }

        protected virtual void DrawLogRow(LogEntry entry, int rowIndex, Rect rect)
        {
            var messageStyle = GetStyleForType(entry.Type);
            var isExpanded = _selectedEntryId == entry.Id;
            var rowHeight = rect.height;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                HandleLogEntryClick(entry, isExpanded);
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
            {
                DrawRowBackground(rect, rowIndex);
            }

            var messageWidth = rect.width - 34f;
            var messageContent = ToReusableContent(entry.DisplayLine);
            var messageHeight = messageStyle.CalcHeight(messageContent, Mathf.Max(120f, messageWidth));

            var countRect = new Rect(rect.x + 4f, rect.y + 2f, 24f, rowHeight - 4f);
            GUI.Label(countRect, entry.Count.ToString(), _countStyle);

            var messageRect = new Rect(rect.x + 30f, rect.y + 2f, messageWidth, messageHeight);
            GUI.Label(messageRect, messageContent, messageStyle);

            if (isExpanded && CanShowStackTrace(entry))
            {
                var dividerY = messageRect.yMax + 2f;
                GUI.DrawTexture(new Rect(messageRect.x, dividerY, messageRect.width, 1f), _buttonTexture);

                var stackTraceRect = new Rect(messageRect.x, dividerY + 4f, messageRect.width, rowHeight - (dividerY - rect.y) - 6f);
                GUI.Label(stackTraceRect, ToReusableContent(entry.Stacktrace), _stackTraceStyle);
            }
        }

        protected virtual void HandleLogEntryClick(LogEntry entry, bool isExpanded)
        {
            if (CanShowStackTrace(entry))
            {
                _selectedEntryId = isExpanded ? -1 : entry.Id;
                return;
            }

            _selectedEntryId = -1;
        }

        protected virtual void DrawRowBackground(Rect rect, int rowIndex)
        {
            GUI.DrawTexture(rect, rowIndex % 2 == 0 ? _rowEvenTexture : _rowOddTexture);
        }

        protected virtual float GetRowHeight(LogEntry entry, GUIStyle messageStyle, bool isExpanded)
        {
            var estimatedMessageWidth = Mathf.Max(120f, windowRect.width - 80f);
            return GetRowHeight(entry, messageStyle, isExpanded, estimatedMessageWidth);
        }

        protected virtual float GetRowHeight(LogEntry entry, GUIStyle messageStyle, bool isExpanded, float estimatedMessageWidth)
        {
            var collapsedHeight = GetCollapsedRowHeight(entry, messageStyle, estimatedMessageWidth);
            if (!isExpanded || !CanShowStackTrace(entry))
            {
                return collapsedHeight;
            }

            var stackTraceHeight = _stackTraceStyle.CalcHeight(ToReusableContent(entry.Stacktrace), estimatedMessageWidth);
            return collapsedHeight + stackTraceHeight + 8f;
        }

        protected virtual float GetCollapsedRowHeight(LogEntry entry, GUIStyle messageStyle, float estimatedMessageWidth)
        {
            if (_collapsedRowHeights.TryGetValue(entry.Id, out var height))
            {
                return height;
            }

            var messageHeight = messageStyle.CalcHeight(ToReusableContent(entry.DisplayLine), estimatedMessageWidth);
            height = Mathf.Max(_rowHeight, messageHeight + 6f);
            _collapsedRowHeights[entry.Id] = height;
            return height;
        }

        protected virtual void EnsureRowHeightCacheForWidth(float estimatedMessageWidth)
        {
            if (Mathf.Approximately(_rowHeightCacheWidth, estimatedMessageWidth))
            {
                return;
            }

            _rowHeightCacheWidth = estimatedMessageWidth;
            _collapsedRowHeights.Clear();
        }

        protected virtual bool CanShowStackTrace(LogEntry entry)
        {
            return IsErrorType(entry.Type) && !string.IsNullOrWhiteSpace(entry.Stacktrace);
        }

        protected virtual void SetTraceFontSize(int size, int stackTraceFontOffset, int stackTraceMinFontSize)
        {
            size = Mathf.Max(8, size);
            _rowHeight = Mathf.Max(20f, size + 8f);
            _collapsedRowHeights.Clear();

            if (_countStyle != null)
            {
                _countStyle.fontSize = size;
                _logStyle.fontSize = size;
                _warningStyle.fontSize = size;
                _errorStyle.fontSize = size;
                _stackTraceStyle.fontSize = Mathf.Max(stackTraceMinFontSize, size + stackTraceFontOffset);
            }
        }

        protected virtual void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            EnqueuePendingLog(CreatePendingLog(condition, stacktrace, type));
        }

        protected virtual PendingLog CreatePendingLog(string condition, string stacktrace, LogType type)
        {
            return new PendingLog(condition, stacktrace, type);
        }

        protected virtual void EnqueuePendingLog(PendingLog pendingLog)
        {
            lock (_pendingLock)
            {
                _pendingLogs.Enqueue(pendingLog);
            }
        }

        protected virtual void ProcessPendingLog(PendingLog pendingLog)
        {
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

        protected virtual void FlushPendingLogs()
        {
            var shouldStickToBottom = IsScrollNearBottom();

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
                ProcessPendingLog(_flushBuffer[i]);
            }

            if (_isOpen && shouldStickToBottom)
            {
                _scrollToBottom = true;
            }
        }

        protected virtual bool IsScrollAtBottom()
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

        protected virtual bool IsScrollNearBottom()
        {
            if (IsScrollAtBottom())
            {
                return true;
            }

            var maxScroll = Mathf.Max(0f, _lastContentHeight - _lastScrollViewHeight);
            return _scrollPosition.y >= maxScroll - Mathf.Max(_rowHeight, 24f);
        }

        protected virtual void UpdateUiScale()
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

        protected virtual void ClampWindowToVisibleArea()
        {
            var safeScale = Mathf.Max(0.001f, _uiScale);
            var visibleWidth = Screen.width / safeScale;
            var visibleHeight = Screen.height / safeScale;

            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, visibleWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, visibleHeight - windowRect.height));
        }

        protected virtual void EnsureUiBlocker()
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

        protected virtual void SetUiBlockerActive(bool isActive)
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

        protected virtual void SyncUiBlockerToWindow()
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

        protected virtual void DestroyUiBlocker()
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

        protected virtual void AddLog(string condition, string stacktrace, LogType type)
        {
            if (collapseDuplicates && _entries.Count > 0)
            {
                var lastIndex = _entries.Count - 1;
                var last = _entries[lastIndex];
                if (CanCollapseWithPrevious(last, condition, stacktrace, type))
                {
                    last.Count++;
                    _entries[lastIndex] = last;
                    return;
                }
            }

            if (_entries.Count >= maxEntries)
            {
                var removedEntry = _entries[0];
                if (_selectedEntryId == removedEntry.Id)
                {
                    _selectedEntryId = -1;
                }

                _collapsedRowHeights.Remove(removedEntry.Id);
                _entries.RemoveAt(0);
            }

            var timestamp = GetLogTimestamp();
            var displayLine = BuildDisplayLine(timestamp, condition);
            _entries.Add(new LogEntry(_nextEntryId++, condition, stacktrace, type, displayLine));
        }

        protected virtual void PauseTimeScaleIfNeeded()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_pausedByConsole)
            {
                return;
            }

            _timeScaleBeforePause = GetCurrentTimeScale();
            SetCurrentTimeScale(0f);
            _pausedByConsole = true;
        }

        protected virtual void RestoreTimeScaleIfNeeded()
        {
            if (!_pausedByConsole)
            {
                return;
            }

            SetCurrentTimeScale(_timeScaleBeforePause);
            _pausedByConsole = false;
        }

        protected virtual void NotifyCopiedToClipboard()
        {
            _copyToastTimeLeft = theme.CopyToastDuration;
        }

        protected virtual float GetCopyToastAlpha()
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

        protected virtual bool ShouldShow(LogType type)
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

        protected virtual GUIStyle GetStyleForType(LogType type)
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

        protected virtual string BuildClipboardText()
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

        protected virtual string BuildLatestErrorsClipboardText(int maxErrors)
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

        protected virtual void AppendEntryToClipboard(StringBuilder builder, LogEntry entry)
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

        protected virtual void ReleaseStyleResources()
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
            _rowHeightCacheWidth = -1f;
            _collapsedRowHeights.Clear();

            SafeDestroyTexture(ref _windowBackgroundTexture);
            SafeDestroyTexture(ref _headerBackgroundTexture);
            SafeDestroyTexture(ref _buttonTexture);
            SafeDestroyTexture(ref _buttonHoverOverlayTexture);
            SafeDestroyTexture(ref _buttonPressedOverlayTexture);
            SafeDestroyTexture(ref _activeButtonTexture);
            SafeDestroyTexture(ref _copyButtonTexture);
            SafeDestroyTexture(ref _copyErrorsButtonTexture);
            SafeDestroyTexture(ref _rowEvenTexture);
            SafeDestroyTexture(ref _rowOddTexture);
        }

        protected virtual void EnsureStyles()
        {
            if (_windowStyle != null && ReferenceEquals(_appliedTheme, theme))
            {
                return;
            }

            ReleaseStyleResources();
            _appliedTheme = theme;

            _windowBackgroundTexture = CreateSolidTexture(theme.WindowBackgroundColor);
            _headerBackgroundTexture = CreateSolidTexture(theme.HeaderBackgroundColor);
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

        protected static RectOffset CopyRectOffset(RectOffset source, int defaultLeft, int defaultRight, int defaultTop, int defaultBottom)
        {
            if (source == null)
            {
                return new RectOffset(defaultLeft, defaultRight, defaultTop, defaultBottom);
            }

            return new RectOffset(source.left, source.right, source.top, source.bottom);
        }

        protected static Texture2D CreateSolidTexture(Color color)
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

        protected static void ApplyBackgroundToAllStates(GUIStyle style, Texture2D background, Color textColor)
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

        protected static void SafeDestroyTexture(ref Texture2D texture)
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

        protected virtual bool IsErrorType(LogType type)
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

        protected static string GetOnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        protected static string NormalizeLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        protected virtual GUIContent ToReusableContent(string text)
        {
            _reusableContent.text = text ?? string.Empty;
            return _reusableContent;
        }

        protected readonly struct PendingLog
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

        protected readonly struct BottomActionButton
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Action Callback;

            public BottomActionButton(string id, string label, Action callback)
            {
                Id = id;
                Label = label;
                Callback = callback;
            }
        }

        protected readonly struct VirtualizedRow
        {
            public readonly int EntryIndex;
            public readonly int RowIndex;
            public readonly float Top;
            public readonly float Height;

            public VirtualizedRow(int entryIndex, int rowIndex, float top, float height)
            {
                EntryIndex = entryIndex;
                RowIndex = rowIndex;
                Top = top;
                Height = height;
            }
        }

        public struct LogEntry
        {
            public readonly int Id;
            public readonly string Condition;
            public readonly string Stacktrace;
            public readonly LogType Type;
            public readonly string DisplayLine;
            public int Count;

            public LogEntry(int id, string condition, string stacktrace, LogType type, string displayLine)
            {
                Id = id;
                Condition = condition ?? string.Empty;
                Stacktrace = NormalizeLineBreaks(stacktrace ?? string.Empty);
                Type = type;
                Count = 1;
                DisplayLine = NormalizeLineBreaks(displayLine ?? string.Empty);
            }
        }

        protected struct ButtonVisualState
        {
            public float HoverAmount;
            public float ClickPulse;
            public int LastSeenFrame;
        }
    }
}
