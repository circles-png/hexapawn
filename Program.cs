using Spectre.Console;

namespace HexapawnAI;

public class Program
{
    private static void Main()
    {
        while (true)
        {
            var game = new Game();
            game.Simulate();
            Console.ReadLine();
        }
    }
}

public class Game
{
    private const int Size = 3;

    private readonly List<State> states;
    private State CurrentState => states.Last();
    private Player currentPlayer;
    private Player OtherPlayer => currentPlayer == Player.White ? Player.Black : Player.White;
    private readonly Random random;

    public Game()
    {
        states = new() { new() };
        random = new();
        currentPlayer = Player.White;
    }

    // public static void Step()
    // {
    //     Console.Clear();
    //     AnsiConsole.MarkupLine($"[bold white]Current player: {currentPlayer}[/]");
    //     AnsiConsole.MarkupLine($"[bold green1]Current state (state {states.Count}):[/]");
    //     AnsiConsole.MarkupLine(CurrentState.Display(1));

    //     var winningPlayer = CurrentState.CheckLastRankReached();
    //     if (winningPlayer != null)
    //     {
    //         AnsiConsole.MarkupLine($"[bold dodgerblue1]Player {winningPlayer} wins![/]");
    //         Environment.Exit(0);
    //     }

    //     var nextStates = CurrentState.GetNextStates().ToList();
    //     if (nextStates.Count == 0)
    //     {
    //         AnsiConsole.MarkupLine("[bold red]No more moves possible. Game over.[/]");
    //         AnsiConsole.MarkupLine($"[bold dodgerblue1]Player {OtherPlayer} wins![/]");
    //         Environment.Exit(0);
    //     }
    //     foreach (var (state, index) in nextStates.Select((state, index) => (state, index)))
    //     {
    //         AnsiConsole.MarkupLine($"    [bold orange3]Next state {index + 1}:[/]");
    //         AnsiConsole.MarkupLine(state.Display(2));
    //     }
    //     var nextState = nextStates[random.Next(nextStates.Count)];

    //     states.Add(nextState);
    // }

