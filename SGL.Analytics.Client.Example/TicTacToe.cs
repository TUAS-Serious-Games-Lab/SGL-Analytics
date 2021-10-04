using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Example {

	public enum Side { Empty, X, O }
	public enum GameState { Running, XWon, OWon, Tie }

	public class TicTacToe {
		private Side nextTurn = Side.X;
		private Side[] cells = Enumerable.Repeat(Side.Empty, 9).ToArray();
		private GameState state = GameState.Running;

		public Side this[int column, int row] {
			get {
				return cells[row * 3 + column];
			}
			internal set {
				if (cells[row * 3 + column] != Side.Empty) throw new InvalidOperationException("The given cell is already set.");
				cells[row * 3 + column] = value;
			}
		}

		private Side checkWinner() {
			var rng3 = Enumerable.Range(0, 3);
			var lines = rng3.Select(column => rng3.Select(row => this[column, row])) // columns
					.Concat(rng3.Select(row => rng3.Select(column => this[column, row]))) // rows
					.Append(rng3.Select(diag => this[diag, diag])) // first diagonal
					.Append(rng3.Select(diag => this[diag, rng3.Count() - diag - 1])); // second diagonal
			var winnersOrEmpty = lines.Select(line => line.Distinct().SingleOrDefaultNoExcept());
			return winnersOrEmpty.FirstOrDefault(candiate => candiate != Side.Empty);
		}

		private bool checkFull() {
			return cells.All(cell => cell != Side.Empty);
		}

		private Side takeTurn() {
			var player = nextTurn;
			nextTurn = nextTurn switch {
				Side.X => Side.O,
				Side.O => Side.X,
				_ => throw new InvalidOperationException("Game is already over.")
			};
			return player;
		}

		public Side NextTurn => nextTurn;
		public GameState State => state;

		public void Reset() {
			nextTurn = Side.X;
			cells = Enumerable.Repeat(Side.Empty, 9).ToArray();
			state = GameState.Running;
		}

		public GameState MakeMove(int columnOneBased, int rowOneBased) {
			var (column, row) = (columnOneBased - 1, rowOneBased - 1);
			var player = takeTurn();
			this[column, row] = player;
			var winner = checkWinner();
			switch (winner) {
				case Side.X: return state = GameState.XWon;
				case Side.O: return state = GameState.OWon;
				default: break;
			}
			if (checkFull()) return state = GameState.Tie;
			return GameState.Running;
		}

		public async Task PrintBoardAsync(TextWriter writer) {
			var separator = new string('-', 7);
			await writer.WriteLineAsync(separator);
			var rng3 = Enumerable.Range(0, 3);
			var symbolRows = rng3.Select(rowIndex => rng3.Select(columnIndex => this[columnIndex, rowIndex]))
				.Select(row => row.Select(c => c switch {
					Side.X => 'X',
					Side.O => 'O',
					Side.Empty => ' ',
					_ => '?'
				}));
			var textRows = symbolRows.Select(row => $"|{string.Join('|', row)}|");
			foreach (var row in textRows) {
				await writer.WriteLineAsync(row);
				await writer.WriteLineAsync(separator);
			}
		}

		public BoardSnapshot TakeSnapshot() {
			return new BoardSnapshot(nextTurn,
				// Deep-copy cells
				cells.Select(c => c).ToArray());
		}
	}

	public class BoardSnapshot {
		public Side NextTurn { get; set; }
		public Side[] Cells { get; set; }

		public BoardSnapshot(Side nextTurn, Side[] cells) {
			NextTurn = nextTurn;
			Cells = cells;
		}
	}

	public class MoveEvent {
		public Side Side { get; set; }
		public int Column { get; set; }
		public int Row { get; set; }

		public MoveEvent(Side side, int column, int row) {
			Side = side;
			Column = column;
			Row = row;
		}
	}

	public enum Winner {
		X, O, Tie
	}

	public class GameOverEvent {
		public Winner Winner { get; set; }

		public GameOverEvent(Winner winner) {
			Winner = winner;
		}
	}

	public class ErrorEvent {
		public string Message { get; set; }
		public string ExceptionType { get; set; }
		public string? StackTrace { get; set; }

		public ErrorEvent(string message, string exceptionType, string? stackTrace) {
			Message = message;
			ExceptionType = exceptionType;
			StackTrace = stackTrace;
		}
	}

	public class TicTacToeController {
		private TicTacToe board = new TicTacToe();
		private SGLAnalytics analytics;
		private bool verbose;
		private TextWriter output;
		public List<Guid> LogIds { get; } = new List<Guid>();

		public TicTacToeController(SGLAnalytics analytics, bool verbose, TextWriter output) {
			this.analytics = analytics;
			this.verbose = verbose;
			this.output = output;
		}

		public async Task ProcessMove(int columnOneBased, int rowOneBased) {
			analytics.RecordSnapshotUnshared("Board_Snapshots", 1, board.TakeSnapshot());
			analytics.RecordEventUnshared($"{board.NextTurn}_Moves", new MoveEvent(board.NextTurn, columnOneBased, rowOneBased));
			await output.WriteLineAsync($"{board.NextTurn} to {columnOneBased},{rowOneBased}");
			var state = board.MakeMove(columnOneBased, rowOneBased);
			Winner winner = Winner.Tie;
			switch (state) {
				case GameState.XWon:
					await output.WriteLineAsync("X won the game!");
					winner = Winner.X;
					break;
				case GameState.OWon:
					await output.WriteLineAsync("O won the game!");
					winner = Winner.O;
					break;
				case GameState.Tie:
					await output.WriteLineAsync("The game ended in a tie!");
					break;
				default:
					return; // No game over yet, next turn
			}
			// The game is over, record game over event, reset board, start a new game log
			analytics.RecordEventUnshared("Game_Over", new GameOverEvent(winner));
			board.Reset();
			LogIds.Add(analytics.StartNewLog());
		}

		public async Task ReadAndProcessMoves(TextReader reader) {
			try {
				LogIds.Add(analytics.StartNewLog());
				string? line = "";
				if (verbose) await board.PrintBoardAsync(output);
				await output.WriteAsync($"{board.NextTurn}'s move: ");
				while ((line = await reader.ReadLineAsync()) != null) {
					if (string.IsNullOrWhiteSpace(line)) continue;
					if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) return;
					var numbers = line.Split(',').Select(part => int.Parse(part)).ToList();
					var column = numbers.Take(1).Single();
					var row = numbers.Skip(1).Single();
					await ProcessMove(column, row);
					if (verbose) await board.PrintBoardAsync(output);
					await output.WriteAsync($"{board.NextTurn}'s move: ");
				}
			}
			catch (Exception ex) {
				analytics.RecordEventUnshared("Errors", new ErrorEvent(ex.Message, ex.GetType().Name, ex.StackTrace));
				await Console.Error.WriteLineAsync($"\nError: {ex.Message}");
				Environment.ExitCode = 1;
			}
		}
	}
}
