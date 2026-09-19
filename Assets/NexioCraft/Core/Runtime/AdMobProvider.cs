using System;
#if NX_ADS_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif

namespace NexioCraft.Core
{
    /// <summary>
    /// Google AdMob implementation, compiled only when the Google Mobile Ads Unity plugin is installed and
    /// the NX_ADS_ADMOB scripting define is set. Without it the game uses <see cref="SimulatedAdProvider"/>.
    ///
    /// Setup:
    ///   1. Install the plugin (Google Mobile Ads Unity plugin .unitypackage).
    ///   2. Assets > Google Mobile Ads > Settings: paste the Android and iOS App IDs from AdMob.
    ///   3. Put the ad unit ids in AdSettings, and add NX_ADS_ADMOB to the scripting define symbols.
    /// </summary>
    public sealed class AdMobProvider : IAdProvider
    {
#if NX_ADS_ADMOB
        InterstitialAd interstitial;
        RewardedAd rewarded;
        BannerView banner;
        bool initialized;

        // The SDK may call back on a background thread, and every callback here ends up touching
        // Unity objects (overlays, game state), so each one is queued onto the main thread.
        // (MobileAds.RaiseAdEventsOnUnityMainThread did this globally but is obsolete since v10.)
        static void OnMainThread(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);

        public void Initialize()
        {
            MobileAds.Initialize(_ => OnMainThread(() =>
            {
                initialized = true;
                Load(AdKind.Interstitial);
                Load(AdKind.Rewarded);
            }));
        }

        public bool IsReady(AdKind kind) => kind == AdKind.Interstitial
            ? interstitial != null && interstitial.CanShowAd()
            : rewarded != null && rewarded.CanShowAd();

        public void Load(AdKind kind)
        {
            if (!initialized) return;
            if (kind == AdKind.Interstitial)
            {
                interstitial?.Destroy();
                interstitial = null;
                InterstitialAd.Load(AdSettings.Interstitial, new AdRequest(), (ad, error) => OnMainThread(() =>
                {
                    if (error != null || ad == null) return;
                    interstitial = ad;
                }));
                return;
            }
            rewarded?.Destroy();
            rewarded = null;
            RewardedAd.Load(AdSettings.Rewarded, new AdRequest(), (ad, error) => OnMainThread(() =>
            {
                if (error != null || ad == null) return;
                rewarded = ad;
            }));
        }

        public void Show(AdKind kind, Action<bool> finished)
        {
            if (kind == AdKind.Interstitial)
            {
                if (interstitial == null || !interstitial.CanShowAd())
                {
                    finished?.Invoke(false);
                    return;
                }
                bool reported = false;
                interstitial.OnAdFullScreenContentClosed += () => OnMainThread(() =>
                {
                    if (reported) return;
                    reported = true;
                    finished?.Invoke(true);
                });
                interstitial.OnAdFullScreenContentFailed += _ => OnMainThread(() =>
                {
                    if (reported) return;
                    reported = true;
                    finished?.Invoke(false);
                });
                interstitial.Show();
                return;
            }

            if (rewarded == null || !rewarded.CanShowAd())
            {
                finished?.Invoke(false);
                return;
            }
            bool earned = false, done = false;
            // The reward arrives before the ad is dismissed, and both go through the same queue,
            // so `earned` is already set when the closed handler runs.
            rewarded.OnAdFullScreenContentClosed += () => OnMainThread(() =>
            {
                if (done) return;
                done = true;
                finished?.Invoke(earned);
            });
            rewarded.OnAdFullScreenContentFailed += _ => OnMainThread(() =>
            {
                if (done) return;
                done = true;
                finished?.Invoke(false);
            });
            rewarded.Show(_ => OnMainThread(() => earned = true));
        }

        public void ShowBanner(bool visible)
        {
            if (!visible)
            {
                banner?.Destroy();
                banner = null;
                return;
            }
            if (banner != null) return;
            banner = new BannerView(AdSettings.Banner, AdSize.Banner, AdPosition.Bottom);
            banner.LoadAd(new AdRequest());
        }
#else
        public void Initialize() { }
        public bool IsReady(AdKind kind) => false;
        public void Load(AdKind kind) { }
        public void Show(AdKind kind, Action<bool> finished) => finished?.Invoke(false);
        public void ShowBanner(bool visible) { }
#endif
    }
}
