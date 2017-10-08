using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent (typeof(Camera))]
public class VolumetricFog : MonoBehaviour
{
	Material m_DebugMaterial;
	[HideInInspector]
	public Shader m_DebugShader;
	[HideInInspector]
	public Shader m_ShadowmapShader;
	[HideInInspector]
	public ComputeShader m_InjectLightingAndDensity;
	[HideInInspector]
	public ComputeShader m_Scatter;
	Material m_ApplyToOpaqueMaterial;
	[HideInInspector]
	public Shader m_ApplyToOpaqueShader;
	Material m_BlurShadowmapMaterial;
	[HideInInspector]
	public Shader m_BlurShadowmapShader;
	[HideInInspector]
	public Texture2D m_Noise;
	[HideInInspector]
	public bool m_Debug = false;
	[HideInInspector]
	[Range(0.0f, 1.0f)]
	public float m_Z = 1.0f;

	[Header("Size")]
	[MinValue(0.1f)]
	public float m_NearClip = 0.1f;
	[MinValue(0.1f)]
	public float m_FarClipMax = 100.0f;

	[Header("Fog Density")]
	[FormerlySerializedAs("m_Density")]
	public float m_GlobalDensityMult = 1.0f;
	Vector3i m_InjectNumThreads = new Vector3i(16, 2, 16);
	Vector3i m_ScatterNumThreads = new Vector3i(32, 2, 1);
	RenderTexture m_VolumeInject;
	RenderTexture m_VolumeScatter;
	Vector3i m_VolumeResolution = new Vector3i(160, 90, 128);
	Camera m_Camera;

	// Density
	public float m_ConstantFog = 0;
	public float m_HeightFogAmount = 0;
	public float m_HeightFogExponent = 0;
	public float m_HeightFogOffset = 0;

	[Tooltip("Noise multiplies with constant fog and height fog, but not with fog ellipsoids.")]
	[Range(0.0f, 1.0f)]
	public float m_NoiseFogAmount = 0;
	public float m_NoiseFogScale = 1;
	public Wind m_Wind;

	[Range(0.0f, 0.999f)]
	public float m_Anisotropy = 0.0f;

	[Header("Lights")]
	[FormerlySerializedAs("m_Intensity")]
	public float m_GlobalIntensityMult = 1.0f;
	[MinValue(0)]
	public float m_AmbientLightIntensity = 0.0f;
	public Color m_AmbientLightColor = Color.white;

	struct Vector3i
	{
		public int x, y, z;
		public Vector3i(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
	}

	struct PointLightParams
	{
		public Vector3 pos;
		public float range;
		public Vector3 color;
		float padding;
	}

	PointLightParams[] m_PointLightParams;
	ComputeBuffer m_PointLightParamsCB;

	struct TubeLightParams
	{
		public Vector3 start;
		public float range;
		public Vector3 end;
		public float radius;
		public Vector3 color;
		float padding;
	}

	TubeLightParams[] m_TubeLightParams;
	ComputeBuffer m_TubeLightParamsCB;

	struct TubeLightShadowPlaneParams
	{
		public Vector4 plane0;
		public Vector4 plane1;
		public float feather0;
		public float feather1;
		float padding0;
		float padding1;
	}

	TubeLightShadowPlaneParams[] m_TubeLightShadowPlaneParams;
	ComputeBuffer m_TubeLightShadowPlaneParamsCB;

	struct AreaLightParams
	{
		public Matrix4x4 mat;
		public Vector4 pos;
		public Vector3 color;
		public float bounded;
	}

	AreaLightParams[] m_AreaLightParams;
	ComputeBuffer m_AreaLightParamsCB;

	struct FogEllipsoidParams
	{
		public Vector3 pos;
		public float radius;
		public Vector3 axis;
		public float stretch;
		public float density;
		public float noiseAmount;
		public float noiseSpeed;
		public float noiseScale;
		public float feather;
		public float blend;
		public float padding1;
		public float padding2;
	}

	FogEllipsoidParams[] m_FogEllipsoidParams;
	ComputeBuffer m_FogEllipsoidParamsCB;

	Camera cam{ get { if (m_Camera == null) m_Camera = GetComponent<Camera>(); return m_Camera; }}

	float nearClip { get { return Mathf.Max(0, m_NearClip); } }
	float farClip { get { return Mathf.Min(cam.farClipPlane, m_FarClipMax); } }

