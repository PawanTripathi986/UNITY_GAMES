using System;
using System.IO;
using System.Linq;
using NexioCraft.Chess;
using NexioCraft.Core;
using NexioCraft.Ludo;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Project setup and builds. Each game is its own app (name, bundle id, icon, NX_GAME_* define);
    /// "all" builds one app with a game picker.
    ///   Unity -batchmode -projectPath . -buildTarget Android -executeMethod NexioCraft.EditorTools.BuildTools.CommandLineAndroid -game chess [-aab] [-development] [-output path]
    ///   Unity -batchmode -projectPath . -buildTarget iOS -executeMethod NexioCraft.EditorTools.BuildTools.CommandLineIOS -game chess [-simulator] [-development] [-output path]
    /// -development makes a debug build: debuggable, script debugging and profiler, full stack traces in the log.
    /// </summary>
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string CompanyName = "NexioCraft";
        const string Version = "1.0.0";
        const int BuildNumber = 1;

        sealed class GameTarget
        {
            public string Id;
            public string ProductName;
            public string BundleId;
            public string Define;
            public string IconPath;
            public Func<Raster> Icon;
        }

        static readonly GameTarget[] Targets =
        {
            new GameTarget
            {
                Id = "ludo", ProductName = "Ludo", BundleId = "com.nexiocraft.ludo", Define = "NX_GAME_LUDO",
                IconPath = "Assets/NexioCraft/Ludo/Art/AppIcon.png", Icon = () => LudoArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "chess", ProductName = "Chess", BundleId = "com.nexiocraft.chess", Define = "NX_GAME_CHESS",
                IconPath = "Assets/NexioCraft/Chess/Art/AppIcon.png", Icon = () => ChessArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "brain", ProductName = "Brain Games", BundleId = "com.nexiocraft.brain", Define = "NX_GAME_BRAIN",
                IconPath = "Assets/NexioCraft/Brain/Art/AppIcon.png", Icon = () => Brain.BrainArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "blocks", ProductName = "Block Puzzle", BundleId = "com.nexiocraft.blockpuzzle", Define = "NX_GAME_BLOCKS",
                IconPath = "Assets/NexioCraft/Blocks/Art/AppIcon.png", Icon = () => Blocks.BlocksArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "sniper", ProductName = "Steel Sniper", BundleId = "com.nexiocraft.sniper", Define = "NX_GAME_SNIPER",
                IconPath = "Assets/NexioCraft/Sniper/Art/AppIcon.png", Icon = () => Sniper.SniperArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "sort", ProductName = "Color Sort", BundleId = "com.nexiocraft.colorsort", Define = "NX_GAME_SORT",
                IconPath = "Assets/NexioCraft/Sort/Art/AppIcon.png", Icon = () => Sort.SortArt.AppIcon(1024)
            },
            new GameTarget
            {
                Id = "all", ProductName = "Game Night", BundleId = "com.nexiocraft.games", Define = null,
                IconPath = "Assets/NexioCraft/Brain/Art/AppIcon.png", Icon = () => Brain.BrainArt.AppIcon(1024)
            }
        };

        [MenuItem("NexioCraft/Build/Ludo - Android APK")] public static void LudoApk() => BuildAndroid(Find("ludo"), false, null);
        [MenuItem("NexioCraft/Build/Ludo - iOS Xcode project")] public static void LudoIOS() => BuildIOS(Find("ludo"), false, null);
        [MenuItem("NexioCraft/Build/Chess - Android APK")] public static void ChessApk() => BuildAndroid(Find("chess"), false, null);
        [MenuItem("NexioCraft/Build/Chess - iOS Xcode project")] public static void ChessIOS() => BuildIOS(Find("chess"), false, null);
        [MenuItem("NexioCraft/Build/All games - Android APK")] public static void AllApk() => BuildAndroid(Find("all"), false, null);

        [MenuItem("NexioCraft/Build/All games - Android debug APK")]
        public static void AllDebugApk()
        {
            development = true;
            try { BuildAndroid(Find("all"), false, null); }
            finally { development = false; }
        }

        /// <summary>Debug build: debuggable app, script debugging and profiler, full stack traces in the log.</summary>
        static bool development;

        public static void CommandLineAndroid() => RunAndExit(() =>
        {
            development = HasArg("-development");
            BuildAndroid(Find(ArgValue("-game")), HasArg("-aab"), ArgValue("-output"));
        });

        public static void CommandLineIOS() => RunAndExit(() =>
        {
            development = HasArg("-development");
            BuildIOS(Find(ArgValue("-game")), HasArg("-simulator"), ArgValue("-output"));
        });

        static GameTarget Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return Targets[0];
            var target = Targets.FirstOrDefault(t => t.Id == id);
            if (target == null)
                throw new ArgumentException("Unknown -game '" + id + "'. Use " + string.Join(", ", Targets.Select(t => t.Id)) + ".");
            return target;
        }

        static void BuildAndroid(GameTarget game, bool appBundle, string output)
        {
            Setup(game);
            EditorUserBuildSettings.buildAppBundle = appBundle;
            output ??= $"Builds/Android/{game.ProductName.Replace(" ", "")}{(development ? "-debug" : "")}.{(appBundle ? "aab" : "apk")}";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            Build(game, BuildTarget.Android, BuildTargetGroup.Android, output);
        }

        static void BuildIOS(GameTarget game, bool simulator, string output)
        {
            Setup(game);
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (simulator) PreferArm64Simulator();
            output ??= $"Builds/{game.ProductName.Replace(" ", "")}-iOS{(simulator ? "-Simulator" : "")}";
            Build(game, BuildTarget.iOS, BuildTargetGroup.iOS, output);
        }

        static void Setup(GameTarget game)
        {
            EnsureScene();
            ConfigurePlayer(game);
            EnsureIcon(game);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Apple Silicon simulators run arm64; set it when this Unity version exposes the option.</summary>
        static void PreferArm64Simulator()
        {
            var property = typeof(PlayerSettings.iOS).GetProperty("simulatorSdkArchitecture",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (property == null || !property.PropertyType.IsEnum) return;
            string name = Enum.GetNames(property.PropertyType).FirstOrDefault(n => n.Equals("ARM64", StringComparison.OrdinalIgnoreCase));
            if (name != null) property.SetValue(null, Enum.Parse(property.PropertyType, name));
        }

        /// <summary>
        /// Compiles the real AdMob provider in. Drop this define to fall back to the simulated ads
        /// (the Google Mobile Ads plugin must be in the project for it to compile).
        /// </summary>
        const string AdsDefine = "NX_ADS_ADMOB";

        static void Build(GameTarget game, BuildTarget target, BuildTargetGroup group, string output)
        {
            var defines = game.Define != null ? new[] { game.Define, AdsDefine } : new[] { AdsDefine };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                targetGroup = group,
                // No ConnectWithProfiler: on a phone with no editor nearby it would only try to connect at startup.
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
                extraScriptingDefines = defines
            });
            var summary = report.summary;
            Debug.Log($"NexioCraft build {summary.result}{(development ? " (debug)" : "")}: {game.ProductName} -> {summary.outputPath} " +
                      $"({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0}s, {summary.totalErrors} errors)");
            if (summary.result != BuildResult.Succeeded) throw new BuildFailedException("Build failed: " + summary.result);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            // The app is built entirely from code (see NexioCraft.Core.App), so this scene stays empty.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void EnsureIcon(GameTarget game)
        {
            if (!File.Exists(game.IconPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(game.IconPath) ?? "Assets");
                var texture = game.Icon().ToTexture("AppIcon", true);
                File.WriteAllBytes(game.IconPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(game.IconPath, ImportAssetOptions.ForceUpdate);
            }

            if (AssetImporter.GetAtPath(game.IconPath) is TextureImporter importer &&
                (importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(game.IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, UnityEditor.IconKind.Any);
        }

        static void ConfigurePlayer(GameTarget game)
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = game.ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, game.BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, game.BundleId);
            PlayerSettings.Android.bundleVersionCode = BuildNumber;
            PlayerSettings.iOS.buildNumber = BuildNumber.ToString();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.runInBackground = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            // Every screen is built at runtime, so Unity's usage analysis cannot see which engine classes we
            // need; stripping them logs "Could not produce class with ID ..." errors.
            PlayerSettings.stripEngineCode = false;
            // Portrait-only apps must opt out of iPad multitasking.
            PlayerSettings.iOS.requiresFullScreen = true;
        }

        static void RunAndExit(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
