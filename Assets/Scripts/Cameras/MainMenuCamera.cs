using UnityEngine;

public class MainMenuCamera : MonoBehaviour
{
	[SerializeField] private Transform target = null;
	[SerializeField] private float orbitRadius = 20f;
	[SerializeField] private float orbitSpeedDegreesPerSecond = 10f;
	[SerializeField] private float baseHeight = 10f;
	[SerializeField] private float verticalAmplitude = 2f;
	[SerializeField] private float verticalFrequencyHz = 0.25f;
	[SerializeField] private bool smoothMotion = true;
	[SerializeField] private float smoothTime = 0.3f;
	[SerializeField] private Vector3 lookAtOffset = Vector3.zero;

	private float angleDegrees;
	private Vector3 velocity = Vector3.zero;
	private bool initialized = false;

	void Start()
	{
		if (target == null)
		{
			var go = GameObject.FindWithTag("Island");
			if (go != null) target = go.transform;
		}

		if (target != null)
		{
			Vector3 toCam = transform.position - target.position;
			angleDegrees = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;

			float angleRad = angleDegrees * Mathf.Deg2Rad;
			Vector3 center = target.position;
			float x = center.x + Mathf.Cos(angleRad) * orbitRadius;
			float z = center.z + Mathf.Sin(angleRad) * orbitRadius;
			float bob = Mathf.Sin(Time.time * verticalFrequencyHz * Mathf.PI * 2f) * verticalAmplitude;
			float y = center.y + baseHeight + bob;
			transform.position = new Vector3(x, y, z);
			velocity = Vector3.zero;
			initialized = true;
		}
		else
		{
			angleDegrees = 0f;
		}
	}

	void LateUpdate()
	{
		if (target == null) return;

		if (!initialized)
		{
			Vector3 toCam = transform.position - target.position;
			angleDegrees = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;

			float angleRadSnap = angleDegrees * Mathf.Deg2Rad;
			Vector3 centerSnap = target.position;
			float xSnap = centerSnap.x + Mathf.Cos(angleRadSnap) * orbitRadius;
			float zSnap = centerSnap.z + Mathf.Sin(angleRadSnap) * orbitRadius;
			float bobSnap = Mathf.Sin(Time.time * verticalFrequencyHz * Mathf.PI * 2f) * verticalAmplitude;
			float ySnap = centerSnap.y + baseHeight + bobSnap;
			transform.position = new Vector3(xSnap, ySnap, zSnap);
			velocity = Vector3.zero;
			initialized = true;
		}

		angleDegrees += orbitSpeedDegreesPerSecond * Time.deltaTime;
		float angleRad = angleDegrees * Mathf.Deg2Rad;

		Vector3 center = target.position;
		float x = center.x + Mathf.Cos(angleRad) * orbitRadius;
		float z = center.z + Mathf.Sin(angleRad) * orbitRadius;
		float bob = Mathf.Sin(Time.time * verticalFrequencyHz * Mathf.PI * 2f) * verticalAmplitude;
		float y = center.y + baseHeight + bob;

		Vector3 desiredPos = new Vector3(x, y, z);

		if (smoothMotion)
		{
			transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);
		}
		else
		{
			transform.position = desiredPos;
		}

		transform.LookAt(center + lookAtOffset);
	}
}
