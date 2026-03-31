using System.Text.Json.Serialization;
using StrategyGame.Core.Models.Cards;

namespace StrategyGame.Core.Models.Board;

public sealed class Board
{
    public const int Rows = 4;
    public const int Cols = 5;

    /// <summary>Starting grid of Empty land cards: 2 rows × 2 cols.</summary>
    public const int StartRows = 2;
    public const int StartCols = 2;

    /// <summary>Flat array of cells; index = row * Cols + col.</summary>
    public BoardCell[] Cells { get; set; }

    [JsonConstructor]
    public Board(BoardCell[] cells)
    {
        Cells = cells;
    }

    public Board()
    {
        Cells = new BoardCell[Rows * Cols];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var isStarting = r < StartRows && c < StartCols;
                Cells[r * Cols + c] = new BoardCell
                {
                    Row = r,
                    Col = c,
                    IsLocked = !isStarting,
                    Land = isStarting ? LandCard.CreateEmpty() : null
                };
            }
    }

    public BoardCell GetCell(int row, int col) => Cells[row * Cols + col];

    public IEnumerable<BoardCell> AllCells() => Cells;
}
