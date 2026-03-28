using System.Text.Json.Serialization;

namespace StrategyGame.Core.Models.Board;

public sealed class Board
{
    public const int Rows = 4;
    public const int Cols = 5;

    /// <summary>Rows available without expansion at game start (rows 0 to StartRows-1).</summary>
    public const int StartRows = 2;
    /// <summary>Columns available without expansion at game start (cols 0 to StartCols-1).</summary>
    public const int StartCols = 3;

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
                Cells[r * Cols + c] = new BoardCell
                {
                    Row = r,
                    Col = c,
                    IsLocked = !(r < StartRows && c < StartCols)
                };
    }

    public BoardCell GetCell(int row, int col) => Cells[row * Cols + col];

    public IEnumerable<BoardCell> AllCells() => Cells;

    /// <summary>
    /// Unlock all orthogonally adjacent locked cells when a land card is placed at (row, col).
    /// Called after every successful land placement.
    /// </summary>
    public void UnlockAdjacent(int row, int col)
    {
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = {  0, 0,-1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int r = row + dr[i];
            int c = col + dc[i];
            if (r >= 0 && r < Rows && c >= 0 && c < Cols)
                GetCell(r, c).IsLocked = false;
        }
    }
}