	void ReleaseComputeBuffer(ref ComputeBuffer buffer)
	{
		if(buffer != null)
			buffer.Release();
		buffer = null;
	}

	void OnDestroy()
	{
		Cleanup();
	}

	void OnDisable()
	{
		Cleanup();
	}

	void Cleanup()
	{
		DestroyImmediate(m_VolumeInject);
		DestroyImmediate(m_VolumeScatter);
		ReleaseComputeBuffer(ref m_PointLightParamsCB);
		ReleaseComputeBuffer(ref m_TubeLightParamsCB);
		ReleaseComputeBuffer(ref m_TubeLightShadowPlaneParamsCB);
		ReleaseComputeBuffer(ref m_AreaLightParamsCB);
		ReleaseComputeBuffer(ref m_FogEllipsoidParamsCB);
		m_VolumeInject = null;
		m_VolumeScatter = null;
	}

	void SanitizeInput()
	{
		m_GlobalDensityMult = Mathf.Max(m_GlobalDensityMult, 0);
		m_ConstantFog = Mathf.Max(m_ConstantFog, 0);
		m_HeightFogAmount = Mathf.Max(m_HeightFogAmount, 0);
	}

	void SetUpPointLightBuffers(int kernel)
	{
		int count = m_PointLightParamsCB == null ? 0 : m_PointLightParamsCB.count;
		m_InjectLightingAndDensity.SetFloat("_PointLightsCount", count);
		if (count == 0)
			return;

		if (m_PointLightParams == null || m_PointLightParams.Length != count)
			m_PointLightParams = new PointLightParams[count];

		HashSet<FogLight> fogLights = LightManagerFogLights.Get();

		int j = 0;
		for (var x = fogLights.GetEnumerator(); x.MoveNext();)
		{
			var fl = x.Current;
			if (fl == null || fl.type != FogLight.Type.Point || !fl.isOn)
				continue;

			Light light = fl.light;
			m_PointLightParams[j].pos = light.transform.position;
			float range = light.range * fl.m_RangeMult;
			m_PointLightParams[j].range = 1.0f / (range * range);
			m_PointLightParams[j].color = new Vector3(light.color.r, light.color.g, light.color.b) * light.intensity * fl.m_IntensityMult;
			j++;
		}

		// TODO: try a constant buffer with setfloats instead for perf
		m_PointLightParamsCB.SetData(m_PointLightParams);
		m_InjectLightingAndDensity.SetBuffer(kernel, "_PointLights", m_PointLightParamsCB);
	}

	TubeLightShadowPlane.Params[] sppArr;

	void SetUpTubeLightBuffers(int kernel)
	{
		int count = m_TubeLightParamsCB == null ? 0 : m_TubeLightParamsCB.count;
		m_InjectLightingAndDensity.SetFloat("_TubeLightsCount", count);
		if (count == 0)
			return;

		if (m_TubeLightParams == null || m_TubeLightParams.Length != count)
			m_TubeLightParams = new TubeLightParams[count];

		if (m_TubeLightShadowPlaneParams == null || m_TubeLightShadowPlaneParams.Length != count)
			m_TubeLightShadowPlaneParams = new TubeLightShadowPlaneParams[count];

		HashSet<FogLight> fogLights = LightManagerFogLights.Get();

		int j = 0;
		for (var x = fogLights.GetEnumerator(); x.MoveNext();)
		{
			var fl = x.Current;
			if (fl == null || fl.type != FogLight.Type.Tube || !fl.isOn)
				continue;

			TubeLight light = fl.tubeLight;
			Transform t = light.transform;
			Vector3 pos = t.position;
			Vector3 halfLength = 0.5f * t.up * light.m_Length;

			// Tube lights
			m_TubeLightParams[j].start = pos + halfLength;
			m_TubeLightParams[j].end = pos - halfLength;
			float range = light.m_Range * fl.m_RangeMult;
			m_TubeLightParams[j].range = 1.0f / (range * range);
			m_TubeLightParams[j].color = new Vector3(light.m_Color.r, light.m_Color.g, light.m_Color.b) * light.m_Intensity * fl.m_IntensityMult;
			m_TubeLightParams[j].radius = light.m_Radius;

			// Tube light shadow planes
			var p = light.GetShadowPlaneParams(ref sppArr);
			m_TubeLightShadowPlaneParams[j].plane0 = p[0].plane;
			m_TubeLightShadowPlaneParams[j].plane1 = p[1].plane;
			m_TubeLightShadowPlaneParams[j].feather0 = p[0].feather;
			m_TubeLightShadowPlaneParams[j].feather1 = p[1].feather;

			j++;
		}

		m_TubeLightParamsCB.SetData(m_TubeLightParams);
		m_InjectLightingAndDensity.SetBuffer(kernel, "_TubeLights", m_TubeLightParamsCB);
		m_TubeLightShadowPlaneParamsCB.SetData(m_TubeLightShadowPlaneParams);
		m_InjectLightingAndDensity.SetBuffer(kernel, "_TubeLightShadowPlanes", m_TubeLightShadowPlaneParamsCB);
	}

