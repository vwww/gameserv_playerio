static class PlayerIOExtensions {
	public static void Send(this BasePlayer player, byte[] msg) => player.Send(Message.Create("", msg));

	public static int ParseGameProp<P>(this Game<P> game, string name, int defaultValue, int min, int max) where P : BasePlayer, new() {
		if (game.RoomData.TryGetValue(name, out string s) && int.TryParse(s, out int v)) {
			return MathUtil.Clamp(v, min, max);
		}
		return defaultValue;
	}

	public static long ParseGameProp<P>(this Game<P> game, string name, long defaultValue, long min, long max) where P : BasePlayer, new() {
		if (game.RoomData.TryGetValue(name, out string s) && long.TryParse(s, out long v)) {
			return MathUtil.Clamp(v, min, max);
		}
		return defaultValue;
	}

	public static ulong ParseGameProp<P>(this Game<P> game, string name, ulong defaultValue, ulong min, ulong max) where P : BasePlayer, new() {
		if (game.RoomData.TryGetValue(name, out string s) && ulong.TryParse(s, out ulong v)) {
			return MathUtil.Clamp(v, min, max);
		}
		return defaultValue;
	}

	public static bool ParseGameProp<P>(this Game<P> game, string name, bool defaultValue) where P : BasePlayer, new() {
		if (game.RoomData.TryGetValue(name, out string v) && (v == "true" || v == "false")) {
			return v == "true";
		}
		return defaultValue;
	}

	public static void WriteGameProp<P, V>(this Game<P> game, string name, V value) where P : BasePlayer, new() where V : IFormattable {
		game.RoomData[name] = value.ToString();
	}

	public static void WriteGameProp<P>(this Game<P> game, string name, bool value) where P : BasePlayer, new() {
		game.RoomData[name] = value ? "true" : "false";
	}
}
