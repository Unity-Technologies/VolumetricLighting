#ifndef TUBE_LIGHT_ATTENUATION_LEGACY
	#define TUBE_LIGHT_ATTENUATION_LEGACY 0
#endif

#if TUBE_LIGHT_ATTENUATION_LEGACY

	float Attenuation(float distNorm)
	{
		return 1.0 / (1.0 + 25.0 * distNorm);
	}

	float AttenuationToZero(float distNorm)
	{
		float att = Attenuation(distNorm);
		
		// Replicating unity light attenuation - pulled to 0 at range
		// if (distNorm > 0.8 * 0.8)
		// 		att *= 1 - (distNorm - 0.8 * 0.8) / (1 - 0.8 * 0.8);
		// Same, simplified
		float oneDistNorm = 1.0 - distNorm;
		att *= lerp(1.0, oneDistNorm * 2.78, step(0.64, distNorm));

		att *= step(0.0, oneDistNorm);

		return att;
	}

#else

	float Attenuation(float distSqr)
	{
		float d = sqrt(distSqr);
		float kDefaultPointLightRadius = 0.25;
		return 1.0 / pow(1.0 +   d/kDefaultPointLightRadius, 2);
	}

	float AttenuationToZero(float distSqr)
	{
		// attenuation = 1 / (1 + distance_to_light / light_radius)^2
		//             = 1 / (1 + 2*(d/r) + (d/r)^2)
		// For more details see: https://imdoingitwrong.wordpress.com/2011/01/31/light-attenuation/
		float d = sqrt(distSqr);
		float kDefaultPointLightRadius = 0.25;
		float atten =         1.0 / pow(1.0 +   d/kDefaultPointLightRadius, 2);
		float kCutoff = 1.0 / pow(1.0 + 1.0/kDefaultPointLightRadius, 2); // cutoff equal to attenuation at distance 1.0

		// Force attenuation to fall towards zero at distance 1.0
		atten = (atten - kCutoff) / (1.f - kCutoff);
		if (d >= 1.f)
			atten = 0.f;
		
		return atten;
	}

#endif