using UnityEngine;
using UnityEngine.Rendering;

public partial class FogLight : LightOverride
{
	CommandBuffer m_BufGrabShadowmap;
	CommandBuffer m_BufGrabShadowParams;
	RenderTexture m_Shadowmap;

	ComputeBuffer m_ShadowParamsCB;
	//[HideInInspector]
	public Shader m_BlurShadowmapShader;
	Material m_BlurShadowmapMaterial;

	public Shader m_CopyShadowParamsShader;
	Material m_CopyShadowParamsMaterial;

	bool directionalShadow {get{ return m_Shadows && type == Type.Directional; }}

	void InitDirectionalShadowmap()
	{
		if (m_BufGrabShadowmap != null || !directionalShadow)
			return;

		Light light = GetComponent<Light>();

		m_BufGrabShadowmap = new CommandBuffer();
		m_BufGrabShadowmap.name = "Grab shadowmap for Volumetric Fog";
		light.AddCommandBuffer(LightEvent.AfterShadowMap, m_BufGrabShadowmap);

		m_BufGrabShadowParams = new CommandBuffer();
		m_BufGrabShadowParams.name = "Grab shadow params for Volumetric Fog";
		light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, m_BufGrabShadowParams);

		m_BlurShadowmapMaterial = new Material(m_BlurShadowmapShader);
		m_BlurShadowmapMaterial.hideFlags = HideFlags.HideAndDontSave;

		m_CopyShadowParamsMaterial = new Material(m_CopyShadowParamsShader);
		m_CopyShadowParamsMaterial.hideFlags = HideFlags.HideAndDontSave;
	}

	int[] temp;

	public void UpdateDirectionalShadowmap()
	{
		InitDirectionalShadowmap();

		if (m_BufGrabShadowmap != null)
			m_BufGrabShadowmap.Clear();
		if (m_BufGrabShadowParams != null)
			m_BufGrabShadowParams.Clear();

		if (!directionalShadow)
			return;

		// Copy directional shadowmap params - they're only set for regular shaders, but we need them in compute
		if (m_ShadowParamsCB == null)
			m_ShadowParamsCB = new ComputeBuffer(1, 336);
		Graphics.SetRandomWriteTarget(2, m_ShadowParamsCB);
		m_BufGrabShadowParams.DrawProcedural(Matrix4x4.identity, m_CopyShadowParamsMaterial, 0, MeshTopology.Points, 1);

		// TODO: get the real size of the shadowmap
		int startRes = 4096;
		// To make things easier, blurred shadowmap is at most half the size of the regular.
		int targetRes = Mathf.Min((int)m_ShadowmapRes, startRes/2);
		int downsampleSteps = (int)Mathf.Log(startRes / targetRes, 2);

		RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
		m_BufGrabShadowmap.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);

		// RFloat for ESM, RGHalf for VSM
		RenderTextureFormat format = RenderTextureFormat.RGHalf;

		ReleaseTemporary(ref m_Shadowmap);
		m_Shadowmap = RenderTexture.GetTemporary(targetRes, targetRes, 0, format, RenderTextureReadWrite.Linear);
		m_Shadowmap.filterMode = FilterMode.Bilinear;
		m_Shadowmap.wrapMode = TextureWrapMode.Clamp;

		if (temp == null || temp.Length != downsampleSteps - 1)
			temp = new int[downsampleSteps - 1];

		for (int i = 0, currentRes = startRes/2; i < downsampleSteps; i++)
		{
			m_BufGrabShadowmap.SetGlobalVector("_TexelSize", new Vector4(0.5f/currentRes, 0.5f/currentRes, 0, 0));
			
			RenderTargetIdentifier targetRT;

			if (i < downsampleSteps - 1)
			{
				temp[i] = Shader.PropertyToID("ShadowmapDownscaleTemp" + i);
				m_BufGrabShadowmap.GetTemporaryRT(temp[i], currentRes, currentRes, 0, FilterMode.Bilinear, format, RenderTextureReadWrite.Linear);
				targetRT = new RenderTargetIdentifier(temp[i]);
			}
			else
			{
				// Last step: write into the shadowmap texture
				targetRT = new RenderTargetIdentifier(m_Shadowmap);
			}

			if (i == 0)
			{
				// This step should convert to ESM/VSM
				// m_BufGrabShadowmap.Blit(shadowmap, targetRT);
				m_BufGrabShadowmap.SetGlobalTexture("_DirShadowmap", shadowmap);
				m_BufGrabShadowmap.Blit(null, targetRT, m_BlurShadowmapMaterial, /*sample & convert to VSM*/ 4);
			}
			else
			{
				m_BufGrabShadowmap.Blit(temp[i - 1], targetRT, m_BlurShadowmapMaterial, /*regular sample*/ 5);
			}

			currentRes /= 2;
		}

		//var directionalShadowmapBlurred = Shader.PropertyToID("_DirectionalShadowmapBlurred");
		//m_BufGrabShadowmap.GetTemporaryRT(directionalShadowmapBlurred, 1024, 1024, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
		//m_BufGrabShadowmap.Blit(shadowmap, m_Shadowmap);
		//m_BufGrabShadowmap.SetGlobalTexture(directionalShadowmapBlurred, directionalShadowmapBlurred);
	}

	void CleanupDirectionalShadowmap()
	{
		if (m_BufGrabShadowmap != null)
			m_BufGrabShadowmap.Clear();

		if (m_BufGrabShadowParams != null)
			m_BufGrabShadowParams.Clear();

		if(m_ShadowParamsCB != null)
			m_ShadowParamsCB.Release();
		m_ShadowParamsCB = null;
	}

	public bool SetUpDirectionalShadowmapForSampling(bool shadows, ComputeShader cs, int kernel)
	{
		if (!shadows || m_ShadowParamsCB == null || m_Shadowmap == null)
		{
			cs.SetFloat("_DirLightShadows", 0);
			return false;
		}

		cs.SetFloat("_DirLightShadows", 1);
		cs.SetBuffer(kernel, "_ShadowParams", m_ShadowParamsCB);
		cs.SetTexture(kernel, "_DirectionalShadowmap", m_Shadowmap);

		return true;
	}

	void ReleaseTemporary(ref RenderTexture rt)
	{
		if (rt == null)
			return;

		RenderTexture.ReleaseTemporary(rt);
		rt = null;
	}
}
