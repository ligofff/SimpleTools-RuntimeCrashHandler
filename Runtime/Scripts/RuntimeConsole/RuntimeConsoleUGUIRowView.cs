using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    [DisallowMultipleComponent]
    public sealed class RuntimeConsoleUGUIRowView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private RectTransform rowRoot;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private TMP_Text countLabel;

        [SerializeField]
        private TMP_Text messageLabel;

        [SerializeField]
        private RectTransform messageRect;

        [SerializeField]
        private RectTransform stackRoot;

        [SerializeField]
        private TMP_Text stackLabel;

        [SerializeField]
        private RectTransform stackRect;

        [SerializeField]
        private LayoutElement rowLayoutElement;

        [SerializeField]
        private bool disableRootContentSizeFitterAtRuntime = true;

        private ContentSizeFitter _rootContentSizeFitter;
        private bool _cachedOffsets;
        private float _cachedOffsetMinX;
        private float _cachedOffsetMaxX;

        private int _entryId;
        private Action<int> _clickHandler;

        public RectTransform RowRoot => rowRoot != null ? rowRoot : (RectTransform)transform;
        public Image BackgroundImage => backgroundImage;
        public TMP_Text CountLabel => countLabel;
        public TMP_Text MessageLabel => messageLabel;
        public RectTransform MessageRect => messageRect;
        public RectTransform StackRoot => stackRoot;
        public TMP_Text StackLabel => stackLabel;
        public RectTransform StackRect => stackRect;
        public LayoutElement RowLayoutElement => rowLayoutElement;

        public void BindClick(int entryId, Action<int> clickHandler)
        {
            _entryId = entryId;
            _clickHandler = clickHandler;
        }

        public void SetStackVisible(bool visible)
        {
            if (stackRoot != null)
            {
                stackRoot.gameObject.SetActive(visible);
            }
        }

        public void EnsureLayoutElement()
        {
            if (rowLayoutElement != null)
            {
                return;
            }

            var target = RowRoot;
            rowLayoutElement = target.GetComponent<LayoutElement>();
            if (rowLayoutElement == null)
            {
                rowLayoutElement = target.gameObject.AddComponent<LayoutElement>();
            }
        }

        public void PrepareForVirtualizedLayout()
        {
            if (disableRootContentSizeFitterAtRuntime)
            {
                if (_rootContentSizeFitter == null)
                {
                    _rootContentSizeFitter = RowRoot.GetComponent<ContentSizeFitter>();
                }

                if (_rootContentSizeFitter != null && _rootContentSizeFitter.enabled)
                {
                    _rootContentSizeFitter.enabled = false;
                }
            }

            EnsureLayoutElement();
            if (rowLayoutElement != null)
            {
                rowLayoutElement.ignoreLayout = true;
            }

            CacheHorizontalOffsets();
        }

        public void ApplyVirtualizedLayout(float top, float height)
        {
            var target = RowRoot;

            if (!_cachedOffsets)
            {
                CacheHorizontalOffsets();
            }

            var anchorMin = target.anchorMin;
            var anchorMax = target.anchorMax;
            var pivot = target.pivot;

            if (!Mathf.Approximately(anchorMin.x, 0f) ||
                !Mathf.Approximately(anchorMin.y, 1f) ||
                !Mathf.Approximately(anchorMax.x, 1f) ||
                !Mathf.Approximately(anchorMax.y, 1f) ||
                !Mathf.Approximately(pivot.y, 1f))
            {
                target.anchorMin = new Vector2(0f, 1f);
                target.anchorMax = new Vector2(1f, 1f);
                target.pivot = new Vector2(0.5f, 1f);
            }

            var safeTop = Mathf.Max(0f, top);
            var safeHeight = Mathf.Max(0f, height);
            var offsetMin = target.offsetMin;
            var offsetMax = target.offsetMax;
            offsetMin.x = _cachedOffsetMinX;
            offsetMax.x = _cachedOffsetMaxX;
            offsetMin.y = -(safeTop + safeHeight);
            offsetMax.y = -safeTop;
            target.offsetMin = offsetMin;
            target.offsetMax = offsetMax;

            SetPreferredHeight(safeHeight);
        }

        private void CacheHorizontalOffsets()
        {
            var target = RowRoot;
            _cachedOffsetMinX = target.offsetMin.x;
            _cachedOffsetMaxX = target.offsetMax.x;
            _cachedOffsets = true;
        }

        public void SetPreferredHeight(float height)
        {
            EnsureLayoutElement();
            if (rowLayoutElement != null)
            {
                var safeHeight = Mathf.Max(0f, height);
                if (Mathf.Abs(rowLayoutElement.preferredHeight - safeHeight) < 0.1f &&
                    Mathf.Abs(rowLayoutElement.minHeight - safeHeight) < 0.1f &&
                    Mathf.Abs(rowLayoutElement.flexibleHeight) < 0.1f)
                {
                    return;
                }

                rowLayoutElement.minHeight = safeHeight;
                rowLayoutElement.preferredHeight = safeHeight;
                rowLayoutElement.flexibleHeight = 0f;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _clickHandler?.Invoke(_entryId);
        }

        private void OnValidate()
        {
            if (rowRoot == null)
            {
                rowRoot = GetComponent<RectTransform>();
            }

            if (rowLayoutElement == null && rowRoot != null)
            {
                rowLayoutElement = rowRoot.GetComponent<LayoutElement>();
            }
        }
    }
}
