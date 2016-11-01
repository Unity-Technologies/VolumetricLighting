using UnityEngine;

[ExecuteInEditMode]
public partial class AreaLight : MonoBehaviour
{
	public bool m_RenderSource = true;
	public Vector3 m_Size = new Vector3(1, 1, 2);
	[Range(0, 179)]
	public float m_Angle = 0.0f;
	[MinValue(0)]
	public float m_Intensity = 0.8f;
	public Color m_Color = Color.white;

	[Header("Shadows")]
	public bool m_Shadows = false;
	public LayerMask m_ShadowCullingMask = ~0;
	public TextureSize m_ShadowmapRes = TextureSize.x2048;
	[MinValue(0)]
	public float m_ReceiverSearchDistance = 24.0f;
	[MinValue(0)]
	public float m_ReceiverDistanceScale = 5.0f;
	[MinValue(0)]
	public float m_LightNearSize = 4.0f;
	[MinValue(0)]
	public float m_LightFarSize = 22.0f;
	[Range(0, 0.1f)]
	public float m_ShadowBias = 0.001f;

	MeshRenderer m_SourceRenderer;
	Mesh m_SourceMesh;
	[HideInInspector]
	public Mesh m_Quad;
	Vector2 m_CurrentQuadSize = Vector2.zero;
	Vector3 m_CurrentSize = Vector3.zero;
	float m_CurrentAngle = -1.0f;

	bool m_Initialized = false;
	MaterialPropertyBlock m_props;

	void Awake()
	{
		if(!Init())
			return;
		UpdateSourceMesh();
	}

	bool Init()
	{
		if (m_Initialized)
			return true;

		if (m_Quad == null || !InitDirect())
			return false;

		m_SourceRenderer = GetComponent<MeshRenderer>();
		m_SourceRenderer.enabled = true;
		m_SourceMesh = Instantiate<Mesh>(m_Quad);
		m_SourceMesh.hideFlags = HideFlags.HideAndDontSave;
		MeshFilter mfs = gameObject.GetComponent<MeshFilter>();
		mfs.sharedMesh = m_SourceMesh;

		Transform t = transform;
		if (t.localScale != Vector3.one)
		{
#if UNITY_EDITOR
			Debug.LogError("AreaLights don't like to be scaled. Setting local scale to 1.", this);
#endif
			t.localScale = Vector3.one;
		}

		SetUpLUTs();

		m_Initialized = true;
		return true;
	}

	void OnEnable()
	{
		m_props = new MaterialPropertyBlock();

		if(!Init())
			return;
		UpdateSourceMesh();
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

	static Vector3[] vertices = new Vector3[4];

	void UpdateSourceMesh()
	{
		m_Size.x = Mathf.Max(m_Size.x, 0);
		m_Size.y = Mathf.Max(m_Size.y, 0);
		m_Size.z = Mathf.Max(m_Size.z, 0);

		Vector2 quadSize = m_RenderSource && enabled ? new Vector2(m_Size.x, m_Size.y) : Vector2.one * 0.0001f;
		if (quadSize != m_CurrentQuadSize)
		{

			float x = quadSize.x * 0.5f;
			float y = quadSize.y * 0.5f;
			// To prevent the source quad from getting into the shadowmap, offset it back a bit.
			float z = -0.001f;
			vertices[0].Set(-x,  y, z);
			vertices[1].Set( x, -y, z);
			vertices[2].Set( x,  y, z);
			vertices[3].Set(-x, -y, z);

			m_SourceMesh.vertices = vertices;

			m_CurrentQuadSize = quadSize;
		}

		if (m_Size != m_CurrentSize || m_Angle != m_CurrentAngle)
		{
			// Set the bounds of the mesh to large, so that they drive rendering of the entire light
			// TODO: Make the bounds tight around the shape of the light. Right now they're just tight around
			// the shadow frustum, which is fine if the shadows are enable (ok, maybe far plane should be more clever),
			// but doesn't make sense if shadows are disabled.
			m_SourceMesh.bounds = GetFrustumBounds();
		}
	}

	void Update()
	{
		if (!gameObject.activeInHierarchy || !enabled)
		{
			Cleanup();
			return;
		}

		if(!Init())
			return;

		UpdateSourceMesh();

		if(Application.isPlaying)
			for(var e = m_Cameras.GetEnumerator(); e.MoveNext();)
				if(e.Current.Value != null)
					e.Current.Value.Clear();
	}

	void OnWillRenderObject()
	{
		if(!Init())
			return;

		// TODO: This is just a very rough guess. Need to properly calculate the surface emission
		// intensity based on light's intensity.
		Color color = new Color(
			Mathf.GammaToLinearSpace(m_Color.r),
			Mathf.GammaToLinearSpace(m_Color.g),
			Mathf.GammaToLinearSpace(m_Color.b),
			1.0f);
		m_props.SetVector("_EmissionColor", color * m_Intensity);
		m_SourceRenderer.SetPropertyBlock(m_props);

		SetUpCommandBuffer();
	}

	float GetNearToCenter()
	{
		if (m_Angle == 0.0f)
			return 0;

		return m_Size.y * 0.5f / Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad);
	}

