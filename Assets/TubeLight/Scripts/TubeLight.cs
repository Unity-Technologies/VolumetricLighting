using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[ExecuteInEditMode]
public class TubeLight : MonoBehaviour
{
	public float m_Intensity = 0.8f;
	public Color m_Color = Color.white;
	public float m_Range = 10.0f;
	public float m_Radius = 0.3f;
	public float m_Length = 0.0f;

	[HideInInspector]
	public Mesh m_Sphere;
	[HideInInspector]
	public Mesh m_Capsule;
	[HideInInspector]
	public Shader m_ProxyShader;
	Material m_ProxyMaterial;

	public bool m_RenderSource = false;
	Renderer m_SourceRenderer;
	Transform m_SourceTransform;
	Mesh m_SourceMesh;
	float m_LastLength = -1;

	public const int maxPlanes = 2;
	[HideInInspector]
	public TubeLightShadowPlane[] m_ShadowPlanes = new TubeLightShadowPlane[maxPlanes];

	bool m_Initialized = false;
	MaterialPropertyBlock m_props;

	const float kMinRadius = 0.001f;
	bool renderSource {get{return m_RenderSource && m_Radius >= kMinRadius;}}

	Dictionary<Camera, CommandBuffer> m_Cameras = new Dictionary<Camera, CommandBuffer>();
	static CameraEvent kCameraEvent = CameraEvent.AfterLighting;

	void Start()
	{
		if(!Init())
			return;
		UpdateMeshesAndBounds();
	}

	bool Init()
	{
		if (m_Initialized)
			return true;

		// Sometimes on editor startup (especially after project upgrade?), Init() gets called
		// while m_ProxyShader, m_Sphere or m_Capsule is still null/hasn't loaded.
		if (m_ProxyShader == null || m_Sphere  == null || m_Capsule == null)
			return false;

		// Proxy
		m_ProxyMaterial = new Material(m_ProxyShader);
		m_ProxyMaterial.hideFlags = HideFlags.HideAndDontSave;

		// Source
		m_SourceMesh = Instantiate<Mesh>(m_Capsule);
		m_SourceMesh.hideFlags = HideFlags.HideAndDontSave;

		// Can't create the MeshFilter here, since for some reason the DontSave flag has
		// no effect on it. Has to be added to the prefab instead.
		//m_SourceMeshFilter = gameObject.AddComponent<MeshFilter>();
		MeshFilter mfs = gameObject.GetComponent<MeshFilter>();
		// Hmm, causes trouble
		// mfs.hideFlags = HideFlags.HideInInspector;
		mfs.sharedMesh = m_SourceMesh;

		// A similar problem here.
		// m_SourceRenderer = gameObject.AddComponent<MeshRenderer>();
		m_SourceRenderer = gameObject.GetComponent<MeshRenderer>();
		m_SourceRenderer.enabled = true;

		// We want it to be pickable in the scene view, so no HideAndDontSave
		// Hmm, causes trouble
		// m_SourceRenderer.hideFlags = HideFlags.DontSave | HideFlags.HideInInspector;

		m_SourceTransform = transform;

		m_Initialized = true;
		return true;
	}

	void OnEnable()
	{
		if(m_props == null)
			m_props = new MaterialPropertyBlock();

		if(!Init())
			return;
		UpdateMeshesAndBounds();
	}

	void OnDisable()
	{
		if(!Application.isPlaying)
			Cleanup();
		else
			for(var e = m_Cameras.GetEnumerator(); e.MoveNext();)
				if(e.Current.Value != null)
					e.Current.Value.Clear();
	}

	void OnDestroy()
	{
		if (m_ProxyMaterial != null)
			DestroyImmediate(m_ProxyMaterial);
		if (m_SourceMesh != null)
			DestroyImmediate(m_SourceMesh);
		Cleanup();
	}

	void Cleanup()
	{
		for(var e = m_Cameras.GetEnumerator(); e.MoveNext();)
		{
			var cam = e.Current;
			if(cam.Key != null && cam.Value != null)
				cam.Key.RemoveCommandBuffer (kCameraEvent, cam.Value);
		}
		m_Cameras.Clear();
	}

	void UpdateMeshesAndBounds()
	{
		// Sanitize
		m_Range = Mathf.Max(m_Range, 0);
		m_Radius = Mathf.Max(m_Radius, 0);
		m_Length = Mathf.Max(m_Length, 0);
		m_Intensity = Mathf.Max(m_Intensity, 0);

		Vector3 sourceSize = renderSource ? Vector3.one * m_Radius * 2.0f : Vector3.one;
		if (m_SourceTransform.localScale != sourceSize || m_Length != m_LastLength)
		{
			m_LastLength = m_Length;
			
			Vector3[] vertices = m_Capsule.vertices;
			for (int i = 0; i < vertices.Length; i++)
			{
				if (renderSource)
					vertices[i].y += Mathf.Sign(vertices[i].y) * (- 0.5f + 0.25f * m_Length / m_Radius);
				else
					vertices[i] = Vector3.one * 0.0001f;
			}

			m_SourceMesh.vertices = vertices;
		}

		m_SourceTransform.localScale = sourceSize;

		float range = m_Range + m_Radius;

		// TODO: lazy for now, should draw a tight capsule
		range += 0.5f * m_Length;
		range *= 1.02f;

		range /= m_Radius;

		m_SourceMesh.bounds = new Bounds(Vector3.zero, Vector3.one * range);
	}

