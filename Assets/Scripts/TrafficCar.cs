using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Movement")]
    public float targetSpeed = 20f;   // m/s (siempre positiva)
    public float accel = 8f;          // m/s^2
    public float brake = 12f;         // m/s^2
    public float minFollowSpeed = 6f; // m/s (positiva)

    [Header("Detection")]
    public float safeDistance = 18f;
    public float rayRadius = 0.6f;
    public LayerMask trafficMask;     // capa "Traffic"

    [Header("Lanes")]
    public float[] laneX;
    public int currentLane = 0;
    public float laneChangeSpeed = 6f;

    [Header("Direction")]
    public float dirZ = +1f; // +1 = avanza a +Z, -1 = viene a -Z

    [Range(0f, 1f)] public float minFollowRatio = 0.55f;

    Rigidbody rb;

    // ventana de gracia post-spawn para no frenar de entrada
    float detectDisabledUntil = -1f;

    public void ArmSpawnGrace(float seconds)
    {
        detectDisabledUntil = Time.time + Mathf.Max(0f, seconds);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.freezeRotation = true;

        gameObject.layer = LayerMask.NameToLayer("Traffic");
        if (trafficMask.value == 0)
            trafficMask = 1 << LayerMask.NameToLayer("Traffic");

        transform.rotation = Quaternion.LookRotation(dirZ > 0f ? Vector3.forward : Vector3.back);
    }

    void FixedUpdate()
    {
        float v = rb.linearVelocity.z;

        Vector3 fwd = (dirZ > 0f) ? Vector3.forward : Vector3.back;

        Vector3 origin = transform.position + Vector3.up * 0.5f + fwd * (rayRadius + 0.25f);

        float rayLen = Mathf.Max(safeDistance, Mathf.Abs(v) * 0.8f + 8f);

        bool blocked = false;
        RaycastHit hit;

        if (Time.time >= detectDisabledUntil &&
            Physics.SphereCast(origin, rayRadius, fwd, out hit, rayLen, trafficMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != rb)
            {
                float leaderV = 0f;

                if (hit.rigidbody)
                    leaderV = hit.rigidbody.linearVelocity.z;
                else
                    leaderV = dirZ * targetSpeed;

                float closing = (v - leaderV) * dirZ;
                blocked = closing > 0.1f; 
            }
        }

        float desiredZ = dirZ * targetSpeed;

        float minZ = dirZ * Mathf.Max(minFollowSpeed, targetSpeed * minFollowRatio);

        if (blocked)
        {
            v = Mathf.MoveTowards(v, minZ, brake * Time.fixedDeltaTime);
        }
        else
        {
            v = Mathf.MoveTowards(v, desiredZ, accel * Time.fixedDeltaTime);
        }

        if (dirZ > 0f && v < 0f) v = 0f;
        if (dirZ < 0f && v > 0f) v = 0f;

        float dx = laneX != null && laneX.Length > 0 ? (laneX[currentLane] - rb.position.x) : 0f;
        float xSpeed = Mathf.Clamp(dx / Time.fixedDeltaTime, -laneChangeSpeed, laneChangeSpeed);

        rb.linearVelocity = new Vector3(xSpeed, 0f, v);

#if UNITY_EDITOR
        Debug.DrawRay(origin, fwd * rayLen, blocked ? Color.red : Color.cyan);
#endif
    }
}