	Matrix4x4 GetOffsetMatrix(float zOffset)
	{
		Matrix4x4 m = Matrix4x4.identity;
		m.SetColumn(3, new Vector4(0, 0, zOffset, 1));
		return m;
	}

	public Matrix4x4 GetProjectionMatrix(bool linearZ = false)
	{
		Matrix4x4 m;

		if (m_Angle == 0.0f)
		{
			m = Matrix4x4.Ortho(-0.5f * m_Size.x, 0.5f * m_Size.x, -0.5f * m_Size.y, 0.5f * m_Size.y, 0, -m_Size.z);
		}
		else
		{
			float near = GetNearToCenter();
			if (linearZ)
			{
				m = PerspectiveLinearZ(m_Angle, m_Size.x/m_Size.y, near, near + m_Size.z);
			}
			else
			{
				m = Matrix4x4.Perspective(m_Angle, m_Size.x/m_Size.y, near, near + m_Size.z);
				m = m * Matrix4x4.Scale(new Vector3(1, 1, -1));
			}
			m = m * GetOffsetMatrix(near);
		}
		
		return m * transform.worldToLocalMatrix;
	}

	public Vector4 MultiplyPoint(Matrix4x4 m, Vector3 v)
	{
		Vector4 res;
		res.x = m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03;
		res.y = m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13;
		res.z = m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23;
		res.w = m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33;
		return res;
	}

	Matrix4x4 PerspectiveLinearZ(float fov, float aspect, float near, float far)
	{
		// A vector transformed with this matrix should get perspective division on x and y only:
		// Vector4 vClip = MultiplyPoint(PerspectiveLinearZ(...), vEye);
		// Vector3 vNDC = Vector3(vClip.x / vClip.w, vClip.y / vClip.w, vClip.z);
		// vNDC is [-1, 1]^3 and z is linear, i.e. z = 0 is half way between near and far in world space.

		float rad = Mathf.Deg2Rad * fov * 0.5f;
		float cotan = Mathf.Cos(rad) / Mathf.Sin(rad);
		float deltainv = 1.0f / (far - near);
		Matrix4x4 m;

		m.m00 = cotan / aspect;	m.m01 = 0.0f;	m.m02 = 0.0f;			 m.m03 = 0.0f;
		m.m10 = 0.0f;			m.m11 = cotan; 	m.m12 = 0.0f;			 m.m13 = 0.0f;
		m.m20 = 0.0f;			m.m21 = 0.0f;	m.m22 = 2.0f * deltainv; m.m23 = - (far + near) * deltainv;
		m.m30 = 0.0f;			m.m31 = 0.0f;	m.m32 = 1.0f;			 m.m33 = 0.0f;

		return m;
	}

	public Vector4 GetPosition()
	{
		Transform t = transform;

		if (m_Angle == 0.0f)
		{
			Vector3 dir = -t.forward;
			return new Vector4(dir.x, dir.y, dir.z, 0);
		}

		Vector3 pos = t.position - GetNearToCenter() * t.forward;
		return new Vector4(pos.x, pos.y, pos.z, 1);
	}

	Bounds GetFrustumBounds()
	{
		if (m_Angle == 0.0f)
			return new Bounds(Vector3.zero, m_Size);

		float tanhalffov = Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad);
		float near = m_Size.y * 0.5f / tanhalffov;
		float z = m_Size.z;
		float y = (near + m_Size.z) * tanhalffov * 2.0f;
		float x = m_Size.x * y / m_Size.y;
		return new Bounds(Vector3.forward * m_Size.z * 0.5f, new Vector3(x, y, z));
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.white;

		if (m_Angle == 0.0f)
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(new Vector3(0, 0, 0.5f * m_Size.z), m_Size);
			return;
		}

		float near = GetNearToCenter();
		Gizmos.matrix = transform.localToWorldMatrix * GetOffsetMatrix(-near);

		Gizmos.DrawFrustum(Vector3.zero, m_Angle, near + m_Size.z, near, m_Size.x/m_Size.y);

		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.color = Color.yellow;
		Bounds bounds = GetFrustumBounds();
		Gizmos.DrawWireCube(bounds.center, bounds.size);
	}
}