	void Update()
	{
		if(!Init())
			return;

		UpdateMeshesAndBounds();

		if(Application.isPlaying)
			for(var e = m_Cameras.GetEnumerator(); e.MoveNext();)
				if(e.Current.Value != null)
					e.Current.Value.Clear();
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

	void OnWillRenderObject()
	{
		if(InsideShadowmapCameraRender())
			return;

		if(!Init())
			return;

		// TODO: This is just a very rough guess. Need to properly calculate the surface emission
		// intensity based on light's intensity.
		m_props.SetVector("_EmissionColor", m_Color * Mathf.Sqrt(m_Intensity) * 2.0f);
		m_SourceRenderer.SetPropertyBlock(m_props);

		SetUpCommandBuffer();
	}

	void SetUpCommandBuffer()
	{
		Camera cam = Camera.current;
		CommandBuffer buf = null;
		if (!m_Cameras.ContainsKey(cam))
		{
			buf = new CommandBuffer();
			buf.name = gameObject.name;
			m_Cameras[cam] = buf;
			cam.AddCommandBuffer(kCameraEvent, buf);
			cam.depthTextureMode |= DepthTextureMode.Depth;
		}
		else
		{
			buf = m_Cameras[cam];
			buf.Clear();
		}

		Transform t = transform;
		Vector3 lightAxis = t.up;
		Vector3 lightPos = t.position - 0.5f * lightAxis * m_Length;
		buf.SetGlobalVector("_LightPos", new Vector4(lightPos.x, lightPos.y, lightPos.z, 1.0f/(m_Range*m_Range)));
		buf.SetGlobalVector("_LightAxis", new Vector4(lightAxis.x, lightAxis.y, lightAxis.z, 0.0f));
		buf.SetGlobalFloat("_LightAsQuad", 0);
		buf.SetGlobalFloat("_LightRadius", m_Radius);
		buf.SetGlobalFloat("_LightLength", m_Length);
		buf.SetGlobalVector("_LightColor", GetColor());

		SetShadowPlaneVectors(buf);

		float range = m_Range + m_Radius;
		// TODO: lazy for now, should draw a tight capsule
		range += 0.5f * m_Length;
		range *= 1.02f;
		range /= m_Radius;

		Matrix4x4 m = Matrix4x4.Scale(Vector3.one * range);
		buf.DrawMesh(m_Sphere, t.localToWorldMatrix * m, m_ProxyMaterial, 0, 0);
	}

	public TubeLightShadowPlane.Params[] GetShadowPlaneParams(ref TubeLightShadowPlane.Params[] p)
	{
		if (p == null || p.Length != maxPlanes)
			p = new TubeLightShadowPlane.Params[maxPlanes];
		for(int i = 0; i < maxPlanes; i++)
		{
			TubeLightShadowPlane sp = m_ShadowPlanes[i];
			p[i].plane = sp == null ? new Vector4(0, 0, 0, 1) : sp.GetShadowPlaneVector();
			p[i].feather = sp == null ? 1 : sp.feather;
		}
		return p;
	}

	TubeLightShadowPlane.Params[] sppArr = new TubeLightShadowPlane.Params[maxPlanes];

	void SetShadowPlaneVectors(CommandBuffer buf)
	{
		var p = GetShadowPlaneParams(ref sppArr);

		for (int i = 0, n = p.Length; i < n; ++i)
		{
			var spp = p[i];
			if(i == 0) {
				buf.SetGlobalVector("_ShadowPlane0", spp.plane);
				buf.SetGlobalFloat("_ShadowPlaneFeather0", spp.feather);
			} else if(i == 1) {
				buf.SetGlobalVector("_ShadowPlane1", spp.plane);
				buf.SetGlobalFloat("_ShadowPlaneFeather1", spp.feather);
			} else {
				buf.SetGlobalVector("_ShadowPlane" + i, spp.plane);
				buf.SetGlobalFloat("_ShadowPlaneFeather" + i, spp.feather);
			}
		}
	}

	void OnDrawGizmosSelected()
	{
		if (m_SourceTransform == null)
			return;

		Gizmos.color = Color.white;
		// Skip the scale
		Matrix4x4 m = new Matrix4x4();
		m.SetTRS(m_SourceTransform.position, m_SourceTransform.rotation, Vector3.one);
		Gizmos.matrix = m;

		Gizmos.DrawWireSphere(Vector3.zero, m_Radius + m_Range + 0.5f * m_Length);

		Vector3 start = 0.5f * Vector3.up * m_Length;
		Gizmos.DrawWireSphere(start, m_Radius);

		if (m_Length == 0.0f)
			return;

		Vector3 end = - 0.5f * Vector3.up * m_Length;
		Gizmos.DrawWireSphere(end, m_Radius);

		Vector3 r = Vector3.forward * m_Radius;
		Gizmos.DrawLine(start + r, end + r);
		Gizmos.DrawLine(start - r, end - r);

		r = Vector3.right * m_Radius;
		Gizmos.DrawLine(start + r, end + r);
		Gizmos.DrawLine(start - r, end - r);

	}

	void OnDrawGizmos()
	{
		// TODO: Looks like this changed the name. Find a more robust way to use the icon.
        // Gizmos.DrawIcon(transform.position, "PointLight Gizmo_MIP0.png", true);
    }

    bool InsideShadowmapCameraRender()
	{
		RenderTexture target = Camera.current.targetTexture;
		return target != null && target.format == RenderTextureFormat.Shadowmap;
	}
}
