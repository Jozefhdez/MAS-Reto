using UnityEngine;

public class DroneLandingController : MonoBehaviour
{
    public enum DroneState { Idle, Takeoff, FlyHorizontal, Hover, Descend, Landed }

    [Header("Ground")]
    [Tooltip("Layer(s) considered ground for landing height detection.")]
    public LayerMask groundMask;
    [Tooltip("Fallback ground height if no collider is hit (e.g., flat plane at y=0).")]
    public float defaultGroundY = 0f;

    [Header("Flight Profile")]
    [Tooltip("Cruise height above ground while moving horizontally.")]
    public float cruiseHeight = 30f;
    [Tooltip("Horizontal movement speed (m/s).")]
    public float horizontalSpeed = 8f;
    [Tooltip("Vertical descent/ascent speed (m/s).")]
    public float verticalSpeed = 7f;
    [Tooltip("How close to the XY target (XZ in Unity) we consider 'arrived' before descending.")]
    public float arriveRadius = 0.5f;
    [Tooltip("Final landing threshold above ground to consider 'Landed'.")]
    public float landThreshold = 0.1f;

    [Header("Obstacles")]
    public LayerMask obstacleMask;
    public float clearanceRadius = 1f;
    public float avoidLookAhead = 4f;
    public float landClearRadius = 2f;

    public Vector2 demoTargetXZ = new Vector2(0, 0);
    public bool enableMouseClickCommand = false; // DESACTIVADO PERMANENTEMENTE

    // State
    private DroneState _state = DroneState.Idle;
    private Vector3 _targetWorld;
    private float _targetGroundY;
    private bool _hasTarget;
    private Vector3 _origTargetWorld;
    private bool _hasDetour;
    private Vector3 _detourPoint;

    // --- Public API ---
    
    public DroneState CurrentState => _state;
    public bool IsLanded => _state == DroneState.Landed;
    public bool HasActiveTarget => _hasTarget;

    public void SetLandingTarget(float x, float z)
    {
        Vector3 point = new Vector3(x, SampleGroundYAt(new Vector3(x, 1000f, z)), z);

        _targetWorld = point;
        _origTargetWorld = point;
        _targetGroundY = point.y;
        _hasTarget = true;
        _hasDetour = false;

        _state = DroneState.Takeoff;
    }

    // --- Unity ---

    void Update()
    {
        
        if (!_hasTarget) return;

        switch (_state)
        {
            case DroneState.Takeoff:
                DoTakeoff();
                break;
            case DroneState.FlyHorizontal:
                DoFlyHorizontal();
                break;
            case DroneState.Hover:
                DoHover();
                break;
            case DroneState.Descend:
                DoDescend();
                break;
            case DroneState.Landed:
                break;
        }
    }

