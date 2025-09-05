using UnityEngine;

public class DroneLandingController : MonoBehaviour
{
    public enum DroneState { Idle, Takeoff, FlyHorizontal, Descend, Landed }

    [Header("Ground")]
    [Tooltip("Layer(s) considered ground for landing height detection.")]
    public LayerMask groundMask;
    [Tooltip("Fallback ground height if no collider is hit (e.g., flat plane at y=0).")]
    public float defaultGroundY = 0f;

    [Header("Flight Profile")]
    [Tooltip("Cruise height above ground while moving horizontally.")]
    public float cruiseHeight = 10f;
    [Tooltip("Horizontal movement speed (m/s).")]
    public float horizontalSpeed = 8f;
    [Tooltip("Vertical descent/ascent speed (m/s).")]
    public float verticalSpeed = 3.5f;
    [Tooltip("How close to the XY target (XZ in Unity) we consider 'arrived' before descending.")]
    public float arriveRadius = 0.5f;
    [Tooltip("Final landing threshold above ground to consider 'Landed'.")]
    public float landThreshold = 0.1f;

    [Header("Clearance")]
    [Tooltip("Optional: radius checked for obstacles before landing (0 to skip).")]
    public float landClearRadius = 0.0f;
    [Tooltip("Layer(s) to check for obstacle clearance at landing.")]
    public LayerMask obstacleMask;

    [Header("Debug / Input")]
    [Tooltip("Press L to send the drone to 'demoTargetXZ'.")]
    public Vector2 demoTargetXZ = new Vector2(0, 0);
    [Tooltip("Enable mouse click-to-command: Left-Click on ground to set target.")]
    public bool enableMouseClickCommand = true;

    // State
    private DroneState _state = DroneState.Idle;
    private Vector3 _targetWorld;   // world landing point (x, groundY, z)
    private float _targetGroundY;   // sampled ground Y at target
    private bool _hasTarget;

    // --- Public API ---

    /// <summary>
    /// Command the drone to land at park coordinates (x,z).
    /// If you pass (x,y) from your request, treat y as z here.
    /// </summary>
    public void SetLandingTarget(float x, float z)
    {
        Vector3 point = new Vector3(x, SampleGroundYAt(new Vector3(x, 1000f, z)), z);

        // Optional: check for obstacle clearance
        if (landClearRadius > 0f)
        {
            bool blocked = Physics.CheckSphere(point + Vector3.up * 0.5f, landClearRadius, obstacleMask, QueryTriggerInteraction.Ignore);
            if (blocked)
            {
                Debug.LogWarning("Landing spot blocked. Choose another point or reduce landClearRadius.");
                return;
            }
        }

        _targetWorld = point;
        _targetGroundY = point.y;
        _hasTarget = true;

        // If we’re on the ground or idle, first go up to cruise height, then move horizontally.
        _state = DroneState.Takeoff;
    }

    // --- Unity ---

    void Update()
    {
        // Optional: quick testing
        if (Input.GetKeyDown(KeyCode.L))
        {
            SetLandingTarget(demoTargetXZ.x, demoTargetXZ.y); // L = Go to demo target
        }
        if (enableMouseClickCommand && Input.GetMouseButtonDown(0))
        {
            if (TryGetMouseGroundPoint(out Vector3 hitPoint))
            {
                SetLandingTarget(hitPoint.x, hitPoint.z);
            }
        }

        if (!_hasTarget) return;

        switch (_state)
        {
            case DroneState.Takeoff:
                DoTakeoff();
                break;
            case DroneState.FlyHorizontal:
                DoFlyHorizontal();
                break;
            case DroneState.Descend:
                DoDescend();
                break;
            case DroneState.Landed:
                // Stay put
                break;
        }
    }

    void DoTakeoff()
    {
        // Target takeoff height above current ground under the drone (not target ground)
        float currentGroundY = SampleGroundYAt(transform.position);
        float targetY = currentGroundY + cruiseHeight;

        Vector3 pos = transform.position;
        if (pos.y < targetY - 0.02f)
        {
            float step = verticalSpeed * Time.deltaTime;
            pos.y = Mathf.MoveTowards(pos.y, targetY, step);
            transform.position = pos;
        }
        else
        {
            _state = DroneState.FlyHorizontal;
        }
    }

    void DoFlyHorizontal()
    {
        // Keep cruising at target ground + cruiseHeight while moving toward target XZ
        Vector3 pos = transform.position;

        // Horizontal move
        Vector3 targetXZ = new Vector3(_targetWorld.x, pos.y, _targetWorld.z);
        Vector3 toTarget = (targetXZ - pos);
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        float distXZ = toTargetXZ.magnitude;

        if (distXZ > arriveRadius)
        {
            Vector3 dir = toTargetXZ.normalized;
            pos += dir * horizontalSpeed * Time.deltaTime;
        }

        // Maintain cruise height relative to target ground
        float targetCruiseY = _targetGroundY + cruiseHeight;
        pos.y = Mathf.MoveTowards(pos.y, targetCruiseY, verticalSpeed * Time.deltaTime);

        // Face movement direction if moving
        if (toTargetXZ.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(toTargetXZ.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 6f * Time.deltaTime);
        }

        transform.position = pos;

        if (distXZ <= arriveRadius)
        {
            _state = DroneState.Descend;
        }
    }

    void DoDescend()
    {
        // Gently come down to target ground height
        Vector3 pos = transform.position;
        float targetY = _targetGroundY;

        // Optional: small hover offset before final contact could be used
        pos.y = Mathf.MoveTowards(pos.y, targetY, verticalSpeed * Time.deltaTime);
        transform.position = pos;

        // Update rotation to a neutral landing orientation
        Quaternion flat = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, flat, 2f * Time.deltaTime);

        if (Mathf.Abs(pos.y - targetY) <= landThreshold)
        {
            // Snap and mark landed
            pos.y = targetY;
            transform.position = pos;
            _state = DroneState.Landed;
            _hasTarget = false;
            // Optional: trigger landing animation/event here
        }
    }

    // --- Helpers ---

    bool TryGetMouseGroundPoint(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;
            return true;
        }
        return false;
    }

    float SampleGroundYAt(Vector3 worldProbe)
    {
        // Cast downward from high above the probe to find ground
        Vector3 start = new Vector3(worldProbe.x, worldProbe.y + 1000f, worldProbe.z);
        if (Physics.Raycast(start, Vector3.down, out var hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return defaultGroundY; // fallback if no ground collider hit
    }

    void OnDrawGizmosSelected()
    {
        if (_hasTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_targetWorld + Vector3.up * 0.05f, 0.5f);
            Gizmos.DrawLine(transform.position, _targetWorld + Vector3.up * cruiseHeight);
        }
    }
}