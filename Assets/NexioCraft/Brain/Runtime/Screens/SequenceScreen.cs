using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Watch a growing pattern of pads light up, then repeat it.</summary>
    public sealed class SequenceScreen : BrainGameScreen
    {
        static readonly float[] Pitches = { 0.8f, 1f, 1.25f, 1.5f };

        protected override string Title => "Sequence";

        readonly List<int> sequence = new List<int>();
        readonly Image[] pads = new Image[4];
        readonly RectTransform[] padRects = new RectTransform[4];
        Text prompt;
        int inputIndex;
        bool acceptingInput;
        bool finished;
        bool usedContinue;
        readonly System.Random rng = new System.Random();

        protected override void BuildGame()
        {
            prompt = UIFactory.AddLabel(Content, "Prompt", "", 48f, Palette.TextDim, FontWeight.ExtraBold);
            prompt.rectTransform.anchorMin = new Vector2(0f, 1f);
            prompt.rectTransform.anchorMax = new Vector2(1f, 1f);
            prompt.rectTransform.pivot = new Vector2(0.5f, 1f);
            prompt.rectTransform.sizeDelta = new Vector2(0f, 80f);
            prompt.rectTransform.anchoredPosition = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var pad = UIFactory.AddPanel(Content, "Pad" + i, Dim(BrainArt.PadColors[i]), 40f, true);
                UIFactory.MakeButton(pad.gameObject, () => Tap(index));
                pads[i] = pad;
                padRects[i] = pad.rectTransform;
            }
            NewGame();
        }

        static Color Dim(Color color) => Palette.Darken(color, 0.45f);

        protected override void LayoutGame(Vector2 size)
        {
            if (size.x <= 0f) return;
            const float gap = 22f;
            const float promptSpace = 110f;
            float available = Mathf.Min(size.x, size.y - promptSpace);
            float cell = (available - gap) * 0.5f;
            // Centre the pads in the space under the prompt.
            float top = -promptSpace - (size.y - promptSpace) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (cell + gap) * 0.5f;
                float y = top + (i / 2 == 0 ? (cell + gap) * 0.5f : -(cell + gap) * 0.5f);
                padRects[i].anchorMin = padRects[i].anchorMax = new Vector2(0.5f, 1f);
                padRects[i].pivot = new Vector2(0.5f, 0.5f);
                padRects[i].sizeDelta = new Vector2(cell, cell);
                padRects[i].anchoredPosition = new Vector2(x, y);
            }
        }

        protected override void Restart() => NewGame();

        void NewGame()
        {
            StopAllCoroutines();
            sequence.Clear();
            finished = false;
            acceptingInput = false;
            usedContinue = false;
            UpdateStats();
            StartCoroutine(NextRound());
        }

        IEnumerator NextRound(bool addStep = true)
        {
            acceptingInput = false;
            inputIndex = 0;
            if (addStep) sequence.Add(rng.Next(4));
            UpdateStats();
            prompt.text = "Watch";
            yield return Tween.Delay(0.6f);
            foreach (int pad in sequence)
            {
                yield return Flash(pad, 0.42f);
                yield return Tween.Delay(0.16f);
            }
            prompt.text = "Your turn";
            acceptingInput = true;
        }

        IEnumerator Flash(int index, float duration)
        {
            var bright = BrainArt.PadColors[index];
            App.Instance.Audio.Play(Sfx.Pop, 0.7f, Pitches[index]);
            yield return Tween.Run(duration, t =>
            {
                float k = t < 0.35f ? t / 0.35f : 1f - (t - 0.35f) / 0.65f;
                pads[index].color = Color.Lerp(Dim(bright), bright, k);
                float scale = 1f + k * 0.06f;
                padRects[index].localScale = new Vector3(scale, scale, 1f);
            });
            pads[index].color = Dim(bright);
            padRects[index].localScale = Vector3.one;
        }

        void Tap(int index)
        {
            if (!acceptingInput || finished) return;
            StartCoroutine(Flash(index, 0.25f));
            Haptics.Play(HapticKind.Selection);
            if (sequence[inputIndex] == index)
            {
                inputIndex++;
                UpdateStats();
                if (inputIndex < sequence.Count) return;
                acceptingInput = false;
                prompt.text = "Correct!";
                StartCoroutine(RoundComplete());
                return;
            }

            acceptingInput = false;
            prompt.text = "Wrong pad";
            App.Instance.Audio.Play(Sfx.NoMove);
            Haptics.Play(HapticKind.Warning);

            // One optional retry per game, offered only once there is something to lose.
            if (!usedContinue && Ads.RewardedReady && sequence.Count > 2)
            {
                usedContinue = true;
                Ads.OfferReward(UI, "Wrong pad", $"You are on round {sequence.Count}. Watch a short video to try this round again?", "Watch", earned =>
                {
                    if (earned)
                    {
                        UI.Toast("Watch the pattern again", BrainArt.PadColors[1], 1f);
                        StartCoroutine(NextRound(false));
                        return;
                    }
                    EndGame();
                });
                return;
            }
            EndGame();
        }

        void EndGame()
        {
            finished = true;
            int level = sequence.Count - 1;
            bool record = BrainPrefs.SubmitHighScore("sequence", level);
            UpdateStats();
            ShowResult(level > 0 ? "Level " + level : "Game over", record
                ? $"New best: level {level}"
                : $"You reached level {level} (best {BrainPrefs.BestScore("sequence")})", record && level > 0, NewGame);
        }

        IEnumerator RoundComplete()
        {
            App.Instance.Audio.Play(Sfx.TokenHome, 0.5f);
            BrainPrefs.SubmitHighScore("sequence", sequence.Count);
            UpdateStats();
            yield return Tween.Delay(0.7f);
            yield return NextRound();
        }

        void UpdateStats()
        {
            SetStat(0, "Round", sequence.Count.ToString());
            SetStat(1, "Progress", $"{inputIndex}/{sequence.Count}");
            SetStat(2, "Best", BrainPrefs.BestScore("sequence").ToString());
        }
    }
}