    void DoTakeoff()
    {
        float currentGroundY = SampleGroundYAt(transform.position);
        float targetY = currentGroundY + cruiseHeight;

        Vector3 pos = transform.position;

        // If something is directly ahead while we’re taking off, make a small detour in XZ first
        Vector3 toXZ = new Vector3(_targetWorld.x - pos.x, 0f, _targetWorld.z - pos.z);
        Vector3 dirXZ = toXZ.sqrMagnitude > 1e-6f ? toXZ.normalized : transform.forward;

        if (!_hasDetour && BlockedAheadTall(pos, dirXZ, avoidLookAhead))
        {
            // choose a quick side-step (left or right) by probing which side is open
            Vector3 right = Vector3.Cross(Vector3.up, dirXZ);
            Vector3 candR = pos + right * 2.0f;
            Vector3 candL = pos - right * 2.0f;

            bool freeR = SpotIsClear(candR, clearanceRadius);
            bool freeL = SpotIsClear(candL, clearanceRadius);
            _detourPoint = freeR && !freeL ? candR : (!freeR && freeL ? candL : FindNearestFreeAround(pos));
            _detourPoint.y = pos.y; // keep current height while we step aside
            _hasDetour = true;
        }

        // Move toward detour first (XZ), else climb and go horizontal
        if (_hasDetour)
        {
            Vector3 d = _detourPoint - pos; d.y = 0f;
            if (d.magnitude > 0.2f)
            {
                pos += d.normalized * horizontalSpeed * Time.deltaTime;
                transform.position = pos;
            }
            else
            {
                _hasDetour = false; // detour reached — proceed with normal takeoff
            }
        }

        // Continue vertical climb
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

    void DoHover()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            _state = DroneState.Descend;
        }
    }

    void DoDescend()
    {
        Vector3 pos = transform.position;
        float targetY = _targetGroundY;

        Vector3 targetXZ   = new Vector3(_targetWorld.x, 0f, _targetWorld.z);
        Vector3 originalXZ = new Vector3(_origTargetWorld.x, 0f, _origTargetWorld.z);

        // Require full column clearance (ground -> current height)
        if (!LandingColumnIsClear(targetXZ, _targetGroundY, pos.y, landClearRadius))
        {
            Vector3 clearXZ = FindNearestFreeAround(originalXZ);
            if ((clearXZ - targetXZ).sqrMagnitude > 0.01f)
            {
                _targetWorld     = new Vector3(clearXZ.x, SampleGroundYAt(new Vector3(clearXZ.x, 1000f, clearXZ.z)), clearXZ.z);
                _targetGroundY   = _targetWorld.y;
                _state           = DroneState.FlyHorizontal;
                return;
            }
        }

        // Keep your lateral sidestep if blocked directly under you
        if (BlockedBelow(pos, 2.0f))
        {
            Vector3 side = FindNearestFreeAround(new Vector3(pos.x, 0f, pos.z));
            Vector3 step = (new Vector3(side.x, pos.y, side.z) - pos);
            step.y = 0f;
            if (step.sqrMagnitude > 1e-6f)
            {
                pos += step.normalized * (horizontalSpeed * 0.6f) * Time.deltaTime;
                transform.position = pos;
                return;
            }
        }

        // Re-check the column at our current XY before moving down this frame
        if (!LandingColumnIsClear(new Vector3(pos.x, 0f, pos.z), _targetGroundY, pos.y, landClearRadius))
        {
            // small lateral nudge toward nearest free column
            Vector3 side = FindNearestFreeAround(new Vector3(pos.x, 0f, pos.z));
            Vector3 step = (new Vector3(side.x, pos.y, side.z) - pos);
            step.y = 0f;
            if (step.sqrMagnitude > 1e-6f)
            {
                pos += step.normalized * (horizontalSpeed * 0.6f) * Time.deltaTime;
                transform.position = pos;
                return;
            }
        }

        // Vertical descent
        pos.y = Mathf.MoveTowards(pos.y, targetY, verticalSpeed * Time.deltaTime);
        transform.position = pos;

        if (Mathf.Abs(pos.y - targetY) <= landThreshold)
        {
            pos.y = targetY;
            transform.position = pos;
            _state = DroneState.Landed;
            _hasTarget = false;
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

    bool BlockedAheadTall(Vector3 pos, Vector3 dir, float look, float height = 1.5f)
    {
        dir = new Vector3(dir.x, 0f, dir.z).normalized;
        Vector3 bottom = pos + Vector3.up * 0.5f;
        Vector3 top    = bottom + Vector3.up * height;
        return Physics.CapsuleCast(bottom, top, clearanceRadius, dir, out _, look, obstacleMask, QueryTriggerInteraction.Collide);
    }

    bool BlockedBelow(Vector3 pos, float downDist)
    {
        return Physics.SphereCast(pos, landClearRadius, Vector3.down,
                                out _, downDist, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    public void SetTarget(Transform person)
    {
        if (person != null)
        {
            Vector3 directionAway = Random.insideUnitCircle.normalized;
            Vector3 landingPoint = person.position + new Vector3(directionAway.x, 0, directionAway.y) * 4f;

            SetLandingTarget(landingPoint.x, landingPoint.z);
            Debug.Log($"Dron moving towards person in coordinates: {person.position}, landing at position: {landingPoint}");
        }
    }

    bool SpotIsClear(Vector3 posXZ, float radius)
    {
        Vector3 p = new Vector3(posXZ.x, _targetGroundY + 0.1f, posXZ.z);
        return !Physics.CheckSphere(p, radius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    bool LandingColumnIsClear(Vector3 centerXZ, float groundY, float topY, float radius)
    {
        Vector3 bottom = new Vector3(centerXZ.x, groundY + landThreshold, centerXZ.z);
        Vector3 top    = new Vector3(centerXZ.x, topY,                centerXZ.z);

        // Use Collide so trigger leaves/foliage colliders are considered too
        return !Physics.CheckCapsule(top, bottom, radius, obstacleMask, QueryTriggerInteraction.Collide);
    }

    Vector3 FindNearestFreeAround(Vector3 centerXZ, float startR = 1.0f, float maxR = 6f, float stepR = 0.6f, int samples = 16)
    {
        Vector3 best = centerXZ;

        // Use the column (ground -> current drone height). If called before takeoff, you can pass a reasonable topY.
        float topY = transform.position.y;

        if (LandingColumnIsClear(centerXZ, _targetGroundY, topY, landClearRadius))
            return best;

        for (float r = startR; r <= maxR; r += stepR)
        {
            for (int i = 0; i < samples; i++)
            {
                float a = (i / (float)samples) * Mathf.PI * 2f;
                Vector3 cand = centerXZ + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (LandingColumnIsClear(cand, _targetGroundY, topY, landClearRadius))
                    return cand;
            }
        }
        return centerXZ;
 
    }
}