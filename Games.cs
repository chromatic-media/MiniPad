// The Bunnies: MiniPad's easter-egg games, reached from About > Bunnies.
//
// The rules live in plain classes (MinesweeperBoard, TicTacToe) with no UI attached,
// so the logic can be tested without a window.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// ==========================================================================
// Minesweeper - rules
// ==========================================================================

public enum MineState { Playing, Won, Lost }

public class MinesweeperBoard
{
    private readonly bool[,] mine;
    private readonly bool[,] revealed;
    private readonly bool[,] flagged;
    private bool minesPlaced;
    private readonly Random random;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Mines { get; private set; }
    public MineState State { get; private set; }

    public MinesweeperBoard(int width, int height, int mines, Random random)
    {
        Width = width;
        Height = height;
        // Leave room for the safe opening: the first click and its neighbours.
        int maximum = Math.Max(1, width * height - 9);
        Mines = Math.Max(1, Math.Min(mines, maximum));
        this.random = random ?? new Random();

        mine = new bool[width, height];
        revealed = new bool[width, height];
        flagged = new bool[width, height];
        State = MineState.Playing;
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool IsMine(int x, int y) { return InBounds(x, y) && mine[x, y]; }
    public bool IsRevealed(int x, int y) { return InBounds(x, y) && revealed[x, y]; }
    public bool IsFlagged(int x, int y) { return InBounds(x, y) && flagged[x, y]; }

    public int FlagCount
    {
        get
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (flagged[x, y]) count++;
            return count;
        }
    }

    public int RevealedCount
    {
        get
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (revealed[x, y]) count++;
            return count;
        }
    }

    public int Adjacent(int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && IsMine(x + dx, y + dy)) count++;
        return count;
    }

    public void ToggleFlag(int x, int y)
    {
        if (State != MineState.Playing || !InBounds(x, y) || revealed[x, y]) return;
        flagged[x, y] = !flagged[x, y];
    }

    public void Reveal(int x, int y)
    {
        if (State != MineState.Playing || !InBounds(x, y)) return;
        if (flagged[x, y] || revealed[x, y]) return;

        if (!minesPlaced) PlaceMines(x, y);

        if (mine[x, y])
        {
            revealed[x, y] = true;
            State = MineState.Lost;
            for (int mx = 0; mx < Width; mx++)
                for (int my = 0; my < Height; my++)
                    if (mine[mx, my]) revealed[mx, my] = true;
            return;
        }

        // Flood outwards from empty cells. Iterative, so a large board cannot
        // overflow the stack.
        Stack<Point> pending = new Stack<Point>();
        pending.Push(new Point(x, y));

        while (pending.Count > 0)
        {
            Point cell = pending.Pop();
            if (!InBounds(cell.X, cell.Y)) continue;
            if (revealed[cell.X, cell.Y] || flagged[cell.X, cell.Y] || mine[cell.X, cell.Y]) continue;

            revealed[cell.X, cell.Y] = true;
            if (Adjacent(cell.X, cell.Y) != 0) continue;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) pending.Push(new Point(cell.X + dx, cell.Y + dy));
        }

        if (RevealedCount == Width * Height - Mines) State = MineState.Won;
    }

    // Mines are placed after the first click so that click is always safe, and so
    // it always opens a pocket rather than a single number.
    private void PlaceMines(int safeX, int safeY)
    {
        List<Point> candidates = new List<Point>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (Math.Abs(x - safeX) > 1 || Math.Abs(y - safeY) > 1)
                    candidates.Add(new Point(x, y));

        for (int placed = 0; placed < Mines && candidates.Count > 0; placed++)
        {
            int pick = random.Next(candidates.Count);
            mine[candidates[pick].X, candidates[pick].Y] = true;
            candidates.RemoveAt(pick);
        }
        minesPlaced = true;
    }
}

// ==========================================================================
// Tic-tac-toe - rules
// ==========================================================================

public enum Skill { Easy, Medium, Hard }

public static class TicTacToe
{
    public const char Empty = ' ';
    public const char Human = 'X';
    public const char Computer = 'O';

