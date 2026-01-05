partial class Room {
	ModeServe optServe;
	ushort optIntermission;
	ushort optSizePlayer;
	ushort optSizeBall;
	ushort optSizeNet;
	ushort optSpeedGame;
	ushort optSpeedPlayer;
	ushort optSpeedBall;
	ushort optGravity;

	double netWidthHalf, netHeight;
	double radiusBall, radiusPlayer;

	double gameSpeed;
	double playerSpeedX, playerSpeedY;
	double ballMaxVelX, ballMaxVelY;
	double playerGravity, ballGravity;

	double botStrikeY;

	void ParseMode() {
		optServe = (ModeServe)this.ParseGameProp("optServe", (int)ModeServe.ALTERNATE, 0, (int)ModeServe.NUM - 1);
		optIntermission = (ushort)this.ParseGameProp("optIntermission", 1000, 0, 3000);
		optSizePlayer = (ushort)this.ParseGameProp("optSizePlayer", 100, 10, 400);
		optSizeBall = (ushort)this.ParseGameProp("optSizeBall", 100, 10, 1600);
		optSizeNet = (ushort)this.ParseGameProp("optSizeNet", 100, 0, 200);
		optSpeedGame = (ushort)this.ParseGameProp("optSpeedGame", 100, 25, 800);
		optSpeedPlayer = (ushort)this.ParseGameProp("optSpeedPlayer", 100, 25, 800);
		optSpeedBall = (ushort)this.ParseGameProp("optSpeedBall", 100, 10, 800);
		optGravity = (ushort)this.ParseGameProp("optGravity", 100, 10, 800);

		netWidthHalf = NET_W * optSizeNet / 100;
		netHeight = NET_H * optSizeNet / 100;
		radiusPlayer = RAD_PL * optSizePlayer / 100;
		radiusBall = RAD_BALL * optSizeBall / 100;

		gameSpeed = optSpeedGame / 100.0 / PHYS_FPS;
		playerSpeedX = PL_SPEED_X * optSpeedPlayer / 100;
		playerSpeedY = PL_VEL_JUMP * optSpeedPlayer / 100;
		ballMaxVelX = BALL_POST_COLLISION_VEL_X_MAX * optSpeedBall / 100;
		ballMaxVelY = BALL_POST_COLLISION_VEL_Y_MAX * optSpeedBall / 100;
		playerGravity = PL_GRAV_ACCEL * optGravity / 100;
		ballGravity = BALL_GRAV_ACCEL * optGravity / 100;

		botStrikeY = (1 - Math.Sqrt(1 - 0.3 * 0.3)) * radiusPlayer + playerSpeedY * 0.1;

		RoomData.Clear();

		this.WriteGameProp("optServe", (int)optServe);
		this.WriteGameProp("optIntermission", optIntermission);
		this.WriteGameProp("optSizePlayer", optSizePlayer);
		this.WriteGameProp("optSizeBall", optSizeBall);
		this.WriteGameProp("optSizeNet", optSizeNet);
		this.WriteGameProp("optSpeedGame", optSpeedGame);
		this.WriteGameProp("optSpeedPlayer", optSpeedPlayer);
		this.WriteGameProp("optSpeedBall", optSpeedBall);
		this.WriteGameProp("optGravity", optGravity);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.Add((byte)optServe);
		w.PutULong(optIntermission);
		w.PutULong(optSizePlayer);
		w.PutULong(optSizeBall);
		w.PutULong(optSizeNet);
		w.PutULong(optSpeedGame);
		w.PutULong(optSpeedPlayer);
		w.PutULong(optSpeedBall);
		w.PutULong(optGravity);
	}
}

enum ModeServe {
	ALTERNATE,
	WINNER,
	LOSER,
	RANDOM,
	NUM,
}
