static class HypergeometricSampler {
	public static ulong[][] DealCards(this RNG r, ulong[] count, ulong countTotal, uint players) {
		var playerCardsBase = countTotal / players;
		var playerExtraCard = countTotal % players;

		// randomize player order just in case the Sample implementation has bias
		var playerOrder = new uint[players];
		for (uint i = 0; i < playerOrder.Length; i++) {
			playerOrder[i] = i;
		}
		r.Shuffle(playerOrder);

		var cards = new ulong[players][];
		foreach (var p in playerOrder) {
			var cardsLeft = playerCardsBase + (p < playerExtraCard ? 1u : 0u);

			var N = countTotal;
			countTotal -= cardsLeft;

			var row = cards[p] = new ulong[count.Length];
			for (var i = 0; i < count.Length; i++) {
				var c = count[i];
				N -= c;

				var v = Sample(r, c, N, cardsLeft);
				row[i] = v;

				cardsLeft -= v;
				count[i] -= v;
			}
		}
		return cards;
	}

	public static ulong Sample(RNG r, ulong n1, ulong n2, ulong k) {
		// negate what we sample for
		if (n1 > n2) return k - Sample(r, n2, n1, k);
		// sample for complement
		if (2 * k > n1 + n2) return n1 - Sample(r, n1, n2, n1 + n2 - k);

		return k < 10 ? SampleNaive(r, n1, n2, k) : SampleHRUA(r, (long)n1, (long)n2, (long)k);
	}

	public static ulong SampleNaive(RNG r, ulong n1, ulong n2, ulong k) {
		if (n1 == 0) return 0;
		else if (n2 == 0) return k;

		ulong N = n1 + n2;
		ulong result = 0;
		while (k-- != 0) {
			if (r.NextUInt64(N--) < n1) {
				result++;
				if (--n1 == 0) {
					break;
				}
			} else {
				if (--n2 == 0) {
					result += k;
					break;
				}
			}
		}

		return result;
	}

	// https://github.com/numpy/numpy/blob/v2.3.1/numpy/random/src/distributions/random_hypergeometric.c
	// no doi
	// Ernst Stadlober's thesis "Sampling from Poisson, Binomial and Hypergeometric Distributions: Ratio of Uniforms as a Simple and Fast Alternative" (1989)
	// doi:10.1016/0377-0427(90)90349-5
	// Ernst Stadlober, "The ratio of uniforms approach for generating discrete random variates", Journal of Computational and Applied Mathematics, 31, pp. 181-189 (1990).
	public static ulong SampleHRUA(RNG rng, long good, long bad, long sample) {
		// D1 = 2*sqrt(2/e)
		// D2 = 3 - 2*sqrt(3/e)
		const double D1 = 1.7155277699214135;
		const double D2 = 0.8989161620588988;

		double p, q;
		double mu, var;
		double a, c, b, h, g;
		long m, K;

		long popsize = good + bad;

		/*
		 *  Variables that do not match Stadlober (1989)
		 *    Here      Stadlober
		 *    -------   ---------
		 *    good         M
		 *    popsize      N
		 *    sample       n
		 */

		p = ((double)good) / popsize;
		q = ((double)bad) / popsize;

		// mu is the mean of the distribution.
		mu = sample * p;

		a = mu + 0.5;

		// var is the variance of the distribution.
		var = ((double)(popsize - sample) *
			   sample * p * q / (popsize - 1));

		c = Math.Sqrt(var + 0.5);

		/*
		 *  h is 2*s_hat (See Stadlober's thesis (1989), Eq. (5.17); or
		 *  Stadlober (1990), Eq. 8).  s_hat is the scale of the "table mountain"
		 *  function that dominates the scaled hypergeometric PMF ("scaled" means
		 *  normalized to have a maximum value of 1).
		 */
		h = D1*c + D2;

		m = (long)Math.Floor((double)(sample + 1) * (good + 1) /
							(popsize + 2));

		g = (logfactorial(m) +
			 logfactorial(good - m) +
			 logfactorial(sample - m) +
			 logfactorial(bad - sample + m));

		/*
		 *  b is the upper bound for random samples:
		 *  ... min(computed_sample, mingoodbad) + 1 is the length of the support.
		 *  ... floor(a + 16*c) is 16 standard deviations beyond the mean.
		 *
		 *  The idea behind the second upper bound is that values that far out in
		 *  the tail have negligible probabilities.
		 *
		 *  There is a comment in a previous version of this algorithm that says
		 *      "16 for 16-decimal-digit precision in D1 and D2",
		 *  but there is no documented justification for this value.  A lower value
		 *  might work just as well, but I've kept the value 16 here.
		 */
		b = Math.Min(Math.Min(sample, good) + 1, Math.Floor(a + 16*c));

		for (;;) {
			double U, V, X, T;
			double gp;
			U = rng.NextDouble();
			V = rng.NextDouble();  // "U star" in Stadlober (1989)
			X = a + h*(V - 0.5) / U;

			// fast rejection:
			if ((X < 0.0) || (X >= b)) {
				continue;
			}

			K = (long)Math.Floor(X);

			gp = (logfactorial(K) +
				  logfactorial(good - K) +
				  logfactorial(sample - K) +
				  logfactorial(bad - sample + K));

			T = g - gp;

			// fast acceptance:
			if ((U*(4.0 - U) - 3.0) <= T) {
				break;
			}

			// fast rejection:
			if (U*(U - T) >= 1) {
				continue;
			}

			if (2.0*Math.Log(U) <= T) {
				// acceptance
				break;
			}
		}

		return (ulong)K;
	}

