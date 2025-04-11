[RoomType("DiscardRoom")]
partial class Room {
	const int PROTOCOL_VERSION = 0;

	partial void Setup() {
		ParseMode();

		AddTimer(PingClients, PING_CLIENTS_INTERVAL);
	}
}
