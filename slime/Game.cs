[RoomType("SlimeRoom")]
partial class Room {
	const int PROTOCOL_VERSION = 0;

	partial void Setup() {
		ParseMode();
		GameInit();

		AddTimer(PingClients, PING_CLIENTS_INTERVAL);
		AddTimer(GameLoop, 1000 / NETW_FPS);

		gameStart = DateTime.UtcNow;
	}

	const int MIN_PLAYERS_ACTIVE = 2;
	const int MAX_PLAYERS_ACTIVE = 2;

	readonly RNG rng = new();

	static partial void WriteJoin(Player player, ByteWriter joinMsg, ByteReader clientMsg) {
		var colorHighBit = clientMsg.Get();
		var colorLowBits = clientMsg.GetUShort();

		player.color = (uint)(colorHighBit << 16) | colorLowBits;

		joinMsg.Add(colorHighBit);
		joinMsg.PutUShort(colorLowBits);
	}

	partial void WriteWelcome(ByteWriter w) {
		w.PutULong((ulong)(DateTime.UtcNow - gameStart).TotalMilliseconds);
		w.PutULong((ulong)(DateTime.UtcNow - roundStart).TotalMilliseconds);
		w.Add((byte)((numActive == 2 && p1CN > p2CN ? 4 : 0) | winner));
		w.PutULong(p1.score);
		w.PutULong(p2.score);
		PutWorldState(w);
	}

	void PlayerActivated2(Player player) {
		(numActive == 2 ? ref p2CN : ref p1CN) = player.cn;
		GameInit();
	}

	void PlayerDeactivated2(Player player) {
		player.score.AddLoss();

		if (numActive == 1) {
			if (p1CN == player.cn) {
				p1CN = p2CN;
			}
			players[p1CN].score.AddWin();
		}

		GameInit();
	}

	void ProcessMsgMove(Player player, ByteReader msg) {
		var flags = msg.Get();

		if (!player.active) return;

		ProcessMove(ref p1CN == player.cn ? ref p1 : ref p2, flags);
	}

	void ProcessMove(ref SlimePlayer p, byte flags) {
		p.L = (flags & (1 << 0)) != 0;
		p.R = (flags & (1 << 1)) != 0;
		p.U = (flags & (1 << 2)) != 0;
	}

	// Timing constants
	// Physics frames per second
	const int PHYS_FPS = 120;
	// Network world states per second
	const int NETW_FPS = 40;

	// Net constants
	const double NET_W = 0.02;
	const double NET_H = 0.175;

	// Ball constants
	// Maximum horizontal velocity after collision
	const double BALL_POST_COLLISION_VEL_X_MAX = 0.9375;
	// Maximum vertical velocity after collision
	const double BALL_POST_COLLISION_VEL_Y_MAX = 1.375;
	// Gravitational acceleration
	const double BALL_GRAV_ACCEL = 3.125;

	// Radius
	const double RAD_BALL = 0.03;

	// Player constants
	// Horizontal movement speed
	const double PL_SPEED_X = 0.5;
	// Initial vertical speed when jumping
	const double PL_VEL_JUMP = 1.9375;
	// Gravitational acceleration
	const double PL_GRAV_ACCEL = 6.25;

	// Radius
	const double RAD_PL = 0.1;

	int p1CN, p2CN;
	SlimePlayer p1, p2;
	SlimeBall ball;

	int winner;
	bool p1First;

	DateTime gameStart;
	DateTime roundStart;
	DateTime lastPhysics;
	DateTime intermissionEnd;

	void StartRound(bool p1First) {
		p1.o.x = 0.45;
		p1.o.y = 0;
		p1.v.x = 0;
		p1.v.y = 0;
		p2.o.x = 1.55;
		p2.o.y = 0;
		p2.v.x = 0;
		p2.v.y = 0;

		ball.o.x = (p1First ? p1 : p2).o.x;
		ball.o.y = 0.4;
		ball.v.x = 0;
		ball.v.y = 0;

		winner = 0;
		roundStart = lastPhysics = DateTime.UtcNow;
	}

