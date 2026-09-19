using System;
using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>
    /// Draws the board and tokens and animates moves. The board can be rotated in 90° steps so the local
    /// player's yard sits at the bottom-left; tokens stay upright.
    /// </summary>
    public sealed class LudoBoardView : MonoBehaviour, IPointerClickHandler
    {
        const float FramePadding = 14f;

        public RectTransform Holder { get; private set; }
        public float Size { get; private set; }
        public float Cell => Size / LudoBoard.Grid;

        /// <summary>Raised with the tapped point in board-local coordinates.</summary>
        public event Action<Vector2> Tapped;

        readonly TokenView[] tokens = new TokenView[LudoState.SeatCount * LudoState.TokensPerSeat];
        RectTransform board;
        RawImage boardImage;
        RectTransform tokenLayer;
        RectTransform fxLayer;
        float rotation;
        int texturePixels;

        public static LudoBoardView Create(Transform parent, LudoState state, int bottomLeftSeat)
        {
            var holder = UIFactory.AddRect("Board", parent);
            var hit = holder.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var view = holder.gameObject.AddComponent<LudoBoardView>();
            view.Holder = holder;

            UIFactory.AddShadow(holder, "Shadow", 26f, 0.5f, new Vector2(0f, -22f));
            var frame = UIFactory.AddPanel(holder, "Frame", LudoTheme.Frame, 40f);
            UIFactory.Stretch(frame.rectTransform);

            view.board = UIFactory.AddRect("Surface", holder);
            view.boardImage = UIFactory.AddRaw(view.board, "Image", null);
            UIFactory.Stretch(view.boardImage.rectTransform);
            view.tokenLayer = UIFactory.Stretch(UIFactory.AddRect("Tokens", view.board));
            view.fxLayer = UIFactory.Stretch(UIFactory.AddRect("Fx", view.board));

            // Clockwise quarter turns that bring bottomLeftSeat's yard (seat index == corner index) to the bottom-left.
            int quarterTurns = (LudoState.SeatCount - bottomLeftSeat) % LudoState.SeatCount;
            view.rotation = -90f * quarterTurns;
            view.board.localEulerAngles = new Vector3(0f, 0f, view.rotation);

            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                for (int t = 0; t < LudoState.TokensPerSeat; t++)
                {
                    var token = TokenView.Create(view.tokenLayer, seat, t);
                    token.SetBoardRotation(view.rotation);
                    token.gameObject.SetActive(state.IsActive(seat));
                    view.tokens[seat * LudoState.TokensPerSeat + t] = token;
                }
            }
            return view;
        }

        /// <summary>Screen corner (0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right) of a seat's yard.</summary>
        public int ScreenCorner(int seat)
        {
            int quarterTurns = Mathf.RoundToInt(-rotation / 90f);
            return (seat + quarterTurns) % LudoState.SeatCount;
        }

        public void Resize(float size, LudoState state)
        {
            Size = size;
            Holder.sizeDelta = new Vector2(size + FramePadding * 2f, size + FramePadding * 2f);
            UIFactory.Place(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            board.localEulerAngles = new Vector3(0f, 0f, rotation);

            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int pixels = Mathf.RoundToInt(size * scale);
            if (texturePixels == 0 || Mathf.Abs(pixels - texturePixels) > texturePixels * 0.08f)
            {
                texturePixels = pixels;
                boardImage.texture = LudoArt.Board(pixels);
            }

            foreach (var token in tokens) token.SetCellSize(Cell);
            Layout(state);
        }

        public TokenView TokenOf(int seat, int token) => tokens[seat * LudoState.TokensPerSeat + token];

        public Vector2 BoardToLocal(Vector2 units) => new Vector2((units.x / LudoBoard.Grid - 0.5f) * Size, (units.y / LudoBoard.Grid - 0.5f) * Size);

        public Vector2 PointOf(int seat, int token, int progress) => BoardToLocal(LudoBoard.TokenPoint(seat, token, progress));

        /// <summary>Snaps every token to its square, fanning out tokens that share one.</summary>
        public void Layout(LudoState state)
        {
            foreach (var target in ComputeLayout(state))
            {
                target.View.SetStack(target.Offset, target.Scale);
                target.View.SetPosition(target.Point);
            }
            SortDrawOrder();
        }

        public void SetMovable(List<LudoMove> moves)
        {
            ClearMovable();
            foreach (var move in moves) TokenOf(move.Seat, move.Token).SetHighlighted(true);
        }

        public void ClearMovable()
        {
            foreach (var token in tokens) token.SetHighlighted(false);
        }

        /// <summary>Closest token (by its head) to a board-local point that passes the filter.</summary>
        public TokenView HitTest(Vector2 boardLocal, Func<TokenView, bool> filter)
        {
            TokenView best = null;
            float bestDistance = Cell * 0.85f;
            foreach (var token in tokens)
            {
                if (!token.gameObject.activeSelf || !filter(token)) continue;
                float head = Vector2.Distance(boardLocal, token.HeadPointInBoard());
                float foot = Vector2.Distance(boardLocal, token.Rect.anchoredPosition);
                float distance = Mathf.Min(head, foot);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = token;
                }
            }
            return best;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position, eventData.pressEventCamera, out var local))
                Tapped?.Invoke(local);
        }

        public IEnumerator AnimateMove(LudoState after, LudoMoveResult result, float stepTime)
        {
            var sound = App.Instance.Audio;
            var move = result.Move;
            var view = TokenOf(move.Seat, move.Token);
            view.transform.SetAsLastSibling();
            float startScale = view.StackScale;
            var startOffset = view.StackOffset;
            StartCoroutine(Tween.Run(0.12f, t => view.SetStack(Vector2.Lerp(startOffset, Vector2.zero, t), Mathf.Lerp(startScale, 1f, t))));

            if (move.From == LudoEngine.Yard)
            {
                sound.Play(Sfx.Unlock);
                Haptics.Play(HapticKind.Light);
                yield return Hop(view, PointOf(move.Seat, move.Token, LudoEngine.Yard), PointOf(move.Seat, move.Token, 0), stepTime * 2.4f, Cell * 1.4f);
            }
            else
            {
                for (int p = move.From + 1; p <= move.To; p++)
                {
                    yield return Hop(view, PointOf(move.Seat, move.Token, p - 1), PointOf(move.Seat, move.Token, p), stepTime, Cell * 0.6f);
                    sound.Play(Sfx.Step, 0.75f, 1f + (p - move.From) * 0.035f);
                }
            }

            if (result.Captures.Count > 0)
            {
                sound.Play(Sfx.Capture);
                Haptics.Play(HapticKind.Heavy);
                Effects.Burst(fxLayer, view.Rect.anchoredPosition, LudoTheme.Seat(result.Captures[0].Seat), 16, Cell * 1.6f, Cell * 0.28f);
                int flying = 0;
                foreach (var capture in result.Captures)
                {
                    var victim = TokenOf(capture.Seat, capture.Token);
                    flying++;
                    StartCoroutine(Run(FlyHome(victim, PointOf(capture.Seat, capture.Token, capture.From),
                        PointOf(capture.Seat, capture.Token, LudoEngine.Yard)), () => flying--));
                }
                while (flying > 0) yield return null;
            }
            else if (result.ReachedHome)
            {
                sound.Play(Sfx.TokenHome);
                Haptics.Play(HapticKind.Success);
                Effects.Burst(fxLayer, view.Rect.anchoredPosition, LudoTheme.Seat(move.Seat), 18, Cell * 2f, Cell * 0.3f);
            }
            else
            {
                Haptics.Play(HapticKind.Light);
            }

            yield return Regroup(after, 0.16f);
        }

        IEnumerator Hop(TokenView view, Vector2 from, Vector2 to, float duration, float height)
        {
            yield return Tween.Run(duration, t =>
            {
                view.SetPosition(Vector2.LerpUnclamped(from, to, Ease.InOutQuad(t)));
                float arc = Ease.Arc(t);
                view.SetLift(arc * height, 1f + arc * 0.07f);
            });
            view.SetLift(0f);
        }

        IEnumerator FlyHome(TokenView view, Vector2 from, Vector2 to)
        {
            view.transform.SetAsLastSibling();
            var offset = view.StackOffset;
            float scale = view.StackScale;
            yield return Tween.Run(0.5f, t =>
            {
                view.SetStack(Vector2.Lerp(offset, Vector2.zero, t), Mathf.Lerp(scale, 1f, t));
                view.SetPosition(Vector2.LerpUnclamped(from, to, Ease.InOutQuad(t)));
                view.SetLift(Ease.Arc(t) * Cell * 2.4f, 1f);
            });
            view.SetLift(0f);
        }

        IEnumerator Regroup(LudoState state, float duration)
        {
            var targets = ComputeLayout(state);
            var startOffsets = new Vector2[targets.Count];
            var startScales = new float[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                startOffsets[i] = targets[i].View.StackOffset;
                startScales[i] = targets[i].View.StackScale;
            }
            SortDrawOrder(targets);
            yield return Tween.Run(duration, t =>
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    target.View.SetStack(Vector2.Lerp(startOffsets[i], target.Offset, t), Mathf.Lerp(startScales[i], target.Scale, t));
                    target.View.SetPosition(target.Point);
                }
            }, Ease.OutQuad);
            SortDrawOrder();
        }

        static IEnumerator Run(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        struct TokenTarget
        {
            public TokenView View;
            public Vector2 Point;
            public Vector2 Offset;
            public float Scale;
        }

        List<TokenTarget> ComputeLayout(LudoState state)
        {
            var groups = new Dictionary<int, List<TokenView>>();
            foreach (var token in tokens)
            {
                bool active = state.IsActive(token.Seat);
                if (token.gameObject.activeSelf != active) token.gameObject.SetActive(active);
                if (!active) continue;
                int key = LudoBoard.SpotKey(token.Seat, token.Index, state.Token(token.Seat, token.Index));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<TokenView>();
                list.Add(token);
            }

            var result = new List<TokenTarget>();
            foreach (var pair in groups)
            {
                var list = pair.Value;
                bool home = pair.Key >= 2000 && pair.Key < 3000;
                for (int i = 0; i < list.Count; i++)
                {
                    var view = list[i];
                    FanOut(list.Count, i, out var offset, out float scale);
                    if (home)
                    {
                        offset *= 0.75f;
                        scale *= 0.62f;
                    }
                    result.Add(new TokenTarget
                    {
                        View = view,
                        Point = PointOf(view.Seat, view.Index, state.Token(view.Seat, view.Index)),
                        Offset = offset * Cell,
                        Scale = scale
                    });
                }
            }
            return result;
        }

        static void FanOut(int count, int index, out Vector2 offset, out float scale)
        {
            switch (count)
            {
                case 1:
                    offset = Vector2.zero;
                    scale = 1f;
                    break;
                case 2:
                    offset = new Vector2(index == 0 ? -0.2f : 0.2f, 0f);
                    scale = 0.8f;
                    break;
                case 3:
                    offset = index == 2 ? new Vector2(0f, 0.15f) : new Vector2(index == 0 ? -0.22f : 0.22f, -0.08f);
                    scale = 0.72f;
                    break;
                case 4:
                    offset = new Vector2(index % 2 == 0 ? -0.2f : 0.2f, index < 2 ? 0.13f : -0.11f);
                    scale = 0.66f;
                    break;
                default:
                    float angle = index * Mathf.PI * 2f / count;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.26f;
                    scale = 0.52f;
                    break;
            }
        }

        void SortDrawOrder(List<TokenTarget> targets = null)
        {
            var order = new List<(TokenView view, float y)>();
            if (targets != null)
            {
                foreach (var target in targets)
                    order.Add((target.View, ScreenY(target.Point) + target.Offset.y));
            }
            else
            {
                foreach (var token in tokens)
                    if (token.gameObject.activeSelf)
                        order.Add((token, ScreenY(token.Rect.anchoredPosition - token.ScreenToBoard(token.StackOffset)) + token.StackOffset.y));
            }
            // Higher on screen is further away, so it is drawn first.
            order.Sort((a, b) => b.y.CompareTo(a.y));
            for (int i = 0; i < order.Count; i++) order[i].view.transform.SetSiblingIndex(i);
        }

        float ScreenY(Vector2 boardLocal)
        {
            float rad = rotation * Mathf.Deg2Rad;
            return boardLocal.x * Mathf.Sin(rad) + boardLocal.y * Mathf.Cos(rad);
        }
    }
}
