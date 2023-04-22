using System.Text.Json;
using Spectre.Console;

namespace HexapawnAI;

public class Program
{
    private static void Main()
    {
        var states = new List<Game.State>();
        while (true)
        {
            states = new Game().Simulate(states).ToList();
        }
    }
}

public class Game
{
    private const int Size = 3;
    private const double Reward = 0.1;
    private const double InitialWeight = 1;
    private const double LowerBound = 0.1;
    private List<State> states;
    private State CurrentState => states.ElementAt(currentState);
    private int currentState;
    private Player currentPlayer;
    private Player OtherPlayer => currentPlayer == Player.White ? Player.Black : Player.White;
    private static int currentGame;
    private static State InitialState => new()
    {
        PlayerToMove = Player.White,
        Weight = InitialWeight,
        Game = currentGame
    };

    public Game()
    {
        currentState = 0;
        currentPlayer = Player.White;
        states = new() { InitialState };
    }

    public IEnumerable<State> Simulate(IEnumerable<State> previousStates)
    {
        if (previousStates.Any())
        {
            states = previousStates
                .Select(state => (State)state.Clone())
                .ToList();
            states.First().Game = currentGame;
        }
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine($"[bold white]Current player: {currentPlayer}[/]");
            AnsiConsole.MarkupLine($"[bold green1]Current state (state {currentState + 1}):[/]");
            AnsiConsole.MarkupLine(CurrentState.Display(1));
            var (winner, nextStates, winReason) = Step();
            if (winner != null)
            {
                AnsiConsole.MarkupLine($"[bold dodgerblue1]Player {winner} wins! [[{winReason}]][/]");
                foreach (var state in states.Where(state => state.Game == currentGame))
                {
                    if (winner == state.CurrentPlayer)
                        state.Weight += Reward;
                    else
                    {
                        state.Weight -= Reward;
                        if (state.Weight < LowerBound)
                            state.Weight = LowerBound;
                    }
                }
                break;
            }
            foreach (var (state, index, chance) in nextStates.Select((pair, index) => (pair.Key, index, pair.Value)))
            {
                AnsiConsole.MarkupLine($"    [bold orange3]Next state {index + 1} (chance {chance:P2}):[/]");
                AnsiConsole.MarkupLine(state.Display(2));
            }
        }
        currentGame++;
        return states;
    }

    private (Player? winner, IDictionary<State, double> nextStates, WinReason? reason) Step()
    {
        var winningPlayer = CurrentState.CheckLastRankReached();
        var nextStates = CurrentState.GetNextStates().ToList();
        foreach (var nextState in nextStates)
        {
            State? state = states.Find(state => state.Equals(nextState));
            nextState.Weight = state?.Weight ?? InitialWeight;
            if (state != null)
                state.Game = currentGame;
                nextState.Game = currentGame;
        }
        var chances = State.GetStateChances(nextStates);
        var dictionary = Enumerable
                .Zip(nextStates, chances)
                .ToDictionary(pair => pair.First, pair => pair.Second);
        if (winningPlayer != null)
            return (winningPlayer, dictionary, WinReason.LastRankReached);
        if (nextStates.Count == 0)
            return (OtherPlayer, dictionary, WinReason.NoMovesLeft);

        var chosenState = State.ChooseState(nextStates);

        if (!states.Contains(chosenState))
            states.Insert(currentState + 1, chosenState);
        currentState++;
        CurrentState.PlayerToMove = currentPlayer = OtherPlayer;
        return (null, dictionary, null);
    }

    public class State : ICloneable, IEquatable<State>
    {
        private List<Piece> pieces = new();
        public required double Weight { get; set; }
        public required Player PlayerToMove { get; set; }
        public Player CurrentPlayer => PlayerToMove == Player.White ? Player.Black : Player.White;
        public required int Game { get; set; }

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

        public IEnumerable<State> GetNextStates()
            => pieces
                .Where(piece => piece.Player == PlayerToMove)
                .SelectMany(
                    piece => GetNextPositions(piece)
                        .Where(position => TryMovePiece(piece, position, out _))
                        .Select(position =>
                            {
                                TryMovePiece(piece, position, out var state);
                                return state!;
                            }
                        )
                );

        private bool TryMovePiece(Piece piece, Position position, out State? state)
        {
            var movingDiagonally = position.X != piece.Position.X;
            if (movingDiagonally)
            {
                if (!pieces.Any(piece => piece.Position == position && piece.Player != PlayerToMove))
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

            var clone = (State)Clone();
            clone.Game = currentGame;
            clone.PlayerToMove = CurrentPlayer;
            clone.pieces.Remove(piece);
            clone.pieces.Add(new Piece(piece.Player, position));

            if (movingDiagonally && clone.pieces.Any(piece => piece.Position == position && piece.Player == CurrentPlayer))
            {
                clone.pieces.Remove(
                    clone.pieces
                        .Where(piece => piece.Player == CurrentPlayer && piece.Position == position)
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

        public static State ChooseState(IEnumerable<State> nextStates)
        {
            var weights = nextStates
                .Select(state => state.Weight);
            var accumulatedWeights =
                weights
                    .Aggregate(
                        new List<double>(),
                        (list, weight) =>
                            list.Concat(new[] { list.LastOrDefault() + weight }).ToList()
                    );
            var randomWeight = new Random().NextDouble() * accumulatedWeights.LastOrDefault();
            var index = accumulatedWeights.FindIndex(weight => randomWeight < weight);
            return nextStates.ElementAt(index);
        }

        public static IEnumerable<double> GetStateChances(IEnumerable<State> states)
            => states.Select(state => state.Weight / states.Sum(state => state.Weight));

        public object Clone() => new State
        {
            pieces = pieces.Select(piece => (Piece)piece.Clone()).ToList(),
            PlayerToMove = PlayerToMove,
            Weight = Weight,
            Game = Game
        };

        public bool Equals(State? other)
        {
            if (other == null)
                return false;
            if (pieces.Count != other.pieces.Count)
                return false;
            if (PlayerToMove != other.PlayerToMove)
                return false;
            return pieces.All(other.pieces.Contains);
        }

        public override bool Equals(object? obj) => obj is State state && Equals(state);

        public override int GetHashCode() => HashCode.Combine(pieces, PlayerToMove);

        public override string ToString()
            => $"{pieces.Aggregate(
                pieces.First().GetHashCode(),
                (hash, piece) => hash ^ piece.GetHashCode()
            )} {PlayerToMove} {Weight} {Game}";
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

    public enum Player { Black, White }

    public enum WinReason { LastRankReached, NoMovesLeft }
}
