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
	END_TURN,
	OFFER_DRAW,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	READY,
	MOVE,
	MOVE_END,
	FORFEIT,
	OFFER_DRAW,
	REJECT_DRAW,
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
			case MsgC2S.MOVE: ProcessMsgMove(player, msg); break;
			case MsgC2S.MOVE_END: ProcessMsgMoveEnd(player); break;
			case MsgC2S.FORFEIT: ProcessMsgForfeit(player); break;
			case MsgC2S.OFFER_DRAW: ProcessMsgOfferDraw(player); break;
			case MsgC2S.REJECT_DRAW: ProcessMsgRejectDraw(player); break;
			default: return false;
		}
		return true;
	}
}
