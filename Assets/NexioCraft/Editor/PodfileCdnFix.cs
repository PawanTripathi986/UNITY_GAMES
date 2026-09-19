using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Google's AdMob dependency file lists https://github.com/CocoaPods/Specs as a pod source. With that
    /// line in the Podfile, `pod install` clones the entire CocoaPods Specs git history (several GB; it
    /// was still going after 1.3 GB) instead of using the CDN.
    ///
    /// The External Dependency Manager writes the Podfile at post-process order 40 and runs
    /// `pod install` at 50, so this runs in between and drops that source.
    /// </summary>
    public static class PodfileCdnFix
    {
        public const int CallbackOrder = 45;

        static readonly Regex GitSpecsSource =
            new Regex(@"^\s*source\s+['""]https://github\.com/CocoaPods/Specs(\.git)?/?['""]\s*$");

        [PostProcessBuild(CallbackOrder)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string podfile = Path.Combine(path, "Podfile");
            if (File.Exists(podfile) && Apply(podfile))
                Debug.Log("NexioCraft: Podfile pods now resolve from the CocoaPods CDN");
        }

        /// <summary>Rewrites the Podfile in place; returns true if it changed.</summary>
        public static bool Apply(string podfile)
        {
            var lines = File.ReadAllLines(podfile);
            var kept = lines.Where(line => !GitSpecsSource.IsMatch(line)).ToList();
            if (kept.Count == lines.Length) return false;
            if (!kept.Any(line => line.Contains("cdn.cocoapods.org")))
                kept.Insert(0, "source 'https://cdn.cocoapods.org/'");
            File.WriteAllLines(podfile, kept);
            return true;
        }
    }
}
