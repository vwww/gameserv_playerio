#define USE_BTRD

public static class MultinomialSampler {
	public static ulong[] SampleEqualP(RNG rnd, ulong n, int k) {
		var result = new ulong[k];
		while (n != 0 && k != 0) {
			ulong v = random_binomial(rnd, 1.0 / k, n);
			result[k - 1] = v;
			n -= v;

			k--;
		}
		return result;
	}

#if false
	static uint[] SampleEqualPNaive(RNG rnd, uint n, int k) {
		var result = new uint[k];
		for (ulong i = 0; i < n; i++) {
			result[rnd.Next(k)]++;
		}
		return result;
	}
#endif

#if USE_BTRD
	// https://github.com/stdlib-js/random-base-binomial/blob/main/lib/sample2.js
	// doi:10.1080/00949659308811496
	// Wolfgang Hormann, "The generation of binomial random variates" _Journal of Statistical Computation and Simulation_ 46 (1-2): 101–10. (1993)
	static ulong random_binomial(RNG rnd, double p, ulong n) {
		if (p > 0.5)
			return n - random_binomial(rnd, 1.0 - p, n);

		if ((n == 0) || (p == 0.0))
			return 0;

		if (p * n <= 10) {
			return random_binomial_inversion(rnd, n, p);
		} else {
			return random_binomial_btrd(rnd, n, p);
		}
	}

	static ulong random_binomial_btrd(RNG rnd, ulong n, double p) {
		const double ONE_SIXTH = 1 / 6.0;

		ulong m = (ulong)Math.Floor((n + 1) * p);
		double nm = n - m + 1;

		double q = 1.0 - p;

		double r = p / q;
		double nr = (n + 1) * r;

		double npq = n * p * q;
		double snpq = Math.Sqrt(npq);

		double b = 1.15 + (2.53 * snpq);
		double a = -0.0873 + (0.0248 * b) + (0.01 * p);
		double c = (n * p) + 0.5;

		double alpha = (2.83 + (5.1 / b)) * snpq;

		double vr = 0.92 - (4.2 / b);
		double urvr = 0.86 * vr;

		double h = (m + 0.5) * Math.Log((m + 1) / (r * nm));
		h += correction(m) + correction(n - m);

		while (true) {
			double u, v = rnd.NextDouble();
			if (v <= urvr) {
				u = (v / vr) - 0.43;
				r = (u * ((2.0 * a / (0.5 - Math.Abs(u))) + b)) + c;
				return (ulong)r; // casting does floor for us
			}
			if (v >= vr) {
				u = rnd.NextDouble() - 0.5;
			} else {
				u = (v / vr) - 0.93;
				u = (Math.Sign(u) * 0.5) - u;
				v = vr * rnd.NextDouble();
			}
			double us = 0.5 - Math.Abs(u);
			ulong k = (ulong)Math.Floor((u * ((2.0 * a / us) + b)) + c);
			if (k < 0 || k > n) {
				// Try again...
				continue;
			}
			v = v * alpha / ((a / (us * us)) + b);
			double km = Math.Abs((double)k - m);
			if (km > 15) {
				v = Math.Log(v);
				double rho = km / npq;
				double tmp = ((km / 3) + 0.625) * km;
				tmp += ONE_SIXTH;
				tmp /= npq;
				rho *= tmp + 0.5;
				double t = -(km * km) / (2.0 * npq);
				if (v < t - rho) {
					return k;
				}
				if (v <= t + rho) {
					ulong nk = n - k + 1;
					double x = h + ((n + 1) * Math.Log(nm / nk));
					x += (k + 0.5) * Math.Log(nk * r / (k + 1));
					x += -(correction(k) + correction(n - k));
					if (v <= x) {
						return k;
					}
				}
			} else {
				double f = 1.0;
				if (m < k) {
					for (ulong i = m; i <= k; i++) {
						f *= (nr / i) - r;
					}
				} else if (m > k) {
					for (ulong i = k; i <= m; i++) {
						v *= (nr / i) - r;
					}
				}
				if (v <= f) {
					return k;
				}
			}
		}
	}

	static double correction(ulong k) {
		return k switch {
			0 => 0.08106146679532726,
			1 => 0.04134069595540929,
			2 => 0.02767792568499834,
			3 => 0.02079067210376509,
			4 => 0.01664469118982119,
			5 => 0.01387612882307075,
			6 => 0.01189670994589177,
			7 => 0.01041126526197209,
			8 => 0.009255462182712733,
			9 => 0.008330563433362871,
			_ => correction2(k),
		};

		static double correction2(ulong k) {
			const double ONE_12 = 1.0 / 12.0;
			const double ONE_360 = 1.0 / 360.0;
			const double ONE_1260 = 1.0 / 1260.0;

			double k1 = (double)k + 1;
			double v = k1 * k1;
			return (ONE_12 - ((ONE_360 - (ONE_1260 / v)) / v)) / k1;
		}
	}
#endif

#if !USE_BTRD
	// https://github.com/numpy/numpy/blob/v2.3.1/numpy/random/src/distributions/distributions.c#L619
	// doi:10.1145/42372.42381
	// Voratas Kachitvichyanukul, Bruce W. Schmeiser, "Binomial random variate generation" (1988)
	static ulong random_binomial(RNG rnd, double p, ulong n) {
		if (p > 0.5)
			return n - random_binomial(rnd, 1.0 - p, n);

		if ((n == 0) || (p == 0.0f))
			return 0;

		if (p * n <= 30.0) {
			return random_binomial_inversion(rnd, n, p);
		} else {
			return random_binomial_btpe(rnd, n, p);
		}
	}

