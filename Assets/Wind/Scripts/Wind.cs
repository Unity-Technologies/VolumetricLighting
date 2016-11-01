using UnityEngine;

public class Wind : MonoBehaviour {

	[MinValue(0)]
	public float m_Speed = 1.0f;

	void OnDrawGizmosSelected()
	{
		Vector3[] arrow =
		{
			new Vector3(0,0,1.5f),
			new Vector3( 1.0f,0.0f, 0.5f), new Vector3( 0.5f,0.0f,0.5f), new Vector3( 0.5f,0.0f,-1.0f),
			new Vector3(-0.5f,0.0f,-1.0f), new Vector3(-0.5f,0.0f,0.5f), new Vector3(-1.0f,0.0f, 0.5f),
			new Vector3(0,0,1.5f)
		};

		Gizmos.matrix = transform.localToWorldMatrix;
		int count = arrow.Length;

		for(int i = 0; i < count; i++)
			Gizmos.DrawLine(arrow[i], arrow[(i + 1) % count]);

		Gizmos.matrix = Gizmos.matrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one);

		for(int i = 0; i < count; i++)
			Gizmos.DrawLine(arrow[i], arrow[(i + 1) % count]);
	}

}
