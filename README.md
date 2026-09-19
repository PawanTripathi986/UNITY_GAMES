# NexioCraft Games

Unity **6.3 LTS (6000.3.24f1)** project for **Android and iOS**. It holds several casual games that share `NexioCraft/Core`:

| Game | Status | App name / bundle id |
|------|--------|----------------------|
| Ludo | done, tested on the iPhone simulator and an Android emulator | Ludo / `com.nexiocraft.ludo` |
| Chess | done, tested on an Android emulator | Chess / `com.nexiocraft.chess` |
| Brain Games (5 mini-games) | done, tested on an Android emulator | Brain Games / `com.nexiocraft.brain` |
| Block Puzzle | done, tested on an Android emulator | Block Puzzle / `com.nexiocraft.blockpuzzle` |
| Steel Sniper (3D) | done, tested on an Android emulator (aim, scope, bullet cam, results, upgrades) | Steel Sniper / `com.nexiocraft.sniper` |
| Color Sort | done, logic and solver tests pass | Color Sort / `com.nexiocraft.colorsort` |

Each game builds as its **own app**. A build without a game flag (`-game all`) produces one "Game Night" app with a game picker. That is also what you get when you press Play in the editor.

## Ludo

- **Play vs Computer**: 2, 3 or 4 players. Pick your colour and an Easy, Normal or Hard computer.
- **Pass & Play**: 2 to 4 players on one phone. Each seat can be a human or a computer.
- **Classic rules**:
  - A 6 brings a token out.
  - Bonus rolls for a 6, a capture or reaching home.
  - Three 6s in a row lose the turn.
  - Start and star squares are safe.
  - Reaching home takes an exact roll.
  - Players are ranked as they finish.
- **House-rule switches** (Settings): each rule above can be turned off, plus fast mode and auto-move.
- **Auto-save**: the menu offers *Continue Game*. A roll that was already made is kept, so pausing can't be used to re-roll.

## Chess

- **Play vs Computer**: play as White, Black or Random against Easy, Medium or Hard. Hard searches up to 12 moves ahead within about 1.4 s on a background thread.
- **Pass & Play** for two players on one phone.
- **Full rules**: castling, en passant, promotion (you choose the piece), check, checkmate, stalemate, threefold repetition, the fifty-move rule, and insufficient material.
- **Moving pieces**: tap or drag. Legal-move dots, last-move highlight and a check glow.
- **Buttons**: Undo, Hint (best move highlighted), Flip board, and Resign (in Pause).
- **Other**: three board colour themes, auto-save and resume, and win/loss/draw stats against the computer.

## Brain Games

Five quick games, each with its own record:

- **Memory Match** — find every pair on a 3x4, 4x4 or 4x5 grid. Records fewest moves.
- **2048** — swipe to slide and merge. Undo, best score, and the board is kept when you leave.
- **Quick Maths** — 60 seconds of sums. Streaks add bonus points, wrong answers cost 3 seconds.
- **Sequence** — repeat a growing pattern of colours and tones. Records the best round.
- **Sliding Puzzle** — 3x3, 4x4 or 5x5. Shuffled with legal moves only, so it is always solvable.

## Block Puzzle

Drag one of three pieces onto an 8x8 board; a full row or column clears. Clearing several lines at once is
worth far more (100, 400, 900 points). The game ends when none of the pieces left in the tray fit anywhere.
The board, tray and score are saved, so you can leave mid-game.

## Steel Sniper

A 3D sniper game in the style of Sniper 3D, built entirely from code (low-poly meshes, custom shaders). You are
on a rooftop; rogue robots are in the city below. Targets are robots with no blood or gore, which keeps
Game Night family-friendly for its store age rating.

- **Aiming**: drag to look, tap the scope, slide to zoom (2x up to 12x). The scope sways; hold the breath button
  to steady it. Hold your breath too long and you gasp, and the rifle shakes.
- **Real bullets**: travel time, drop and wind. The rifle is zeroed at 100 m. Red marks under the reticle show
  where to aim for 150/200/250/300 m, and a rangefinder shows the distance to what's under the crosshair. A
  running robot moves while the bullet flies, so you have to lead it.
- **Robots**: hostiles (red visor, blaster), a gold boss with a crown and cape, and civilians (cyan visor) you must
  not hit. They idle, patrol, and run for an exit when a shot lands near them or a friend goes down. Head, body
  and limbs take different damage. When destroyed they come apart under physics. Explosive barrels chain-react.
- **Bullet cam**: the shot that finishes a mission follows the bullet in slow motion and circles the target.
- **Campaign**: 30 missions in three districts:
  - Downtown in daylight
  - Harbour at sunset, with cranes, containers and a ship
  - Neon City at night, with lit windows and neon signs

  Each district adds longer range, more wind and more civilians, plus boss missions. Stars: finish / no misses / all headshots.
- **Rifle upgrades** bought with coins: Power, Stability, Scope zoom, Bolt speed and a Ballistic Computer that shows
  where the bullet will land.
- **Ads**: interstitial (paced) on the results buttons, a rewarded video to double a mission's coins, and rewarded
  free coins on the upgrade screen (5 minute cooldown).