    private static readonly int[][] Lines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
    };

    // 'X' or 'O' if won, 'D' for a draw, '\0' while the game is still going.
    public static char Winner(char[] board)
    {
        foreach (int[] line in Lines)
        {
            char first = board[line[0]];
            if (first != Empty && first == board[line[1]] && first == board[line[2]])
                return first;
        }
        foreach (char cell in board)
            if (cell == Empty) return '\0';
        return 'D';
    }

    public static bool IsOver(char[] board) { return Winner(board) != '\0'; }

    public static int BestMove(char[] board, Skill skill, Random random)
    {
        List<int> free = new List<int>();
        for (int n = 0; n < 9; n++)
            if (board[n] == Empty) free.Add(n);
        if (free.Count == 0) return -1;

        if (skill == Skill.Easy)
            return free[random.Next(free.Count)];

        if (skill == Skill.Medium)
        {
            int win = FindImmediate(board, Computer);
            if (win >= 0) return win;
            int block = FindImmediate(board, Human);
            if (block >= 0) return block;
            if (board[4] == Empty) return 4;

            List<int> corners = new List<int>();
            foreach (int corner in new[] { 0, 2, 6, 8 })
                if (board[corner] == Empty) corners.Add(corner);
            if (corners.Count > 0) return corners[random.Next(corners.Count)];

            return free[random.Next(free.Count)];
        }

        // Hard: full minimax, so it cannot be beaten.
        int bestScore = int.MinValue;
        List<int> bestMoves = new List<int>();
        foreach (int move in free)
        {
            board[move] = Computer;
            int score = Minimax(board, false, 0);
            board[move] = Empty;

            if (score > bestScore)
            {
                bestScore = score;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (score == bestScore)
            {
                bestMoves.Add(move);
            }
        }
        return bestMoves[random.Next(bestMoves.Count)];
    }

    private static int FindImmediate(char[] board, char player)
    {
        foreach (int[] line in Lines)
        {
            int own = 0, free = -1;
            foreach (int cell in line)
            {
                if (board[cell] == player) own++;
                else if (board[cell] == Empty) free = cell;
                else { own = -9; break; }
            }
            if (own == 2 && free >= 0) return free;
        }
        return -1;
    }

    // Positive favours the computer. Depth is used so it wins sooner and stalls longer.
    private static int Minimax(char[] board, bool computerTurn, int depth)
    {
        char winner = Winner(board);
        if (winner == Computer) return 10 - depth;
        if (winner == Human) return depth - 10;
        if (winner == 'D') return 0;

        int best = computerTurn ? int.MinValue : int.MaxValue;
        for (int n = 0; n < 9; n++)
        {
            if (board[n] != Empty) continue;
            board[n] = computerTurn ? Computer : Human;
            int score = Minimax(board, !computerTurn, depth + 1);
            board[n] = Empty;
            best = computerTurn ? Math.Max(best, score) : Math.Min(best, score);
        }
        return best;
    }
}

// ==========================================================================
// Minesweeper - window
// ==========================================================================

public class MinesweeperForm : Form
{
    private const int Cell = 22;

    private MinesweeperBoard board;
    private readonly Panel grid;
    private readonly Label mineLabel;
    private readonly Label timeLabel;
    private readonly Label statusLabel;
    private readonly Timer clock;
    private readonly Random random = new Random();

    private int width = 9, height = 9, mines = 10;
    private int seconds;
    private bool started;

    public MinesweeperForm()
    {
        Text = "Minesweeper";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        MenuStrip menu = new MenuStrip();
        ToolStripMenuItem game = new ToolStripMenuItem("&Game");
        game.DropDownItems.Add(Item("&New", delegate { NewGame(width, height, mines); }));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(Item("&Beginner  (9 x 9, 10 mines)", delegate { NewGame(9, 9, 10); }));
        game.DropDownItems.Add(Item("&Intermediate  (16 x 16, 40 mines)", delegate { NewGame(16, 16, 40); }));
        game.DropDownItems.Add(Item("&Expert  (30 x 16, 99 mines)", delegate { NewGame(30, 16, 99); }));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(Item("&Close", delegate { Close(); }));
        menu.Items.Add(game);
        MainMenuStrip = menu;

        Panel header = new Panel();
        header.Height = 30;
        header.Dock = DockStyle.Top;

        mineLabel = new Label();
        mineLabel.AutoSize = true;
        mineLabel.Location = new Point(8, 7);
        mineLabel.Font = new Font("Consolas", 10f, FontStyle.Bold);

        timeLabel = new Label();
        timeLabel.AutoSize = true;
        timeLabel.Location = new Point(90, 7);
        timeLabel.Font = new Font("Consolas", 10f, FontStyle.Bold);

        statusLabel = new Label();
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(170, 7);
        statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        header.Controls.Add(mineLabel);
        header.Controls.Add(timeLabel);
        header.Controls.Add(statusLabel);

        grid = new Panel();
        grid.Dock = DockStyle.Fill;
        grid.BackColor = Color.FromArgb(189, 189, 189);
        SetDoubleBuffered(grid);
        grid.Paint += new PaintEventHandler(OnGridPaint);
        grid.MouseDown += new MouseEventHandler(OnGridMouseDown);

        Controls.Add(grid);
        Controls.Add(header);
        Controls.Add(menu);

        clock = new Timer();
        clock.Interval = 1000;
        clock.Tick += delegate
        {
            seconds++;
            timeLabel.Text = "Time " + seconds;
        };

        // Without this the clock keeps ticking against a disposed window, holding it
        // alive and writing to controls that no longer exist.
        FormClosed += delegate
        {
            clock.Stop();
            clock.Dispose();
        };

        NewGame(width, height, mines);
        AppInfo.ApplyIcon(this);
        Theme.Apply(this);
    }

