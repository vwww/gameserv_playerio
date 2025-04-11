partial class Room {
	partial void PlayerJoined(Player player, ByteReader msg) {
		player.ping.Update(-1);

		var joinMsg = new ByteWriter();
		WriteJoinCommon(player, joinMsg, msg);
		WriteJoin(player, joinMsg, msg);
		Broadcast(joinMsg, player);

		var welcomeMsg = new ByteWriter();
		WriteWelcomeCommon(player, welcomeMsg);
		WriteWelcome(welcomeMsg);
		player.Send(welcomeMsg);
	}

	static void WriteJoinCommon(Player player, ByteWriter joinMsg, ByteReader clientMsg) {
		clientMsg.GetInt(); // protocol version
		player.name = Util.FilterName(clientMsg.GetString(MAX_NAME_LEN), MAX_NAME_LEN);

		joinMsg.PutType(MsgS2C.JOIN);
		joinMsg.PutInt(player.cn);
		joinMsg.PutInt(player.ping.ping);
		joinMsg.PutString(player.name);
	}

	void WriteWelcomeCommon(Player player, ByteWriter welcomeMsg) {
		welcomeMsg.PutType(MsgS2C.WELCOME);
		welcomeMsg.PutInt(PROTOCOL_VERSION);
		welcomeMsg.PutInt(player.cn);
		WriteWelcomeMode(welcomeMsg);

		foreach (var p in players) {
			if (p == null) continue;

			welcomeMsg.PutInt(p.cn);
			welcomeMsg.PutInt((p.ping.ping << 1) | (p.active ? 1 : 0));
			welcomeMsg.PutString(p.name);
			WriteWelcomePlayer(welcomeMsg, p);
		}
		welcomeMsg.PutInt(-1);
	}

	static partial void WriteJoin(Player player, ByteWriter joinMsg, ByteReader clientMsg);
	partial void WriteWelcome(ByteWriter welcomeMsg);

	partial void PlayerLeaving(Player player) {
		if (player.active) {
			player.active = false;
			PlayerDeactivated(player);
		}

		Broadcast(new ByteWriter().PutType(MsgS2C.LEAVE).PutInt(player.cn), player);
	}
}
