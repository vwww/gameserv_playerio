partial class Room {
	void ProcessMsgReset(Player player) {
		if (player.CanResetScore()) {
			player.ResetScore();

			Broadcast(new ByteWriter()
						.PutType(MsgS2C.RESET)
						.PutInt(player.cn));
		}
	}
}