    private ToolStripMenuItem Item(string text, EventHandler handler)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text);
        item.Click += handler;
        return item;
    }

    private static void SetDoubleBuffered(Control control)
    {
        typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(control, true, null);
    }

    private void NewGame(int newWidth, int newHeight, int newMines)
    {
        width = newWidth;
        height = newHeight;
        mines = newMines;
        board = new MinesweeperBoard(width, height, mines, random);

        seconds = 0;
        started = false;
        clock.Stop();
        timeLabel.Text = "Time 0";
        statusLabel.Text = "";
        UpdateMineLabel();

        ClientSize = new Size(Math.Max(260, width * Cell + 2),
                              height * Cell + 2 + 30 + MainMenuStrip.Height);
        grid.Invalidate();
    }

    private void UpdateMineLabel()
    {
        mineLabel.Text = "Mines " + (board.Mines - board.FlagCount);
    }

    private void OnGridMouseDown(object sender, MouseEventArgs e)
    {
        if (board.State != MineState.Playing) return;

        int x = e.X / Cell;
        int y = e.Y / Cell;
        if (!board.InBounds(x, y)) return;

        if (e.Button == MouseButtons.Right)
        {
            board.ToggleFlag(x, y);
        }
        else if (e.Button == MouseButtons.Left)
        {
            if (!started)
            {
                started = true;
                clock.Start();
            }
            board.Reveal(x, y);
        }

        UpdateMineLabel();

        if (board.State == MineState.Won)
        {
            clock.Stop();
            statusLabel.Text = "Cleared in " + seconds + "s";
            statusLabel.ForeColor = Color.FromArgb(20, 120, 40);
        }
        else if (board.State == MineState.Lost)
        {
            clock.Stop();
            statusLabel.Text = "Boom. Game > New to try again";
            statusLabel.ForeColor = Color.FromArgb(170, 30, 30);
        }

        grid.Invalidate();
    }

    private static readonly Color[] NumberColours =
    {
        Color.Transparent,
        Color.FromArgb(0, 0, 220), Color.FromArgb(0, 120, 0), Color.FromArgb(220, 0, 0),
        Color.FromArgb(0, 0, 130), Color.FromArgb(130, 0, 0), Color.FromArgb(0, 130, 130),
        Color.Black, Color.FromArgb(90, 90, 90),
    };

    private void OnGridPaint(object sender, PaintEventArgs e)
    {
        Graphics canvas = e.Graphics;
        Font numberFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        StringFormat centre = new StringFormat();
        centre.Alignment = StringAlignment.Center;
        centre.LineAlignment = StringAlignment.Center;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Rectangle box = new Rectangle(x * Cell, y * Cell, Cell, Cell);

                if (!board.IsRevealed(x, y))
                {
                    canvas.FillRectangle(new SolidBrush(Color.FromArgb(198, 198, 198)), box);
                    ControlPaint.DrawBorder3D(canvas, box, Border3DStyle.Raised);
                    if (board.IsFlagged(x, y))
                        canvas.DrawString("!", numberFont, Brushes.Firebrick, box, centre);
                    continue;
                }

                canvas.FillRectangle(new SolidBrush(Color.FromArgb(222, 222, 222)), box);
                canvas.DrawRectangle(Pens.Silver, box);

                if (board.IsMine(x, y))
                {
                    canvas.FillEllipse(Brushes.Black,
                        box.X + 6, box.Y + 6, Cell - 12, Cell - 12);
                    continue;
                }

                int adjacent = board.Adjacent(x, y);
                if (adjacent > 0)
                    canvas.DrawString(adjacent.ToString(), numberFont,
                        new SolidBrush(NumberColours[adjacent]), box, centre);
            }
        }
    }
}

