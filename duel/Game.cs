[RoomType("DuelRoom")]
partial class Room {
	const int PROTOCOL_VERSION = 0;

	partial void Setup() {
		ParseMode();

		for (var i = 0; i < duelPlayers.Length; i++) {
			var p = duelPlayers[i] = new DuelPlayer();
			p.Reset();

			if (i < optBotBalance) {
				p.InitBot(rng.NextByte());
			}
		}

		AddTimer(PingClients, PING_CLIENTS_INTERVAL);
		AddTimer(GameLoop, 1000 / NETW_FPS);
	}

	const int MAX_PLAYERS_ACTIVE = 0;

	readonly RNG rng = new();

	static partial void WriteJoin(Player player, ByteWriter joinMsg, ByteReader clientMsg) {
		joinMsg.Add(player.hue = clientMsg.Get());
	}

	partial void WriteWelcome(ByteWriter w) {
		for (var i = 0; i < duelPlayers.Length; i++) {
			var p = duelPlayers[i];

			if (!p.IsValid) continue;

			w.PutULong((((ulong)i + 1) << 1) | (p.owner < 0 ? 1u : 0u));
			w.PutULong(p.Kills);
			w.PutULong(p.Deaths);
			w.PutULong(p.Combo);
			w.PutULong(p.Score);

			if (p.owner < 0) {
				w.Add(p.hue);
			}
		}

		w.Add(0);
	}

	void PlayerActivated2(Player player) {
		var p = duelPlayers[player.cn];

		if (numActive <= optBotBalance && !p.IsValid) {
			var bot = Array.FindLastIndex(duelPlayers, p => p.IsValid && p.owner < 0);
			if (bot >= 0) {
				duelPlayers[bot].Reset();

				Broadcast(new ByteWriter()
					.PutType(MsgS2C.DEL_BOT)
					.PutULong((ulong)bot)
				);
			}
		}

		p.InitPlayer(player.cn);
	}

	void PlayerDeactivated2(Player player) {
		var p = duelPlayers[player.cn];
		p.Reset();

		if (numActive < optBotBalance) {
			p.InitBot(rng.NextByte());

			var msg = new ByteWriter()
				.PutType(MsgS2C.ADD_BOT)
				.PutULong((ulong)player.cn);
			msg.Add(p.hue);
			Broadcast(msg);
		}
	}

	void ProcessMsgMove(Player player, ByteReader msg) {
		if (!player.active) return;

		var p = duelPlayers[player.cn];
		p.D.x = msg.GetUShort() * (MAX_W / 0xFFFF);
		p.D.y = msg.GetUShort() * (MAX_H / 0xFFFF);
	}

	// Timing constants
	// Physics frames per second
	const int PHYS_FPS = 40;
	// Network world states per second
	const int NETW_FPS = 25;

	// Arena constants
	const double MAX_W = 1600.0;
	const double MAX_H = 900.0;

	// Player constants
	// Movement speed (per second)
	const double PL_SPEED = 200.0;

	// Starting size
	const uint PL_RAD_START = 20;

	// Minimum size
	const uint PL_RAD_MIN = 15;

	// Maximum size
	const uint PL_RAD_MAX = 900;

	// Decay (factor of 1 / 2**(-10) per frame)
	const double PL_MASS_DECAY_FACTOR = 0.9990234375;

	readonly DuelPlayer[] duelPlayers = new DuelPlayer[MAX_PLAYERS];

	// DateTime gameStart = DateTime.UtcNow;
	DateTime lastPhysics = DateTime.UtcNow;

	void GameLoop() {
		lock (players) {
			var now = DateTime.UtcNow;

			// Apply physics
			if (now > lastPhysics) {
				PhysicsFrame();
				lastPhysics += TimeSpan.FromMilliseconds(1000.0 / PHYS_FPS);
			}

			// Send world state
			Broadcast(BuildWorldState());
		}
	}

	void PhysicsFrame() {
		for (var i = 0; i < duelPlayers.Length; i++) {
			var p = duelPlayers[i];
			if (!p.IsValid) {
				continue;
			} else if (p.IsAlive) {
				if (p.owner < 0) {
					BotThinkPlayer(p);
				}
				p.MovePlayer();
				p.DecayPlayer(massMin, invDimension);

				// check only against higher players,
				// to avoid double-checking
				for (var j = i + 1; j < duelPlayers.Length; j++) {
					var b = duelPlayers[j];
					if (!b.IsAlive) {
						continue;
					}
					CheckCollision(p, b, i, j);
					// Stop if the player died
					if (!p.IsAlive) {
						break;
					}
				}
			} else {
				// force spawn (use spawn queue in future?)
				SpawnPlayer(p);
			}
		}
	}

