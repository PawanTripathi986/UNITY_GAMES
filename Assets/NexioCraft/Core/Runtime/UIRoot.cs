using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    /// <summary>
    /// The single overlay canvas. Holds one full-screen <see cref="UIScreen"/> at a time, a stack of
    /// overlays (dialogs) above it, and an effects layer for toasts and particles.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        public Canvas Canvas { get; private set; }
        public RectTransform Rect { get; private set; }
        public RectTransform FxLayer { get; private set; }
        public UIScreen CurrentScreen { get; private set; }
        public bool HasOverlay => overlays.Count > 0;

        readonly List<UIScreen> overlays = new List<UIScreen>();
        RectTransform screenLayer;
        RectTransform overlayLayer;
        RawImage background;

        public static UIRoot Create(Transform parent)
        {
            var go = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var root = go.AddComponent<UIRoot>();
            root.Setup();
            return root;
        }

        void Setup()
        {
            Canvas = GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            // Expand: the 1080x1920 design always fits; taller phones get extra height, tablets extra width.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // Re-enable so the scale factor is applied now rather than on the scaler's first Update.
            scaler.enabled = false;
            scaler.enabled = true;
            Rect = (RectTransform)transform;

            background = UIFactory.AddRaw(Rect, "Background", Sprites.VerticalGradient(Palette.BackgroundTop, Palette.BackgroundBottom));
            UIFactory.Stretch(background.rectTransform);
            screenLayer = UIFactory.Stretch(UIFactory.AddRect("Screens", Rect));
            overlayLayer = UIFactory.Stretch(UIFactory.AddRect("Overlays", Rect));
            FxLayer = UIFactory.Stretch(UIFactory.AddRect("Fx", Rect));
        }

        public void SetBackground(Color top, Color bottom) => background.texture = Sprites.VerticalGradient(top, bottom);

        /// <summary>Hides the gradient behind every screen so a 3D camera can show through (the sniper game).</summary>
        public void SetBackgroundVisible(bool visible) => background.enabled = visible;

        /// <summary>Replaces the current screen (cross-fade). <paramref name="configure"/> runs before the screen builds.</summary>
        public T Show<T>(Action<T> configure = null) where T : UIScreen
        {
            CloseAllOverlays();
            var previous = CurrentScreen;
            var screen = Create(screenLayer, configure);
            CurrentScreen = screen;
            if (previous != null) StartCoroutine(FadeOutAndDestroy(previous));
            StartCoroutine(FadeIn(screen));
            return screen;
        }

        public T PushOverlay<T>(Action<T> configure = null) where T : UIScreen
        {
            var overlay = Create(overlayLayer, configure);
            overlays.Add(overlay);
            StartCoroutine(FadeIn(overlay));
            return overlay;
        }

        public void CloseOverlay(UIScreen overlay)
        {
            if (overlay == null || !overlays.Remove(overlay)) return;
            StartCoroutine(FadeOutAndDestroy(overlay));
        }

        public void CloseAllOverlays()
        {
            for (int i = overlays.Count - 1; i >= 0; i--) CloseOverlay(overlays[i]);
        }

        /// <summary>Android back button / Escape: the top overlay first, then the screen.</summary>
        public void HandleBack()
        {
            if (overlays.Count > 0)
            {
                var top = overlays[overlays.Count - 1];
                if (!top.HandleBack()) CloseOverlay(top);
                return;
            }
            if (CurrentScreen != null) CurrentScreen.HandleBack();
        }

        /// <summary>Short message pill in the middle of the screen.</summary>
        public void Toast(string message, Color color, float seconds = 1.4f, float yOffset = 0f)
        {
            StartCoroutine(ToastRoutine(message, color, seconds, yOffset));
        }

        IEnumerator ToastRoutine(string message, Color color, float seconds, float yOffset)
        {
            var group = UIFactory.AddRect("Toast", FxLayer).gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var rt = (RectTransform)group.transform;
            var label = UIFactory.AddLabel(rt, "Label", message, 54f, Color.white, FontWeight.ExtraBold);
            float width = label.preferredWidth + 110f;
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, yOffset), new Vector2(width, 124f));
            var pill = UIFactory.AddPanel(rt, "Pill", color, 62f);
            pill.transform.SetAsFirstSibling();
            UIFactory.Stretch(pill.rectTransform);
            UIFactory.Stretch(label.rectTransform);
            UIFactory.AddTextShadow(label, 3f, 0.3f);

            yield return Tween.Run(0.22f, t =>
            {
                group.alpha = t;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, t);
            }, Ease.OutBack);
            yield return Tween.Delay(seconds);
            yield return Tween.Run(0.2f, t =>
            {
                group.alpha = 1f - t;
                rt.anchoredPosition = new Vector2(0f, yOffset + 40f * t);
            });
            Destroy(rt.gameObject);
        }

        T Create<T>(RectTransform layer, Action<T> configure) where T : UIScreen
        {
            var rt = UIFactory.Stretch(UIFactory.AddRect(typeof(T).Name, layer));
            rt.gameObject.AddComponent<CanvasGroup>();
            var screen = rt.gameObject.AddComponent<T>();
            configure?.Invoke(screen);
            screen.Initialize(this);
            return screen;
        }

        static IEnumerator FadeIn(UIScreen screen)
        {
            var group = screen.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            yield return Tween.Run(0.2f, t =>
            {
                if (group != null) group.alpha = t;
            }, Ease.OutQuad);
        }

        static IEnumerator FadeOutAndDestroy(UIScreen screen)
        {
            var group = screen.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Tween.Run(0.15f, t =>
            {
                if (group != null) group.alpha = 1f - t;
            });
            if (screen != null)
            {
                screen.NotifyClosed();
                Destroy(screen.gameObject);
            }
        }
    }

    /// <summary>A full-screen page or dialog. Build its UI inside <see cref="SafeRoot"/>.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        protected UIRoot UI { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Area inside the notch / home indicator insets.</summary>
        protected RectTransform SafeRoot { get; private set; }

        internal void Initialize(UIRoot ui)
        {
            UI = ui;
            SafeRoot = UIFactory.AddRect("Safe", transform);
            SafeRoot.gameObject.AddComponent<SafeAreaFitter>();
            Build();
        }

        internal void NotifyClosed() => OnClosed();

        protected abstract void Build();

        protected virtual void OnClosed() { }

        /// <summary>Return true when the back press was handled.</summary>
        public virtual bool HandleBack() => false;
    }

    /// <summary>Dialog with a dimmed backdrop and a centred card.</summary>
    public abstract class UIOverlay : UIScreen
    {
        protected virtual bool CloseOnBackdropTap => true;

        protected RectTransform Card { get; private set; }

        protected RectTransform BuildCard(float width, float height, Color color)
        {
            var dim = UIFactory.AddImage(transform, "Backdrop", Sprites.White, Palette.Dim, true);
            UIFactory.Stretch(dim.rectTransform);
            dim.transform.SetAsFirstSibling();
            if (CloseOnBackdropTap) UIFactory.MakeButton(dim.gameObject, () => UI.CloseOverlay(this));

            var holder = UIFactory.AddRect("CardHolder", SafeRoot);
            UIFactory.Place(holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            UIFactory.AddShadow(holder, "Shadow", 30f, 0.45f, new Vector2(0f, -18f));
            var card = UIFactory.AddPanel(holder, "Card", color, 56f, true);
            UIFactory.Stretch(card.rectTransform);
            Card = card.rectTransform;
            StartCoroutine(Tween.Run(0.28f, t => holder.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, t), Ease.OutBack));
            return Card;
        }

        public override bool HandleBack()
        {
            UI.CloseOverlay(this);
            return true;
        }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, rounded corners, home indicator).</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int screenSize;

        void Awake() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != screenSize.x || Screen.height != screenSize.y) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            applied = Screen.safeArea;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = applied.position;
            var max = applied.position + applied.size;
            rt.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rt.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Two-choice confirmation dialog.</summary>
    public sealed class ConfirmOverlay : UIOverlay
    {
        public string Title = "Are you sure?";
        public string Message = "";
        public string ConfirmLabel = "Yes";
        public string CancelLabel = "Cancel";
        public Color ConfirmColor = Palette.Red;
        public Action Confirmed;
        /// <summary>Runs when the dialog closes without confirming (cancel, backdrop tap or back button).</summary>
        public Action Cancelled;

        bool confirmed;

        protected override void Build()
        {
            var card = BuildCard(880f, 640f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", Title, 72f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 100f));
            var message = UIFactory.AddLabel(card, "Message", Message, 46f, Palette.TextDim, FontWeight.Medium);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(760f, 160f));

            var cancel = UIFactory.AddGameButton(card, CancelLabel, Palette.Neutral, new Vector2(360f, 140f), () => UI.CloseOverlay(this), 52f);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(-195f, 130f), new Vector2(360f, 140f));
            var confirm = UIFactory.AddGameButton(card, ConfirmLabel, ConfirmColor, new Vector2(360f, 140f), () =>
            {
                confirmed = true;
                UI.CloseOverlay(this);
                Confirmed?.Invoke();
            }, 52f);
            UIFactory.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(195f, 130f), new Vector2(360f, 140f));
        }

        protected override void OnClosed()
        {
            if (!confirmed) Cancelled?.Invoke();
        }
    }
}
