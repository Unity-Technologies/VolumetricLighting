using UnityEngine;

public partial class AreaLightLUT
{
	const int kLUTResolution = 64;
	const int kLUTMatrixDim = 3;

	public enum LUTType
	{
		TransformInv_DisneyDiffuse,
		TransformInv_GGX,
		AmpDiffAmpSpecFresnel
	}

	public static Texture2D LoadLUT(LUTType type)
	{
		switch(type)
		{
			case LUTType.TransformInv_DisneyDiffuse: return LoadLUT(s_LUTTransformInv_DisneyDiffuse);
			case LUTType.TransformInv_GGX: return LoadLUT(s_LUTTransformInv_GGX);
			case LUTType.AmpDiffAmpSpecFresnel: return LoadLUT(s_LUTAmplitude_DisneyDiffuse, s_LUTAmplitude_GGX, s_LUTFresnel_GGX);
		}

		return null;
	}

	static Texture2D CreateLUT(TextureFormat format, Color[] pixels)
	{
		Texture2D tex = new Texture2D(kLUTResolution, kLUTResolution, format, false /*mipmap*/, true /*linear*/);
		tex.hideFlags = HideFlags.HideAndDontSave;
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.SetPixels(pixels);
		tex.Apply();
		return tex;
	}

	static Texture2D LoadLUT(double[,] LUTTransformInv)
	{
		const int count = kLUTResolution * kLUTResolution;
		Color[] pixels = new Color[count];
		
		// transformInv
		for (int i = 0; i < count; i++)
		{
			// Only columns 0, 2, 4 and 6 contain interesting values (at least in the case of GGX).
			pixels[i] = new Color(	(float)LUTTransformInv[i, 0],
									(float)LUTTransformInv[i, 2],
									(float)LUTTransformInv[i, 4],
									(float)LUTTransformInv[i, 6]);
		}

		return CreateLUT(TextureFormat.RGBAHalf, pixels);
	}

	static Texture2D LoadLUT(float[] LUTScalar0, float[] LUTScalar1, float[] LUTScalar2)
	{
		const int count = kLUTResolution * kLUTResolution;
		Color[] pixels = new Color[count];

		// amplitude
		for (int i = 0; i < count; i++)
		{
			pixels[i] = new Color(LUTScalar0[i], LUTScalar1[i], LUTScalar2[i], 0);
		}

		return CreateLUT(TextureFormat.RGBAHalf, pixels);
	}
}