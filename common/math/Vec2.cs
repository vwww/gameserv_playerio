/// <summary>
/// Vec2 represents a 2-dimensional vector.
/// </summary>
struct Vec2 {
	public double x, y;

	public Vec2(double x, double y) {
		this.x = x;
		this.y = y;
	}

	public static Vec2 operator +(Vec2 v, Vec2 w) {
		return new Vec2(v.x + w.x, v.y + w.y);
	}

	public static Vec2 operator -(Vec2 v, Vec2 w) {
		return new Vec2(v.x - w.x, v.y - w.y);
	}

	public static Vec2 operator *(Vec2 v, double f) {
		return new Vec2(v.x * f, v.y * f);
	}

	public static Vec2 operator /(Vec2 v, double f) {
		return new Vec2(v.x / f, v.y / f);
	}

	public static double operator *(Vec2 v, Vec2 w) {
		return v.x * w.x + v.y * w.y;
	}

	public readonly double Length() => Math.Sqrt(LengthSquared());

	public readonly double LengthSquared() => this * this;

	public readonly Vec2 Normalize() => this / Length();
}