	static double logfactorial(long k) {
		double[] logfact = {
			0,
			0,
			0.69314718055994529,
			1.791759469228055,
			3.1780538303479458,
			4.7874917427820458,
			6.5792512120101012,
			8.5251613610654147,
			10.604602902745251,
			12.801827480081469,
			15.104412573075516,
			17.502307845873887,
			19.987214495661885,
			22.552163853123425,
			25.19122118273868,
			27.89927138384089,
			30.671860106080672,
			33.505073450136891,
			36.395445208033053,
			39.339884187199495,
			42.335616460753485,
			45.380138898476908,
			48.471181351835227,
			51.606675567764377,
			54.784729398112319,
			58.003605222980518,
			61.261701761002001,
			64.557538627006338,
			67.88974313718154,
			71.257038967168015,
			74.658236348830158,
			78.092223553315307,
			81.557959456115043,
			85.054467017581516,
			88.580827542197682,
			92.136175603687093,
			95.719694542143202,
			99.330612454787428,
			102.96819861451381,
			106.63176026064346,
			110.32063971475739,
			114.03421178146171,
			117.77188139974507,
			121.53308151543864,
			125.3172711493569,
			129.12393363912722,
			132.95257503561632,
			136.80272263732635,
			140.67392364823425,
			144.5657439463449,
			148.47776695177302,
			152.40959258449735,
			156.3608363030788,
			160.3311282166309,
			164.32011226319517,
			168.32744544842765,
			172.35279713916279,
			176.39584840699735,
			180.45629141754378,
			184.53382886144948,
			188.6281734236716,
			192.7390472878449,
			196.86618167289001,
			201.00931639928152,
			205.1681994826412,
			209.34258675253685,
			213.53224149456327,
			217.73693411395422,
			221.95644181913033,
			226.1905483237276,
			230.43904356577696,
			234.70172344281826,
			238.97838956183432,
			243.26884900298271,
			247.57291409618688,
			251.89040220972319,
			256.22113555000954,
			260.56494097186322,
			264.92164979855278,
			269.29109765101981,
			273.67312428569369,
			278.06757344036612,
			282.4742926876304,
			286.89313329542699,
			291.32395009427029,
			295.76660135076065,
			300.22094864701415,
			304.68685676566872,
			309.1641935801469,
			313.65282994987905,
			318.1526396202093,
			322.66349912672615,
			327.1852877037752,
			331.71788719692847,
			336.26118197919845,
			340.81505887079902,
			345.37940706226686,
			349.95411804077025,
			354.53908551944079,
			359.1342053695754,
			363.73937555556347,
			368.35449607240474,
			372.97946888568902,
			377.61419787391867,
			382.25858877306001,
			386.91254912321756,
			391.57598821732961,
			396.24881705179155,
			400.93094827891576,
			405.6222961611449,
			410.32277652693733,
			415.03230672824964,
			419.75080559954472,
			424.47819341825709,
			429.21439186665157,
			433.95932399501481,
			438.71291418612117,
			443.47508812091894,
			448.24577274538461,
			453.02489623849613,
			457.81238798127816,
			462.60817852687489,
			467.4121995716082,
			472.22438392698058,
			477.04466549258564,
			481.87297922988796
		};

		const double halfln2pi = 0.9189385332046728;

		if (k < logfact.Length) {
			/* Use the lookup table. */
			return logfact[k];
		}

		/*
		 *  Use the Stirling series, truncated at the 1/k**3 term.
		 *  (In a Python implementation of this approximation, the result
		 *  was within 2 ULP of the best 64 bit floating point value for
		 *  k up to 10000000.)
		 */
		return (k + 0.5) * Math.Log(k) - k + (halfln2pi + (1.0 / k) * (1 / 12.0 - 1 / (360.0 * k * k)));
	}
}