	void MovePlayer(ref SlimePlayer p, bool left) {
		// simple horizontal movements
		if (p.L != p.R) {
			if (p.L == left) {
				p.v.x = -playerSpeedX;
			} else {
				p.v.x = +playerSpeedX;
			}
		} else {
			p.v.x = 0;
		}
		// can jump on floor
		if (p.U && p.o.y == 0) {
			p.v.y += playerSpeedY;
		}

		// Move X
		p.o.x += p.v.x * gameSpeed;
		if (MathUtil.Clamp(
		ref p.o.x,
		left ? radiusPlayer : 1.0 + radiusPlayer + netWidthHalf,
		left ? 1.0 - radiusPlayer - netWidthHalf : 2.0 - radiusPlayer)) {
			p.v.x = 0;
		}

		// Move Y
		if (p.o.y != 0 || p.v.y != 0) {
			p.v.y -= playerGravity * gameSpeed;
			p.o.y += p.v.y * gameSpeed;
			if (p.o.y <= 0) {
				p.o.y = 0;
				p.v.y = 0; // stick to ground
			} else if (p.o.y > 1) {
				p.o.y = 1;
			}
		}
	}

	void MoveBallCollide(ref SlimeBall b, in SlimePlayer p) {
		double COLLISION_DIST = radiusPlayer + radiusBall;
		// COLLISION_FACTOR = 2 / (mB/mP + 1)
		//  player mass >> ball mass
		//   -> mB/mP = 0
		const int COLLISION_FACTOR = 2;

		// difference in position
		var dx = ball.o - p.o;
		if (dx.x > COLLISION_DIST || dx.y > COLLISION_DIST) return;
		var l = dx.LengthSquared();
		if (l > COLLISION_DIST * COLLISION_DIST) return;

		// move out of the bounding box
		l = Math.Sqrt(l);
		b.o = p.o + (dx * (COLLISION_DIST / l * 1.01));

		// elastic collision
		var dv = b.v - p.v;
		b.v -= dx * (COLLISION_FACTOR * (dx * dv / l) / l);

		// limit velocity components
		MathUtil.ClampAbs(ref b.v.x, ballMaxVelX);
		MathUtil.ClampAbs(ref b.v.y, ballMaxVelY);
	}

	void MoveBallCollideNet(ref SlimeBall b) {
		double L = 1 - netWidthHalf;
		double R = 1 + netWidthHalf;

		// fast bounding box check
		if (b.o.y - radiusBall >= netHeight ||
			b.o.x + radiusBall <= L ||
			b.o.x - radiusBall >= R) {
			return;
		}

		var closest = b.o;
		MathUtil.Clamp(ref closest.x, L, R);
		MathUtil.Clamp(ref closest.y, 0, netHeight);

		Vec2 normal;

		if (closest.x == b.o.x && closest.y == b.o.y) {
			// inside: just force the ball to go up
			closest.y = netHeight;
			normal.x = 0;
			normal.y = netHeight - b.o.y;
		} else {
			// outside: check if ball is too far away
			normal = b.o - closest;
			if (normal.LengthSquared() > radiusBall * radiusBall) {
				return;
			}
		}

		var l = normal.Length();

		// move out of the bounding box
		b.o = closest + (normal * (radiusBall / l * 1.01));

		// elastic collision (net has infinite mass)
		b.v -= normal * (2 * (normal * b.v / l) / l);
	}

	// MoveBall moves the ball and returns whether the ball hit the ground.
	bool MoveBall() {
		var hitGround = false;

		// update positions
		ball.v.y -= ballGravity * gameSpeed;
		ball.o.x += ball.v.x * gameSpeed;
		ball.o.y += ball.v.y * gameSpeed;

		// collide with players
		MoveBallCollide(ref ball, in p1);
		MoveBallCollide(ref ball, in p2);

		// collide with net
		MoveBallCollideNet(ref ball);

		// constrain x
		if (MathUtil.Clamp(ref ball.o.x, radiusBall, 2 - radiusBall)) {
			ball.v.x = -ball.v.x;
		}

		// constrain y (check if floor is hit, ignore upper bound)
		if (ball.o.y < radiusBall) {
			ball.o.y = radiusBall;
			hitGround = true;
		} else if (ball.o.y > 0.8) {
			ball.o.y = 0.8;
		}

		return hitGround;
	}

	bool GetNextFallingIntercept(double y, out double t, out double x) {
		var discrim = ball.v.y * ball.v.y + 2 * ballGravity * (ball.o.y - y);
		if (discrim < 0) {
			t = x = 0;
			return false;
		}

		t = (ball.v.y + Math.Sqrt(discrim)) / ballGravity;
		x = ball.o.x + ball.v.x * t;
		return true;
	}

