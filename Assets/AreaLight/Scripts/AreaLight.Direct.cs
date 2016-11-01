using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public partial class AreaLight : MonoBehaviour
{
	[HideInInspector]
	public Mesh m_Cube;
	[HideInInspector]
	public Shader m_ProxyShader;
	Material m_ProxyMaterial;
	static Texture2D s_TransformInvTexture_Specular;
	static Texture2D s_TransformInvTexture_Diffuse;
	static Texture2D s_AmpDiffAmpSpecFresnel;

	Dictionary<Camera, CommandBuffer> m_Cameras = new Dictionary<Camera, CommandBuffer>();
	static CameraEvent kCameraEvent = CameraEvent.AfterLighting;

	bool InitDirect()
	{
		if (m_ProxyShader == null || m_Cube  == null)
			return false;

		// Proxy
		m_ProxyMaterial = new Material(m_ProxyShader);
		m_ProxyMaterial.hideFlags = HideFlags.HideAndDontSave;

		return true;
	}

	void SetUpLUTs()
	{
		if (s_TransformInvTexture_Diffuse == null)
			s_TransformInvTexture_Diffuse = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.TransformInv_DisneyDiffuse);
		if (s_TransformInvTexture_Specular == null)
			s_TransformInvTexture_Specular = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.TransformInv_GGX);
		if (s_AmpDiffAmpSpecFresnel == null)
			s_AmpDiffAmpSpecFresnel = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.AmpDiffAmpSpecFresnel);

		m_ProxyMaterial.SetTexture("_TransformInv_Diffuse", s_TransformInvTexture_Diffuse);
		m_ProxyMaterial.SetTexture("_TransformInv_Specular", s_TransformInvTexture_Specular);
		m_ProxyMaterial.SetTexture("_AmpDiffAmpSpecFresnel", s_AmpDiffAmpSpecFresnel);
	}

	void Cleanup()
	{
		for(var e = m_Cameras.GetEnumerator(); e.MoveNext();)
		{
			var cam = e.Current;
			if (cam.Key != null && cam.Value != null)
			{
				cam.Key.RemoveCommandBuffer (kCameraEvent, cam.Value);
			}
		}
		m_Cameras.Clear();
	}

	static readonly float[,] offsets = new float[4,2] {{1, 1}, {1, -1}, {-1, -1}, {-1, 1}};

	CommandBuffer GetOrCreateCommandBuffer(Camera cam)
	{
		if(cam == null)
			return null;

		CommandBuffer buf = null;
		if(!m_Cameras.ContainsKey(cam)) {
			buf = new CommandBuffer();
			buf.name = /*"Area light: " +*/ gameObject.name;
			m_Cameras[cam] = buf;
			cam.AddCommandBuffer(kCameraEvent, buf);
			cam.depthTextureMode |= DepthTextureMode.Depth;
		} else {
			buf = m_Cameras[cam];
			buf.Clear();
		}

		return buf;
	}

	public void SetUpCommandBuffer()
	{
		if (InsideShadowmapCameraRender())
			return;

		Camera cam = Camera.current;
		CommandBuffer buf = GetOrCreateCommandBuffer(cam);

		buf.SetGlobalVector("_LightPos", transform.position);
		buf.SetGlobalVector("_LightColor", GetColor());
		SetUpLUTs();

		// Needed as we're using the vert_deferred vertex shader from UnityDeferredLibrary.cginc
		// TODO: Make the light render as quad if it intersects both near and far plane.
		// (Also missing: rendering as front faces when near doesn't intersect, stencil optimisations)
		buf.SetGlobalFloat("_LightAsQuad", 0);
		
		// A little bit of bias to prevent the light from lighting itself - the source quad
		float z = 0.01f;
		Transform t = transform;

		Matrix4x4 lightVerts = new Matrix4x4();
		for (int i = 0; i < 4; i++)
			lightVerts.SetRow(i, t.TransformPoint(new Vector3(m_Size.x * offsets[i,0], m_Size.y * offsets[i,1], z) * 0.5f));
		buf.SetGlobalMatrix("_LightVerts", lightVerts);

		if (m_Shadows)
			SetUpShadowmapForSampling(buf);

		Matrix4x4 m = Matrix4x4.TRS(new Vector3(0, 0, 10.0f), Quaternion.identity, Vector3.one * 20);
		buf.DrawMesh(m_Cube, t.localToWorldMatrix * m, m_ProxyMaterial, 0, m_Shadows ? /*shadows*/ 0 : /*no shadows*/ 1);
	}

	void SetKeyword(string keyword, bool on)
	{
		if (on)
			m_ProxyMaterial.EnableKeyword(keyword);
		else
			m_ProxyMaterial.DisableKeyword(keyword);
	}

	void ReleaseTemporary(ref RenderTexture rt)
	{
		if (rt == null)
			return;

		RenderTexture.ReleaseTemporary(rt);
		rt = null;
	}

	Color GetColor()
	{
		if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
			return m_Color * m_Intensity;

		return new Color(
			Mathf.GammaToLinearSpace(m_Color.r * m_Intensity),
			Mathf.GammaToLinearSpace(m_Color.g * m_Intensity),
			Mathf.GammaToLinearSpace(m_Color.b * m_Intensity),
			1.0f
		);
	}

}
