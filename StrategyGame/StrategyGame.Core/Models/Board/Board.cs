using System.Text.Json.Serialization;

namespace StrategyGame.Core.Models.Board;

public sealed class Board
{
    public const int Rows = 4;
    public const int Cols = 5;

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
                Cells[r * Cols + c] = new BoardCell { Row = r, Col = c };
    }

    public BoardCell GetCell(int row, int col) => Cells[row * Cols + col];

    public IEnumerable<BoardCell> AllCells() => Cells;
}
