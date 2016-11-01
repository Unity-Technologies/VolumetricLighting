using UnityEngine;

public class TubeLightShadowPlane : MonoBehaviour
{
	[MinValue(0)]
	public float m_Feather = 1.0f;

	public float feather {get{ return m_Feather * 0.1f;}}

	public Vector4 GetShadowPlaneVector()
	{
		Transform t = transform;
		Vector3 v = t.forward;
		float d = Vector3.Dot(t.position, v);
		return new Vector4(v.x, v.y, v.z, d);
	}

	void OnDrawGizmosSelected()
	{
		Matrix4x4 m = Matrix4x4.zero;
		Transform t = transform;
		m.SetTRS(t.position, t.rotation, new Vector3(1, 1, 0));
		Gizmos.matrix = m;
		Gizmos.DrawWireSphere(Vector3.zero, 1);
	}

	public struct Params
	{
		public Vector4 plane;
		public float feather;
	}
}
