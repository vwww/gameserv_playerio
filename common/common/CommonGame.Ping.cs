partial class Player {
	public PlayerPing ping = new();
}

partial class Room {
	const int PING_CLIENTS_INTERVAL = 3000;

	void ProcessMsgPong(Player player, ByteReader msg) {
		player.ping.Update(msg.GetInt());
	}

	void PingClients() {
		lock (players) {
			foreach (var p in players) {
				p?.ping.CheckMissedPings(PING_CLIENTS_INTERVAL);
			}

			foreach (var p in players) {
				p?.Send(new ByteWriter()
					.PutType(MsgS2C.PING)
					.PutInt(p.ping.Next())
				);
			}

			var b = new ByteWriter();
			b.PutType(MsgS2C.PING_TIME);
			foreach (var p in players) {
				if (p == null) continue;
				b.PutInt(p.cn);
				b.PutInt(p.ping.ping);
			}
			b.PutInt(-1);
			Broadcast(b);
		}
	}
}
