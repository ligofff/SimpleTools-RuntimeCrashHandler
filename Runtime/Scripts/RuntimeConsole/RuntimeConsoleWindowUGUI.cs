using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    [DisallowMultipleComponent]
    public partial class RuntimeConsoleWindowUGUI : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField]
        protected bool openOnStart;

        [SerializeField]
        protected KeyCode toggleKey = KeyCode.BackQuote;

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

        [Header("uGUI Setup")]
        [SerializeField]
        protected RectTransform windowRoot;

        [SerializeField]
        protected Image windowBackground;

        [SerializeField]
        protected Image headerBackground;

        [SerializeField]
        protected Button clearButton;

        [SerializeField]
        protected Button autoOpenButton;

        [SerializeField]
        protected TMP_Text autoOpenButtonLabel;

        [SerializeField]
        protected Button pauseButton;

        [SerializeField]
        protected TMP_Text pauseButtonLabel;

        [SerializeField]
        protected Button infoFilterButton;

        [SerializeField]
        protected Button warningFilterButton;

        [SerializeField]
        protected Button errorFilterButton;

        [SerializeField]
        protected Button closeButton;

        [SerializeField]
        protected ScrollRect logScrollRect;

        [SerializeField]
        protected RectTransform logContent;

        [SerializeField]
        protected RuntimeConsoleUGUIRowView rowPrefab;

        [SerializeField]
        protected Button goDownButton;

        [SerializeField]
        protected RectTransform bottomCenterActionsContainer;

        [SerializeField]
        protected Button bottomCenterActionButtonPrefab;

        [SerializeField]
        protected Button copyLatestErrorsButton;

        [SerializeField]
        protected Button copyAllButton;

        [SerializeField]
        protected TMP_Text copyToastLabel;

        [Header("Row Layout")]
        [SerializeField]
        protected bool forceClampedScrollInInspectorLayout = true;

        [SerializeField]
        protected bool forceDisableInertiaInInspectorLayout = true;

        [SerializeField]
        protected bool disableLogContentSizeFitterAtRuntime = true;

        [SerializeField]
        protected bool disableLogContentVerticalLayoutGroupAtRuntime = true;

        [Header("uGUI Performance")]
        [SerializeField]
        [Min(8)]
        protected int pooledRowCount = 48;

        [SerializeField]
        [Min(1)]
        protected int overscanRows = 4;

        protected readonly List<LogEntry> _entries = new List<LogEntry>(256);
        protected readonly Queue<PendingLog> _pendingLogs = new Queue<PendingLog>(128);
        protected readonly List<PendingLog> _flushBuffer = new List<PendingLog>(128);
        protected readonly object _pendingLock = new object();
        protected readonly List<RowLayout> _rows = new List<RowLayout>(256);
        protected readonly List<float> _rowHeightPrefix = new List<float>(257);
        protected readonly List<RuntimeConsoleUGUIRowView> _rowPool = new List<RuntimeConsoleUGUIRowView>(64);
        protected readonly Dictionary<int, float> _collapsedHeightCache = new Dictionary<int, float>(512);
        protected readonly List<BottomActionButton> _bottomCenterActions = new List<BottomActionButton>(8);
        protected readonly List<Button> _bottomCenterRuntimeButtons = new List<Button>(8);
        protected readonly List<RectTransform> _widthHierarchyBuffer = new List<RectTransform>(16);

        protected bool _isOpen;
        protected bool _hasOpenedAtLeastOnce;
        protected bool _scrollToBottom;
        protected bool _layoutDirty = true;
        protected bool _rowsDirty = true;
        protected bool _logSubscriptionActive;
        protected bool _pausedByConsole;
        protected bool _suppressScrollValueChanged;
        protected bool _hasLoggedMissingThemeError;
        protected bool _hasLoggedMissingReferencesError;

        protected int _selectedEntryId = -1;
        protected int _nextEntryId;
        protected int _nextBottomActionId;
        protected int _lastVirtualizedFirst = -1;
        protected int _lastVirtualizedLast = -1;
        protected int _lastVirtualizedVisibleCount = -1;

        protected float _timeScaleBeforePause = 1f;
        protected float _copyToastTimeLeft;
        protected float _rowHeight = 24f;
        protected float _baseCollapsedRowHeight = 24f;
        protected float _lastViewportWidth = -1f;
        protected float _lastViewportHeight = -1f;
        protected float _lastContentHeight;
        protected float _cachedMessageWidth = -1f;
        protected float _cachedStackWidth = -1f;

        protected TMP_Text _infoFilterLabel;
        protected TMP_Text _warningFilterLabel;
        protected TMP_Text _errorFilterLabel;
        protected TMP_Text _closeButtonLabel;
        protected VerticalLayoutGroup _logContentVerticalLayoutGroup;
        protected ContentSizeFitter _logContentSizeFitter;

        public bool IsOpen => _isOpen;
        public bool IsPausedByConsole => _pausedByConsole;
        public int EntryCount => _entries.Count;
        public int SelectedEntryId => _selectedEntryId;
        public IReadOnlyList<LogEntry> Entries => _entries;

        protected virtual void Awake()
        {
            _isOpen = openOnStart;
            if (_isOpen)
            {
                _hasOpenedAtLeastOnce = true;
                _scrollToBottom = true;
            }

            if (!EnsureThemeAssigned())
            {
                return;
            }

            if (!ValidateReferences())
            {
                return;
            }

            ResolveLabels();
            CacheTemplateMetrics();
            ApplyWindowRect();
            RegisterButtonEvents();
            ApplyTheme();
            UpdateHeaderState();
            UpdateCopyToastVisual();

            if (rowPrefab.gameObject.scene.IsValid())
            {
                rowPrefab.gameObject.SetActive(false);
            }

            if (bottomCenterActionButtonPrefab.gameObject.scene.IsValid())
            {
                bottomCenterActionButtonPrefab.gameObject.SetActive(false);
            }

            SetWindowActive(_isOpen);
            EnsureRowPoolSize(Mathf.Max(8, pooledRowCount));
        }

        protected virtual void OnEnable()
        {
            if (!EnsureThemeAssigned() || !ValidateReferences())
            {
                return;
            }

            SubscribeToLogEvents();
            SetWindowActive(_isOpen);
            _layoutDirty = true;
            _rowsDirty = true;
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeFromLogEvents();
            RestoreTimeScaleIfNeeded();
            ClearRuntimeBottomActionButtons();
            ClearRuntimeRows();
        }

        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _layoutDirty = true;
            _rowsDirty = true;
            _cachedMessageWidth = -1f;
            _cachedStackWidth = -1f;
            _collapsedHeightCache.Clear();
            ApplyWindowRect();
            ApplyTheme();
            UpdateHeaderState();
        }

        protected virtual void OnGUI()
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

        protected virtual void Update()
        {
            if (!EnsureThemeAssigned() || !ValidateReferences())
            {
                return;
            }

            FlushPendingLogs();
            UpdateCopyToast();

            if (!_isOpen)
            {
                return;
            }
        }

        protected virtual void LateUpdate()
        {
            if (!EnsureThemeAssigned() || !ValidateReferences())
            {
                return;
            }

            if (!_isOpen)
            {
                return;
            }

            if (HasViewportSizeChanged())
            {
                _layoutDirty = true;
                _rowsDirty = true;
            }

            RunVirtualizationPass();

            // Scrollbar visibility/viewport can change after the first pass in the same frame.
            if (HasViewportSizeChanged())
            {
                _layoutDirty = true;
                _rowsDirty = true;
                RunVirtualizationPass();
            }
        }

        public virtual void Open()
        {
            _isOpen = true;
            if (!_hasOpenedAtLeastOnce)
            {
                _hasOpenedAtLeastOnce = true;
                _scrollToBottom = true;
            }

            SetWindowActive(true);
            _rowsDirty = true;
            OnConsoleOpened();
        }

        public virtual void Close()
        {
            _isOpen = false;
            RestoreTimeScaleIfNeeded();
            SetWindowActive(false);
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

        public virtual void ScrollToBottom()
        {
            _scrollToBottom = true;
        }

        public virtual void ClearLogs()
        {
            _entries.Clear();
            _rows.Clear();
            _rowHeightPrefix.Clear();
            _collapsedHeightCache.Clear();
            _selectedEntryId = -1;
            _lastContentHeight = 0f;
            SetLogContentHeight(0f);
            ResetVirtualizedRangeCache();
            _layoutDirty = true;
            _rowsDirty = true;
            OnLogsCleared();
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
            RebuildBottomCenterActions();
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
                RebuildBottomCenterActions();
                return true;
            }

            return false;
        }

        public virtual void ClearBottomCenterActions()
        {
            _bottomCenterActions.Clear();
            RebuildBottomCenterActions();
        }
        public virtual void SetTheme(RuntimeConsoleTheme runtimeConsoleTheme)
        {
            theme = runtimeConsoleTheme;
            _hasLoggedMissingThemeError = false;
            if (!EnsureThemeAssigned())
            {
                return;
            }

            ApplyTheme();
            _cachedMessageWidth = -1f;
            _cachedStackWidth = -1f;
            _collapsedHeightCache.Clear();
            _layoutDirty = true;
            _rowsDirty = true;
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

        protected virtual bool EnsureThemeAssigned()
        {
            if (theme != null)
            {
                _hasLoggedMissingThemeError = false;
                return true;
            }

            if (!_hasLoggedMissingThemeError)
            {
                Debug.LogException(new InvalidOperationException($"{nameof(RuntimeConsoleWindowUGUI)} requires a {nameof(RuntimeConsoleTheme)} asset."), this);
                _hasLoggedMissingThemeError = true;
            }

            enabled = false;
            return false;
        }

        protected virtual bool ValidateReferences()
        {
            var valid =
                windowRoot != null &&
                clearButton != null &&
                autoOpenButton != null &&
                pauseButton != null &&
                infoFilterButton != null &&
                warningFilterButton != null &&
                errorFilterButton != null &&
                closeButton != null &&
                logScrollRect != null &&
                logContent != null &&
                rowPrefab != null &&
                goDownButton != null &&
                bottomCenterActionsContainer != null &&
                bottomCenterActionButtonPrefab != null &&
                copyLatestErrorsButton != null &&
                copyAllButton != null &&
                copyToastLabel != null;

            if (valid)
            {
                _hasLoggedMissingReferencesError = false;
                if (logScrollRect.content != logContent)
                {
                    logScrollRect.content = logContent;
                }

                _logContentVerticalLayoutGroup = logContent != null
                    ? logContent.GetComponent<VerticalLayoutGroup>()
                    : null;
                _logContentSizeFitter = logContent != null
                    ? logContent.GetComponent<ContentSizeFitter>()
                    : null;

                if (disableLogContentSizeFitterAtRuntime &&
                    _logContentSizeFitter != null &&
                    _logContentSizeFitter.enabled)
                {
                    _logContentSizeFitter.enabled = false;
                }

                if (disableLogContentVerticalLayoutGroupAtRuntime &&
                    _logContentVerticalLayoutGroup != null &&
                    _logContentVerticalLayoutGroup.enabled)
                {
                    _logContentVerticalLayoutGroup.enabled = false;
                }

                if (forceClampedScrollInInspectorLayout &&
                    logScrollRect.movementType != ScrollRect.MovementType.Clamped)
                {
                    logScrollRect.movementType = ScrollRect.MovementType.Clamped;
                }

                if (forceDisableInertiaInInspectorLayout && logScrollRect.inertia)
                {
                    logScrollRect.inertia = false;
                }

                return true;
            }

            if (!_hasLoggedMissingReferencesError)
            {
                Debug.LogError($"{nameof(RuntimeConsoleWindowUGUI)} has missing uGUI references. Assign all required fields in inspector.", this);
                _hasLoggedMissingReferencesError = true;
            }

            enabled = false;
            return false;
        }

        protected virtual void ResolveLabels()
        {
            autoOpenButtonLabel = ResolveButtonLabel(autoOpenButton, autoOpenButtonLabel);
            pauseButtonLabel = ResolveButtonLabel(pauseButton, pauseButtonLabel);
            _infoFilterLabel = ResolveButtonLabel(infoFilterButton, null);
            _warningFilterLabel = ResolveButtonLabel(warningFilterButton, null);
            _errorFilterLabel = ResolveButtonLabel(errorFilterButton, null);
            _closeButtonLabel = ResolveButtonLabel(closeButton, null);
        }

        protected virtual TMP_Text ResolveButtonLabel(Button button, TMP_Text current)
        {
            if (current != null)
            {
                return current;
            }

            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        protected virtual void CacheTemplateMetrics()
        {
            if (rowPrefab == null)
            {
                return;
            }

            if (rowPrefab.MessageLabel != null)
            {
                _rowHeight = Mathf.Max(_rowHeight, rowPrefab.MessageLabel.fontSize + 8f);
            }

            if (rowPrefab.RowLayoutElement != null)
            {
                _baseCollapsedRowHeight = Mathf.Max(
                    _baseCollapsedRowHeight,
                    rowPrefab.RowLayoutElement.preferredHeight,
                    rowPrefab.RowLayoutElement.minHeight);
            }

            if (rowPrefab.RowRoot != null)
            {
                _baseCollapsedRowHeight = Mathf.Max(_baseCollapsedRowHeight, rowPrefab.RowRoot.rect.height);
            }
        }

        protected virtual void RegisterButtonEvents()
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(ClearLogs);

            autoOpenButton.onClick.RemoveAllListeners();
            autoOpenButton.onClick.AddListener(() =>
            {
                autoOpenOnError = !autoOpenOnError;
                UpdateHeaderState();
            });

            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(() =>
            {
                pauseOnError = !pauseOnError;
                if (!pauseOnError)
                {
                    RestoreTimeScaleIfNeeded();
                }

                UpdateHeaderState();
            });

            infoFilterButton.onClick.RemoveAllListeners();
            infoFilterButton.onClick.AddListener(() =>
            {
                showLogs = !showLogs;
                _layoutDirty = true;
                _rowsDirty = true;
                UpdateHeaderState();
            });

            warningFilterButton.onClick.RemoveAllListeners();
            warningFilterButton.onClick.AddListener(() =>
            {
                showWarnings = !showWarnings;
                _layoutDirty = true;
                _rowsDirty = true;
                UpdateHeaderState();
            });

            errorFilterButton.onClick.RemoveAllListeners();
            errorFilterButton.onClick.AddListener(() =>
            {
                showErrors = !showErrors;
                _layoutDirty = true;
                _rowsDirty = true;
                UpdateHeaderState();
            });

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);

            goDownButton.onClick.RemoveAllListeners();
            goDownButton.onClick.AddListener(ScrollToBottom);

            copyLatestErrorsButton.onClick.RemoveAllListeners();
            copyLatestErrorsButton.onClick.AddListener(() => CopyLatestErrorsToClipboard(3));

            copyAllButton.onClick.RemoveAllListeners();
            copyAllButton.onClick.AddListener(CopyAllVisibleEntriesToClipboard);

            logScrollRect.onValueChanged.RemoveAllListeners();
            logScrollRect.onValueChanged.AddListener(_ =>
            {
                if (_suppressScrollValueChanged)
                {
                    return;
                }

                _rowsDirty = true;
            });
        }

        protected virtual void ApplyWindowRect()
        {
            // Prepared-uGUI mode: keep transform fully controlled by scene/prefab.
        }

        protected virtual void ApplyTheme()
        {
            if (theme == null)
            {
                return;
            }

            _rowHeight = Mathf.Max(20f, theme.TraceFontSize + 8f);

            if (windowBackground != null)
            {
                windowBackground.color = theme.WindowBackgroundColor;
            }

            if (headerBackground != null)
            {
                headerBackground.color = theme.HeaderBackgroundColor;
            }

            ApplyButtonVisual(clearButton, ResolveButtonLabel(clearButton, null), theme.ButtonColor, Color.white);
            ApplyButtonVisual(autoOpenButton, autoOpenButtonLabel, autoOpenOnError ? theme.ActiveButtonColor : theme.ButtonColor, Color.white);
            ApplyButtonVisual(pauseButton, pauseButtonLabel, pauseOnError ? theme.ActiveButtonColor : theme.ButtonColor, Color.white);
            ApplyButtonVisual(infoFilterButton, _infoFilterLabel, showLogs ? theme.ActiveButtonColor : theme.ButtonColor, theme.InfoFilterColor);
            ApplyButtonVisual(warningFilterButton, _warningFilterLabel, showWarnings ? theme.ActiveButtonColor : theme.ButtonColor, theme.WarningFilterColor);
            ApplyButtonVisual(errorFilterButton, _errorFilterLabel, showErrors ? theme.ActiveButtonColor : theme.ButtonColor, theme.ErrorFilterColor);
            ApplyButtonVisual(closeButton, _closeButtonLabel, theme.ButtonColor, Color.white);

            ApplyButtonVisual(goDownButton, ResolveButtonLabel(goDownButton, null), theme.ButtonColor, Color.white);
            ApplyButtonVisual(copyLatestErrorsButton, ResolveButtonLabel(copyLatestErrorsButton, null), theme.CopyErrorsButtonColor, Color.white);
            ApplyButtonVisual(copyAllButton, ResolveButtonLabel(copyAllButton, null), theme.CopyAllButtonColor, Color.white);

            copyToastLabel.fontSize = theme.CopyToastFontSize;
            UpdateCopyToastVisual();

            _cachedMessageWidth = -1f;
            _cachedStackWidth = -1f;
            _collapsedHeightCache.Clear();
            _layoutDirty = true;
            _rowsDirty = true;

            RebuildBottomCenterActions();
        }

        protected virtual void ApplyButtonVisual(Button button, TMP_Text label, Color backgroundColor, Color textColor)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic is Graphic graphic)
            {
                graphic.color = backgroundColor;
            }

            if (label != null)
            {
                label.color = textColor;
                label.fontSize = theme.ButtonFontSize;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        protected virtual void UpdateHeaderState()
        {
            if (autoOpenButtonLabel != null)
            {
                autoOpenButtonLabel.text = $"Auto-open is {GetOnOff(autoOpenOnError)}";
            }

            if (pauseButtonLabel != null)
            {
                pauseButtonLabel.text = $"Pause on error is {GetOnOff(pauseOnError)}";
            }

            ApplyTheme();
        }

        protected virtual void SetWindowActive(bool active)
        {
            if (windowRoot != null)
            {
                windowRoot.gameObject.SetActive(active);
            }
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

        protected virtual void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            lock (_pendingLock)
            {
                _pendingLogs.Enqueue(new PendingLog(condition, stacktrace, type));
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

        protected virtual void ProcessPendingLog(PendingLog log)
        {
            var isError = IsErrorType(log.Type);
            AddLog(log.Condition, log.Stacktrace, log.Type);

            if (autoOpenOnError && isError)
            {
                Open();
            }

            if (pauseOnError && isError)
            {
                PauseTimeScaleIfNeeded();
            }
        }
        protected virtual void AddLog(string condition, string stacktrace, LogType type)
        {
            var normalizedCondition = NormalizeLineBreaks(condition ?? string.Empty);
            var normalizedStacktrace = NormalizeLineBreaks(stacktrace ?? string.Empty);

            if (collapseDuplicates && _entries.Count > 0)
            {
                var lastIndex = _entries.Count - 1;
                var last = _entries[lastIndex];
                if (last.Type == type &&
                    string.Equals(last.Condition, normalizedCondition, StringComparison.Ordinal) &&
                    string.Equals(last.Stacktrace, normalizedStacktrace, StringComparison.Ordinal))
                {
                    last.Count++;
                    _entries[lastIndex] = last;
                    _layoutDirty = true;
                    _rowsDirty = true;
                    return;
                }
            }

            if (_entries.Count >= maxEntries)
            {
                var removed = _entries[0];
                if (_selectedEntryId == removed.Id)
                {
                    _selectedEntryId = -1;
                }

                _collapsedHeightCache.Remove(removed.Id);
                _entries.RemoveAt(0);
            }

            var displayLine = $"[{DateTime.Now:HH:mm:ss}] {normalizedCondition}";
            _entries.Add(new LogEntry(_nextEntryId++, normalizedCondition, normalizedStacktrace, type, displayLine));
            _layoutDirty = true;
            _rowsDirty = true;
        }

        protected virtual void RebuildRowsLayout()
        {
            if (logContent == null)
            {
                return;
            }

            var messageWidth = GetMessageWidth();
            var stackWidth = GetStackWidth(messageWidth);
            if (!Mathf.Approximately(messageWidth, _cachedMessageWidth) ||
                !Mathf.Approximately(stackWidth, _cachedStackWidth))
            {
                _cachedMessageWidth = messageWidth;
                _cachedStackWidth = stackWidth;
                _collapsedHeightCache.Clear();
            }

            var viewport = logScrollRect != null ? logScrollRect.viewport : null;
            if (viewport != null)
            {
                _lastViewportWidth = viewport.rect.width;
                _lastViewportHeight = viewport.rect.height;
            }

            _rows.Clear();
            _rowHeightPrefix.Clear();
            _rowHeightPrefix.Add(0f);
            var spacing = GetLogContentSpacing();
            GetLogContentPadding(out var paddingTop, out var paddingBottom);
            var rowIndex = 0;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!ShouldShow(entry.Type))
                {
                    continue;
                }

                var expanded = _selectedEntryId == entry.Id && CanShowStackTrace(entry);
                var rowHeight = GetRowHeight(entry, messageWidth, stackWidth, expanded);
                var heightBefore = _rowHeightPrefix[rowIndex];
                var rowTop = paddingTop + heightBefore + rowIndex * spacing;

                _rows.Add(new RowLayout(i, rowIndex, rowTop, rowHeight, expanded));
                _rowHeightPrefix.Add(heightBefore + rowHeight);

                rowIndex++;
            }

            var rowsHeight = _rowHeightPrefix[rowIndex];
            var rowsSpacing = rowIndex > 0 ? (rowIndex - 1) * spacing : 0f;
            _lastContentHeight = paddingTop + rowsHeight + rowsSpacing + paddingBottom;
            SetLogContentHeight(_lastContentHeight);

            ResetVirtualizedRangeCache();
            _layoutDirty = false;
            _rowsDirty = true;
        }

        protected virtual float GetRowHeight(LogEntry entry, float messageWidth, float stackWidth, bool expanded)
        {
            if (!_collapsedHeightCache.TryGetValue(entry.Id, out var collapsedHeight))
            {
                var measuredMessageHeight = MeasureTextHeight(
                    rowPrefab.MessageLabel,
                    entry.DisplayLine,
                    messageWidth,
                    GetMessageFontSize(),
                    theme != null && theme.EnableRichText);
                collapsedHeight = Mathf.Max(_rowHeight, _baseCollapsedRowHeight, measuredMessageHeight + 8f);
                _collapsedHeightCache[entry.Id] = collapsedHeight;
            }

            if (!expanded)
            {
                return collapsedHeight;
            }

            var stackHeight = MeasureTextHeight(
                rowPrefab.StackLabel,
                entry.Stacktrace,
                stackWidth,
                GetStackFontSize(),
                theme != null && theme.EnableRichText);
            return collapsedHeight + stackHeight + 12f;
        }

        protected virtual float MeasureTextHeight(TMP_Text templateLabel, string text, float width, float fontSize, bool richText)
        {
            if (templateLabel == null)
            {
                return _rowHeight;
            }

            var previousFontSize = templateLabel.fontSize;
            var previousRichText = templateLabel.richText;
            if (!Mathf.Approximately(previousFontSize, fontSize))
            {
                templateLabel.fontSize = fontSize;
            }

            if (previousRichText != richText)
            {
                templateLabel.richText = richText;
            }

            var result = templateLabel.GetPreferredValues(text ?? string.Empty, width, 0f).y;

            if (!Mathf.Approximately(templateLabel.fontSize, previousFontSize))
            {
                templateLabel.fontSize = previousFontSize;
            }

            if (templateLabel.richText != previousRichText)
            {
                templateLabel.richText = previousRichText;
            }

            return result;
        }

        protected virtual float GetMessageFontSize()
        {
            return theme != null ? theme.TraceFontSize : _rowHeight;
        }

        protected virtual float GetStackFontSize()
        {
            if (theme == null)
            {
                return _rowHeight;
            }

            return Mathf.Max(theme.StackTraceMinFontSize, theme.TraceFontSize + theme.StackTraceFontOffset);
        }

        protected virtual float GetMessageWidth()
        {
            var viewport = logScrollRect != null ? logScrollRect.viewport : null;
            if (viewport == null)
            {
                return 240f;
            }

            var width = GetTemplateTextWidth(rowPrefab != null ? rowPrefab.MessageRect : null);
            if (width > 1f)
            {
                return Mathf.Max(120f, width);
            }

            return Mathf.Max(120f, viewport.rect.width - 34f);
        }

        protected virtual float GetStackWidth(float fallbackWidth)
        {
            var width = GetTemplateTextWidth(rowPrefab != null ? rowPrefab.StackRect : null);
            if (width > 1f)
            {
                return Mathf.Max(120f, width);
            }

            return Mathf.Max(120f, fallbackWidth);
        }

        protected virtual float GetTemplateTextWidth(RectTransform targetRect)
        {
            if (logScrollRect == null || rowPrefab == null)
            {
                return 0f;
            }

            var viewport = logScrollRect.viewport;
            if (viewport == null)
            {
                return 0f;
            }

            var contentWidth = logContent != null ? logContent.rect.width : 0f;
            if (contentWidth <= 1f)
            {
                contentWidth = viewport.rect.width;
            }

            if (contentWidth <= 1f)
            {
                return 0f;
            }

            var rowRoot = rowPrefab.RowRoot;
            var rowWidth = EstimateRectWidthByParent(rowRoot, contentWidth);
            if (rowWidth <= 1f)
            {
                rowWidth = contentWidth;
            }

            if (targetRect == null)
            {
                targetRect = rowPrefab.MessageLabel != null ? rowPrefab.MessageLabel.rectTransform : null;
            }

            if (targetRect == null)
            {
                return 0f;
            }

            var targetWidth = EstimateDescendantWidth(rowRoot, targetRect, rowWidth);
            if (targetWidth <= 1f)
            {
                targetWidth = targetRect.rect.width;
            }

            return targetWidth;
        }

        protected virtual float EstimateRectWidthByParent(RectTransform rect, float parentWidth)
        {
            if (rect == null)
            {
                return 0f;
            }

            var stretch = rect.anchorMax.x - rect.anchorMin.x;
            if (stretch <= 0.0001f)
            {
                return Mathf.Max(0f, rect.sizeDelta.x);
            }

            return Mathf.Max(0f, parentWidth * stretch + rect.sizeDelta.x);
        }

        protected virtual float EstimateDescendantWidth(RectTransform root, RectTransform target, float rootWidth)
        {
            if (root == null || target == null)
            {
                return 0f;
            }

            if (target == root)
            {
                return rootWidth;
            }

            _widthHierarchyBuffer.Clear();
            var current = target;
            while (current != null && current != root)
            {
                _widthHierarchyBuffer.Add(current);
                current = current.parent as RectTransform;
            }

            if (current != root)
            {
                return EstimateRectWidthByParent(target, rootWidth);
            }

            var width = rootWidth;
            for (var i = _widthHierarchyBuffer.Count - 1; i >= 0; i--)
            {
                width = EstimateRectWidthByParent(_widthHierarchyBuffer[i], width);
            }

            return width;
        }

        protected virtual bool HasViewportSizeChanged()
        {
            var viewport = logScrollRect != null ? logScrollRect.viewport : null;
            if (viewport == null)
            {
                return false;
            }

            var width = viewport.rect.width;
            var height = viewport.rect.height;

            if (_lastViewportWidth < 0f || _lastViewportHeight < 0f)
            {
                _lastViewportWidth = width;
                _lastViewportHeight = height;
                return false;
            }

            if (!Mathf.Approximately(_lastViewportWidth, width) ||
                !Mathf.Approximately(_lastViewportHeight, height))
            {
                _lastViewportWidth = width;
                _lastViewportHeight = height;
                return true;
            }

            return false;
        }

        protected virtual void RefreshVisibleRows()
        {
            if (!_isOpen)
            {
                HideAllRows();
                _rowsDirty = false;
                return;
            }

            var viewport = logScrollRect.viewport;
            if (viewport == null)
            {
                HideAllRows();
                _rowsDirty = false;
                return;
            }

            _lastViewportWidth = Mathf.Max(1f, viewport.rect.width);
            _lastViewportHeight = Mathf.Max(1f, viewport.rect.height);
            var maxScroll = Mathf.Max(0f, _lastContentHeight - _lastViewportHeight);
            var scrollTop = GetCurrentScrollTop(maxScroll);
            var overscanHeight = Mathf.Max(_rowHeight * overscanRows, 24f);
            var visibleTop = Mathf.Max(0f, scrollTop - overscanHeight);
            var visibleBottom = Mathf.Min(_lastContentHeight, scrollTop + _lastViewportHeight + overscanHeight);

            if (_rows.Count == 0)
            {
                HideAllRows();
                _rowsDirty = false;
                return;
            }

            var first = FindFirstVisibleRowIndex(visibleTop);
            var last = first >= 0 ? FindLastVisibleRowIndex(visibleBottom) : -1;
            if (first >= 0 && last < first)
            {
                last = first;
            }

            if (first >= 0)
            {
                var indexBuffer = Mathf.Max(2, overscanRows);
                first = Mathf.Max(0, first - indexBuffer);
                last = Mathf.Min(_rows.Count - 1, last + indexBuffer);
            }

            var visibleCount = first >= 0 ? last - first + 1 : 0;
            EnsureRowPoolSize(Mathf.Max(pooledRowCount, visibleCount));

            if (!_rowsDirty &&
                first == _lastVirtualizedFirst &&
                last == _lastVirtualizedLast &&
                visibleCount == _lastVirtualizedVisibleCount)
            {
                _rowsDirty = false;
                return;
            }

            ApplyAnchoredVirtualization(first, last, visibleCount);
            _rowsDirty = false;
        }

        protected virtual void RunVirtualizationPass()
        {
            if (_layoutDirty)
            {
                RebuildRowsLayout();
                Canvas.ForceUpdateCanvases();
            }

            if (_scrollToBottom)
            {
                ScrollToBottomInternal();
            }

            RefreshVisibleRows();
        }

        protected virtual int FindFirstVisibleRowIndex(float visibleTop)
        {
            var low = 0;
            var high = _rows.Count - 1;
            var result = -1;

            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var row = _rows[mid];
                var rowBottom = row.Top + row.Height;
                if (rowBottom >= visibleTop)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return result;
        }

        protected virtual int FindLastVisibleRowIndex(float visibleBottom)
        {
            var low = 0;
            var high = _rows.Count - 1;
            var result = -1;

            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var row = _rows[mid];
                if (row.Top <= visibleBottom)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return result;
        }

        protected virtual void ApplyAnchoredVirtualization(int first, int last, int visibleCount)
        {
            for (var i = 0; i < _rowPool.Count; i++)
            {
                var rowView = _rowPool[i];
                if (i >= visibleCount)
                {
                    if (rowView.gameObject.activeSelf)
                    {
                        rowView.gameObject.SetActive(false);
                    }

                    continue;
                }

                var row = _rows[first + i];
                BindRow(rowView, row);
                rowView.ApplyVirtualizedLayout(row.Top, row.Height);
                if (!rowView.gameObject.activeSelf)
                {
                    rowView.gameObject.SetActive(true);
                }
            }

            _lastVirtualizedFirst = first;
            _lastVirtualizedLast = last;
            _lastVirtualizedVisibleCount = visibleCount;
        }

        protected virtual void EnsureRowPoolSize(int requiredCount)
        {
            requiredCount = Mathf.Max(1, requiredCount);
            while (_rowPool.Count < requiredCount)
            {
                var rowView = Instantiate(rowPrefab, logContent);
                rowView.gameObject.name = $"RuntimeConsoleRow_{_rowPool.Count}";
                rowView.PrepareForVirtualizedLayout();
                rowView.gameObject.SetActive(false);
                _rowPool.Add(rowView);
            }
        }

        protected virtual void BindRow(RuntimeConsoleUGUIRowView rowView, RowLayout layout)
        {
            var entry = _entries[layout.EntryIndex];

            rowView.BindClick(entry.Id, HandleRowClick);

            if (rowView.BackgroundImage != null)
            {
                rowView.BackgroundImage.color = layout.RowIndex % 2 == 0 ? theme.RowEvenColor : theme.RowOddColor;
            }

            if (rowView.CountLabel != null)
            {
                rowView.CountLabel.text = entry.Count.ToString();
                rowView.CountLabel.color = theme.CountTextColor;
                rowView.CountLabel.fontSize = GetMessageFontSize();
            }

            if (rowView.MessageLabel != null)
            {
                rowView.MessageLabel.text = entry.DisplayLine;
                rowView.MessageLabel.color = GetTextColor(entry.Type);
                rowView.MessageLabel.fontSize = GetMessageFontSize();
                rowView.MessageLabel.richText = theme.EnableRichText;
            }

            if (!layout.Expanded || rowView.StackRoot == null)
            {
                rowView.SetStackVisible(false);
                return;
            }

            rowView.SetStackVisible(true);

            if (rowView.StackLabel != null)
            {
                rowView.StackLabel.text = entry.Stacktrace;
                rowView.StackLabel.color = theme.StackTraceTextColor;
                rowView.StackLabel.fontSize = GetStackFontSize();
                rowView.StackLabel.richText = theme.EnableRichText;
            }

        }

        protected virtual void HandleRowClick(int entryId)
        {
            if (!TryGetEntryById(entryId, out var clickedEntry))
            {
                _selectedEntryId = -1;
                return;
            }

            if (!CanShowStackTrace(clickedEntry))
            {
                _selectedEntryId = -1;
                _layoutDirty = true;
                _rowsDirty = true;
                return;
            }

            _selectedEntryId = _selectedEntryId == entryId ? -1 : entryId;
            _layoutDirty = true;
            _rowsDirty = true;
        }

        protected virtual bool TryGetEntryById(int entryId, out LogEntry entry)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == entryId)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        protected virtual void HideAllRows()
        {
            for (var i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i].gameObject.activeSelf)
                {
                    _rowPool[i].gameObject.SetActive(false);
                }
            }

            ResetVirtualizedRangeCache();
        }

        protected virtual void ClearRuntimeRows()
        {
            for (var i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                {
                    Destroy(_rowPool[i].gameObject);
                }
            }

            _rowPool.Clear();
        }
        protected virtual void RebuildBottomCenterActions()
        {
            ClearRuntimeBottomActionButtons();

            for (var i = 0; i < _bottomCenterActions.Count; i++)
            {
                var action = _bottomCenterActions[i];
                var runtimeButton = Instantiate(bottomCenterActionButtonPrefab, bottomCenterActionsContainer);
                runtimeButton.gameObject.name = $"BottomAction_{i}_{action.Id}";
                runtimeButton.gameObject.SetActive(true);

                var label = runtimeButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = action.Label;
                }

                ApplyButtonVisual(runtimeButton, label, theme.ButtonColor, Color.white);

                var callback = action.Callback;
                runtimeButton.onClick.AddListener(() =>
                {
                    try
                    {
                        callback.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, this);
                    }
                });

                _bottomCenterRuntimeButtons.Add(runtimeButton);
            }
        }

        protected virtual void ClearRuntimeBottomActionButtons()
        {
            for (var i = 0; i < _bottomCenterRuntimeButtons.Count; i++)
            {
                var button = _bottomCenterRuntimeButtons[i];
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            _bottomCenterRuntimeButtons.Clear();
        }

        protected virtual void SetLogContentHeight(float height)
        {
            if (logContent == null)
            {
                return;
            }

            var safeHeight = Mathf.Max(0f, height);
            if (Mathf.Abs(logContent.sizeDelta.y - safeHeight) < 0.1f)
            {
                return;
            }

            logContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, safeHeight);
            if (logContent.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.MarkLayoutForRebuild(logContent);
            }
        }

        protected virtual void ResetVirtualizedRangeCache()
        {
            _lastVirtualizedFirst = -1;
            _lastVirtualizedLast = -1;
            _lastVirtualizedVisibleCount = -1;
        }

        protected virtual float GetLogContentSpacing()
        {
            if (logContent == null)
            {
                return 0f;
            }

            var verticalLayout = GetLogContentVerticalLayoutGroup();
            return verticalLayout != null ? Mathf.Max(0f, verticalLayout.spacing) : 0f;
        }

        protected virtual void GetLogContentPadding(out float top, out float bottom)
        {
            top = 0f;
            bottom = 0f;
            if (logContent == null)
            {
                return;
            }

            var verticalLayout = GetLogContentVerticalLayoutGroup();
            if (verticalLayout == null || verticalLayout.padding == null)
            {
                return;
            }

            top = Mathf.Max(0f, verticalLayout.padding.top);
            bottom = Mathf.Max(0f, verticalLayout.padding.bottom);
        }

        protected virtual VerticalLayoutGroup GetLogContentVerticalLayoutGroup()
        {
            if (_logContentVerticalLayoutGroup == null && logContent != null)
            {
                _logContentVerticalLayoutGroup = logContent.GetComponent<VerticalLayoutGroup>();
            }

            return _logContentVerticalLayoutGroup;
        }

        protected virtual float GetCurrentScrollTop(float maxScroll)
        {
            maxScroll = Mathf.Max(0f, maxScroll);
            if (maxScroll <= 0.01f)
            {
                return 0f;
            }

            if (logContent != null)
            {
                var anchoredY = logContent.anchoredPosition.y;
                if (!float.IsNaN(anchoredY) && !float.IsInfinity(anchoredY))
                {
                    return Mathf.Clamp(anchoredY, 0f, maxScroll);
                }
            }

            if (logScrollRect != null)
            {
                var normalized = logScrollRect.verticalNormalizedPosition;
                if (!float.IsNaN(normalized) && !float.IsInfinity(normalized))
                {
                    return Mathf.Clamp01(1f - normalized) * maxScroll;
                }
            }

            return 0f;
        }

        protected virtual bool IsScrollNearBottom()
        {
            var viewport = logScrollRect.viewport;
            if (viewport == null)
            {
                return true;
            }

            var viewportHeight = Mathf.Max(1f, viewport.rect.height);
            var maxScroll = Mathf.Max(0f, _lastContentHeight - viewportHeight);
            var currentScroll = GetCurrentScrollTop(maxScroll);

            if (_lastContentHeight <= viewportHeight + 1f)
            {
                return true;
            }

            return currentScroll >= maxScroll - Mathf.Max(_rowHeight, 24f);
        }

        protected virtual void ScrollToBottomInternal()
        {
            var viewport = logScrollRect.viewport;
            if (viewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            _suppressScrollValueChanged = true;
            try
            {
                logScrollRect.StopMovement();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
            finally
            {
                _suppressScrollValueChanged = false;
            }

            _scrollToBottom = false;
            _rowsDirty = true;
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

        protected virtual bool CanShowStackTrace(LogEntry entry)
        {
            return IsErrorType(entry.Type) && !string.IsNullOrWhiteSpace(entry.Stacktrace);
        }

        protected virtual Color GetTextColor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return theme.WarningTextColor;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return theme.ErrorTextColor;
                default:
                    return theme.LogTextColor;
            }
        }

        protected virtual void PauseTimeScaleIfNeeded()
        {
            if (!Application.isPlaying || _pausedByConsole)
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _pausedByConsole = true;
        }

        protected virtual void RestoreTimeScaleIfNeeded()
        {
            if (!_pausedByConsole)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _pausedByConsole = false;
        }

        protected virtual void CopyLatestErrorsToClipboard(int maxErrors)
        {
            GUIUtility.systemCopyBuffer = BuildLatestErrorsClipboardText(maxErrors);
            _copyToastTimeLeft = theme.CopyToastDuration;
            UpdateCopyToastVisual();
        }

        protected virtual void CopyAllVisibleEntriesToClipboard()
        {
            GUIUtility.systemCopyBuffer = BuildClipboardText();
            _copyToastTimeLeft = theme.CopyToastDuration;
            UpdateCopyToastVisual();
        }

        protected virtual void UpdateCopyToast()
        {
            if (_copyToastTimeLeft <= 0f)
            {
                UpdateCopyToastVisual();
                return;
            }

            _copyToastTimeLeft = Mathf.Max(0f, _copyToastTimeLeft - Time.unscaledDeltaTime);
            UpdateCopyToastVisual();
        }

        protected virtual void UpdateCopyToastVisual()
        {
            if (copyToastLabel == null)
            {
                return;
            }

            if (_copyToastTimeLeft <= 0f)
            {
                copyToastLabel.text = string.Empty;
                return;
            }

            var t = Mathf.Clamp01(_copyToastTimeLeft / Mathf.Max(0.1f, theme.CopyToastDuration));
            var alpha = Mathf.Pow(t, 0.7f);
            var color = theme.CopyToastTextColor;
            copyToastLabel.text = "Copied to clipboard!";
            copyToastLabel.color = new Color(color.r, color.g, color.b, color.a * alpha);
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

                AppendEntry(builder, entry);
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
            maxErrors = Mathf.Max(1, maxErrors);

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

                AppendEntry(builder, entry);
                copiedCount++;
            }

            if (copiedCount == 0)
            {
                builder.AppendLine("No error entries found.");
            }

            return builder.ToString();
        }

        protected virtual void AppendEntry(StringBuilder builder, LogEntry entry)
        {
            builder.Append(entry.Count).Append(' ').Append(entry.DisplayLine).Append('\n');

            if (includeStackTraceInCopy && !string.IsNullOrWhiteSpace(entry.Stacktrace))
            {
                builder.AppendLine(entry.Stacktrace.TrimEnd());
            }

            builder.AppendLine();
        }

        protected static string NormalizeLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        protected static string GetOnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }
    }
}
