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
	ROUND_WAIT,
	ROUND_INTERM,
	ROUND_START,
	READY,
	END_ROUND,
	MOVE_CONFIRM,
	MOVE_READY,
	END_TURN,
	PLAYER_ELIMINATE,
	PLAYER_ELIMINATE_EARLY = PLAYER_ELIMINATE,
	PLAYER_PRIVATE_INFO_HAND,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	READY,
	MOVE_KEEP,
	MOVE_PLAY,
	MOVE_PRE,
	MOVE_POST,
}

partial class Room {
	private partial bool ProcessMessage(Player player, ByteReader msg) {
		switch ((MsgC2S)msg.GetInt()) {
			case MsgC2S.RESET: ProcessMsgReset(player); break;
			case MsgC2S.RENAME: ProcessMsgRename(player, msg); break;
			case MsgC2S.PONG: ProcessMsgPong(player, msg); break;
			case MsgC2S.CHAT: ProcessMsgChat(player, msg); break;
			case MsgC2S.ACTIVE: ProcessMsgActive(player); break;
			case MsgC2S.READY: ProcessMsgReady(player); break;
			case MsgC2S.MOVE_KEEP: ProcessMsgMoveKeep(player, msg); break;
			case MsgC2S.MOVE_PLAY: ProcessMsgMovePlay(player, msg); break;
			case MsgC2S.MOVE_PRE: ProcessMsgMoveReady(player, false); break;
			case MsgC2S.MOVE_POST: ProcessMsgMoveReady(player, true); break;
			default: return false;
		}
		return true;
	}
}