	void SetUpAreaLightBuffers(int kernel)
	{
		int count = m_AreaLightParamsCB == null ? 0 : m_AreaLightParamsCB.count;
		m_InjectLightingAndDensity.SetFloat("_AreaLightsCount", count);
		if (count == 0)
			return;

		if (m_AreaLightParams == null || m_AreaLightParams.Length != count)
			m_AreaLightParams = new AreaLightParams[count];

		HashSet<FogLight> fogLights = LightManagerFogLights.Get();

		int shadowedAreaLightIndex = fogLights.Count;
		int j = 0;
		for (var x = fogLights.GetEnumerator(); x.MoveNext();)
		{
			var fl = x.Current;
			if (fl == null || fl.type != FogLight.Type.Area || !fl.isOn)
				continue;

			AreaLight light = fl.areaLight;

			m_AreaLightParams[j].mat = light.GetProjectionMatrix(true);
			m_AreaLightParams[j].pos = light.GetPosition();
			m_AreaLightParams[j].color = new Vector3(light.m_Color.r, light.m_Color.g, light.m_Color.b) * light.m_Intensity * fl.m_IntensityMult;
			m_AreaLightParams[j].bounded = fl.m_Bounded ? 1 : 0;

			if (fl.m_Shadows)
			{
				RenderTexture shadowmap = light.GetBlurredShadowmap();
				if (shadowmap != null)
				{
					m_InjectLightingAndDensity.SetTexture(kernel, "_AreaLightShadowmap", shadowmap);
					m_InjectLightingAndDensity.SetFloat("_ESMExponentAreaLight", fl.m_ESMExponent);
					shadowedAreaLightIndex = j;
				}
			}

			j++;
		}

		m_AreaLightParamsCB.SetData(m_AreaLightParams);
		m_InjectLightingAndDensity.SetBuffer(kernel, "_AreaLights", m_AreaLightParamsCB);
		m_InjectLightingAndDensity.SetFloat("_ShadowedAreaLightIndex", shadowedAreaLightIndex);
	}

	void SetUpFogEllipsoidBuffers(int kernel)
	{
		int count = 0;
		HashSet<FogEllipsoid> fogEllipsoids = LightManagerFogEllipsoids.Get();
		for (var x = fogEllipsoids.GetEnumerator(); x.MoveNext();) {
			var fe = x.Current;
			if (fe != null && fe.enabled && fe.gameObject.activeSelf)
				count++;
		}

		m_InjectLightingAndDensity.SetFloat("_FogEllipsoidsCount", count);
		if (count == 0)
			return;

		if (m_FogEllipsoidParams == null || m_FogEllipsoidParams.Length != count)
			m_FogEllipsoidParams = new FogEllipsoidParams[count];

		int j = 0;
		for (var x = fogEllipsoids.GetEnumerator(); x.MoveNext();)
		{
			var fe = x.Current;
			if (fe == null || !fe.enabled || !fe.gameObject.activeSelf)
				continue;

			Transform t = fe.transform;

			m_FogEllipsoidParams[j].pos = t.position;
			m_FogEllipsoidParams[j].radius = fe.m_Radius * fe.m_Radius;
			m_FogEllipsoidParams[j].axis = -t.up;
			m_FogEllipsoidParams[j].stretch = 1.0f/fe.m_Stretch - 1.0f;
			m_FogEllipsoidParams[j].density = fe.m_Density;
			m_FogEllipsoidParams[j].noiseAmount = fe.m_NoiseAmount;
			m_FogEllipsoidParams[j].noiseSpeed = fe.m_NoiseSpeed;
			m_FogEllipsoidParams[j].noiseScale = fe.m_NoiseScale;
			m_FogEllipsoidParams[j].feather = 1.0f - fe.m_Feather;
			m_FogEllipsoidParams[j].blend = fe.m_Blend == FogEllipsoid.Blend.Additive ? 0 : 1;
			j++;
		}

		m_FogEllipsoidParamsCB.SetData(m_FogEllipsoidParams);
		m_InjectLightingAndDensity.SetBuffer(kernel, "_FogEllipsoids", m_FogEllipsoidParamsCB);
	}

