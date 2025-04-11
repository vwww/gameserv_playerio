static class Trigonometry {
    public static double Hypot(double a, double b) {
        a = Math.Abs(a);
        b = Math.Abs(b);

        if (a < b)
            Util.Swap(ref a, ref b);

        if (a == 0.0)
            return 0.0;

        double ba = b / a;
        return a * Math.Sqrt(1.0 + (ba * ba));
    }
}