	byte[] BuildWorldState() {
		var worldState = new ByteWriter()
			.PutType(MsgS2C.WORLDSTATE);

		for (var pn = 0; pn < duelPlayers.Length; pn++) {
			var p = duelPlayers[pn];
			if (!p.IsAlive) continue;

			worldState
				.PutULong((ulong)pn + 1)
				.PutUShort((ushort)(p.O.x * (0xFFFF / MAX_W)))
				.PutUShort((ushort)(p.O.y * (0xFFFF / MAX_H)))
				.PutUShort((ushort)(p.D.x * (0xFFFF / MAX_W)))
				.PutUShort((ushort)(p.D.y * (0xFFFF / MAX_H)))
				.PutFloat64(p.M);
		}
		worldState.Add(0);

		return worldState.ToArray();
	}

	class DuelPlayer {
		public Vec2 D; // Destination

		public Vec2 O; // Origin
		public double M; // Mass
		public double R; // Radius

		public ulong Kills, Deaths, Combo;
		public bool IsAlive;

		public ulong Score;

		public int owner;
		public uint BotDivider;
		public byte hue; // for bots
		public bool IsValid;

		public void Reset() {
			owner = -1;
			IsValid = false;
			IsAlive = false;
		}

		private void Init() {
			Kills = 0;
			Deaths = 0;
			IsAlive = false;
			IsValid = true;
			Score = 0;
		}

		public void InitPlayer(int owner) {
			Init();
			this.owner = owner;
		}

		public void InitBot(byte hue) {
			Init();
			// owner = -1;
			this.hue = hue;
		}

		public void SetMass(double mass, double invDimension) {
			M = mass;
			R = invDimension == 0.5 ? Math.Sqrt(mass) : Math.Pow(mass, invDimension);
		}

		public void MovePlayer() {
			var diff = D - O;
			const double moveDist = PL_SPEED / PHYS_FPS;
			if (moveDist * moveDist < diff.LengthSquared()) {
				diff = diff.Normalize() * moveDist;
			}

			O += diff;
		}

		public void DecayPlayer(double massMin, double invDimension) {
			var newMass = Math.Max(massMin, M * PL_MASS_DECAY_FACTOR);
			SetMass(newMass, invDimension);
		}

		public bool Collide(DuelPlayer b, double overlapSmall) {
			var diff = O - b.O;
			var dist = overlapSmall == 1 ? R + b.R : Math.Max(R, b.R) + overlapSmall * Math.Min(R, b.R);
			return diff.x <= dist &&
				diff.y <= dist &&
				diff.LengthSquared() <= dist * dist;
		}
	}

	void BotThinkPlayer(DuelPlayer p) {
		if (p.BotDivider == 0) {
			p.BotDivider = PHYS_FPS * 250 / 1000;
		} else {
			p.BotDivider--;
			return;
		}
		var best = p.D;
		var bestDist2 = 1e200;
		foreach (var pp in duelPlayers) {
			if (!pp.IsAlive || p == pp || pp.M > p.M) {
				continue;
			}
			var dist2 = (p.O - pp.O).LengthSquared();
			if (bestDist2 > dist2) {
				best = pp.O;
				bestDist2 = dist2;
			}
		}
		p.D = best;
	}

	void CheckCollision(DuelPlayer a, DuelPlayer b, int aCn, int bCn) {
		if (!a.Collide(b, overlapSmall)) {
			return;
		}

		// Calculate probability that Player A wins
		var p = randomWeight + (double)a.M / (a.M + b.M) * skillWeight;
		var aIsBot = a.owner < 0;
		var bIsBot = b.owner < 0;
		if (aIsBot != bIsBot) {
			p *= optBotWeightInv;
			if (bIsBot) {
				p += optBotWeight;
			}
		}
		if (rng.NextDouble() >= p) {
			// Player B wins
			Util.Swap(ref a, ref b);
			Util.Swap(ref aCn, ref bCn);
			Util.Swap(ref aIsBot, ref bIsBot);
		}

		// 75% of mass is transferable
		var newMass = Math.Min(a.M + b.M * transferRatio, massMax);
		a.SetMass(newMass, invDimension);

		a.Kills++;
		a.Combo++;
		a.Score += a.Combo;
		b.Deaths++;
		b.Combo = 0;
		b.IsAlive = false;

		if (!aIsBot) {
			var ac = players[a.owner];
			ac.kills++;
			ac.score += a.Combo;
		}
		if (!bIsBot) {
			var bc = players[b.owner];
			bc.deaths++;
		}

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.KILL)
			.PutULong((ulong)aCn)
			.PutULong((ulong)bCn));
	}

	void SpawnPlayer(DuelPlayer p) {
		// brute-force spawn position
		for (var i = 0; i < 256; i++) {
			p.O.x = rng.NextDouble() * MAX_W;
			p.O.y = rng.NextDouble() * MAX_H;

			if (!duelPlayers.Any(pp => p.Collide(pp, overlapSmall))) {
				break;
			}
		}

		p.D.x = p.O.x;
		p.D.y = p.O.y;
		p.M = massStart;
		p.R = PL_RAD_START;
		p.IsAlive = true;
		p.BotDivider = 0;
	}
}