	void BotThink() {
		if (numActive < 2) {
			if (numActive == 0) {
				// move P1
				var jump1 = false;
				if (GetNextFallingIntercept(botStrikeY, out var t1, out var x1) && x1 < 1) {
					if (x1 < 0) {
						x1 = -x1;
					}

					if (t1 < 0.1) {
						jump1 = true;
					}
				} else {
					x1 = 0.6;
				}
				x1 -= 0.3 * radiusPlayer;

				p1.U = jump1;
				p1.L = p1.o.x > x1 - botTolX;
				p1.R = p1.o.x < x1 + botTolX;
			}

			// move P2
			var jump = false;
			if (GetNextFallingIntercept(botStrikeY, out var t, out var x) && x > 1) {
				if (x > 2) {
					x = 4 - x; // 2 - (x - 2)
				}

				if (t < 0.1) {
					jump = true;
				}
			} else {
				x = 1.4;
			}
			x += 0.3 * radiusPlayer;

			p2.U = jump;
			p2.L = p2.o.x < x - botTolX;
			p2.R = p2.o.x > x + botTolX;
		}
	}

	void PhysicsFrame(ref int winner) {
		// Move players first
		MovePlayer(ref p1, true);
		MovePlayer(ref p2, false);
		// Move ball
		if (MoveBall()) {
			// Check winner by position of ball
			if (ball.o.x < 1) {
				winner = 2;
			} else {
				winner = 1;
			}
		}
	}

	void PutWorldState(ByteWriter worldstate) {
		const double DMF = 0xFFFF;
		const double DVF = 0x3FFF;

		worldstate.Add((byte)(
			(p1.L ? (1 << 0) : 0) |
			(p1.R ? (1 << 1) : 0) |
			(p1.U ? (1 << 2) : 0) |
			(p2.L ? (1 << 3) : 0) |
			(p2.R ? (1 << 4) : 0) |
			(p2.U ? (1 << 5) : 0)
		));

		worldstate
			.PutUShort((ushort)(p1.o.x * DMF))
			.PutUShort((ushort)(p1.o.y * DMF))
			.PutUShort((ushort)(short)(p1.v.y * DVF))
			.PutUShort((ushort)((p2.o.x - 1) * DMF))
			.PutUShort((ushort)(p2.o.y * DMF))
			.PutUShort((ushort)(short)(p2.v.y * DVF))
			.PutUShort((ushort)(ball.o.x * 0.5 * DMF))
			.PutUShort((ushort)(ball.o.y * DMF))
			.PutUShort((ushort)(short)(ball.v.x * DVF))
			.PutUShort((ushort)(short)(ball.v.y * DVF));
	}

	void GameInit() {
		p1.Reset();
		p2.Reset();

		winner = 3;
		p1First = rng.NextBool();

		gameStart = intermissionEnd = DateTime.UtcNow;
	}

	void GameLoop() {
		lock (players) {
			var now = DateTime.UtcNow;
			var oldWinner = winner;
			if (winner == 0) {
				BotThink();

				// Apply physics
				while (winner == 0 && now > lastPhysics) {
					PhysicsFrame(ref winner);
					lastPhysics += TimeSpan.FromMilliseconds(1000.0 / PHYS_FPS);
				}
			}

			// Update world state
			var worldstate = new ByteWriter()
				.PutType(MsgS2C.WORLDSTATE);
			PutWorldState(worldstate);
			Broadcast(worldstate.ToArray());

			// Update winner
			if (winner != 0) {
				if (oldWinner == 0) {
					if (winner == 1) {
						p1.score++;

						if (numActive != 0) {
							players[p1CN].score.AddWin();
							if (numActive == 2) {
								players[p2CN].score.AddLoss();
							}
						}
					} else {
						p2.score++;

						if (numActive != 0) {
							players[p1CN].score.AddLoss();
							if (numActive == 2) {
								players[p2CN].score.AddWin();
							}
						}
					}

					Broadcast(new ByteWriter()
						.PutType(winner == 1 ? MsgS2C.ROUND_WIN1 : MsgS2C.ROUND_WIN2)
					);

					intermissionEnd = now.AddMilliseconds(optIntermission);
				} else if (now >= intermissionEnd) {
					p1First = optServe switch {
						ModeServe.ALTERNATE => !p1First,
						ModeServe.WINNER => winner == 1,
						ModeServe.LOSER => winner == 2,
						_ => rng.NextBool(),
					};
					Broadcast(new ByteWriter()
						.PutType(p1First ? MsgS2C.ROUND_START1 : MsgS2C.ROUND_START2)
					);
					StartRound(p1First);
				}
			}
		}
	}

	struct SlimePlayer {
		public ulong score;
		// move state
		public Vec2 o, v;
		// input state
		public bool L, R, U;

		public void Reset() {
			score = 0;
			o = v = new();
			L = R = U = false;
		}
	}

	struct SlimeBall {
		// move state
		public Vec2 o, v;
	}
}