Code: `Sniper/Runtime/` holds `Logic/` (ballistics, missions, rifle, progress), `World/` (mesh builder, city
generator, themes), `Actors/` (robots, effects, barrels), `Gameplay/SniperSession` (the rifle and the rules),
`View/` and `Screens/`. Shaders are in `Sniper/Resources/SniperShaders` (lighting and fog come from shader globals,
so there are no keywords to strip).

## Color Sort

Tap a tube to lift the colours on top, tap another tube to pour. A colour may only go onto the same colour or
into an empty tube, and a tube is finished when it holds four of one colour.

- **Endless levels**: three colours to start, one more every three levels up to eleven, always with two spare tubes.
- **Never a dead end**: each level is generated and then run through the solver before it is handed out. If a
  random fill cannot be solved the generator unwinds a finished board instead, which cannot produce an
  impossible puzzle.
- **Helpers, which are also the ad placements**: three free undos per level then a rewarded video for three more,
  one free hint (the solver's next move) then a rewarded video, and up to two extra tubes for a rewarded video.
  An interstitial (paced) plays on the level-complete buttons.
- The board, level and helper counts are saved, so you can leave mid-level.

## How it is built

Everything is created from code, so there are no scenes, prefabs or art files to wire up:

- `NexioCraft.Core.App` boots at startup (`RuntimeInitializeOnLoadMethod`). Each game registers itself with `App.RegisterGame`.
- Boards, pieces, tokens, dice, icons and app icons are drawn by a small signed-distance rasteriser (`Core/Runtime/Raster.cs`).
- Sound effects are synthesized at startup. Haptics use UIFeedbackGenerator on iOS and VibrationEffect on Android.
- Layout is portrait and safe-area aware, at 60 fps. The UI is uGUI with the Baloo 2 font (SIL OFL, see `Assets/Resources/Fonts/FONT-LICENSE.txt`).

```
Assets/NexioCraft/
  Core/Runtime/        App + game registry, game picker, UI kit, tweens, raster art, audio, haptics, settings
  Ludo/Runtime/        Logic/ (rules + AI), View/, Screens/
  Chess/Runtime/       Logic/ (move generator, game, AI), View/, Screens/
  Brain/Runtime/       Logic/ (2048, sliding puzzle, maths, decks), View/, Screens/
  Editor/              BuildTools, LudoSimulation, ChessTests, BrainTests, ArtPreview
Assets/Plugins/iOS/    NativeHaptics.mm
```

Adding another game takes a folder like those and one registration:

```csharp
App.RegisterGame(new GameInfo { Id = "puzzle", Title = "Block Puzzle", Tagline = "...",
    Artwork = () => PuzzleArt.Preview(300), ShowMenu = ui => ui.Show<PuzzleMenuScreen>() });
```

The game picker builds itself from the registered games, so nothing else needs changing. Add a matching entry to `BuildTools.Targets` if it should also ship as its own app.

## Build

Use the **NexioCraft → Build** menu in the editor, or build from the command line. `-game` is `ludo`, `chess`, `brain`, `blocks`, or `all` for one app containing every game.

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity"

# Android APK -> Builds/Android/Chess.apk (add -aab for a Play Store bundle)
"$UNITY" -batchmode -nographics -projectPath . -buildTarget Android \
  -executeMethod NexioCraft.EditorTools.BuildTools.CommandLineAndroid -game chess -logFile -

# Android debug APK -> Builds/Android/GameNight-debug.apk (Development Build: debuggable, script debugging,
# full stack traces in logcat, "Development Build" mark in the corner). Not for the store.
"$UNITY" -batchmode -nographics -projectPath . -buildTarget Android \
  -executeMethod NexioCraft.EditorTools.BuildTools.CommandLineAndroid -game all -development -logFile -

# iOS Xcode project -> Builds/Chess-iOS (add -simulator for Builds/Chess-iOS-Simulator)
# Needs CocoaPods (brew install cocoapods) for the Google Mobile Ads SDK, and a UTF-8 locale for pod.
export LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8
"$UNITY" -batchmode -nographics -projectPath . -buildTarget iOS \
  -executeMethod NexioCraft.EditorTools.BuildTools.CommandLineIOS -game chess -logFile -
```

The iOS export runs `pod install`, so **always open `Unity-iPhone.xcworkspace`, not the `.xcodeproj`** — the
project alone is missing the ads SDK and will not link. For a device build, open
`Builds/<Game>-iOS/Unity-iPhone.xcworkspace`, choose your signing team, and run.

`Assets/NexioCraft/Editor/PodfileCdnFix.cs` removes the `https://github.com/CocoaPods/Specs` source that
Google's dependency file adds. Without it `pod install` clones the whole CocoaPods Specs history (many GB)
instead of using the CDN, and the export appears to hang.

To install on an **Android emulator or phone** (Unity batch builds stop the adb server, so the first adb command restarts it):

```bash
adb install -r Builds/Android/Chess.apk
adb shell monkey -p com.nexiocraft.chess -c android.intent.category.LAUNCHER 1
```

To run on the **iPhone Simulator**, after a `-simulator` build:

```bash
xcodebuild -workspace Builds/Chess-iOS-Simulator/Unity-iPhone.xcworkspace -scheme Unity-iPhone -configuration Release \
  -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' \
  -derivedDataPath Builds/DerivedData-Chess-Sim CODE_SIGNING_ALLOWED=NO build
xcrun simctl install booted Builds/DerivedData-Chess-Sim/Build/Products/Release-iphonesimulator/Chess.app
xcrun simctl launch booted com.nexiocraft.chess
```

## Tests

```bash
# Ludo: 4,000 computer games with random house rules, invariants checked after every move
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.LudoSimulation.CommandLine -logFile -

# Chess: perft move counts on 5 standard positions, mate/draw detection, SAN, save/restore,
# hash consistency over 150 random games, and Hard vs Easy
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.ChessTests.CommandLine -logFile -

# Brain games: 2048 slide/merge cases and 200 random games, 900 puzzle shuffles checked for
# solvability, 6,000 maths questions, memory decks
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.BrainTests.CommandLine -logFile -

# Block puzzle: shape definitions, placement, row/column clears, scoring, 300 random games
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.BlockTests.CommandLine -logFile -

# Steel Sniper: ballistics against the exact trajectory, wind drift, 30 missions, rifle economy, save repair,
# stars; builds all three districts with real physics, checks every robot spot is visible and reachable, and fires
# real shots (holdover at ~250 m lands a headshot, no holdover drops into the body)
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.SniperTests.CommandLine -logFile -

# Steel Sniper look check without a device: renders views to Screenshots/sniper (needs a GPU, so no -nographics)
"$UNITY" -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.SniperPreview.CommandLine -logFile -

# Color Sort: pouring rules, undo over random play, and 45 generated levels solved by the solver with the
# solution replayed to make sure it really finishes
"$UNITY" -batchmode -nographics -projectPath . -executeMethod NexioCraft.EditorTools.SortTests.CommandLine -logFile -
```

## Ads

The **Google Mobile Ads Unity plugin v11.5.0 is installed** and builds use it (`NX_ADS_ADMOB`, set by
`BuildTools`). Ads still sit behind `IAdProvider` (`Assets/NexioCraft/Core/Runtime/Ads.cs`), so dropping
that define falls back to `SimulatedAdProvider`, a placeholder with the same timing and callbacks.

⚠ **The ad unit IDs are still Google's public test IDs** — they serve "Test Ad" creatives and earn
nothing. Replace them with your own before publishing (below).

**Where they appear**

| Placement | Where | Rule |
| --- | --- | --- |
| Interstitial | Play Again / Menu on the result screen of every game | never during play |
| Rewarded: one more hint | Chess | after 2 free hints per game |
| Rewarded: keep playing | Block Puzzle (clears the two fullest rows), 2048 (undo the last move) | once per game |
| Rewarded: +20 seconds | Quick Maths | once per round |
| Rewarded: retry the round | Sequence | once per game, from round 3 |

Pacing (`AdSettings`): no interstitial for the first **2** games, then at most one per **2** games and
never within **100 seconds** of the last one. Rewarded ads are always optional and never block a game.

**To switch to your own AdMob account**

1. Create the app in AdMob and copy the App ID plus the interstitial/rewarded ad unit ids.
2. Write the App IDs into the plugin settings:

   ```bash
   "$UNITY" -batchmode -quit -projectPath . -executeMethod NexioCraft.EditorTools.AdMobSetup.CommandLine \
            -android ca-app-pub-XXXX~YYYY -ios ca-app-pub-XXXX~ZZZZ -logFile -
   ```

3. Put the ad unit ids in `AdSettings` (`AndroidInterstitial`, `AndroidRewarded`, `IosInterstitial`,
   `IosRewarded`), then rebuild. Never click your own live ads — it gets the account suspended.
4. Add a consent flow (UMP — the library ships with the plugin) and, for iOS, App Tracking Transparency.

**Notes for anyone touching the ad code**

- `MobileAds.RaiseAdEventsOnUnityMainThread = true` is required: the callbacks open UI overlays.
- A full-screen ad pauses the Android activity exactly like the player leaving the app, so the games
  check `Ads.ShowingAd` before opening their own pause menu.
- The Android dependencies (`play-services-ads`, UMP) are resolved into `Assets/Plugins/Android/
  mainTemplate.gradle` by the External Dependency Manager. Re-run it after changing them:
  `-executeMethod NexioCraft.EditorTools.AdMobResolve.Run`.

`AdSettings.Enabled = false` ships a build with no ads at all; `Ads.Removed = true` is the hook for a
"remove ads" purchase (it stops interstitials and keeps the optional rewarded bonuses).

## Before publishing

- Set the final app names and bundle ids in `Assets/NexioCraft/Editor/BuildTools.cs` (`Targets`).
- Replace the test ad unit ids in `AdSettings` with your own (see **Ads**).
- **Android**: create a release keystore per app (Player Settings → Publishing Settings). Builds are debug-signed until then.
- **iOS**: set the Apple developer team in Xcode or in Player Settings.
- Ship the full SIL Open Font License text with the Baloo 2 font files.