	FogLight GetDirectionalLight()
	{
		HashSet<FogLight> fogLights = LightManagerFogLights.Get();
		FogLight fogLight = null;

		for (var x = fogLights.GetEnumerator(); x.MoveNext();)
		{
			var fl = x.Current;
			if (fl == null || fl.type != FogLight.Type.Directional || !fl.isOn)
				continue;

			fogLight = fl;
			break;
		}

		return fogLight;
	}

	FogLight m_DirectionalLight;

	void OnPreRender()
	{
		m_DirectionalLight = GetDirectionalLight();

		if (m_DirectionalLight != null)
			m_DirectionalLight.UpdateDirectionalShadowmap();
	}

	float[] m_dirLightColor;
	float[] m_dirLightDir;

	void SetUpDirectionalLight(int kernel)
	{
		if (m_dirLightColor == null || m_dirLightColor.Length != 3)
			m_dirLightColor = new float[3];
		if (m_dirLightDir == null || m_dirLightDir.Length != 3)
			m_dirLightDir = new float[3];

		if (m_DirectionalLight == null)
		{
			m_dirLightColor[0] = 0;
			m_dirLightColor[1] = 0;
			m_dirLightColor[2] = 0;
			m_InjectLightingAndDensity.SetFloats("_DirLightColor", m_dirLightColor);
			return;
		}

		m_DirectionalLight.SetUpDirectionalShadowmapForSampling(m_DirectionalLight.m_Shadows, m_InjectLightingAndDensity, kernel);
		// TODO: if above fails, disable shadows

		Light light = m_DirectionalLight.light;
		Vector4 color = light.color;
		color *= light.intensity * m_DirectionalLight.m_IntensityMult;
		m_dirLightColor[0] = color.x;
		m_dirLightColor[1] = color.y;
		m_dirLightColor[2] = color.z;
		m_InjectLightingAndDensity.SetFloats("_DirLightColor", m_dirLightColor);
		Vector3 dir = light.GetComponent<Transform>().forward;
		m_dirLightDir[0] = dir.x;
		m_dirLightDir[1] = dir.y;
		m_dirLightDir[2] = dir.z;
		m_InjectLightingAndDensity.SetFloats("_DirLightDir", m_dirLightDir);
		
	}

	float[] m_fogParams;
	float[] m_windDir;
	float[] m_ambientLight;

	void SetUpForScatter(int kernel)
	{
		SanitizeInput();
		InitResources();
		SetFrustumRays();
		
		// Compensate for more light and density being injected in per world space meter when near and far are closer.
		// TODO: Not quite correct yet.
		float depthCompensation = (farClip - nearClip) * 0.01f;
		m_InjectLightingAndDensity.SetFloat("_Density", m_GlobalDensityMult * 0.128f * depthCompensation);
		m_InjectLightingAndDensity.SetFloat("_Intensity", m_GlobalIntensityMult);
		m_InjectLightingAndDensity.SetFloat("_Anisotropy", m_Anisotropy);
		m_InjectLightingAndDensity.SetTexture(kernel, "_VolumeInject", m_VolumeInject);
		m_InjectLightingAndDensity.SetTexture(kernel, "_Noise", m_Noise);

		if (m_fogParams == null || m_fogParams.Length != 4)
			m_fogParams = new float[4];
		if (m_windDir == null || m_windDir.Length != 3)
			m_windDir = new float[3];
		if (m_ambientLight == null || m_ambientLight.Length != 3)
			m_ambientLight = new float[3];
		m_fogParams[0] = m_ConstantFog;
		m_fogParams[1] = m_HeightFogExponent;
		m_fogParams[2] = m_HeightFogOffset;
		m_fogParams[3] = m_HeightFogAmount;

		m_InjectLightingAndDensity.SetFloats("_FogParams", m_fogParams);
		m_InjectLightingAndDensity.SetFloat("_NoiseFogAmount", m_NoiseFogAmount);
		m_InjectLightingAndDensity.SetFloat("_NoiseFogScale", m_NoiseFogScale);
		m_InjectLightingAndDensity.SetFloat("_WindSpeed", m_Wind == null ? 0 : m_Wind.m_Speed);
		Vector3 windDir = m_Wind == null ? Vector3.forward : m_Wind.transform.forward;
		m_windDir[0] = windDir.x;
		m_windDir[1] = windDir.y;
		m_windDir[2] = windDir.z;
		m_InjectLightingAndDensity.SetFloats("_WindDir", m_windDir);
		m_InjectLightingAndDensity.SetFloat("_Time", Time.time);
		m_InjectLightingAndDensity.SetFloat("_NearOverFarClip", nearClip/farClip);
		Color ambient = m_AmbientLightColor * m_AmbientLightIntensity * 0.1f;
		m_ambientLight[0] = ambient.r;
		m_ambientLight[1] = ambient.g;
		m_ambientLight[2] = ambient.b;
		m_InjectLightingAndDensity.SetFloats("_AmbientLight", m_ambientLight);

		SetUpPointLightBuffers(kernel);
		SetUpTubeLightBuffers(kernel);
		SetUpAreaLightBuffers(kernel);
		SetUpFogEllipsoidBuffers(kernel);
		SetUpDirectionalLight(kernel);
	}

