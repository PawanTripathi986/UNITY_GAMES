using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Writes the AdMob App IDs into the Google Mobile Ads plugin settings.
    ///
    /// The plugin normally wants this typed into Assets &gt; Google Mobile Ads &gt; Settings, and its
    /// settings class is internal, so this goes through reflection and can run in batch mode:
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod NexioCraft.EditorTools.AdMobSetup.CommandLine \
    ///         -android ca-app-pub-XXXX~YYYY -ios ca-app-pub-XXXX~ZZZZ
    ///
    /// With no arguments it writes Google's sample App IDs, which serve test ads only.
    /// </summary>
    public static class AdMobSetup
    {
        // Google's documented sample App IDs. They only ever serve test ads.
        public const string SampleAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string SampleIosAppId = "ca-app-pub-3940256099942544~1458002511";

        public static void CommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            Apply(Arg(args, "-android") ?? SampleAndroidAppId, Arg(args, "-ios") ?? SampleIosAppId);
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static void Apply(string androidAppId, string iosAppId)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "GoogleMobileAds.Editor.GoogleMobileAdsSettings");
            if (type == null)
                throw new Exception("Google Mobile Ads plugin not found. Import the plugin first.");

            var load = type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (load == null) throw new Exception("GoogleMobileAdsSettings.LoadInstance is missing (plugin version changed?).");
            var settings = (ScriptableObject)load.Invoke(null, null);

            Set(type, settings, "GoogleMobileAdsAndroidAppId", androidAppId);
            Set(type, settings, "GoogleMobileAdsIOSAppId", iosAppId);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            bool sample = androidAppId == SampleAndroidAppId || iosAppId == SampleIosAppId;
            Debug.Log($"NexioCraft AdMob settings written: android={androidAppId} ios={iosAppId}" +
                      (sample ? " (SAMPLE ids - test ads only, replace before publishing)" : ""));
        }

        static void Set(Type type, object target, string property, string value)
        {
            var info = type.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new Exception($"GoogleMobileAdsSettings.{property} is missing (plugin version changed?).");
            info.SetValue(target, value);
        }

        static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }
    }
}
