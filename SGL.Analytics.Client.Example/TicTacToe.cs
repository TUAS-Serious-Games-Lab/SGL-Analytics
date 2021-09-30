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
	}
}