	void Scatter()
	{
		// Inject lighting and density
		int kernel = 0;

		SetUpForScatter(kernel);

		m_InjectLightingAndDensity.Dispatch(kernel, m_VolumeResolution.x/m_InjectNumThreads.x, m_VolumeResolution.y/m_InjectNumThreads.y, m_VolumeResolution.z/m_InjectNumThreads.z);

		// Solve scattering
		m_Scatter.SetTexture(0, "_VolumeInject", m_VolumeInject);
		m_Scatter.SetTexture(0, "_VolumeScatter", m_VolumeScatter);
		m_Scatter.Dispatch(0, m_VolumeResolution.x/m_ScatterNumThreads.x, m_VolumeResolution.y/m_ScatterNumThreads.y, 1);
	}

	void DebugDisplay(RenderTexture src, RenderTexture dest)
	{
		InitMaterial(ref m_DebugMaterial, m_DebugShader);

		m_DebugMaterial.SetTexture("_VolumeInject", m_VolumeInject);
		m_DebugMaterial.SetTexture("_VolumeScatter", m_VolumeScatter);
		m_DebugMaterial.SetFloat("_Z", m_Z);

		m_DebugMaterial.SetTexture("_MainTex", src);

		Graphics.Blit(src, dest, m_DebugMaterial);
	}

	void SetUpGlobalFogSamplingUniforms(int width, int height)
	{
		Shader.SetGlobalTexture("_VolumeScatter", m_VolumeScatter);
		Shader.SetGlobalVector("_Screen_TexelSize", new Vector4(1.0f / width, 1.0f / height, width, height));
		Shader.SetGlobalVector("_VolumeScatter_TexelSize", new Vector4(1.0f / m_VolumeResolution.x, 1.0f / m_VolumeResolution.y, 1.0f / m_VolumeResolution.z, 0));
		Shader.SetGlobalFloat("_CameraFarOverMaxFar", cam.farClipPlane / farClip);
		Shader.SetGlobalFloat("_NearOverFarClip", nearClip / farClip);
	}

	[ImageEffectOpaque]
	void OnRenderImage(RenderTexture src, RenderTexture dest)
	{
		if (!CheckSupport())
		{
			Debug.LogError(GetUnsupportedErrorMessage());
			Graphics.Blit(src, dest);
			enabled = false;
			return;
		}

		if(m_Debug)
		{
			DebugDisplay(src, dest);
			return;
		}

		Scatter();

		InitMaterial(ref m_ApplyToOpaqueMaterial, m_ApplyToOpaqueShader);

		// TODO: This shouldn't be needed. Is it because the shader doesn't have the Property block?
		m_ApplyToOpaqueMaterial.SetTexture("_MainTex", src);

		SetUpGlobalFogSamplingUniforms(src.width, src.height);

		Graphics.Blit(src, dest, m_ApplyToOpaqueMaterial);

		VolumetricFogInForward(true);
	}

	void OnPostRender()
	{
		VolumetricFogInForward(false);	
	}

	void VolumetricFogInForward(bool enable)
	{
		if (enable)
			Shader.EnableKeyword("VOLUMETRIC_FOG");
		else
			Shader.DisableKeyword("VOLUMETRIC_FOG");
	}

