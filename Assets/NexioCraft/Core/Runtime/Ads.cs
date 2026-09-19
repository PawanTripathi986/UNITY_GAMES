using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    public enum AdKind { Interstitial, Rewarded }

    /// <summary>
    /// Ad unit ids and rules. Google's public test ids are the default, so ads work before a real AdMob
    /// account exists. Put the real ids here (or set them from your own config) before publishing.
    /// </summary>
    public static class AdSettings
    {
        /// <summary>Master switch. Turn off to ship a version with no ads at all.</summary>
        public static bool Enabled = true;

        // Google's official test ad units. Replace with your AdMob ids.
        public const string TestAndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string TestAndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string TestAndroidBanner = "ca-app-pub-3940256099942544/6300978111";
        public const string TestIosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string TestIosRewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string TestIosBanner = "ca-app-pub-3940256099942544/2934735716";

        public static string AndroidInterstitial = TestAndroidInterstitial;
        public static string AndroidRewarded = TestAndroidRewarded;
        public static string AndroidBanner = TestAndroidBanner;
        public static string IosInterstitial = TestIosInterstitial;
        public static string IosRewarded = TestIosRewarded;
        public static string IosBanner = TestIosBanner;

        public static bool UsingTestIds => AndroidInterstitial == TestAndroidInterstitial || IosInterstitial == TestIosInterstitial;

        public static string Interstitial => Application.platform == RuntimePlatform.IPhonePlayer ? IosInterstitial : AndroidInterstitial;
        public static string Rewarded => Application.platform == RuntimePlatform.IPhonePlayer ? IosRewarded : AndroidRewarded;
        public static string Banner => Application.platform == RuntimePlatform.IPhonePlayer ? IosBanner : AndroidBanner;

        // How often a full-screen ad may appear.
        public static int GamesBeforeFirstAd = 2;
        public static float SecondsBetweenInterstitials = 100f;
        public static int GamesBetweenInterstitials = 2;
    }

    /// <summary>What the game needs from an ad SDK. <see cref="SimulatedAdProvider"/> stands in until one is installed.</summary>
    public interface IAdProvider
    {
        void Initialize();
        bool IsReady(AdKind kind);
        void Load(AdKind kind);
        /// <summary>Shows the ad; the callback gets true when it played to the end (rewarded).</summary>
        void Show(AdKind kind, Action<bool> finished);
        void ShowBanner(bool visible);
    }

    /// <summary>
    /// Ad placements and pacing. Games call <see cref="ShowInterstitial"/> when a match ends and
    /// <see cref="OfferReward"/> to sell an optional bonus; both are safe no-ops when ads are off.
    /// </summary>
    public static class Ads
    {
        const string RemovedKey = "ads.removed";
        const string GamesKey = "ads.games";
        const string LastGameCountKey = "ads.lastGameCount";

        static IAdProvider provider;

        // Session-scoped: Time.realtimeSinceStartup restarts at 0 every launch, so this must not be
        // persisted or an ad shown late in one session would block ads early in the next one.
        static float lastInterstitial = float.NegativeInfinity;

        static bool adOnScreen;
        static float adClosedAt = float.NegativeInfinity;

        /// <summary>
        /// True while a full-screen ad covers the game, and for a moment after it closes. A full-screen
        /// ad pauses the Android activity exactly like the player leaving, so games check this before
        /// treating a pause as "the player switched away" and opening their own pause menu.
        /// </summary>
        public static bool ShowingAd => adOnScreen || Time.realtimeSinceStartup - adClosedAt < 0.5f;

        static void Present(AdKind kind, Action<bool> finished)
        {
            adOnScreen = true;
            provider.Show(kind, result =>
            {
                adOnScreen = false;
                adClosedAt = Time.realtimeSinceStartup;
                finished?.Invoke(result);
            });
        }

        /// <summary>Set when the player has bought "remove ads" (full-screen ads stop; rewarded stays).</summary>
        public static bool Removed
        {
            get => PlayerPrefs.GetInt(RemovedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(RemovedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool Enabled => AdSettings.Enabled && provider != null;
        public static bool RewardedReady => Enabled && provider.IsReady(AdKind.Rewarded);

        public static void Install(IAdProvider newProvider)
        {
            provider = newProvider;
            provider.Initialize();
            provider.Load(AdKind.Interstitial);
            provider.Load(AdKind.Rewarded);
        }

        /// <summary>Counts a finished game; pacing uses this so the first games are always ad-free.</summary>
        public static void GameFinished()
        {
            PlayerPrefs.SetInt(GamesKey, PlayerPrefs.GetInt(GamesKey, 0) + 1);
            PlayerPrefs.Save();
        }

        public static bool CanShowInterstitial()
        {
            if (!Enabled || Removed || !provider.IsReady(AdKind.Interstitial)) return false;
            int games = PlayerPrefs.GetInt(GamesKey, 0);
            if (games < AdSettings.GamesBeforeFirstAd) return false;
            if (games - PlayerPrefs.GetInt(LastGameCountKey, -99) < AdSettings.GamesBetweenInterstitials) return false;
            return Time.realtimeSinceStartup - lastInterstitial >= AdSettings.SecondsBetweenInterstitials;
        }

        /// <summary>Shows a full-screen ad if pacing allows, then calls <paramref name="done"/> either way.</summary>
        public static void ShowInterstitial(Action done)
        {
            if (!CanShowInterstitial())
            {
                done?.Invoke();
                return;
            }
            lastInterstitial = Time.realtimeSinceStartup;
            PlayerPrefs.SetInt(LastGameCountKey, PlayerPrefs.GetInt(GamesKey, 0));
            PlayerPrefs.Save();
            Present(AdKind.Interstitial, _ =>
            {
                provider.Load(AdKind.Interstitial);
                done?.Invoke();
            });
        }

        /// <summary>
        /// Asks whether the player wants to watch a video for a bonus, then plays it.
        /// <paramref name="done"/> gets true only when the video was watched to the end.
        /// </summary>
        public static void OfferReward(UIRoot ui, string title, string message, string confirmLabel, Action<bool> done)
        {
            if (!RewardedReady)
            {
                done?.Invoke(false);
                return;
            }
            ui.PushOverlay<ConfirmOverlay>(o =>
            {
                o.Title = title;
                o.Message = message;
                o.ConfirmLabel = confirmLabel;
                o.CancelLabel = "No thanks";
                o.ConfirmColor = Palette.Green;
                o.Confirmed = () => Present(AdKind.Rewarded, earned =>
                {
                    provider.Load(AdKind.Rewarded);
                    done?.Invoke(earned);
                });
                o.Cancelled = () => done?.Invoke(false);
            });
        }

        public static void ShowBanner(bool visible)
        {
            if (Enabled && !Removed) provider.ShowBanner(visible);
        }
    }

    /// <summary>
    /// Stand-in used until the Google Mobile Ads plugin is installed: shows a placeholder screen with a
    /// countdown so the whole flow (pacing, rewards, callbacks) can be played and tested.
    /// </summary>
    public sealed class SimulatedAdProvider : IAdProvider
    {
        public void Initialize() { }

        public bool IsReady(AdKind kind) => true;

        public void Load(AdKind kind) { }

        public void Show(AdKind kind, Action<bool> finished)
        {
            var ui = App.Instance != null ? App.Instance.UI : null;
            if (ui == null)
            {
                finished?.Invoke(false);
                return;
            }
            ui.PushOverlay<SimulatedAdOverlay>(o =>
            {
                o.Rewarded = kind == AdKind.Rewarded;
                o.Finished = finished;
            });
        }

        public void ShowBanner(bool visible) { }
    }

    /// <summary>Placeholder ad screen for the simulated provider.</summary>
    public sealed class SimulatedAdOverlay : UIOverlay
    {
        public bool Rewarded;
        public Action<bool> Finished;

        protected override bool CloseOnBackdropTap => false;

        Text countdown;
        Button skip;
        float remaining = 4f;
        bool completed;

        protected override void Build()
        {
            var card = BuildCard(900f, 1180f, Palette.Hex(0x101426));
            var tag = UIFactory.AddLabel(card, "Tag", "Advertisement (test)", 34f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 50f));

            var art = UIFactory.AddPanel(card, "Art", Palette.Hex(0x1B2340), 40f);
            UIFactory.Stretch(art.rectTransform, 50f, 110f, 50f, 260f);
            var title = UIFactory.AddLabel(art.rectTransform, "Title", Rewarded ? "Your reward is on the way" : "Your ad would play here", 56f, Color.white, FontWeight.ExtraBold);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Stretch(title.rectTransform, 40f, 120f, 40f, 300f);
            var note = UIFactory.AddLabel(art.rectTransform, "Note", "Google AdMob is not installed yet, so this stands in for a real video ad.", 38f, Palette.TextDim, FontWeight.Medium);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Stretch(note.rectTransform, 50f, 300f, 50f, 140f);

            countdown = UIFactory.AddLabel(card, "Countdown", "", 44f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(countdown.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(700f, 60f));

            skip = UIFactory.AddGameButton(card, Rewarded ? "Skip (no reward)" : "Close", Palette.Neutral, new Vector2(520f, 140f), Close, 48f, IconKind.Close);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(520f, 140f));
            skip.gameObject.SetActive(false);
            StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            while (remaining > 0f)
            {
                countdown.text = Rewarded ? $"Reward in {Mathf.CeilToInt(remaining)}s" : $"Closes in {Mathf.CeilToInt(remaining)}s";
                yield return null;
                remaining -= Time.unscaledDeltaTime;
                if (remaining <= 2.5f && !skip.gameObject.activeSelf) skip.gameObject.SetActive(true);
            }
            completed = true;
            countdown.text = Rewarded ? "Reward earned" : "";
            yield return Tween.Delay(0.4f);
            Close();
        }

        void Close()
        {
            UI.CloseOverlay(this);
        }

        protected override void OnClosed()
        {
            Finished?.Invoke(completed);
            Finished = null;
        }
    }
}
