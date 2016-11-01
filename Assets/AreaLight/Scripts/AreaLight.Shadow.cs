using UnityEngine;
using UnityEngine.Rendering;

public partial class AreaLight : MonoBehaviour
{
	Camera m_ShadowmapCamera;
	Transform m_ShadowmapCameraTransform;
	
	[HideInInspector]
	public Shader m_ShadowmapShader;
	[HideInInspector]
	public Shader m_BlurShadowmapShader;
	Material m_BlurShadowmapMaterial;
	RenderTexture m_Shadowmap = null;
	RenderTexture m_BlurredShadowmap = null;
	Texture2D m_ShadowmapDummy = null;

	int m_ShadowmapRenderTime = -1;
	int m_BlurredShadowmapRenderTime = -1;
	FogLight m_FogLight = null;

	public enum TextureSize
	{
		x512 = 512,
		x1024 = 1024,
		x2048 = 2048,
		x4096 = 4096,
	}

	void UpdateShadowmap(int res)
	{
		if (m_Shadowmap != null && m_ShadowmapRenderTime == Time.renderedFrameCount)
			return;

		// Create the camera
		if (m_ShadowmapCamera == null)
		{
			if (m_ShadowmapShader == null)
			{
				Debug.LogError("AreaLight's shadowmap shader not assigned.", this);
				return;
			}

			GameObject go = new GameObject("Shadowmap Camera");
			go.AddComponent(typeof(Camera));
			m_ShadowmapCamera = go.GetComponent<Camera>();
			go.hideFlags = HideFlags.HideAndDontSave;
			m_ShadowmapCamera.enabled = false;
			m_ShadowmapCamera.clearFlags = CameraClearFlags.SolidColor;
			m_ShadowmapCamera.renderingPath = RenderingPath.Forward;
			// exp(EXPONENT) for ESM, white for VSM
			// m_ShadowmapCamera.backgroundColor = new Color(Mathf.Exp(EXPONENT), 0, 0, 0);
			m_ShadowmapCamera.backgroundColor = Color.white;
			m_ShadowmapCameraTransform = go.transform;
			m_ShadowmapCameraTransform.parent = transform;
			m_ShadowmapCameraTransform.localRotation = Quaternion.identity;
		}

		if (m_Angle == 0.0f)
		{
			m_ShadowmapCamera.orthographic = true;
			m_ShadowmapCameraTransform.localPosition = Vector3.zero;
			m_ShadowmapCamera.nearClipPlane = 0;
			m_ShadowmapCamera.farClipPlane = m_Size.z;
			m_ShadowmapCamera.orthographicSize = 0.5f * m_Size.y;
			m_ShadowmapCamera.aspect = m_Size.x / m_Size.y;
		}
		else
		{
			m_ShadowmapCamera.orthographic = false;
			float near = GetNearToCenter();
			m_ShadowmapCameraTransform.localPosition = -near * Vector3.forward;
			m_ShadowmapCamera.nearClipPlane = near;
			m_ShadowmapCamera.farClipPlane = near + m_Size.z;
			m_ShadowmapCamera.fieldOfView = m_Angle;
			m_ShadowmapCamera.aspect = m_Size.x / m_Size.y;
		}

		ReleaseTemporary(ref m_Shadowmap);
		m_Shadowmap = RenderTexture.GetTemporary(res, res, 24, RenderTextureFormat.Shadowmap);
		m_Shadowmap.name = "AreaLight Shadowmap";
		m_Shadowmap.filterMode = FilterMode.Bilinear;
		m_Shadowmap.wrapMode = TextureWrapMode.Clamp;

		m_ShadowmapCamera.targetTexture = m_Shadowmap;

		// Clear. RenderWithShader() should clear too, but it doesn't.
		// TODO: Check if it's a bug.
		m_ShadowmapCamera.cullingMask = 0;
		m_ShadowmapCamera.Render();
		m_ShadowmapCamera.cullingMask = m_ShadowCullingMask;

		// We might be rendering inside PlaneReflections, which invert culling. Disable temporarily.
		var oldCulling = GL.invertCulling;
		GL.invertCulling = false;

		m_ShadowmapCamera.RenderWithShader(m_ShadowmapShader, "RenderType");

		// Back to whatever was the culling mode.
		GL.invertCulling = oldCulling;

		m_ShadowmapRenderTime = Time.renderedFrameCount;
	}