	Vector3 ViewportToLocalPoint(Camera c, Transform t, Vector3 p)
	{
		// TODO: viewporttoworldpoint inverts the clip-to-world matrix every time without caching it.
		return t.InverseTransformPoint(c.ViewportToWorldPoint(p));
	}

	static readonly Vector2[] frustumUVs = 
		new Vector2[] {new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)};
	static float[] frustumRays = new float[16];

	void SetFrustumRays()
	{
		float far = farClip;
		Vector3 cameraPos = cam.transform.position;
		Vector2[] uvs = frustumUVs;

		for (int i = 0; i < 4; i++)
		{
			Vector3 ray = cam.ViewportToWorldPoint(new Vector3(uvs[i].x, uvs[i].y, far)) - cameraPos;
			frustumRays[i*4+0] = ray.x;
			frustumRays[i*4+1] = ray.y;
			frustumRays[i*4+2] = ray.z;
			frustumRays[i*4+3] = 0;
		}

		m_InjectLightingAndDensity.SetVector("_CameraPos", cameraPos);
		m_InjectLightingAndDensity.SetFloats("_FrustumRays", frustumRays);
	}

	void InitVolume(ref RenderTexture volume)
	{
		if(volume)
			return;

		volume = new RenderTexture (m_VolumeResolution.x, m_VolumeResolution.y, 0, RenderTextureFormat.ARGBHalf);
		volume.volumeDepth = m_VolumeResolution.z;
		volume.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		volume.enableRandomWrite = true;
		volume.Create();
	}

	void CreateBuffer(ref ComputeBuffer buffer, int count, int stride)
	{
		if (buffer != null && buffer.count == count)
			return;

		if(buffer != null)
		{
			buffer.Release();
			buffer = null;
		}

		if (count <= 0)
			return;

		buffer = new ComputeBuffer(count, stride);
	}

	void InitResources ()
	{
		// Volume
		InitVolume(ref m_VolumeInject);
		InitVolume(ref m_VolumeScatter);


		// Compute buffers
		int pointLightCount = 0, tubeLightCount = 0, areaLightCount = 0;
		HashSet<FogLight> fogLights = LightManagerFogLights.Get();
		for (var x = fogLights.GetEnumerator(); x.MoveNext();)
		{
			var fl = x.Current;
			if (fl == null)
				continue;

			bool isOn = fl.isOn;

			switch(fl.type)
			{
				case FogLight.Type.Point: 	if (isOn) pointLightCount++; break;
				case FogLight.Type.Tube: 	if (isOn) tubeLightCount++; break;
				case FogLight.Type.Area: 	if (isOn) areaLightCount++; break;
			}
		}

		// PointLightParams {float3 float float3 float} -> 32 bytes
		CreateBuffer(ref m_PointLightParamsCB, pointLightCount, 32);

		// TubeLightParams {float3 float float3 float float3 float} -> 48 bytes
		CreateBuffer(ref m_TubeLightParamsCB, tubeLightCount, 48);

		// TubeLightShadowPlaneParams {float4 float4 float float float float} -> 48 bytes
		CreateBuffer(ref m_TubeLightShadowPlaneParamsCB, tubeLightCount, 48);

		// TubeLightParams {float4x4 float4 float3 float} -> 96 bytes
		CreateBuffer(ref m_AreaLightParamsCB, areaLightCount, 96);

		// FogEllipsoidParams {float3 float float3 9xfloat} -> 64 bytes
		HashSet<FogEllipsoid> fogEllipsoids = LightManagerFogEllipsoids.Get();
		CreateBuffer(ref m_FogEllipsoidParamsCB, fogEllipsoids == null ? 0 : fogEllipsoids.Count, 64);
	}

	void ReleaseTemporary(ref RenderTexture rt)
	{
		if (rt == null)
			return;

		RenderTexture.ReleaseTemporary(rt);
		rt = null;
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

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, farClip, nearClip, cam.aspect);
	}

	public static bool CheckSupport()
	{
		return SystemInfo.supportsComputeShaders;
	}

	public static string GetUnsupportedErrorMessage()
	{
		return "Volumetric Fog requires compute shaders and this platform doesn't support them. Disabling. \nDetected device type: " + 
			SystemInfo.graphicsDeviceType + ", version: " + SystemInfo.graphicsDeviceVersion;
	}
}
