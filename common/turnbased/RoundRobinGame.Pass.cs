partial class RoundPlayerInfo {
	public bool passed;
}

partial class Room {
	void NextTrick() {
		UnsetPassed();
		trickNum++;
		trickTurn = 0;
	}

	void NextTurnAfterPass(RoundPlayerInfo p) {
		var nextIndex = NextUnpassed(turnIndex);
		if (nextIndex == turnIndex || passIndex < 0 && NextUnpassed(nextIndex) == turnIndex) {
			turnIndex = passIndex < 0 ? nextIndex : passIndex;
			passIndex = -1;
			NextTrick();
		} else {
			turnIndex = nextIndex;
			p.passed = true;
		}
	}

	int NextUnpassed(int start) {
		var i = start;
		do {
			if (++i == playerInfo.Count) {
				i = 0;
			}
		} while (i != start && playerInfo[i].passed);
		return i;
	}

	void UnsetPassed() {
		foreach (var pl in playerInfo) {
			if (pl == null) continue;
			pl.passed = false;
		}
	}
}