	public RenderTexture GetBlurredShadowmap()
	{
		UpdateBlurredShadowmap();
		return m_BlurredShadowmap;
	}

	RenderTexture[] temp;

	void UpdateBlurredShadowmap()
	{
		if (m_BlurredShadowmap != null && m_BlurredShadowmapRenderTime == Time.renderedFrameCount)
			return;

		InitFogLight();

		int startRes = (int)m_ShadowmapRes;
		int targetRes = (int)m_FogLight.m_ShadowmapRes;

		// To make things easier, blurred shadowmap is at most half the size of the regular.
		if (isActiveAndEnabled && m_Shadows)
		{
			targetRes = Mathf.Min(targetRes, startRes/2);
		}
		else
		{
			// If the area light or the shadows on it are disabled, we can
			// just get the most convenient resolution for us.
			startRes = 2 * startRes;
		}

		UpdateShadowmap(startRes);

		RenderTexture originalRT = RenderTexture.active;

		// Downsample
		ReleaseTemporary(ref m_BlurredShadowmap);
		InitMaterial(ref m_BlurShadowmapMaterial, m_BlurShadowmapShader);
		int downsampleSteps = (int)Mathf.Log(startRes / targetRes, 2);
		if (temp == null || temp.Length != downsampleSteps)
			temp = new RenderTexture[downsampleSteps];
		// RFloat for ESM, RGHalf for VSM
		RenderTextureFormat format = RenderTextureFormat.RGHalf;

		for(int i = 0, currentRes = startRes/2; i < downsampleSteps; i++)
		{
			temp[i] = RenderTexture.GetTemporary(currentRes, currentRes, 0, format, RenderTextureReadWrite.Linear);
			temp[i].name = "AreaLight Shadow Downsample";
			temp[i].filterMode = FilterMode.Bilinear;
			temp[i].wrapMode = TextureWrapMode.Clamp;
			m_BlurShadowmapMaterial.SetVector("_TexelSize", new Vector4(0.5f/currentRes, 0.5f/currentRes, 0, 0));

			if (i == 0)
			{
				m_BlurShadowmapMaterial.SetTexture("_Shadowmap", m_Shadowmap);
				InitShadowmapDummy();
				m_BlurShadowmapMaterial.SetTexture("_ShadowmapDummy", m_ShadowmapDummy);
				m_BlurShadowmapMaterial.SetVector("_ZParams", GetZParams());
				m_BlurShadowmapMaterial.SetFloat("_ESMExponent", m_FogLight.m_ESMExponent);
				Blur(m_Shadowmap, temp[i], /*sample & convert shadowmap*/ 0);
			}
			else
			{
				m_BlurShadowmapMaterial.SetTexture("_MainTex", temp[i - 1]);
				Blur(temp[i - 1], temp[i], /*regular sample*/ 1);
			}

			currentRes /= 2;
		}

		for (int i = 0; i < downsampleSteps - 1; i++)
			RenderTexture.ReleaseTemporary(temp[i]);

		m_BlurredShadowmap = temp[downsampleSteps - 1];

		// Blur
		if (m_FogLight.m_BlurIterations > 0)
		{
			RenderTexture tempBlur = RenderTexture.GetTemporary (targetRes, targetRes, 0, format, RenderTextureReadWrite.Linear);
			tempBlur.name = "AreaLight Shadow Blur";
			tempBlur.filterMode = FilterMode.Bilinear;
			tempBlur.wrapMode = TextureWrapMode.Clamp;

			m_BlurShadowmapMaterial.SetVector("_MainTex_TexelSize", new Vector4(1.0f/targetRes, 1.0f/targetRes, 0, 0));

			float blurSize = m_FogLight.m_BlurSize;
			for(int i = 0; i < m_FogLight.m_BlurIterations; i++)
			{
				m_BlurShadowmapMaterial.SetFloat ("_BlurSize", blurSize);
				Blur(m_BlurredShadowmap, tempBlur,   /*vertical blur*/2);
				Blur(tempBlur, m_BlurredShadowmap, /*horizontal blur*/3);
				blurSize *= 1.2f;
			}

			RenderTexture.ReleaseTemporary(tempBlur);
		}

		RenderTexture.active = originalRT;

		m_BlurredShadowmapRenderTime = Time.renderedFrameCount;
	}

