static class RPSBattle {
	public static int[] CalculateBattleResults(ulong[] count, bool modeInverted, bool modeCount) {
		var defaultResult = modeInverted ? 1 : -1;
		var battleResult = new int[] { defaultResult, defaultResult, defaultResult };
		if (modeCount) {
			for (int i = 0; i < 3; i++) {
				var a = count[i];
				var b = count[(i + 1) % 3];
				if (defaultResult == -1 && a > b) {
					battleResult[i] = a >= 2 * b ? 1 : 0;
				} else if (defaultResult == 1 && b > a) {
					battleResult[i] = b >= 2 * a ? -1 : 0;
				}
			}
		}
		return battleResult;
	}

	public static ulong[][] CalculateLTW(ulong[] count, int[] battleResults) {
		var ltw = new ulong[3][];
		for (int i = 0; i < 3; i++) {
			ltw[i] = new ulong[3];
			if (count[i] != 0) {
				ltw[i][1] += count[i] - 1;
				ltw[i][1 + battleResults[i]] += count[(i + 1) % 3];
				ltw[i][1 - battleResults[(i + 2) % 3]] += count[(i + 2) % 3];
			}
		}
		return ltw;
	}

	public static List<byte> CalculateBestMoves(ulong[] count, ulong extraCount, bool modeInverted, bool modeCount) {
		var bestMoves = new List<byte>();
		var bestScore = long.MinValue;
		for (byte i = 0; i < 3; i++) {
			var countWithMove = (ulong[])count.Clone();
			count[i] += extraCount;

			var battleResults = CalculateBattleResults(countWithMove, modeInverted, modeCount);
			var ltw = CalculateLTW(countWithMove, battleResults)[i];

			var score = (long)ltw[2] - (long)ltw[0];

			if (score > bestScore) {
				bestMoves.Clear();
				bestScore = score;
			}
			if (score >= bestScore) {
				bestMoves.Add(i);
			}
		}
		return bestMoves;
	}
}
