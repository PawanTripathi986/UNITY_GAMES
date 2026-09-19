using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NexioCraft.Core
{
    /// <summary>A game that can be launched from the app (registered by each game at startup).</summary>
    public sealed class GameInfo
    {
        public string Id;
        public string Title;
        public string Tagline;
        public int Order;
        public Color Accent = Palette.Blue;
        /// <summary>Artwork for the game picker card (created lazily).</summary>
        public Func<Texture> Artwork;
        public Action<UIRoot> ShowMenu;
        /// <summary>One line of progress for the home screen ("Level 7", "Best 3,450"). Optional.</summary>
        public Func<string> Status;
        /// <summary>Shows a "NEW" badge on the home screen.</summary>
        public bool IsNew;
    }

    /// <summary>
    /// Builds the app from code at startup (camera, input, UI root, audio), so the only scene needed is an
    /// empty one. Games register themselves with <see cref="RegisterGame"/>. A build defines NX_GAME_LUDO or
    /// NX_GAME_CHESS to become that single game; without a define every registered game is offered in a picker.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        static readonly List<GameInfo> games = new List<GameInfo>();

        public static App Instance { get; private set; }

        /// <summary>Raised with true when the app goes to the background, false when it returns.</summary>
        public static event Action<bool> PauseChanged;

        public UIRoot UI { get; private set; }
        public AudioService Audio { get; private set; }

        public static IReadOnlyList<GameInfo> Games => games;

        /// <summary>The game this build is locked to, or null when the build is a collection.</summary>
        public static string BuildGameId
        {
            get
            {
#if NX_GAME_LUDO
                return "ludo";
#elif NX_GAME_CHESS
                return "chess";
#elif NX_GAME_BRAIN
                return "brain";
#elif NX_GAME_BLOCKS
                return "blocks";
#elif NX_GAME_SNIPER
                return "sniper";
#elif NX_GAME_SORT
                return "sort";
#else
                return null;
#endif
            }
        }

        /// <summary>True when several games are offered from a home screen.</summary>
        public static bool IsCollection => BuildGameId == null && games.Count > 1;

        public static void RegisterGame(GameInfo info)
        {
            games.RemoveAll(g => g.Id == info.Id);
            games.Add(info);
            games.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        const string LastPlayedKey = "nx.lastGame";

        /// <summary>The game opened most recently, for the home screen's Continue card.</summary>
        public static GameInfo LastPlayed
        {
            get
            {
                string id = PlayerPrefs.GetString(LastPlayedKey, string.Empty);
                if (string.IsNullOrEmpty(id)) return null;
                foreach (var game in games)
                    if (game.Id == id) return game;
                return null;
            }
        }

        public static void NoteLastPlayed(string id)
        {
            PlayerPrefs.SetString(LastPlayedKey, id ?? string.Empty);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("App");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<App>();
            Instance.Initialize();
        }

        /// <summary>Returns to the game picker, or to the game's own menu in a single-game build.</summary>
        public void GoHome()
        {
            if (IsCollection)
            {
                UI.Show<GameHubScreen>();
                return;
            }
            GameInfo game = null;
            foreach (var g in games)
                if (BuildGameId == null || g.Id == BuildGameId) { game = g; break; }
            if (game != null) game.ShowMenu(UI);
            else Debug.LogError("No game registered for this build (" + (BuildGameId ?? "collection") + ").");
        }

        void Initialize()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Settings.Load();

            CreateCamera();
            CreateEventSystem();
            Audio = gameObject.AddComponent<AudioService>();
            UI = UIRoot.Create(transform);
#if NX_ADS_ADMOB
            Ads.Install(new AdMobProvider());
#else
            // No ad SDK in the project yet: the simulated provider runs the same flow with a placeholder screen.
            Ads.Install(new SimulatedAdProvider());
#endif
            GoHome();
        }

        void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.BackgroundBottom;
            cam.orthographic = true;
            cam.cullingMask = 0; // everything visible lives on the overlay canvas
            go.AddComponent<AudioListener>();
        }

        void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            // Created at runtime, so it has no actions asset yet: give it the standard UI actions.
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            var eventSystem = go.GetComponent<EventSystem>();
            if (Screen.dpi > 0f) eventSystem.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi * 0.06f));
        }

        void Update()
        {
            if (BackPressed()) UI.HandleBack();
        }

        void OnApplicationPause(bool paused) => PauseChanged?.Invoke(paused);

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
