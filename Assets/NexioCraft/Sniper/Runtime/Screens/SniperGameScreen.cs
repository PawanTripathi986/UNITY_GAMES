using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Sniper
{
    /// <summary>
    /// A mission: the 3D view with the HUD over it. Drag to aim, scope in, hold breath to steady, fire.
    /// </summary>
    public sealed class SniperGameScreen : UIScreen
    {
        public int MissionIndex;

        static readonly float[] RangeMarks = { 150f, 200f, 250f, 300f };

        SniperSession session;
        MissionDef mission;

        CanvasGroup hudGroup, scopeGroup, hipGroup, cinemaGroup;
        RectTransform scopeFrame, reticle, previewDot, zoomTrack, zoomKnob, hitMarker;
        Image scopeTop, scopeBottom, cinemaTop, cinemaBottom;
        Text objectiveText, objectiveLabel, timerText, windText, rangeText, idText, zoomText, statusText;
        Image timerIcon, windArrow, breathRing, fireRing, fireFace, scopeIconImage;
        Image[] ammoPips;
        RectTransform[] rangeTicks;
        GameObject breathRoot, zoomRoot;
        float hitMarkerTime;
        Vector2 laidOut;
        bool resultShown;

        // ------------------------------------------------------------ build

        protected override void Build()
        {
            mission = SniperMissions.Get(MissionIndex);
            UI.SetBackgroundVisible(false);
            session = SniperSession.Create(mission, SniperPrefs.Progress.CurrentRifle);
            session.ShotLanded += OnShotLanded;
            session.Ended += OnEnded;
            session.TargetsAlerted += OnAlerted;
            session.BulletCamChanged += OnBulletCam;
            App.PauseChanged += OnAppPause;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Order matters for touches: the look area sits under everything else.
            BuildLookArea();
            BuildScope();
            BuildHipCrosshair();
            BuildHud();
            BuildCinema();
            SafeRoot.SetSiblingIndex(3);
            cinemaGroup.transform.SetAsLastSibling();

            UI.PushOverlay<SniperBriefingOverlay>(o =>
            {
                o.Mission = mission;
                o.Start = () => session.Begin();
                o.Back = LeaveToMenu;
            });
        }

        void BuildLookArea()
        {
            var area = UIFactory.AddImage(transform, "Look Area", Sprites.White, new Color(0f, 0f, 0f, 0f), true);
            UIFactory.Stretch(area.rectTransform);
            area.gameObject.AddComponent<DragPad>().Dragged = delta => session.Look(delta);
            area.transform.SetSiblingIndex(0);
        }

        void BuildScope()
        {
            var root = UIFactory.Stretch(UIFactory.AddRect("Scope", transform));
            root.SetSiblingIndex(1);
            scopeGroup = root.gameObject.AddComponent<CanvasGroup>();
            scopeGroup.blocksRaycasts = false;
            scopeGroup.alpha = 0f;

            scopeFrame = UIFactory.AddRect("Lens", root);
            UIFactory.Place(scopeFrame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
            var mask = UIFactory.AddRaw(scopeFrame, "Mask", SniperArt.ScopeMask);
            UIFactory.Stretch(mask.rectTransform);
            scopeTop = UIFactory.AddImage(root, "Top", Sprites.White, Color.black);
            scopeBottom = UIFactory.AddImage(root, "Bottom", Sprites.White, Color.black);

            reticle = UIFactory.AddRect("Reticle", scopeFrame);
            UIFactory.Place(reticle, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var ink = new Color(0.02f, 0.02f, 0.03f, 0.95f);
            // Thin centre lines and thick outer posts on both axes.
            foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                bool horizontal = direction.y == 0f;
                var thin = UIFactory.AddImage(reticle, "Line", Sprites.White, ink);
                thin.rectTransform.sizeDelta = horizontal ? new Vector2(470f, 3f) : new Vector2(3f, 470f);
                thin.rectTransform.anchoredPosition = direction * (20f + 235f);
                var post = UIFactory.AddImage(reticle, "Post", Sprites.White, ink);
                post.rectTransform.sizeDelta = horizontal ? new Vector2(250f, 12f) : new Vector2(12f, 250f);
                post.rectTransform.anchoredPosition = direction * (250f + 125f);
                for (int i = 1; i <= 4; i++)
                {
                    var dot = UIFactory.AddImage(reticle, "Mil", Sprites.Circle, ink);
                    dot.rectTransform.sizeDelta = new Vector2(11f, 11f);
                    dot.rectTransform.anchoredPosition = direction * (i * 50f);
                }
            }
            var centre = UIFactory.AddImage(reticle, "Centre", Sprites.Circle, SniperArt.Hostile);
            centre.rectTransform.sizeDelta = new Vector2(12f, 12f);

            rangeTicks = new RectTransform[RangeMarks.Length];
            for (int i = 0; i < RangeMarks.Length; i++)
            {
                var tick = UIFactory.AddImage(reticle, "Range " + RangeMarks[i], Sprites.White, new Color(1f, 0.35f, 0.25f, 0.95f));
                tick.rectTransform.sizeDelta = new Vector2(i % 2 == 0 ? 46f : 30f, 4f);
                var label = UIFactory.AddLabel(tick.rectTransform, "Label", Mathf.RoundToInt(RangeMarks[i]).ToString(), 26f, new Color(1f, 0.45f, 0.35f), FontWeight.Bold, TextAnchor.MiddleLeft);
                UIFactory.Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(40f, 0f), new Vector2(80f, 30f));
                rangeTicks[i] = tick.rectTransform;
            }

            // Readouts sit in the lower half of the lens, clear of the target under the crosshair.
            rangeText = UIFactory.AddLabel(reticle, "Range", "", 40f, new Color(0.75f, 1f, 0.8f), FontWeight.ExtraBold);
            UIFactory.Place(rangeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(190f, -300f), new Vector2(260f, 50f));
            UIFactory.AddTextShadow(rangeText, 3f, 0.7f);
            idText = UIFactory.AddLabel(reticle, "Target", "", 40f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(idText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-190f, -300f), new Vector2(300f, 50f));
            UIFactory.AddTextShadow(idText, 3f, 0.7f);

            var preview = UIFactory.AddImage(root, "Impact", Sprites.Ring(0.35f), new Color(1f, 0.25f, 0.2f, 0.95f));
            previewDot = preview.rectTransform;
            UIFactory.Place(previewDot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        }

        void BuildHipCrosshair()
        {
            var root = UIFactory.Stretch(UIFactory.AddRect("Crosshair", transform));
            root.SetSiblingIndex(2);
            hipGroup = root.gameObject.AddComponent<CanvasGroup>();
            hipGroup.blocksRaycasts = false;
            var colour = new Color(1f, 1f, 1f, 0.85f);
            foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                var line = UIFactory.AddImage(root, "Tick", Sprites.White, colour);
                UIFactory.Place(line.rectTransform, new Vector2(0.5f, 0.5f), direction * 30f, direction.y == 0f ? new Vector2(26f, 5f) : new Vector2(5f, 26f));
            }
            var dot = UIFactory.AddImage(root, "Dot", Sprites.Circle, SniperArt.Hostile);
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));

            var marker = UIFactory.AddRect("Hit Marker", root);
            UIFactory.Place(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            for (int i = 0; i < 4; i++)
            {
                var bar = UIFactory.AddImage(marker, "Bar", Sprites.White, Color.white);
                float angle = 45f + i * 90f;
                var offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 42f;
                UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), offset, new Vector2(30f, 7f));
                bar.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            }
            hitMarker = marker;
            hitMarker.gameObject.SetActive(false);
            // The hit marker should show over the scope too.
            marker.SetParent(transform, true);
        }

        void BuildHud()
        {
            hudGroup = SafeRoot.gameObject.AddComponent<CanvasGroup>();

            var pause = UIFactory.AddIconButton(SafeRoot, IconKind.Pause, Palette.WithAlpha(Palette.CardRaised, 0.9f), 104f, OpenPause);
            UIFactory.Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(40f + 52f, -30f - 52f), new Vector2(104f, 104f));

            // Targets and clock.
            var chip = UIFactory.AddPanel(SafeRoot, "Mission Chip", Palette.WithAlpha(Palette.Inset, 0.82f), 40f);
            UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(-20f, -82f), new Vector2(440f, 110f));
            var targetIcon = UIFactory.AddImage(chip.rectTransform, "Icon", Icons.Get(IconKind.Robot), SniperArt.Hostile);
            UIFactory.Place(targetIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(60f, 60f));
            objectiveText = UIFactory.AddLabel(chip.rectTransform, "Count", "", 50f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(objectiveText.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, 10f), new Vector2(120f, 60f));
            objectiveLabel = UIFactory.AddLabel(chip.rectTransform, "Label", "", 24f, Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(objectiveLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, -30f), new Vector2(120f, 30f));
            timerIcon = UIFactory.AddImage(chip.rectTransform, "Clock", SniperArt.Icon(SniperIcon.Clock), Palette.TextDim);
            UIFactory.Place(timerIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(46f, 46f));
            timerText = UIFactory.AddLabel(chip.rectTransform, "Timer", "", 50f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(timerText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-72f, 2f), new Vector2(140f, 60f));

            // Wind.
            var wind = UIFactory.AddPanel(SafeRoot, "Wind Chip", Palette.WithAlpha(Palette.Inset, 0.82f), 40f);
            UIFactory.Place(wind.rectTransform, new Vector2(1f, 1f), new Vector2(-40f - 95f, -82f), new Vector2(190f, 110f));
            windArrow = UIFactory.AddImage(wind.rectTransform, "Arrow", Icons.Get(IconKind.Back), new Color(0.75f, 0.9f, 1f));
            UIFactory.Place(windArrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 12f), new Vector2(46f, 46f));
            windText = UIFactory.AddLabel(wind.rectTransform, "Speed", "", 36f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(windText.rectTransform, new Vector2(0f, 0.5f), new Vector2(128f, 12f), new Vector2(110f, 44f));
            var windLabel = UIFactory.AddLabel(wind.rectTransform, "Label", "WIND  m/s", 22f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(windLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(180f, 30f));

            statusText = UIFactory.AddLabel(SafeRoot, "Status", "", 40f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(700f, 60f));
            UIFactory.AddTextShadow(statusText, 3f, 0.6f);

            // Fire.
            var fire = RoundButton(SafeRoot, "Fire", SniperArt.Icon(SniperIcon.Bullet), Palette.Red, 250f, 0.42f, out fireFace);
            UIFactory.Place(fire, new Vector2(1f, 0f), new Vector2(-40f - 125f, 70f + 125f), new Vector2(250f, 250f));
            fire.gameObject.AddComponent<TouchPad>().Pressed = () => session.Fire();
            fireRing = UIFactory.AddImage(fire, "Cycle", Sprites.Ring(0.12f), new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Place(fireRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(266f, 266f));
            fireRing.type = Image.Type.Filled;
            fireRing.fillMethod = Image.FillMethod.Radial360;
            fireRing.fillOrigin = (int)Image.Origin360.Top;
            fireRing.fillClockwise = true;

            ammoPips = new Image[Rifle.MagazineSize];
            for (int i = 0; i < ammoPips.Length; i++)
            {
                var pip = UIFactory.AddImage(SafeRoot, "Ammo", SniperArt.Icon(SniperIcon.Bullet), SniperArt.Coin);
                UIFactory.Place(pip.rectTransform, new Vector2(1f, 0f), new Vector2(-40f - 125f + (i - 2) * 40f, 360f), new Vector2(40f, 52f));
                ammoPips[i] = pip;
            }

            // Scope toggle.
            var scope = RoundButton(SafeRoot, "Scope", SniperArt.Icon(SniperIcon.Scope), Palette.Blue, 170f, 0.5f, out _);
            UIFactory.Place(scope, new Vector2(0.5f, 0f), new Vector2(40f, 70f + 85f), new Vector2(170f, 170f));
            scope.gameObject.AddComponent<TouchPad>().Pressed = () => session.SetScoped(!session.Scoped);
            scopeIconImage = scope.Find("Face/Icon").GetComponent<Image>();

            // Hold breath.
            var breath = RoundButton(SafeRoot, "Breath", SniperArt.Icon(SniperIcon.Breath), Palette.Neutral, 210f, 0.44f, out _);
            UIFactory.Place(breath, new Vector2(0f, 0f), new Vector2(40f + 105f, 70f + 105f), new Vector2(210f, 210f));
            var hold = breath.gameObject.AddComponent<TouchPad>();
            hold.Pressed = () => session.SetBreath(true);
            hold.Released = () => session.SetBreath(false);
            breathRing = UIFactory.AddImage(breath, "Meter", Sprites.Ring(0.12f), new Color(0.55f, 0.9f, 1f, 0.95f));
            UIFactory.Place(breathRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(226f, 226f));
            breathRing.type = Image.Type.Filled;
            breathRing.fillMethod = Image.FillMethod.Radial360;
            breathRing.fillOrigin = (int)Image.Origin360.Top;
            var breathLabel = UIFactory.AddLabel(breath, "Label", "HOLD", 26f, Palette.TextDim, FontWeight.ExtraBold);
            UIFactory.Place(breathLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(200f, 34f));
            breathRoot = breath.gameObject;

            // Zoom slider along the right edge.
            var zoom = UIFactory.AddRect("Zoom", SafeRoot);
            UIFactory.Place(zoom, new Vector2(1f, 0.5f), new Vector2(-40f - 45f, 150f), new Vector2(90f, 600f));
            var track = UIFactory.AddPanel(zoom, "Track", Palette.WithAlpha(Palette.Inset, 0.85f), 45f, true);
            UIFactory.Stretch(track.rectTransform);
            zoomTrack = track.rectTransform;
            var fill = UIFactory.AddPanel(zoom, "Rail", new Color(1f, 1f, 1f, 0.2f), 5f);
            UIFactory.Stretch(fill.rectTransform, 40f, 40f, 40f, 40f);
            var knob = UIFactory.AddImage(zoom, "Knob", Sprites.Circle, Color.white);
            zoomKnob = knob.rectTransform;
            UIFactory.Place(zoomKnob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));
            zoomText = UIFactory.AddLabel(zoomKnob, "Zoom", "", 30f, Palette.BackgroundBottom, FontWeight.ExtraBold);
            UIFactory.Stretch(zoomText.rectTransform);
            var plus = UIFactory.AddLabel(zoom, "More", "+", 44f, Palette.TextDim, FontWeight.ExtraBold);
            UIFactory.Place(plus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(80f, 50f));
            var minus = UIFactory.AddLabel(zoom, "Less", "-", 50f, Palette.TextDim, FontWeight.ExtraBold);
            UIFactory.Place(minus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -36f), new Vector2(80f, 50f));
            track.gameObject.AddComponent<DragPad>().Touched = SetZoomFromScreen;
            zoomRoot = zoom.gameObject;
        }

        void BuildCinema()
        {
            var root = UIFactory.Stretch(UIFactory.AddRect("Cinema", transform));
            cinemaGroup = root.gameObject.AddComponent<CanvasGroup>();
            cinemaGroup.blocksRaycasts = false;
            cinemaGroup.alpha = 0f;
            cinemaTop = UIFactory.AddImage(root, "Top", Sprites.White, Color.black);
            cinemaTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            cinemaTop.rectTransform.anchorMax = Vector2.one;
            cinemaTop.rectTransform.pivot = new Vector2(0.5f, 1f);
            cinemaTop.rectTransform.sizeDelta = new Vector2(0f, 230f);
            cinemaBottom = UIFactory.AddImage(root, "Bottom", Sprites.White, Color.black);
            cinemaBottom.rectTransform.anchorMin = Vector2.zero;
            cinemaBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            cinemaBottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            cinemaBottom.rectTransform.sizeDelta = new Vector2(0f, 230f);
            var label = UIFactory.AddLabel(cinemaBottom.rectTransform, "Label", "BULLET CAM", 44f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(label.rectTransform);
            var dot = UIFactory.AddImage(cinemaBottom.rectTransform, "Rec", Sprites.Circle, SniperArt.Hostile);
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-170f, 0f), new Vector2(26f, 26f));
            dot.gameObject.AddComponent<Pulse>().Amplitude = 0.3f;
        }

        /// <summary>A round 3D button face with an icon; touches are handled by the caller.</summary>
        static RectTransform RoundButton(Transform parent, string name, Sprite icon, Color colour, float size, float iconScale, out Image face)
        {
            var root = UIFactory.AddRect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            var hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Sprites.Circle;
            hit.color = new Color(0f, 0f, 0f, 0f);
            float depth = Mathf.Round(size * 0.06f);
            var edge = UIFactory.AddImage(root, "Edge", Sprites.Circle, Palette.Darken(colour, 0.35f));
            UIFactory.Stretch(edge.rectTransform, 0f, depth, 0f, 0f);
            face = UIFactory.AddImage(root, "Face", Sprites.Circle, colour);
            UIFactory.Stretch(face.rectTransform, 0f, 0f, 0f, depth);
            var image = UIFactory.AddImage(face.rectTransform, "Icon", icon, Color.white);
            UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * iconScale);
            root.gameObject.AddComponent<PressFeedback>().Configure(face.rectTransform, depth);
            return root;
        }

        // ------------------------------------------------------------ frame

        void Update()
        {
            if (session == null) return;
            var size = Rect.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOut) Layout(size);

            float dt = Time.unscaledDeltaTime;
            bool scoped = session.Scoped && !session.BulletCamActive;
            scopeGroup.alpha = Mathf.MoveTowards(scopeGroup.alpha, scoped ? 1f : 0f, dt * 9f);
            hipGroup.alpha = session.BulletCamActive || session.Finished ? 0f : scoped ? 0f : 1f;
            hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, session.BulletCamActive ? 0f : 1f, dt * 6f);
            hudGroup.blocksRaycasts = !session.BulletCamActive;
            cinemaGroup.alpha = Mathf.MoveTowards(cinemaGroup.alpha, session.BulletCamActive ? 1f : 0f, dt * 5f);

            UpdateHud();
            UpdateScope(size);

            if (hitMarkerTime > 0f)
            {
                hitMarkerTime -= dt;
                hitMarker.gameObject.SetActive(hitMarkerTime > 0f);
            }
        }

        void Layout(Vector2 size)
        {
            laidOut = size;
            float lens = Mathf.Min(size.x, size.y);
            scopeFrame.sizeDelta = new Vector2(lens, lens);
            float bar = Mathf.Max(0f, (size.y - lens) * 0.5f) + 4f;
            UIFactory.Place(scopeTop.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size.x + 8f, bar), new Vector2(0.5f, 1f));
            UIFactory.Place(scopeBottom.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size.x + 8f, bar), new Vector2(0.5f, 0f));
            float side = Mathf.Max(0f, (size.x - lens) * 0.5f) + 4f;
            // Landscape tablets: fill the sides instead.
            if (size.x > lens + 1f)
            {
                UIFactory.Place(scopeTop.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(side, size.y + 8f), new Vector2(0f, 0.5f));
                UIFactory.Place(scopeBottom.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(side, size.y + 8f), new Vector2(1f, 0.5f));
            }
            reticle.localScale = Vector3.one * (lens / 1080f);
        }

        void UpdateHud()
        {
            int total = session.RequiredTotal;
            objectiveText.text = $"{session.RequiredDown}/{total}";
            objectiveLabel.text = mission.Goal == MissionGoal.EliminateBoss ? "BOSS" : "TARGETS";

            float left = session.TimeLeft;
            int seconds = Mathf.CeilToInt(left);
            timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            bool urgent = session.Running && left < 15f;
            timerText.color = urgent ? Color.Lerp(Palette.Red, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.4f) : Color.white;

            windText.text = session.Wind.magnitude < 0.05f ? "0" : session.Wind.magnitude.ToString("0.0");
            windArrow.enabled = session.Wind.magnitude >= 0.05f;
            if (windArrow.enabled)
            {
                // The wind relative to where the player is looking: right on screen means pushing shots right.
                var local = Quaternion.Euler(0f, -session.Camera.transform.eulerAngles.y, 0f) * session.Wind;
                float angle = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
                windArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle + 180f);
            }

            for (int i = 0; i < ammoPips.Length; i++)
            {
                bool loaded = i < session.Ammo;
                ammoPips[i].color = loaded ? SniperArt.Coin : new Color(1f, 1f, 1f, 0.18f);
            }
            float cycle = session.CycleProgress;
            fireRing.enabled = cycle < 1f;
            fireRing.fillAmount = cycle;
            fireFace.color = session.CanFire || !session.Running ? Palette.Red : Palette.Darken(Palette.Red, 0.45f);

            breathRoot.SetActive(session.Scoped && !session.Finished);
            breathRing.fillAmount = session.Breath;
            breathRing.color = session.Gasping ? Palette.Red : session.HoldingBreath ? Palette.Green : new Color(0.55f, 0.9f, 1f, 0.95f);
            zoomRoot.SetActive(session.Scoped && !session.Finished);
            if (zoomRoot.activeSelf)
            {
                float travel = zoomTrack.rect.height * 0.5f - 44f;
                zoomKnob.anchoredPosition = new Vector2(0f, Mathf.Lerp(-travel, travel, session.Zoom01));
                zoomText.text = session.ZoomTarget.ToString("0.#") + "x";
            }
            scopeIconImage.sprite = session.Scoped ? Icons.Get(IconKind.Close) : SniperArt.Icon(SniperIcon.Scope);

            if (session.Reloading) statusText.text = "RELOADING";
            else if (session.Gasping) statusText.text = "CATCHING BREATH";
            else statusText.text = "";
        }

        void UpdateScope(Vector2 size)
        {
            if (scopeGroup.alpha <= 0f) return;
            float fov = session.Camera.fieldOfView;
            float unitsPerDegree = size.y / fov / reticle.localScale.y;
            // At low zoom the marks crowd the centre: show one only once it has room for its label.
            float previous = 0f;
            for (int i = 0; i < rangeTicks.Length; i++)
            {
                float y = -Ballistics.HoldoverDegrees(RangeMarks[i], Ballistics.Rifle) * unitsPerDegree;
                rangeTicks[i].anchoredPosition = new Vector2(0f, y);
                bool show = y < -40f && y > -470f && previous - y >= 38f;
                rangeTicks[i].gameObject.SetActive(show);
                if (show) previous = y;
            }

            float range = session.AimDistance;
            rangeText.text = range > 0f ? Mathf.RoundToInt(range) + " m" : "-- m";
            var robot = session.AimRobot;
            if (robot != null)
            {
                idText.text = robot.Role == RobotRole.Civilian ? "CIVILIAN" : robot.Role == RobotRole.Boss ? "BOSS" : "HOSTILE";
                idText.color = robot.Role == RobotRole.Civilian ? SniperArt.Civilian : robot.Role == RobotRole.Boss ? SniperArt.Boss : SniperArt.Hostile;
            }
            else
            {
                idText.text = "";
            }

            previewDot.gameObject.SetActive(session.HasImpactPreview);
            if (session.HasImpactPreview)
            {
                var screen = session.Camera.WorldToScreenPoint(session.ImpactPreview);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out var local))
                    previewDot.anchoredPosition = local;
                else
                    previewDot.gameObject.SetActive(false);
            }
        }

        void SetZoomFromScreen(Vector2 screenPosition)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(zoomTrack, screenPosition, null, out var local)) return;
            float travel = zoomTrack.rect.height * 0.5f - 44f;
            session.SetZoom01(Mathf.InverseLerp(-travel, travel, local.y));
        }

        // ------------------------------------------------------------ events

        void OnShotLanded(ShotReport report)
        {
            hitMarkerTime = 0.28f;
            hitMarker.gameObject.SetActive(true);
            var markerColour = report.Civilian ? SniperArt.Civilian : report.Kill || report.BarrelKills > 0 ? SniperArt.Hostile : Color.white;
            foreach (Transform bar in hitMarker) bar.GetComponent<Image>().color = markerColour;
            if (report.Kill || report.BarrelKills > 0) Haptics.Play(HapticKind.Success);

            if (session.BulletCamActive) return;
            float y = 560f;
            if (report.Civilian) UI.Toast("Civilian hit!", Palette.Red, 1.2f, y);
            else if (report.Barrel && report.BarrelKills > 0) UI.Toast(report.BarrelKills > 1 ? $"Barrel blast x{report.BarrelKills}!" : "Barrel blast!", Palette.Orange, 1.1f, y);
            else if (report.Kill && report.Boss) UI.Toast("Boss down!", Palette.Gold, 1.1f, y);
            else if (report.Kill && report.Headshot) UI.Toast($"Headshot!  {Mathf.RoundToInt(report.Distance)} m", Palette.Gold, 1f, y);
            else if (report.Kill) UI.Toast($"Target down  {Mathf.RoundToInt(report.Distance)} m", Palette.Green, 1f, y);
            else if (report.RobotHit) UI.Toast("Hit! Finish it", Palette.Orange, 0.9f, y);
        }

        void OnAlerted()
        {
            if (!session.Finished) UI.Toast("They're running!", Palette.Orange, 1.2f, 700f);
        }

        void OnBulletCam(bool active)
        {
            if (active) App.Instance.Audio.Play(Sfx.ScopeIn, 0.8f, 0.6f);
        }

        void OnEnded(MissionStats stats)
        {
            if (resultShown) return;
            resultShown = true;
            session.SetScoped(false);
            var progress = SniperPrefs.Progress;
            int coins = progress.RecordResult(mission, stats);
            SniperPrefs.Save();
            UI.PushOverlay<SniperResultOverlay>(o =>
            {
                o.Mission = mission;
                o.Stats = stats;
                o.Coins = coins;
                o.Next = () => StartMission(Mathf.Min(mission.Index + 1, SniperMissions.Count - 1));
                o.Retry = () => StartMission(mission.Index);
                o.Menu = LeaveToMenu;
            });
        }

        void OnAppPause(bool paused)
        {
            if (paused && !Ads.ShowingAd && session != null && session.Running && !session.Finished && !session.Paused) OpenPause();
        }

        void OpenPause()
        {
            if (session == null || session.Finished || session.BulletCamActive || session.Paused) return;
            session.SetPaused(true);
            UI.PushOverlay<SniperPauseOverlay>(o =>
            {
                o.Resume = () => session.SetPaused(false);
                o.Restart = () => StartMission(mission.Index);
                o.Quit = LeaveToMenu;
            });
        }

        public override bool HandleBack()
        {
            if (session != null && session.Running && !session.Finished) OpenPause();
            else LeaveToMenu();
            return true;
        }

        void StartMission(int index)
        {
            DisposeSession();
            UI.Show<SniperGameScreen>(s => s.MissionIndex = index);
        }

        void LeaveToMenu()
        {
            DisposeSession();
            UI.Show<SniperMenuScreen>();
        }

        void DisposeSession()
        {
            if (session == null) return;
            session.Quit();
            session.ShotLanded -= OnShotLanded;
            session.Ended -= OnEnded;
            session.TargetsAlerted -= OnAlerted;
            session.BulletCamChanged -= OnBulletCam;
            Destroy(session.gameObject);
            session = null;
            Time.timeScale = 1f;
            UI.SetBackgroundVisible(true);
        }

        protected override void OnClosed()
        {
            App.PauseChanged -= OnAppPause;
            DisposeSession();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
    }

    /// <summary>Reports drags (for looking around) and touches (for sliders) on a UI element.</summary>
    public sealed class DragPad : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler
    {
        public Action<Vector2> Dragged;
        public Action<Vector2> Touched;

        public void OnPointerDown(PointerEventData eventData) => Touched?.Invoke(eventData.position);

        // Aiming needs every pixel: the usual drag threshold would swallow small corrections.
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData.delta);
            Touched?.Invoke(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData.delta);
            Touched?.Invoke(eventData.position);
        }
    }

    /// <summary>Fires on touch-down (no waiting for release) and reports release, for fire and hold buttons.</summary>
    public sealed class TouchPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action Pressed;
        public Action Released;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
    }
}
