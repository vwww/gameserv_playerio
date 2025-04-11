struct PlayerPing {
	public int ping;

	private int pingSeq;
	private DateTime pingTime;
	private double pingUnrounded;
	private uint missedPings;

	public void Reset() {
		ping = -1;
		pingSeq = -1;
		pingTime = DateTime.UtcNow;
		missedPings = 0;
	}

	public int Next() {
		pingTime = DateTime.UtcNow;
		missedPings++;
		return ++pingSeq;
	}

	public void CheckMissedPings(int interval) {
		if (missedPings > 0) {
			UpdatePing(missedPings * interval);
		}
	}

	public bool Update(int seq) {
		if (missedPings > 0) {
			missedPings--;
		}

		if (seq != pingSeq) {
			return false;
		}

		double newPing = (DateTime.UtcNow - pingTime).TotalMilliseconds;
		UpdatePing(newPing);

		return true;
	}

	private void UpdatePing(double newPing) {
		pingUnrounded = ping < 0 ? newPing : ((pingUnrounded * 0.8) + (0.2 * newPing));
		ping = (int)pingUnrounded;
	}
}
