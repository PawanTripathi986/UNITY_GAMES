using System.Collections;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Flip two cards at a time and find every matching pair.</summary>
    public sealed class MemoryMatchScreen : BrainGameScreen
    {
        static readonly Vector2Int[] Grids = { new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(4, 5) };

        protected override string Title => "Memory Match";
        protected override float BottomInset => 150f;

        int variant;
        int[] cards;
        bool[] matched;
        bool[] faceUp;
        Image[] backs;
        Image[] fronts;
        Image[] symbols;
        RectTransform[] tiles;
        int first = -1;
        int moves;
        float elapsed;
        bool busy;
        bool finished;
        readonly System.Random rng = new System.Random();

        public static string VariantName(int variant) => Grids[Mathf.Clamp(variant, 0, 2)].x + "x" + Grids[Mathf.Clamp(variant, 0, 2)].y;

        protected override void BuildGame()
        {
            variant = Mathf.Clamp(BrainPrefs.GetChoice("memory", 1), 0, 2);
            var picker = SegmentedControl.Create(SafeRoot, new[] { VariantName(0), VariantName(1), VariantName(2) }, variant, BrainArt.Accent,
                new Vector2(600f, 104f), index =>
                {
                    variant = index;
                    BrainPrefs.SetChoice("memory", index);
                    NewGame();
                });
            UIFactory.Place((RectTransform)picker.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 104f));
            NewGame();
        }

        protected override void Restart() => NewGame();

        void NewGame()
        {
            StopAllCoroutines();
            busy = false;
            finished = false;
            first = -1;
            moves = 0;
            elapsed = 0f;
            var grid = Grids[variant];
            int count = grid.x * grid.y;
            cards = MemoryDeck.Create(count / 2, rng);
            matched = new bool[count];
            faceUp = new bool[count];

            for (int i = Content.childCount - 1; i >= 0; i--) Destroy(Content.GetChild(i).gameObject);
            tiles = new RectTransform[count];
            backs = new Image[count];
            fronts = new Image[count];
            symbols = new Image[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var tile = UIFactory.AddRect("Card" + i, Content);
                var hit = tile.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                UIFactory.MakeButton(hit.gameObject, () => Flip(index));
                fronts[i] = UIFactory.AddPanel(tile, "Front", Color.white, 26f);
                UIFactory.Stretch(fronts[i].rectTransform);
                symbols[i] = UIFactory.AddImage(fronts[i].rectTransform, "Symbol", BrainArt.Symbol(cards[i]), Color.white);
                symbols[i].preserveAspect = true;
                UIFactory.Stretch(symbols[i].rectTransform, 22f, 22f, 22f, 22f);
                backs[i] = UIFactory.AddImage(tile, "Back", BrainArt.CardBack(), Color.white);
                UIFactory.Stretch(backs[i].rectTransform);
                fronts[i].gameObject.SetActive(false);
                tiles[i] = tile;
            }
            LayoutGame(ContentSize);
            UpdateStats();
        }

        protected override void LayoutGame(Vector2 size)
        {
            if (tiles == null || size.x <= 0f) return;
            var grid = Grids[variant];
            const float gap = 14f;
            float cell = Mathf.Min((size.x - gap * (grid.x - 1)) / grid.x, (size.y - gap * (grid.y - 1)) / grid.y);
            float width = cell * grid.x + gap * (grid.x - 1);
            float height = cell * grid.y + gap * (grid.y - 1);
            for (int i = 0; i < tiles.Length; i++)
            {
                int column = i % grid.x, row = i / grid.x;
                float x = -width * 0.5f + cell * 0.5f + column * (cell + gap);
                float y = height * 0.5f - cell * 0.5f - row * (cell + gap);
                UIFactory.Place(tiles[i], new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(cell, cell));
            }
        }

        void Flip(int index)
        {
            if (busy || finished || matched[index] || faceUp[index]) return;
            StartCoroutine(FlipRoutine(index));
        }

        IEnumerator FlipRoutine(int index)
        {
            busy = true;
            yield return Reveal(index, true);
            App.Instance.Audio.Play(Sfx.Pop, 0.6f, 1.1f);
            Haptics.Play(HapticKind.Selection);

            if (first < 0)
            {
                first = index;
                busy = false;
                yield break;
            }

            moves++;
            UpdateStats();
            if (cards[first] == cards[index])
            {
                matched[first] = true;
                matched[index] = true;
                App.Instance.Audio.Play(Sfx.TokenHome, 0.5f);
                Haptics.Play(HapticKind.Light);
                Pop(first);
                Pop(index);
                first = -1;
                busy = false;
                CheckWin();
                yield break;
            }

            int second = index;
            yield return Tween.Delay(0.65f);
            yield return Reveal(first, false);
            yield return Reveal(second, false);
            first = -1;
            busy = false;
        }

        IEnumerator Reveal(int index, bool up)
        {
            var tile = tiles[index];
            yield return Tween.Run(0.11f, t => tile.localScale = new Vector3(1f - t, 1f, 1f));
            faceUp[index] = up;
            fronts[index].gameObject.SetActive(up);
            backs[index].gameObject.SetActive(!up);
            yield return Tween.Run(0.11f, t => tile.localScale = new Vector3(t, 1f, 1f));
            tile.localScale = Vector3.one;
        }

        void Pop(int index)
        {
            StartCoroutine(Tween.Run(0.3f, t =>
            {
                if (tiles[index] == null) return;
                float scale = 1f + Ease.Arc(t) * 0.14f;
                tiles[index].localScale = new Vector3(scale, scale, 1f);
            }, Ease.OutQuad));
        }

        void CheckWin()
        {
            foreach (bool done in matched)
                if (!done) return;
            finished = true;
            string variantName = VariantName(variant);
            bool best = BrainPrefs.SubmitLowScore("memory", moves, variantName);
            BrainPrefs.SubmitLowScore("memory.time", Mathf.RoundToInt(elapsed), variantName);
            UpdateStats();
            ShowResult("Solved!", best
                ? $"New record: {moves} moves in {Mathf.RoundToInt(elapsed)}s"
                : $"{moves} moves in {Mathf.RoundToInt(elapsed)}s (best {BrainPrefs.BestLowScore("memory", variantName)})", true, NewGame);
        }

        void UpdateStats()
        {
            string variantName = VariantName(variant);
            int best = BrainPrefs.BestLowScore("memory", variantName);
            SetStat(0, "Moves", moves.ToString());
            SetStat(1, "Time", Mathf.RoundToInt(elapsed) + "s");
            SetStat(2, "Best", best > 0 ? best + " moves" : "-");
        }

        protected override void Update()
        {
            base.Update();
            if (finished || cards == null) return;
            float previous = elapsed;
            elapsed += Time.unscaledDeltaTime;
            if (Mathf.FloorToInt(previous) != Mathf.FloorToInt(elapsed)) UpdateStats();
        }
    }
}
