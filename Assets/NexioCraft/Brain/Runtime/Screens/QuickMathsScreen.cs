using System.Collections;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Sixty seconds of arithmetic. Right answers build a streak, wrong ones cost time.</summary>
    public sealed class QuickMathsScreen : BrainGameScreen
    {
        const float RoundSeconds = 60f;

        protected override string Title => "Quick Maths";

        Text questionLabel;
        Text streakLabel;
        Image questionCard;
        Image timerTrack;
        Image timerFill;
        readonly Button[] answers = new Button[4];
        readonly Text[] answerLabels = new Text[4];
        readonly Image[] answerPanels = new Image[4];
        MathQuestion question;
        float timeLeft;
        int score;
        int streak;
        int solved;
        int level;
        bool running;
        bool usedContinue;
        readonly System.Random rng = new System.Random();

        protected override void BuildGame()
        {
            questionCard = UIFactory.AddPanel(Content, "Question", Palette.Card, 44f);
            questionCard.rectTransform.anchorMin = new Vector2(0f, 1f);
            questionCard.rectTransform.anchorMax = new Vector2(1f, 1f);
            questionCard.rectTransform.pivot = new Vector2(0.5f, 1f);
            questionCard.rectTransform.sizeDelta = new Vector2(0f, 320f);
            questionCard.rectTransform.anchoredPosition = Vector2.zero;

            questionLabel = UIFactory.AddLabel(questionCard.rectTransform, "Question", "", 120f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(questionLabel.rectTransform, 20f, 30f, 20f, 100f);
            // Bottom-anchored so it stays under the question however tall the card grows.
            streakLabel = UIFactory.AddLabel(questionCard.rectTransform, "Streak", "", 40f, BrainArt.Accent, FontWeight.Bold);
            streakLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            streakLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            streakLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            streakLabel.rectTransform.sizeDelta = new Vector2(-40f, 56f);
            streakLabel.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            timerTrack = UIFactory.AddPanel(Content, "Timer", Palette.Inset, 18f);
            timerTrack.rectTransform.anchorMin = new Vector2(0f, 1f);
            timerTrack.rectTransform.anchorMax = new Vector2(1f, 1f);
            timerTrack.rectTransform.pivot = new Vector2(0.5f, 1f);
            timerTrack.rectTransform.sizeDelta = new Vector2(0f, 26f);
            timerTrack.rectTransform.anchoredPosition = new Vector2(0f, -344f);
            timerFill = UIFactory.AddPanel(timerTrack.rectTransform, "Fill", BrainArt.SymbolColor(1), 18f);
            timerFill.rectTransform.anchorMin = Vector2.zero;
            timerFill.rectTransform.anchorMax = Vector2.one;
            timerFill.rectTransform.offsetMin = Vector2.zero;
            timerFill.rectTransform.offsetMax = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var button = UIFactory.AddGameButton(Content, "0", Palette.CardRaised, new Vector2(400f, 180f), () => Answer(index), 74f);
                answers[i] = button;
                answerPanels[i] = button.transform.Find("Face").GetComponent<Image>();
                answerLabels[i] = button.transform.Find("Face/Content/Label").GetComponent<Text>();
            }
            NewGame();
        }

        protected override void LayoutGame(Vector2 size)
        {
            if (size.x <= 0f) return;
            const float gap = 20f;
            const float trackHeight = 26f;
            float width = (size.x - gap) * 0.5f;
            // The answers sit at the bottom, in thumb reach, and the question card takes the slack
            // above them so a tall screen has no empty band.
            float height = Mathf.Clamp(size.y * 0.16f, 150f, 300f);
            float first = size.y - (height * 2f + gap);
            float trackTop = first - 40f - trackHeight;
            questionCard.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(260f, trackTop - 24f));
            timerTrack.rectTransform.anchoredPosition = new Vector2(0f, -trackTop);
            for (int i = 0; i < 4; i++)
            {
                var rt = (RectTransform)answers[i].transform;
                float x = (i % 2 == 0 ? -1f : 1f) * (width + gap) * 0.5f;
                float y = -first - (i / 2) * (height + gap) - height * 0.5f;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }

        protected override void Restart() => NewGame();

        void NewGame()
        {
            StopAllCoroutines();
            score = 0;
            streak = 0;
            solved = 0;
            level = 0;
            timeLeft = RoundSeconds;
            running = true;
            usedContinue = false;
            NextQuestion();
            UpdateStats();
        }

        void NextQuestion()
        {
            question = MathQuestion.Create(rng, level);
            questionLabel.text = question.Text + " = ?";
            streakLabel.text = streak >= 2 ? $"Streak {streak}  (+{StreakBonus()} bonus)" : "";
            for (int i = 0; i < 4; i++)
            {
                answerLabels[i].text = question.Options[i].ToString();
                answerPanels[i].color = Palette.CardRaised;
            }
        }

        int StreakBonus() => Mathf.Min(streak, 8) * 2;

        void Answer(int index)
        {
            if (!running || question == null) return;
            bool correct = index == question.CorrectIndex;
            if (correct)
            {
                score += 10 + StreakBonus();
                streak++;
                solved++;
                level++;
                App.Instance.Audio.Play(Sfx.Pop, 0.8f, 1f + Mathf.Min(streak, 8) * 0.05f);
                Haptics.Play(HapticKind.Light);
                answerPanels[index].color = Palette.Green;
            }
            else
            {
                streak = 0;
                timeLeft = Mathf.Max(0f, timeLeft - 3f);
                App.Instance.Audio.Play(Sfx.NoMove, 0.7f);
                Haptics.Play(HapticKind.Warning);
                answerPanels[index].color = Palette.Red;
                answerPanels[question.CorrectIndex].color = Palette.Green;
            }
            UpdateStats();
            StartCoroutine(NextAfter(correct ? 0.12f : 0.5f));
        }

        IEnumerator NextAfter(float delay)
        {
            running = false;
            yield return Tween.Delay(delay);
            if (timeLeft <= 0f)
            {
                Finish();
                yield break;
            }
            running = true;
            NextQuestion();
        }

        void Finish()
        {
            running = false;
            // One optional top-up per round.
            if (!usedContinue && Ads.RewardedReady && score > 0)
            {
                usedContinue = true;
                Ads.OfferReward(UI, "Time!", $"You scored {score}. Watch a short video for 20 more seconds?", "Watch", earned =>
                {
                    if (!earned)
                    {
                        EndRound();
                        return;
                    }
                    timeLeft = 20f;
                    running = true;
                    NextQuestion();
                    UpdateStats();
                    UI.Toast("+20 seconds", Palette.Green, 1f);
                });
                return;
            }
            EndRound();
        }

        void EndRound()
        {
            bool record = BrainPrefs.SubmitHighScore("maths", score);
            UpdateStats();
            ShowResult("Time!", record
                ? $"New best score: {score} ({solved} correct)"
                : $"Score {score} with {solved} correct (best {BrainPrefs.BestScore("maths")})", record, NewGame);
        }

        void UpdateStats()
        {
            SetStat(0, "Score", score.ToString());
            SetStat(1, "Time", Mathf.CeilToInt(Mathf.Max(0f, timeLeft)) + "s");
            SetStat(2, "Best", BrainPrefs.BestScore("maths").ToString());
        }

        protected override void Update()
        {
            base.Update();
            if (!running) return;
            float previous = timeLeft;
            timeLeft -= Time.unscaledDeltaTime;
            timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(timeLeft / RoundSeconds), 1f);
            timerFill.color = timeLeft < 10f ? Palette.Red : BrainArt.SymbolColor(1);
            if (Mathf.CeilToInt(previous) != Mathf.CeilToInt(timeLeft)) UpdateStats();
            if (timeLeft <= 0f) Finish();
        }
    }
}
