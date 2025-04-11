enum MsgS2C {
	WELCOME,
	JOIN,
	LEAVE,
	RESET,
	RENAME,
	PING,
	PING_TIME,
	CHAT,
	ACTIVE,
	ADD_BOT,
	DEL_BOT,
	WORLDSTATE,
	KILL,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	MOVE,
	// QUEUE_SPAWN,
	// DEQUEUE_SPAWN,
}

partial class Room {
	private partial bool ProcessMessage(Player player, ByteReader msg) {
		switch ((MsgC2S)msg.GetInt()) {
			case MsgC2S.RESET: ProcessMsgReset(player); break;
			case MsgC2S.RENAME: ProcessMsgRename(player, msg); break;
			case MsgC2S.PONG: ProcessMsgPong(player, msg); break;
			case MsgC2S.CHAT: ProcessMsgChat(player, msg); break;
			case MsgC2S.ACTIVE: ProcessMsgActive(player); break;
			case MsgC2S.MOVE: ProcessMsgMove(player, msg); break;
			default: return false;
		}
		return true;
	}
}
