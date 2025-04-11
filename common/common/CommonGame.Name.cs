partial class Player {
	public string name;
}

partial class Room {
	const int MAX_NAME_LEN = 20;

	void ProcessMsgRename(Player player, ByteReader msg) {
		var newName = Util.FilterName(msg.GetString(MAX_NAME_LEN), MAX_NAME_LEN);
		if (player.name != newName) {
			player.name = newName;

			Broadcast(new ByteWriter()
				.PutType(MsgS2C.RENAME)
				.PutInt(player.cn)
				.PutString(player.name));
		}
	}
}
