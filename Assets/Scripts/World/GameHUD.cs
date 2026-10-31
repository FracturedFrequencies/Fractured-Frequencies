using FracturedFrequencies.Core;
using FracturedFrequencies.Player;
using FracturedFrequencies.Tools;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// 2.5D HUD drawn with OnGUI (no Canvas setup needed):
    /// - top banner with the current tutorial step ("STEP 3 / 8") and its instruction,
    /// - a delayed hint below it,
    /// - a tooltip next to the cursor (crystal, glass, cracked stone) and a resonance meter,
    /// - a controls panel in the bottom-left corner,
    /// - short messages and the pause screen.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private TutorialDirector tutorial;
        [SerializeField] private PointerInteractor pointer;
        [SerializeField, TextArea] private string controls =
            "W A S D   move\nSPACE   jump\nLEFT CLICK crystal   change gravity\nHOLD RIGHT CLICK glass   resonate\nR   reset room\nESC   pause";

        private string _message;
        private float _messageUntil;
        private bool _paused;
        private int _lastStep = -1;
        private float _stepFlashUntil;

        private GUIStyle _bannerStyle, _stepStyle, _hintStyle, _tooltipStyle, _controlsStyle, _messageStyle;
        private Texture2D _panel, _panelLight;

        private void OnEnable()
        {
            EventBus<HudMessagePostedEvent>.Subscribe(OnMessagePosted);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<HudMessagePostedEvent>.Unsubscribe(OnMessagePosted);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        private void OnMessagePosted(HudMessagePostedEvent e)
        {
            _message = e.Text;
            _messageUntil = Time.unscaledTime + e.Duration;
        }

        private void OnGameStateChanged(GameStateChangedEvent e) => _paused = e.Current == GameState.Paused;

        private static Texture2D MakeTexture(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (_bannerStyle != null) return;
            int size = Mathf.Max(16, Screen.height / 34);
            _panel = MakeTexture(new Color(0.08f, 0.09f, 0.16f, 0.78f));
            _panelLight = MakeTexture(new Color(0.08f, 0.09f, 0.16f, 0.55f));

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true,
                padding = new RectOffset(24, 24, 14, 14)
            };
            _bannerStyle.normal.background = _panel;
            _bannerStyle.normal.textColor = Color.white;

            _stepStyle = new GUIStyle(GUI.skin.label) { fontSize = (int)(size * 0.6f), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            _stepStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _hintStyle = new GUIStyle(_bannerStyle) { fontSize = (int)(size * 0.8f), padding = new RectOffset(18, 18, 8, 8) };
            _hintStyle.normal.background = _panelLight;
            _hintStyle.normal.textColor = new Color(1f, 0.88f, 0.45f);

            _tooltipStyle = new GUIStyle(_hintStyle) { fontSize = (int)(size * 0.65f), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _tooltipStyle.normal.textColor = Color.white;

            _controlsStyle = new GUIStyle(_hintStyle) { fontSize = (int)(size * 0.6f), alignment = TextAnchor.UpperLeft, wordWrap = false };
            _controlsStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);

            _messageStyle = new GUIStyle(GUI.skin.label) { fontSize = (int)(size * 1.2f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            _messageStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width;
            float h = Screen.height;

            // ---- Tutorial banner ----
            if (tutorial != null)
            {
                if (tutorial.StepIndex != _lastStep)
                {
                    _lastStep = tutorial.StepIndex;
                    _stepFlashUntil = Time.unscaledTime + 0.4f;
                }
                float bannerWidth = Mathf.Min(w * 0.7f, 1100f);
                var content = new GUIContent(tutorial.Instruction);
                float bannerHeight = _bannerStyle.CalcHeight(content, bannerWidth);
                var bannerRect = new Rect((w - bannerWidth) / 2f, 28f, bannerWidth, bannerHeight);

                // Short pop when a new step starts.
                if (Time.unscaledTime < _stepFlashUntil)
                {
                    float t = (_stepFlashUntil - Time.unscaledTime) / 0.4f;
                    bannerRect = Expand(bannerRect, 10f * t);
                }

                string stepLabel = tutorial.IsFinished ? "DONE" : $"STEP {tutorial.StepIndex + 1} / {tutorial.StepCount}";
                GUI.Label(new Rect(0, 6f, w, 22f), stepLabel, _stepStyle);
                GUI.Box(bannerRect, content, _bannerStyle);

                string hint = tutorial.Hint;
                if (!string.IsNullOrEmpty(hint))
                {
                    var hintContent = new GUIContent("Hint: " + hint);
                    float hintWidth = bannerWidth * 0.85f;
                    float hintHeight = _hintStyle.CalcHeight(hintContent, hintWidth);
                    GUI.Box(new Rect((w - hintWidth) / 2f, bannerRect.yMax + 8f, hintWidth, hintHeight), hintContent, _hintStyle);
                }
            }

            // ---- Tooltip and resonance meter at the cursor ----
            if (pointer != null && !_paused)
            {
                string text = null;
                if (pointer.HoveredCrystal != null)
                    text = pointer.HoveredCrystal.IsActive ? "This surface is already the floor" : "LEFT CLICK: make this surface the floor";
                else if (pointer.HoveredResonant != null)
                    text = pointer.HoveredResonant.Breakable ? "HOLD RIGHT CLICK: resonate until it shatters" : "Resonates, but cannot break";
                else if (pointer.HoveredImpactBreakable != null)
                    text = pointer.HoveredImpactBreakable.DisplayName + ": too thick to resonate.\nOnly a heavy impact breaks it.";

                Vector2 mouse = Event.current.mousePosition;
                if (text != null)
                {
                    var tip = new GUIContent(text);
                    Vector2 size = _tooltipStyle.CalcSize(tip);
                    GUI.Box(new Rect(mouse.x + 18f, mouse.y + 14f, size.x, size.y), tip, _tooltipStyle);
                }

                Resonant target = pointer.ResonateTarget;
                if (target != null)
                {
                    float a = Mathf.Clamp01(target.Amplitude);
                    var bar = new Rect(mouse.x - 60f, mouse.y - 30f, 120f, 10f);
                    GUI.DrawTexture(bar, _panel);
                    var old = GUI.color;
                    GUI.color = Color.Lerp(new Color(0.6f, 0.85f, 1f), new Color(1f, 0.45f, 0.75f), a);
                    GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * a, bar.height - 2f), Texture2D.whiteTexture);
                    GUI.color = old;
                }
            }

            // ---- Controls panel ----
            var controlsContent = new GUIContent(controls);
            Vector2 controlsSize = _controlsStyle.CalcSize(controlsContent);
            GUI.Box(new Rect(16f, h - controlsSize.y - 16f, controlsSize.x, controlsSize.y), controlsContent, _controlsStyle);

            // ---- Messages ----
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
                GUI.Label(new Rect(w * 0.15f, h * 0.62f, w * 0.7f, 60f), _message, _messageStyle);

            // ---- Pause ----
            if (_paused)
            {
                GUI.DrawTexture(new Rect(0, 0, w, h), _panelLight);
                GUI.Label(new Rect(0, h * 0.45f, w, 60f), "PAUSED\nPress ESC or click to continue", _messageStyle);
            }
        }

        private static Rect Expand(Rect r, float amount) =>
            new Rect(r.x - amount, r.y - amount * 0.5f, r.width + amount * 2f, r.height + amount);
    }
}