	// BTPE (Binomial, Triangle, Parallelogram, Exponential)
	static ulong random_binomial_btpe(RNG rnd, ulong nn, double p) {
		long n = (long)nn;

		double r, q, fm, p1, xm, xl, xr, c, laml, lamr, p2, p3, p4;
		double a, u, v, s, F, rho, t, A, nrq, x1, x2, f1, f2, z, z2, w, w2, x;
		long m, y, k, i;

		/* initialize */
		r = p;
		q = 1.0 - r;
		fm = n * r + r;
		m = (long)Math.Floor(fm);
		p1 = Math.Floor(2.195 * Math.Sqrt(n * r * q) - 4.6 * q) + 0.5;
		xm = m + 0.5;
		xl = xm - p1;
		xr = xm + p1;
		c = 0.134 + 20.5 / (15.3 + m);
		a = (fm - xl) / (fm - xl * r);
		laml = a * (1.0 + a / 2.0);
		a = (xr - fm) / (xr * q);
		lamr = a * (1.0 + a / 2.0);
		p2 = p1 * (1.0 + 2.0 * c);
		p3 = p2 + c / laml;
		p4 = p3 + c / lamr;

		for (;;) {
			nrq = n * r * q;
			u = rnd.NextDouble() * p4;
			v = rnd.NextDouble();
			if (!(u > p1)) {
				y = (long)Math.Floor(xm - p1 * v + u);
				break;
			}

			if (!(u > p2)) {
				x = xl + (u - p1) / c;
				v = v * c + 1.0 - Math.Abs(m - x + 0.5) / p1;
				if (v > 1.0)
					continue;
				y = (long)Math.Floor(x);
			} else if (!(u > p3)) {
				y = (long)Math.Floor(xl + Math.Log(v) / laml);
				/* Reject if v==0.0 since previous cast is undefined */
				if ((y < 0) || (v == 0.0))
					continue;
				v = v * (u - p2) * laml;
			} else {
				y = (long)Math.Floor(xr - Math.Log(v) / lamr);
				/* Reject if v==0.0 since previous cast is undefined */
				if ((y > n) || (v == 0.0))
					continue;
				v = v * (u - p3) * lamr;
			}

			k = Math.Abs(y - m);
			if ((k <= 20) || (k >= ((nrq) / 2.0 - 1))) {
				s = r / q;
				a = s * (n + 1);
				F = 1.0;
				if (m < y) {
					for (i = m + 1; i <= y; i++) {
						F *= (a / i - s);
					}
				} else if (m > y) {
					for (i = y + 1; i <= m; i++) {
						F /= (a / i - s);
					}
				}
				if (v > F)
					continue;
				break;
			}

			rho = (k / (nrq)) * ((k * (k / 3.0 + 0.625) + 0.16666666666666666) / nrq + 0.5);
			t = -k * k / (2 * nrq);
			/* log(0.0) ok here */
			A = Math.Log(v);
			if (A < (t - rho))
				break;
			if (A > (t + rho))
				continue;

			x1 = y + 1;
			f1 = m + 1;
			z = n + 1 - m;
			w = n - y + 1;
			x2 = x1 * x1;
			f2 = f1 * f1;
			z2 = z * z;
			w2 = w * w;
			if (!(A > (xm * Math.Log(f1 / x1) + (n - m + 0.5) * Math.Log(z / w) +
							(y - m) * Math.Log(w * r / (x1 * q)) +
							(13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / f2) / f2) / f2) / f2) / f1 /
									166320.0 +
							(13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / z2) / z2) / z2) / z2) / z /
									166320.0 +
							(13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / x2) / x2) / x2) / x2) / x1 /
									166320.0 +
							(13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / w2) / w2) / w2) / w2) / w /
									166320.0))) {
				break;
			}
		}

		return (ulong)y;
	}
#endif

	// BIN (Binomial Inversion)
	static ulong random_binomial_inversion(RNG rnd, ulong n, double p) {
		double q, qn, np, px, U;
		ulong X, bound;

		q = 1.0 - p;
		qn = Math.Exp(n * Math.Log(q));
		np = n * p;
		bound = (ulong)Math.Min(n, np + 10.0 * Math.Sqrt(np * q + 1));
		X = 0;
		px = qn;
		U = rnd.NextDouble();
		while (U > px) {
			X++;
			if (X > bound) {
				X = 0;
				px = qn;
				U = rnd.NextDouble();
			} else {
				U -= px;
				px = ((n - X + 1) * p * px) / (X * q);
			}
		}
		return X;
	}
}
