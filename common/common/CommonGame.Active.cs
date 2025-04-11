partial class Player {
	public bool active;
}

partial class Room {
	byte numActive = 0;

	void ProcessMsgActive(Player player) {
		bool active = !player.active;

		if (active && MAX_PLAYERS_ACTIVE > 0 && numActive >= MAX_PLAYERS_ACTIVE) return;

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.ACTIVE)
			.PutInt(player.cn));

		player.active = active;
		if (active) {
			PlayerActivated(player);
		} else {
			PlayerDeactivated(player);
		}
	}

	void PlayerActivated(Player player) {
		numActive++;
		UpdateActiveCount();
		PlayerActivated2(player);
	}

	void PlayerDeactivated(Player player) {
		numActive--;
		UpdateActiveCount();
		PlayerDeactivated2(player);
	}

	void UpdateActiveCount() {
		RoomData["activeCount"] = numActive.ToString();
		RoomData.Save();
	}
}