// ==========================================================================
// Tic-tac-toe - window
// ==========================================================================

public class TicTacToeForm : Form
{
    private readonly Button[] cells = new Button[9];
    private readonly char[] board = new char[9];
    private readonly ComboBox skillBox;
    private readonly Label statusLabel;
    private readonly Label scoreLabel;
    private readonly Random random = new Random();

    private int humanWins, computerWins, draws;

    public TicTacToeForm()
    {
        Text = "Tic-tac-toe";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(240, 330);
        Font = new Font("Segoe UI", 9f);

        Label skillLabel = new Label();
        skillLabel.Text = "Skill";
        skillLabel.AutoSize = true;
        skillLabel.Location = new Point(12, 15);

        skillBox = new ComboBox();
        skillBox.DropDownStyle = ComboBoxStyle.DropDownList;
        skillBox.Items.AddRange(new object[] { "Easy", "Medium", "Hard (unbeatable)" });
        skillBox.SelectedIndex = 1;
        skillBox.Location = new Point(50, 12);
        skillBox.Width = 175;
        skillBox.SelectedIndexChanged += delegate { NewGame(); };

        for (int n = 0; n < 9; n++)
        {
            Button cell = new Button();
            cell.Size = new Size(64, 64);
            cell.Location = new Point(12 + (n % 3) * 70, 45 + (n / 3) * 70);
            cell.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
            cell.Tag = n;
            cell.Click += new EventHandler(OnCellClick);
            cells[n] = cell;
            Controls.Add(cell);
        }

        statusLabel = new Label();
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(12, 260);
        statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        scoreLabel = new Label();
        scoreLabel.AutoSize = true;
        scoreLabel.Location = new Point(12, 282);
        scoreLabel.ForeColor = Color.FromArgb(90, 96, 106);

        Button newGame = new Button();
        newGame.Text = "&New game";
        newGame.Size = new Size(90, 26);
        newGame.Location = new Point(135, 297);
        newGame.Click += delegate { NewGame(); };

        Controls.Add(skillLabel);
        Controls.Add(skillBox);
        Controls.Add(statusLabel);
        Controls.Add(scoreLabel);
        Controls.Add(newGame);

        NewGame();
        AppInfo.ApplyIcon(this);
        Theme.Apply(this);
    }

    private Skill CurrentSkill
    {
        get
        {
            if (skillBox.SelectedIndex == 0) return Skill.Easy;
            if (skillBox.SelectedIndex == 2) return Skill.Hard;
            return Skill.Medium;
        }
    }

    private void NewGame()
    {
        for (int n = 0; n < 9; n++)
        {
            board[n] = TicTacToe.Empty;
            cells[n].Text = "";
            cells[n].Enabled = true;
        }
        statusLabel.Text = "Your turn - you are X";
        statusLabel.ForeColor = SystemColors.ControlText;
        UpdateScore();
    }

    private void OnCellClick(object sender, EventArgs e)
    {
        int index = (int)((Button)sender).Tag;
        if (board[index] != TicTacToe.Empty || TicTacToe.IsOver(board)) return;

        Play(index, TicTacToe.Human);
        if (Finish()) return;

        int reply = TicTacToe.BestMove(board, CurrentSkill, random);
        if (reply >= 0) Play(reply, TicTacToe.Computer);
        Finish();
    }

    private void Play(int index, char player)
    {
        board[index] = player;
        cells[index].Text = player.ToString();
        cells[index].ForeColor = player == TicTacToe.Human
            ? Color.FromArgb(0, 90, 200) : Color.FromArgb(190, 40, 40);
    }

    // Returns true when the game has ended.
    private bool Finish()
    {
        char winner = TicTacToe.Winner(board);
        if (winner == '\0') return false;

        foreach (Button cell in cells) cell.Enabled = false;

        if (winner == TicTacToe.Human)
        {
            humanWins++;
            statusLabel.Text = "You win.";
            statusLabel.ForeColor = Color.FromArgb(20, 120, 40);
        }
        else if (winner == TicTacToe.Computer)
        {
            computerWins++;
            statusLabel.Text = "MiniPad wins.";
            statusLabel.ForeColor = Color.FromArgb(170, 30, 30);
        }
        else
        {
            draws++;
            statusLabel.Text = "A draw.";
            statusLabel.ForeColor = SystemColors.ControlText;
        }

        UpdateScore();
        return true;
    }

    private void UpdateScore()
    {
        scoreLabel.Text = string.Format("You {0}   MiniPad {1}   Draws {2}",
                                        humanWins, computerWins, draws);
    }
}