	// Normally would've used Graphics.Blit(), but it breaks picking in the scene view.
	// TODO: bug report
	void Blur(RenderTexture src, RenderTexture dst, int pass)
	{
		RenderTexture.active = dst;
		m_BlurShadowmapMaterial.SetTexture("_MainTex", src);
		m_BlurShadowmapMaterial.SetPass(pass);
		RenderQuad();
	}

	void RenderQuad()
	{
		GL.Begin(GL.QUADS);
		GL.TexCoord2( 0, 0);
		GL.Vertex3	(-1, 1, 0);
		GL.TexCoord2( 0, 1);
		GL.Vertex3	(-1,-1, 0);
		GL.TexCoord2( 1, 1);
		GL.Vertex3	( 1,-1, 0);
		GL.TexCoord2( 1, 0);
		GL.Vertex3	( 1, 1, 0);
		GL.End();
	}

	void SetUpShadowmapForSampling(CommandBuffer buf)
	{
		UpdateShadowmap((int)m_ShadowmapRes);
		
		buf.SetGlobalTexture("_Shadowmap", m_Shadowmap);
		InitShadowmapDummy();
		m_ProxyMaterial.SetTexture("_ShadowmapDummy", m_ShadowmapDummy);
		buf.SetGlobalMatrix("_ShadowProjectionMatrix", GetProjectionMatrix());

		float texelsInMap = (int)m_ShadowmapRes;
		float relativeTexelSize = texelsInMap / 2048.0f;

		buf.SetGlobalFloat("_ShadowReceiverWidth", relativeTexelSize * m_ReceiverSearchDistance / texelsInMap);

		buf.SetGlobalFloat("_ShadowReceiverDistanceScale", m_ReceiverDistanceScale * 0.5f / 10.0f); // 10 samples in shader
		
		Vector2 shadowLightWidth = new Vector2(m_LightNearSize, m_LightFarSize) * relativeTexelSize / texelsInMap;
		buf.SetGlobalVector("_ShadowLightWidth", shadowLightWidth);

		buf.SetGlobalFloat("_ShadowBias", m_ShadowBias);
	}

	void InitMaterial(ref Material material, Shader shader)
	{
		if (material)
			return;

		if (!shader)
		{
			Debug.LogError("Missing shader");
			return;
		}

		material = new Material(shader);
		material.hideFlags = HideFlags.HideAndDontSave;
	}

	void InitShadowmapDummy()
	{
		if(m_ShadowmapDummy != null)
			return;
		m_ShadowmapDummy = new Texture2D(1, 1, TextureFormat.Alpha8, false, true);
		m_ShadowmapDummy.filterMode = FilterMode.Point;
		m_ShadowmapDummy.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
		m_ShadowmapDummy.Apply(false, true);
	}

	void InitFogLight()
	{
		if (m_FogLight != null)
			return;

		// It should always be here, because it triggered this code path in the first place.
		m_FogLight = GetComponent<FogLight>();
	}

	bool InsideShadowmapCameraRender()
	{
		RenderTexture target = Camera.current.targetTexture;
		return target != null && target.format == RenderTextureFormat.Shadowmap;
	}

	Vector4 GetZParams()
	{
		float n = GetNearToCenter();
		float f = n + m_Size.z;
		// linear z, 0 near, 1 far
		// linearz = A * (z + 1.0) / (z + B);
		// A = n/(n - f)
		// B = (n + f)/(n - f)

		return new Vector4(n/(n - f), (n + f)/(n - f), 0, 0);
	}
}
