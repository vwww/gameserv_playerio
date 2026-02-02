partial class Player {
	public DateTime nextChatAllow;
}

partial class Room {
	const int MAX_CHAT_LEN = 100;

	void ProcessMsgChat(Player player, ByteReader msg) {
		var flags = msg.GetInt();
		var target = msg.GetInt();
		var text = msg.GetString(MAX_CHAT_LEN);
		HandleChat(player, text, flags, target);
	}

	void HandleChat(Player player, char[] text, int flags, int target) {
		const int SAY_TARGET_ALL = 0;
		const int SAY_TARGET_PRIVATE = 1;
		const int SAY_TARGET_TEAM = 2;
		const int SAY_TARGET_RESERVED = 3;
		const int SAY_TARGET = 3;
		// const int SAY_ACTION = 1 << 2;
		const int SAY_CLIENT = (1 << 3) - 1;
		const int SAY_DENY_SPAM = 1 << 3;

		if (text.Length == 0 || (text = Util.FilterChat(text)).Length == 0) return;

		flags &= SAY_CLIENT; // filter it out
		var sayTarget = flags & SAY_TARGET;

		if (players.ElementAtOrDefault(target) == null) {
			// route invalid targets to sender
			target = player.cn;
		}

		var b = new ByteWriter()
			.PutType(MsgS2C.CHAT)
			.PutInt(player.cn);

		// rate-limit to fixed 1-second interval
		var disallow = DateTime.UtcNow < player.nextChatAllow;
		if (disallow) {
			flags |= SAY_DENY_SPAM;
		} else {
			player.nextChatAllow = DateTime.UtcNow + TimeSpan.FromSeconds(1);
		}

		b.PutInt(flags);
		if (sayTarget == SAY_TARGET_PRIVATE) {
			b.PutInt(target);
		}
		b.PutString(text);

		if (disallow) {
			player.Send(b);
			return;
		}

		switch (sayTarget) {
			case SAY_TARGET_ALL:
			case SAY_TARGET_RESERVED:
			default:
				Broadcast(b);
				break;

			case SAY_TARGET_PRIVATE:
				player.Send(b);
				if (player.cn != target) {
					players[target].Send(b);
				}
				break;

			case SAY_TARGET_TEAM: // reserved for future teams
				foreach (var p in players) {
					if (p != null && (p == player || /* IsSameTeam(c, player) */ p.cn == target)) {
						p.Send(b);
					}
				}
				break;
		}
	}
}