    public void Simulate()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine($"[bold white]Current player: {currentPlayer}[/]");
            AnsiConsole.MarkupLine($"[bold green1]Current state (state {states.Count}):[/]");
            AnsiConsole.MarkupLine(CurrentState.Display(1));
            var (winner, nextStates) = Step();
            foreach (var (state, index) in nextStates.Select((state, index) => (state, index)))
            {
                AnsiConsole.MarkupLine($"    [bold orange3]Next state {index + 1}:[/]");
                AnsiConsole.MarkupLine(state.Display(2));
            }
            if (winner != null)
            {
                AnsiConsole.MarkupLine($"[bold dodgerblue1]Player {winner} wins![/]");
                break;
            }
            Console.ReadLine();
        }
    }

    private (Player? winner, IEnumerable<State> nextStates) Step()
    {
        var winningPlayer = CurrentState.CheckLastRankReached();
        var nextStates = CurrentState.GetNextStates(currentPlayer, OtherPlayer).ToList();

        if (winningPlayer != null)
            return (winningPlayer, nextStates);
        if (nextStates.Count == 0)
            return (OtherPlayer, nextStates);

        var nextState = nextStates[random.Next(nextStates.Count)];

        states.Add(nextState);
        currentPlayer = OtherPlayer;
        return (null, nextStates);
    }

    private class State : ICloneable
    {
        private List<Piece> pieces = new();

        public State()
        {
            pieces.AddRange(
                Enumerable
                    .Range(0, Size)
                    .Select(index => new Piece(Player.Black, new Position(index, 0)))
            );
            pieces.AddRange(
                Enumerable
                    .Range(0, Size)
                    .Select(index => new Piece(Player.White, new Position(index, Size - 1)))
            );
        }

        public IEnumerable<State> GetNextStates(Player currentPlayer, Player otherPlayer)
            => pieces
                .Where(piece => piece.Player == currentPlayer)
                .SelectMany(
                    piece => GetNextPositions(piece)
                        .Where(position => TryMovePiece(piece, position, currentPlayer, otherPlayer, out _))
                        .Select(position =>
                            {
                                TryMovePiece(piece, position, currentPlayer, otherPlayer, out var state);
                                return state!;
                            }
                        )
                );

        private bool TryMovePiece(Piece piece, Position position, Player currentPlayer, Player otherPlayer, out State? state)
        {
            var movingDiagonally = position.X != piece.Position.X;
            if (movingDiagonally)
            {
                if (!pieces.Any(piece => piece.Position == position && piece.Player != currentPlayer))
                {
                    state = null;
                    return false;
                }
            }
            else if (pieces.Any(piece => piece.Position == position))
            {
                state = null;
                return false;
            }

            var clone = (Clone() as State) ?? throw new();
            clone.pieces.Remove(piece);
            clone.pieces.Add(new Piece(piece.Player, position));

            if (movingDiagonally && clone.pieces.Any(piece => piece.Position == position && piece.Player == otherPlayer))
            {
                clone.pieces.Remove(
                    clone.pieces
                        .Where(piece => piece.Player == otherPlayer && piece.Position == position)
                        .Single()
                );
            }

            state = clone;
            return true;
        }

        private static IEnumerable<Position> GetNextPositions(Piece piece)
        {
            int positionOfRowInFront = piece.Position.Y + (piece.Player == Player.Black ? 1 : -1);
            var nextPositions = new[]
            {
                piece.Position with { Y = positionOfRowInFront },
                piece.Position with { X = piece.Position.X - 1, Y = positionOfRowInFront },
                piece.Position with { X = piece.Position.X + 1, Y = positionOfRowInFront }
            };
            return nextPositions.Where(position => !OutOfBoard(position));
        }

        private static bool OutOfBoard(Position position)
            => position.X < 0
            || position.X >= Size
            || position.Y < 0
            || position.Y >= Size;

        public string Display(int indents = 0)
        {
            var rows = Enumerable
                .Repeat(
                    Enumerable.Repeat("  ", Size), Size
                )
                .Select(row => row.ToList())
                .ToList();
            foreach (var piece in pieces)
                rows[piece.Position.Y][piece.Position.X] = piece.Player == Player.Black ? "B " : "W ";
            return $"{string.Join(
                Environment.NewLine,
                rows.Select(row =>
                {
                    row.Insert(0, "[black on white] ");
                    row.Add("[/]");
                    for (var i = 0; i < indents; i++)
                        row.Insert(0, "    ");
                    return string.Concat(row);
                })
            )}";
        }

        public Player? CheckLastRankReached()
        {
            var whitePieces = pieces.Where(piece => piece.Player == Player.White);
            if (whitePieces.Any(piece => piece.Position.Y == 0))
                return Player.White;
            var blackPieces = pieces.Where(piece => piece.Player == Player.Black);
            if (blackPieces.Any(piece => piece.Position.Y == Size - 1))
                return Player.Black;
            return null;
        }

        public object Clone()
        {
            var clone = new State
            {
                pieces = pieces.Select(piece => (Piece)piece.Clone()).ToList()
            };
            return clone;
        }
    }

    private class Piece : ICloneable, IEquatable<Piece>
    {
        public Player Player { get; set; }
        public Position Position { get; set; }

        public Piece(Player player, Position position)
        {
            Player = player;
            Position = position;
        }

        public object Clone() => new Piece(Player, Position);

        bool IEquatable<Piece>.Equals(Piece? other) => other != null && Player == other.Player && Position == other.Position;

        public override bool Equals(object? obj) => obj is Piece piece && ((IEquatable<Piece>)this).Equals(piece);

        public override int GetHashCode() => HashCode.Combine(Player, Position);
    }

    private record Position(int X, int Y);
    private enum Player { Black, White }
}
