using System.IO;
using NexioCraft.Chess;
using NexioCraft.Core;
using UnityEditor;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>Renders procedural artwork to PNGs in Screenshots/ for quick visual review without a device.</summary>
    public static class ArtPreview
    {
        [MenuItem("NexioCraft/Chess/Render Art Preview")]
        public static void RenderChess()
        {
            Directory.CreateDirectory("Screenshots");
            const int cell = 128;
            var board = new Raster(cell * 8, cell * 8 + cell * 2, Palette.BackgroundBottom);
            for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
            {
                Color color = ((rank + file) & 1) == 0 ? ChessArt.DarkSquare(BoardTheme.Classic) : ChessArt.LightSquare(BoardTheme.Classic);
                board.RoundRect((file + 0.5f) * cell, (rank + 2.5f) * cell, cell * 0.5f, cell * 0.5f, 0f, color, 0.5f);
            }

            var start = ChessPosition.Start();
            for (int sq = 0; sq < 64; sq++)
            {
                int piece = start.Squares[sq];
                if (piece == 0) continue;
                float scale = cell / 256f * 0.92f;
                ChessArt.DrawPiece(board, piece, ((sq & 7) + 0.5f) * cell, ((sq >> 3) + 2f) * cell + cell * 0.04f, scale, true);
            }
            // Large row of pieces underneath for detail.
            for (int type = 1; type <= 6; type++)
            {
                ChessArt.DrawPiece(board, type, (type - 1) * cell * 1.33f + cell * 0.66f, cell * 1.0f, cell / 256f * 1.1f, true);
                ChessArt.DrawPiece(board, -type, (type - 1) * cell * 1.33f + cell * 0.66f, 0f, cell / 256f * 1.1f, true);
            }
            Save(board, "Screenshots/chess-art-preview.png");
            Save(ChessArt.AppIcon(512), "Screenshots/chess-icon-preview.png");
            Save(Ludo.LudoArt.AppIcon(512), "Screenshots/ludo-icon-preview.png");
        }

        public static void CommandLine()
        {
            RenderChess();
            EditorApplication.Exit(0);
        }

        static void Save(Raster raster, string path)
        {
            var texture = raster.ToTexture(Path.GetFileNameWithoutExtension(path), true);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log("Wrote " + path);
        }
    }
}
