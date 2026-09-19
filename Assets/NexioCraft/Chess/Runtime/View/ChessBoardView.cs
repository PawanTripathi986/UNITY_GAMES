using System;
using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Chess
{
    /// <summary>
    /// Interactive chess board: pieces, highlights, legal move markers, tap and drag input, move animation.
    /// Squares use engine indexing (0 = a1); the view can be flipped so black sits at the bottom.
    /// </summary>
    public sealed class ChessBoardView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float FramePadding = 16f;

        static readonly Color LastMoveTint = new Color(1f, 0.86f, 0.2f, 0.42f);
        static readonly Color SelectedTint = new Color(1f, 0.8f, 0.15f, 0.62f);
        static readonly Color HintTint = new Color(0.25f, 0.6f, 1f, 0.55f);
        static readonly Color MarkerColor = new Color(0.08f, 0.1f, 0.15f, 0.28f);

        public RectTransform Holder { get; private set; }
        public bool Flipped { get; private set; }
        public float Size { get; private set; }
        public float Cell => Size / 8f;

        /// <summary>Whether the local player may pick up the piece on a square.</summary>
        public Func<int, bool> CanPickUp;
        public Action<int> SquareTapped;
        public Action<int, int> PieceDropped;

        readonly Image[] pieceImages = new Image[64];
        readonly Image[] tints = new Image[64];
        readonly List<Image> markers = new List<Image>();
        readonly Text[] fileLabels = new Text[8];
        readonly Text[] rankLabels = new Text[8];

        RectTransform board;
        RawImage boardImage;
        Image frame;
        RectTransform tintLayer;
        RectTransform markerLayer;
        RectTransform pieceLayer;
        Image checkGlow;
        BoardTheme theme;
        int texturePixels;
        ChessPosition shown;

        int lastFrom = -1, lastTo = -1, selected = -1, hintFrom = -1, hintTo = -1;
        int dragFrom = -1;
        Image dragImage;

        public static ChessBoardView Create(Transform parent, BoardTheme theme, bool flipped)
        {
            var holder = UIFactory.AddRect("ChessBoard", parent);
            var hit = holder.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var view = holder.gameObject.AddComponent<ChessBoardView>();
            view.Holder = holder;
            view.theme = theme;
            view.Flipped = flipped;

            UIFactory.AddShadow(holder, "Shadow", 26f, 0.5f, new Vector2(0f, -20f));
            view.frame = UIFactory.AddPanel(holder, "Frame", ChessArt.Frame(theme), 30f);
            UIFactory.Stretch(view.frame.rectTransform);

            view.board = UIFactory.AddRect("Board", holder);
            view.boardImage = UIFactory.AddRaw(view.board, "Squares", null);
            UIFactory.Stretch(view.boardImage.rectTransform);
            view.tintLayer = UIFactory.Stretch(UIFactory.AddRect("Tints", view.board));
            for (int sq = 0; sq < 64; sq++)
            {
                var tint = UIFactory.AddImage(view.tintLayer, "Tint", Sprites.White, Color.clear);
                tint.enabled = false;
                view.tints[sq] = tint;
            }
            view.checkGlow = UIFactory.AddImage(view.board, "Check", Sprites.Glow, new Color(1f, 0.15f, 0.1f, 0.85f));
            view.checkGlow.enabled = false;
            var labels = UIFactory.Stretch(UIFactory.AddRect("Labels", view.board));
            for (int i = 0; i < 8; i++)
            {
                view.fileLabels[i] = UIFactory.AddLabel(labels, "File", ((char)('a' + i)).ToString(), 26f, Color.white, FontWeight.ExtraBold, TextAnchor.LowerRight);
                view.rankLabels[i] = UIFactory.AddLabel(labels, "Rank", (i + 1).ToString(), 26f, Color.white, FontWeight.ExtraBold, TextAnchor.UpperLeft);
            }
            view.markerLayer = UIFactory.Stretch(UIFactory.AddRect("Markers", view.board));
            view.pieceLayer = UIFactory.Stretch(UIFactory.AddRect("Pieces", view.board));
            for (int sq = 0; sq < 64; sq++)
            {
                var piece = UIFactory.AddImage(view.pieceLayer, "Piece", null, Color.white);
                piece.enabled = false;
                piece.preserveAspect = true;
                view.pieceImages[sq] = piece;
            }
            return view;
        }

        public void Resize(float size)
        {
            Size = size;
            Holder.sizeDelta = new Vector2(size + FramePadding * 2f, size + FramePadding * 2f);
            UIFactory.Place(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));

            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int pixels = Mathf.RoundToInt(size * scale);
            if (texturePixels != pixels)
            {
                texturePixels = pixels;
                boardImage.texture = ChessArt.Board(pixels, theme);
            }
            LayoutStatic();
            if (shown != null) Sync(shown);
            RefreshHighlights();
        }

        public void SetTheme(BoardTheme newTheme)
        {
            theme = newTheme;
            frame.color = ChessArt.Frame(theme);
            texturePixels = 0;
            if (Size > 0f) Resize(Size);
        }

        public void SetFlipped(bool flipped)
        {
            Flipped = flipped;
            LayoutStatic();
            if (shown != null) Sync(shown);
            RefreshHighlights();
        }

        public Vector2 SquareCenter(int square)
        {
            int file = square & 7, rank = square >> 3;
            if (Flipped)
            {
                file = 7 - file;
                rank = 7 - rank;
            }
            return new Vector2((file + 0.5f) * Cell - Size * 0.5f, (rank + 0.5f) * Cell - Size * 0.5f);
        }

        /// <summary>Shows exactly the pieces of the position.</summary>
        public void Sync(ChessPosition position)
        {
            shown = position;
            float pieceSize = Cell * 0.95f;
            for (int sq = 0; sq < 64; sq++)
            {
                var image = pieceImages[sq];
                int piece = position.Squares[sq];
                if (piece == 0)
                {
                    image.enabled = false;
                    continue;
                }
                image.enabled = true;
                image.sprite = ChessArt.PieceSprite(piece);
                image.color = Color.white;
                var rt = image.rectTransform;
                UIFactory.Place(rt, new Vector2(0.5f, 0.5f), SquareCenter(sq), new Vector2(pieceSize, pieceSize));
                rt.localScale = Vector3.one;
            }
            int king = position.KingSquare(position.SideToMove);
            checkGlow.enabled = king >= 0 && position.InCheck(position.SideToMove);
            if (checkGlow.enabled)
            {
                UIFactory.Place(checkGlow.rectTransform, new Vector2(0.5f, 0.5f), SquareCenter(king), new Vector2(Cell * 1.5f, Cell * 1.5f));
                checkGlow.transform.SetSiblingIndex(markerLayer.GetSiblingIndex());
            }
        }

        public void SetLastMove(int from, int to)
        {
            lastFrom = from;
            lastTo = to;
            RefreshHighlights();
        }

        /// <summary>Selects a square and marks the destinations of the given moves.</summary>
        public void SetSelection(int square, List<ChessMove> moves, bool showMarkers)
        {
            selected = square;
            foreach (var marker in markers) marker.enabled = false;
            if (square >= 0 && showMarkers)
            {
                int used = 0;
                var seen = new HashSet<int>();
                foreach (var move in moves)
                {
                    if (!seen.Add(move.To)) continue;
                    var marker = Marker(used++);
                    bool capture = move.IsCapture;
                    marker.sprite = capture ? Sprites.Ring(0.14f) : Sprites.Circle;
                    float markerSize = capture ? Cell * 0.94f : Cell * 0.32f;
                    UIFactory.Place(marker.rectTransform, new Vector2(0.5f, 0.5f), SquareCenter(move.To), new Vector2(markerSize, markerSize));
                    marker.enabled = true;
                }
            }
            RefreshHighlights();
        }

        public void SetHint(int from, int to)
        {
            hintFrom = from;
            hintTo = to;
            RefreshHighlights();
        }

        /// <summary>Slides the moving piece (and a castling rook) and removes a captured piece.</summary>
        public IEnumerator AnimateMove(ChessMove move, ChessPosition before)
        {
            var mover = pieceImages[move.From];
            if (!mover.enabled) yield break;
            mover.transform.SetAsLastSibling();
            Vector2 start = mover.rectTransform.anchoredPosition;
            if (dragImage == mover) start = mover.rectTransform.anchoredPosition;
            Vector2 end = SquareCenter(move.To);

            Image captured = null;
            if (move.IsCapture)
            {
                int capturedSquare = move.IsEnPassant ? move.To - 8 * Piece.SideOf(before.Squares[move.From]) : move.To;
                captured = pieceImages[capturedSquare];
            }

            Image rook = null;
            Vector2 rookStart = Vector2.zero, rookEnd = Vector2.zero;
            if (move.IsCastle)
            {
                int rookFrom = move.To > move.From ? move.From + 3 : move.From - 4;
                int rookTo = move.To > move.From ? move.From + 1 : move.From - 1;
                rook = pieceImages[rookFrom];
                rookStart = SquareCenter(rookFrom);
                rookEnd = SquareCenter(rookTo);
            }

            float distance = Vector2.Distance(start, end) / Mathf.Max(1f, Cell);
            float duration = Mathf.Clamp(0.1f + distance * 0.03f, 0.12f, 0.26f);
            yield return Tween.Run(duration, t =>
            {
                mover.rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
                mover.rectTransform.localScale = Vector3.one * (1f + Ease.Arc(t) * 0.08f);
                if (rook != null) rook.rectTransform.anchoredPosition = Vector2.LerpUnclamped(rookStart, rookEnd, t);
                if (captured != null && t > 0.6f)
                {
                    float k = (t - 0.6f) / 0.4f;
                    captured.color = new Color(1f, 1f, 1f, 1f - k);
                    captured.rectTransform.localScale = Vector3.one * (1f - 0.3f * k);
                }
            }, Ease.OutQuad);
            dragImage = null;
        }

        /// <summary>Returns a lifted piece to its square (after an illegal drop).</summary>
        public void CancelDrag()
        {
            if (dragFrom >= 0 && shown != null) Sync(shown);
            dragFrom = -1;
            dragImage = null;
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            int square = SquareAt(eventData);
            if (square >= 0) SquareTapped?.Invoke(square);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            int square = SquareAt(eventData.pressPosition, eventData.pressEventCamera);
            if (square < 0 || CanPickUp == null || !CanPickUp(square) || !pieceImages[square].enabled) return;
            dragFrom = square;
            dragImage = pieceImages[square];
            dragImage.transform.SetAsLastSibling();
            dragImage.rectTransform.localScale = Vector3.one * 1.25f;
            SquareTapped?.Invoke(square);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragImage == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position, eventData.pressEventCamera, out var local))
                dragImage.rectTransform.anchoredPosition = local + new Vector2(0f, Cell * 0.45f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragImage == null) return;
            int from = dragFrom;
            int to = SquareAt(eventData);
            dragImage.rectTransform.localScale = Vector3.one;
            if (to < 0 || to == from)
            {
                CancelDrag();
                return;
            }
            dragFrom = -1;
            PieceDropped?.Invoke(from, to);
        }

        int SquareAt(PointerEventData eventData) => SquareAt(eventData.position, eventData.pressEventCamera);

        int SquareAt(Vector2 screenPosition, Camera eventCamera)
        {
            if (Size <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPosition, eventCamera, out var local)) return -1;
            int file = Mathf.FloorToInt((local.x + Size * 0.5f) / Cell);
            int rank = Mathf.FloorToInt((local.y + Size * 0.5f) / Cell);
            if (file < 0 || file > 7 || rank < 0 || rank > 7) return -1;
            if (Flipped)
            {
                file = 7 - file;
                rank = 7 - rank;
            }
            return rank * 8 + file;
        }

        Image Marker(int index)
        {
            while (markers.Count <= index)
            {
                var marker = UIFactory.AddImage(markerLayer, "Marker", Sprites.Circle, MarkerColor);
                marker.enabled = false;
                markers.Add(marker);
            }
            return markers[index];
        }

        void LayoutStatic()
        {
            for (int sq = 0; sq < 64; sq++)
                UIFactory.Place(tints[sq].rectTransform, new Vector2(0.5f, 0.5f), SquareCenter(sq), new Vector2(Cell + 0.5f, Cell + 0.5f));

            float labelSize = Mathf.Max(18f, Cell * 0.22f);
            Color light = ChessArt.LightSquare(theme), dark = ChessArt.DarkSquare(theme);
            for (int i = 0; i < 8; i++)
            {
                // File letters along the bottom edge, rank numbers along the left edge (of the visible board).
                int fileSquare = Flipped ? 56 + i : i;
                var fileLabel = fileLabels[i];
                fileLabel.fontSize = Mathf.RoundToInt(labelSize);
                var fileCenter = SquareCenter(fileSquare);
                UIFactory.Place(fileLabel.rectTransform, new Vector2(0.5f, 0.5f), fileCenter + new Vector2(Cell * 0.38f, -Cell * 0.34f), new Vector2(Cell * 0.4f, Cell * 0.4f),
                    new Vector2(1f, 0f));
                fileLabel.alignment = TextAnchor.LowerRight;
                fileLabel.color = (((fileSquare >> 3) + (fileSquare & 7)) & 1) == 0 ? light : dark;

                int rankSquare = Flipped ? 7 + i * 8 : i * 8;
                var rankLabel = rankLabels[i];
                rankLabel.fontSize = Mathf.RoundToInt(labelSize);
                var rankCenter = SquareCenter(rankSquare);
                UIFactory.Place(rankLabel.rectTransform, new Vector2(0.5f, 0.5f), rankCenter + new Vector2(-Cell * 0.42f, Cell * 0.46f), new Vector2(Cell * 0.4f, Cell * 0.4f),
                    new Vector2(0f, 1f));
                rankLabel.alignment = TextAnchor.UpperLeft;
                rankLabel.color = (((rankSquare >> 3) + (rankSquare & 7)) & 1) == 0 ? light : dark;
            }
        }

        void RefreshHighlights()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                Color color = Color.clear;
                if (sq == lastFrom || sq == lastTo) color = LastMoveTint;
                if (sq == hintFrom || sq == hintTo) color = HintTint;
                if (sq == selected) color = SelectedTint;
                tints[sq].enabled = color.a > 0f;
                tints[sq].color = color;
            }
        }

        void Update()
        {
            if (hintFrom < 0) return;
            float pulse = 0.35f + (Mathf.Sin(Time.unscaledTime * 5f) * 0.5f + 0.5f) * 0.35f;
            var c = HintTint;
            c.a = pulse;
            if (hintFrom != selected) tints[hintFrom].color = c;
            if (hintTo >= 0 && hintTo != selected) tints[hintTo].color = c;
        }
    }
}
