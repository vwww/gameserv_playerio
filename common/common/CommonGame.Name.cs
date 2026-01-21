using Timer = PlayerIO.GameLibrary.Timer;

partial class Player {
	public string name;
	public string nameNew;
	public DateTime nameChangeTime;
	public Timer nameTimer;
}

partial class Room {
	const int MAX_NAME_LEN = 20;

	void ProcessMsgRename(Player player, ByteReader msg) {
		var newName = Util.FilterName(msg.GetString(MAX_NAME_LEN), MAX_NAME_LEN);
		if (player.nameNew == newName) return;

		player.nameNew = newName;

		player.nameTimer ??= ScheduleCallback(() => {
			lock (players) {
				if (player.cn >= 0 && player.name != player.nameNew) {
					Broadcast(new ByteWriter()
						.PutType(MsgS2C.RENAME)
						.PutInt(player.cn)
						.PutString(player.name = player.nameNew));
				}

				player.nameTimer = null;
				player.nameChangeTime = DateTime.UtcNow;
			}
		}, (int)Math.Max(2000 - (DateTime.UtcNow - player.nameChangeTime).TotalMilliseconds, 25));
	}
}
