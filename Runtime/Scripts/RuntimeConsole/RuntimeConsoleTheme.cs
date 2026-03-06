using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    [CreateAssetMenu(fileName = "RuntimeConsoleTheme", menuName = "SimpleTools/Runtime Console Theme")]
    public class RuntimeConsoleTheme : ScriptableObject
    {
        [Header("Scale")]
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

        [Header("Typography")]
        [SerializeField]
        [Min(8)]
        private int traceFontSize = 15;

        [SerializeField]
        [Min(8)]
        private int toolbarLabelFontSize = 15;

        [SerializeField]
        [Min(8)]
        private int buttonFontSize = 12;

        [SerializeField]
        [Min(8)]
        private int copyToastFontSize = 12;

        [SerializeField]
        private int stackTraceFontOffset = -1;

        [SerializeField]
        [Min(8)]
        private int stackTraceMinFontSize = 9;

        [SerializeField]
        private bool enableRichText = true;

        [Header("Timing")]
        [SerializeField]
        [Min(0.1f)]
        private float copyToastDuration = 1f;

        [Header("Layout")]
        [SerializeField]
        [Min(0f)]
        private float controlsToEntriesSpacing = 6f;

        [SerializeField]
        [Min(0f)]
        private float bottomBarTopSpacing = 4f;

        [SerializeField]
        [Min(0f)]
        private float bottomButtonsSpacing = 6f;

        [SerializeField]
        [Min(0f)]
        private float copyToastRightSpacing = 8f;

        [SerializeField]
        private RectOffset windowPadding;

        [SerializeField]
        private RectOffset windowBorder;

        [SerializeField]
        private RectOffset buttonMargin;

        [SerializeField]
        private RectOffset buttonPadding;

        [Header("Colors")]
        [SerializeField]
        private Color windowBackgroundColor = new Color(0.06f, 0.08f, 0.11f, 0.98f);

        [SerializeField]
        private Color headerBackgroundColor = new Color(0.1f, 0.14f, 0.19f, 1f);

        [SerializeField]
        private Color buttonColor = new Color(0.24f, 0.24f, 0.24f, 1f);

        [SerializeField]
        private Color activeButtonColor = new Color(0.28f, 0.36f, 0.48f, 1f);

        [SerializeField]
        private Color copyAllButtonColor = new Color(0.45f, 0.31f, 0.12f, 1f);

        [SerializeField]
        private Color copyErrorsButtonColor = new Color(0.18f, 0.42f, 0.22f, 1f);

        [SerializeField]
        private Color buttonHoverOverlayColor = new Color(1f, 1f, 1f, 0.08f);

        [SerializeField]
        private Color buttonPressedOverlayColor = new Color(0f, 0f, 0f, 0.18f);

        [SerializeField]
        private Color buttonHoverBorderColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        [SerializeField]
        private Color buttonPressedBorderColor = new Color(0.06f, 0.06f, 0.06f, 0.9f);

        [SerializeField]
        private Color rowEvenColor = new Color(0.11f, 0.13f, 0.16f, 1f);

        [SerializeField]
        private Color rowOddColor = new Color(0.07f, 0.09f, 0.12f, 1f);

        [SerializeField]
        private Color toolbarLabelTextColor = Color.white;

        [SerializeField]
        private Color countTextColor = Color.white;

        [SerializeField]
        private Color logTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        [SerializeField]
        private Color warningTextColor = new Color(1f, 0.82f, 0.2f, 1f);

        [SerializeField]
        private Color errorTextColor = new Color(1f, 0.39f, 0.39f, 1f);

        [SerializeField]
        private Color stackTraceTextColor = new Color(0.74f, 0.74f, 0.74f, 1f);

        [SerializeField]
        private Color copyToastTextColor = new Color(0.55f, 1f, 0.62f, 1f);

        [SerializeField]
        private Color infoFilterColor = Color.white;

        [SerializeField]
        private Color warningFilterColor = new Color(1f, 0.82f, 0.2f, 1f);

        [SerializeField]
        private Color errorFilterColor = new Color(1f, 0.33f, 0.33f, 1f);

        [Header("Button Animation")]
        [SerializeField]
        [Range(1f, 40f)]
        private float hoverFadeSpeed = 14f;

        [SerializeField]
        [Range(1f, 20f)]
        private float clickPulseDecaySpeed = 6f;

        [SerializeField]
        [Range(0f, 0.25f)]
        private float clickPulseScale = 0.04f;

        [SerializeField]
        [Range(0.8f, 1f)]
        private float pressedScale = 0.975f;

        [SerializeField]
        private Vector2 pressedOffset = new Vector2(1f, 1f);

        [SerializeField]
        [Range(0f, 2f)]
        private float hoverOverlayAlphaMultiplier = 0.9f;

        [SerializeField]
        [Range(0f, 2f)]
        private float pressedOverlayAlphaMultiplier = 1f;

        [SerializeField]
        [Range(0f, 2f)]
        private float clickFlashAlphaMultiplier = 0.9f;

        [SerializeField]
        [Range(0f, 2f)]
        private float hoverBorderHoverContribution = 0.8f;

        [SerializeField]
        [Range(0f, 2f)]
        private float hoverBorderClickContribution = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        private float hoverBorderBaseAlpha = 0.15f;

        [SerializeField]
        [Range(0f, 1f)]
        private float hoverBorderBoostAlpha = 0.35f;

        [SerializeField]
        [Range(0.5f, 3f)]
        private float buttonBorderThickness = 1f;

        [SerializeField]
        [Range(30, 600)]
        private int buttonStateRetentionFrames = 180;

        private void OnEnable()
        {
            if (windowPadding == null)
            {
                windowPadding = new RectOffset(8, 8, 12, 8);
            }

            if (windowBorder == null)
            {
                windowBorder = new RectOffset(1, 1, 1, 1);
            }

            if (buttonMargin == null)
            {
                buttonMargin = new RectOffset(2, 2, 0, 0);
            }

            if (buttonPadding == null)
            {
                buttonPadding = new RectOffset(6, 6, 1, 1);
            }
        }

        public bool AutoScaleWithScreen => autoScaleWithScreen;
        public Vector2Int ReferenceResolution => referenceResolution;
        public float MinUiScale => Mathf.Max(0.1f, minUiScale);
        public float MaxUiScale => Mathf.Max(MinUiScale, maxUiScale);

        public int TraceFontSize => Mathf.Max(8, traceFontSize);
        public int ToolbarLabelFontSize => Mathf.Max(8, toolbarLabelFontSize);
        public int ButtonFontSize => Mathf.Max(8, buttonFontSize);
        public int CopyToastFontSize => Mathf.Max(8, copyToastFontSize);
        public int StackTraceFontOffset => stackTraceFontOffset;
        public int StackTraceMinFontSize => Mathf.Max(8, stackTraceMinFontSize);
        public bool EnableRichText => enableRichText;
        public float CopyToastDuration => Mathf.Max(0.1f, copyToastDuration);

        public float ControlsToEntriesSpacing => Mathf.Max(0f, controlsToEntriesSpacing);
        public float BottomBarTopSpacing => Mathf.Max(0f, bottomBarTopSpacing);
        public float BottomButtonsSpacing => Mathf.Max(0f, bottomButtonsSpacing);
        public float CopyToastRightSpacing => Mathf.Max(0f, copyToastRightSpacing);

        public RectOffset WindowPadding => windowPadding;
        public RectOffset WindowBorder => windowBorder;
        public RectOffset ButtonMargin => buttonMargin;
        public RectOffset ButtonPadding => buttonPadding;

        public Color WindowBackgroundColor => windowBackgroundColor;
        public Color HeaderBackgroundColor => headerBackgroundColor;
        public Color ButtonColor => buttonColor;
        public Color ActiveButtonColor => activeButtonColor;
        public Color CopyAllButtonColor => copyAllButtonColor;
        public Color CopyErrorsButtonColor => copyErrorsButtonColor;
        public Color ButtonHoverOverlayColor => buttonHoverOverlayColor;
        public Color ButtonPressedOverlayColor => buttonPressedOverlayColor;
        public Color ButtonHoverBorderColor => buttonHoverBorderColor;
        public Color ButtonPressedBorderColor => buttonPressedBorderColor;
        public Color RowEvenColor => rowEvenColor;
        public Color RowOddColor => rowOddColor;
        public Color ToolbarLabelTextColor => toolbarLabelTextColor;
        public Color CountTextColor => countTextColor;
        public Color LogTextColor => logTextColor;
        public Color WarningTextColor => warningTextColor;
        public Color ErrorTextColor => errorTextColor;
        public Color StackTraceTextColor => stackTraceTextColor;
        public Color CopyToastTextColor => copyToastTextColor;
        public Color InfoFilterColor => infoFilterColor;
        public Color WarningFilterColor => warningFilterColor;
        public Color ErrorFilterColor => errorFilterColor;

        public float HoverFadeSpeed => Mathf.Max(0.01f, hoverFadeSpeed);
        public float ClickPulseDecaySpeed => Mathf.Max(0.01f, clickPulseDecaySpeed);
        public float ClickPulseScale => Mathf.Max(0f, clickPulseScale);
        public float PressedScale => Mathf.Clamp(pressedScale, 0.8f, 1f);
        public Vector2 PressedOffset => pressedOffset;
        public float HoverOverlayAlphaMultiplier => Mathf.Max(0f, hoverOverlayAlphaMultiplier);
        public float PressedOverlayAlphaMultiplier => Mathf.Max(0f, pressedOverlayAlphaMultiplier);
        public float ClickFlashAlphaMultiplier => Mathf.Max(0f, clickFlashAlphaMultiplier);
        public float HoverBorderHoverContribution => Mathf.Max(0f, hoverBorderHoverContribution);
        public float HoverBorderClickContribution => Mathf.Max(0f, hoverBorderClickContribution);
        public float HoverBorderBaseAlpha => Mathf.Clamp01(hoverBorderBaseAlpha);
        public float HoverBorderBoostAlpha => Mathf.Clamp01(hoverBorderBoostAlpha);
        public float ButtonBorderThickness => Mathf.Max(0.5f, buttonBorderThickness);
        public int ButtonStateRetentionFrames => Mathf.Max(1, buttonStateRetentionFrames);
    }
}
