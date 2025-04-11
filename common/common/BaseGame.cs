partial class Player : BasePlayer {
	public int cn = -1; // -1: pending connection, -2: disconnected
}

partial class Room : Game<Player> {
	const int MAX_PLAYERS = 64;
	const int CONNECTION_TIMEOUT = 10000;
	readonly Player[] players = new Player[MAX_PLAYERS];
	byte numPlayers = 0;

	partial void Setup();
	partial void PlayerJoined(Player player, ByteReader msg);
	partial void PlayerLeaving(Player player);
	private partial bool ProcessMessage(Player player, ByteReader msg);

	public override void GameStarted() {
		lock (players) {
			Setup();
		}
	}

	public override void UserJoined(Player player) {
		player.ping.Reset();

		ScheduleCallback(() => { // double-checked locking
			if (player.cn == -1) {
				lock (players) {
					if (player.cn == -1) {
						// disconnect: timeout
						player.Disconnect();
						player.cn = -2;
					}
				}
			}
		}, CONNECTION_TIMEOUT);
	}

	public override void UserLeft(Player player) {
		lock (players) {
			if (player.cn >= 0) {
				PlayerLeaving(player);

				players[player.cn] = null;
				numPlayers--;
			}
			player.cn = -2;
		}
	}

	public override void GotMessage(Player player, Message message) {
		if (message.Count == 0) return; // ignore empty message

		byte[] b;
		try {
			b = message.GetByteArray(0);
		} catch {
			// ignore if first item is not byte[]
			return;
		}

		lock (players) {
			if (player.cn >= 0) {
				// handle message
				var msg = new ByteReader(b);
				while (msg.Remaining != 0) {
					if (!ProcessMessage(player, msg) || msg.Overread) {
						// disconnect: tag type or overread
						player.Disconnect();
					}
				}
			} else if (player.cn == -1) {
				for (int i = 0; i < MAX_PLAYERS; i++) {
					if (players[i] == null) {
						players[i] = player;
						player.cn = i;
						numPlayers++;

						// player join
						PlayerJoined(player, new ByteReader(b));
						return;
					}
				}

				// disconnect: too many players
				player.Disconnect();
				player.cn = -2;
			}
		}
	}

	public void Broadcast(byte[] msg, Player except = null) {
		foreach (var p in players) {
			if (p == null || p == except) continue;

			p.Send(msg);
		}
	}
}
