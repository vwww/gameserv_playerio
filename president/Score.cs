partial class Player {
	public PlayerScorePresident score = new();
	public int priority;

	public bool CanResetScore() => score.CanReset();
	public void ResetScore() => score.Reset();

	public void AddResult(int rank, int playerCount) {
		priority = rank;
		score.AddRank(rank, playerCount);
	}
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) => p.score.WriteTo(w);
}

struct PlayerScorePresident {
	public int score = 0;
	public int streak = 0;

	public int rankLast = 0;
	public int roleLast = 0;
	public int[] roleCount = new int[5];

	public PlayerScorePresident() { }

	public bool CanReset() {
		return rankLast != 0;
	}

	public void Reset() {
		score = 0;
		streak = 0;
		rankLast = 0;
		roleLast = 0;
		Array.Clear(roleCount, 0, 5);
	}

	public void AddRank(int rank, int playerCount) {
		rankLast = rank;
		score += playerCount - rank + 1;

		if ((roleLast = RankToRole(rank, playerCount)) == 2) {
			if (streak < 0) {
				streak = 0;
			}
			streak++;
		} else {
			if (streak > 0) {
				streak = 0;
			}
			streak--;
		}
		roleCount[roleLast + 2]++;
	}

	public void WriteTo(ByteWriter b) {
		b.PutInt(score);
		b.PutInt(streak);
		b.PutInt(rankLast);
		b.PutInt(roleLast);
		foreach (var r in roleCount) {
			b.PutInt(r);
		}
	}

	public static int RankToRole(int rank, int totalPlayers) {
		if (rank == 1) return 2;
		if (rank == totalPlayers) return -2;
		if (totalPlayers >= 4) {
			if (rank == 2) return 1;
			if (rank == totalPlayers - 1) return -1;
		}
		return 0;
	}
}
