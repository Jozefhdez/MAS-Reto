using System.Collections.Generic;
using UnityEngine;

public class DroneVisionSystem : MonoBehaviour
{
    [Header("Vision Configuration")]
    [SerializeField] private float visionRange = 100f;
    [SerializeField] private float visionAngle = 60f; // vision cone angle
    [SerializeField] private LayerMask personLayerMask = 1 << 3; // layer 3 for person
    [SerializeField] private LayerMask obstacleLayerMask = 1 << 6; // layer 6 for trees and bushes
    
    [Header("Scanning Pattern")]
    [SerializeField] private float scanSpeed = 45f; // degrees per second
    [SerializeField] private bool continuousRotation = true;
    
    [Header("Debug")]
    [SerializeField] private bool showVisionGizmos = true;
    [SerializeField] private Color visionColor = Color.yellow;
    [SerializeField] private Color detectionColor = Color.red;
    
    // Internal state
    private List<Transform> detectedPersons = new List<Transform>();
    private List<Transform> ignoredPersons = new List<Transform>(); // persons to ignore
    private float currentScanAngle = 0f;
    private bool isPaused = false;
    private float pauseTimer = 0f;
    private Transform targetPerson = null;
    private bool scanningEnabled = true; // Scanning control
    private bool hasLanded = false; // Landing state
    
    // Events
    public System.Action<Transform> OnPersonDetected;
    public System.Action<Transform> OnPersonLost;
    public System.Action<List<Transform>> OnScanComplete;
    public System.Action<DroneLandingController> OnDroneLanded;
    
    // References
    private DroneLandingController droneController;
    
    void Start()
    {
        droneController = GetComponent<DroneLandingController>();
        StartCoroutine(VisionUpdateCoroutine());
    }
    
    void Update()
    {
        // Check if the drone has landed
        CheckIfLanded();
        
        if (scanningEnabled && continuousRotation && !isPaused && !hasLanded)
        {
            RotateScanner();
        }
        
        if (isPaused)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
            }
        }
    }
    
    private void RotateScanner()
    {
        currentScanAngle += scanSpeed * Time.deltaTime;
        if (currentScanAngle >= 360f)
        {
            currentScanAngle = 0f;
            OnScanComplete?.Invoke(new List<Transform>(detectedPersons));
        }
    }
    
    private System.Collections.IEnumerator VisionUpdateCoroutine()
    {
        while (true)
        {
            if (scanningEnabled && !hasLanded)
            {
                PerformVisionCheck();
            }
            yield return new WaitForSeconds(0.1f); // Update 10 times per second
        }
    }
    
    private void PerformVisionCheck()
    {
        List<Transform> currentFrameDetections = new List<Transform>();
        
        // Use different detection methods based on configuration
        if (continuousRotation)
        {
            // Rotational scan - only check current direction
            currentFrameDetections = ScanInDirection(currentScanAngle);
        }
        else
        {
            // Full 360 degree scan
            currentFrameDetections = ScanFullCircle();
        }

        // Process new detections
        // only if we didnt have ratget and havent landed yet
        if (targetPerson == null && !hasLanded)
        {
            foreach (Transform person in currentFrameDetections)
            {
                // Filter ignored persons
                if (ignoredPersons.Contains(person))
                {
                    continue;
                }

                // Check if person is already assigned to another drone
                if (DroneTargetAssigner.IsPersonAssigned(person))
                {
                    ignoredPersons.Add(person);
                    continue;
                }

                if (!detectedPersons.Contains(person))
                {
                    detectedPersons.Add(person);
                    OnPersonDetected?.Invoke(person);

                    // Assign first valid target and STOP searching
                    if (targetPerson == null && droneController != null)
                    {
                        SetTarget(person);
                        break;
                    }
                }
            }
        }
        
        // Remove lost detections
        for (int i = detectedPersons.Count - 1; i >= 0; i--)
        {
            if (!currentFrameDetections.Contains(detectedPersons[i]))
            {
                Transform lostPerson = detectedPersons[i];
                detectedPersons.RemoveAt(i);
                OnPersonLost?.Invoke(lostPerson);
                
                if (lostPerson == targetPerson && !hasLanded)
                {
                    // Check if really lost line of sight completely
                    float distanceToTarget = Vector3.Distance(transform.position, lostPerson.position);
                    
                    // If too far or really can't see it, then release
                    if (distanceToTarget > visionRange || !HasLineOfSight(lostPerson.position))
                    {
                        DroneTargetAssigner.ReleasePerson(lostPerson);
                        targetPerson = null;
                    }
                }
            }
        }
    }
    
    private List<Transform> ScanInDirection(float angle)
    {
        List<Transform> detected = new List<Transform>();
        
        Vector3 scanDirection = Quaternion.Euler(0, angle, 0) * transform.forward;
        
        // SphereCast to simulate a vision cone
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position, 
            1f, // sensor radius
            scanDirection, 
            visionRange, 
            personLayerMask
        );
        
        foreach (RaycastHit hit in hits)
        {
            // Filter only objects with "Person" tag
            if (hit.transform.CompareTag("Person") &&
                IsWithinVisionCone(hit.transform.position, scanDirection) && 
                HasLineOfSight(hit.transform.position))
            {
                detected.Add(hit.transform);
            }
        }
        
        return detected;
    }
    
    private List<Transform> ScanFullCircle()
    {
        List<Transform> detected = new List<Transform>();
        
        // Search for all persons in range
        Collider[] possibleTargets = Physics.OverlapSphere(transform.position, visionRange, personLayerMask);
        
        foreach (Collider target in possibleTargets)
        {
            // Filter only objects with "Person" tag
            if (target.CompareTag("Person") && HasLineOfSight(target.transform.position))
            {
                detected.Add(target.transform);
            }
        }
        
        return detected;
    }
    
    private bool IsWithinVisionCone(Vector3 targetPosition, Vector3 scanDirection)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        float angleToTarget = Vector3.Angle(scanDirection, directionToTarget);
        return angleToTarget <= visionAngle * 0.5f;
    }
    
    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        // elevate the raycast origin point slightly to avoid detecting own collider
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayTarget = targetPosition + Vector3.up * 0.5f;
        Vector3 rayDirection = (rayTarget - rayOrigin).normalized;
        float rayDistance = Vector3.Distance(rayOrigin, rayTarget);
        
        // Raycast to verify no obstacles
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayDistance, obstacleLayerMask))
        {
            return false;
        }
        
        return true;
    }
    
    public void SetTarget(Transform person)
    {
        if (targetPerson != null)
        {
            return;
        }
        
        targetPerson = person;
        if (droneController != null)
        {
            droneController.SetTarget(person);
        }
        
        // stop scanning when having target
        StopScanning();
    }
    
    private void FindNewTarget()
    {
        if (targetPerson != null || hasLanded)
        {
            return;
        }
        
        if (detectedPersons.Count > 0)
        {
            Transform closest = null;
            float closestDistance = float.MaxValue;
            
            foreach (Transform person in detectedPersons)
            {
                float distance = Vector3.Distance(transform.position, person.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = person;
                }
            }
            
            if (closest != null)
            {
                SetTarget(closest);
            }
        }
    }
    
    public bool HasTarget => targetPerson != null;
    public Transform CurrentTarget => targetPerson;
    public List<Transform> GetDetectedPersons() => new List<Transform>(detectedPersons);
    public int DetectedPersonCount => detectedPersons.Count;
    public bool IsLanded => hasLanded;
    public bool IsScanningEnabled => scanningEnabled;
    
    // Methods to change scanning mode
    public void SetContinuousRotation(bool enabled)
    {
        continuousRotation = enabled;
    }
    
    public void SetScanSpeed(float speed)
    {
        scanSpeed = Mathf.Max(0f, speed);
    }
    
    public void SetVisionRange(float range)
    {
        visionRange = Mathf.Max(1f, range);
    }
    
    // New methods for coordination between drones
    public void IgnorePerson(Transform person)
    {
        if (!ignoredPersons.Contains(person))
        {
            ignoredPersons.Add(person);
        }
    }
    
    public void StopScanning()
    {
        scanningEnabled = false;
    }
    
    public void ResumeScanning()
    {
        // Don't resume scanning if drone has successfully landed with a person
        if (hasLanded && targetPerson != null)
        {
            Debug.Log($"Drone {gameObject.name} will not resume scanning - successfully landed with {targetPerson.name}");
            return;
        }
        
        scanningEnabled = true;
        hasLanded = false;
        
        // Make sure the vision coroutine is running
        StopCoroutine(VisionUpdateCoroutine());
        StartCoroutine(VisionUpdateCoroutine());
        
        Debug.Log($"Drone {gameObject.name} resumed scanning for targets");
    }
    
    // Method to manually release a drone from its current assignment (for simulation reset)
    public void ForceReleaseCurrent()
    {
        if (targetPerson != null)
        {
            Debug.Log($"Drone {gameObject.name} force releasing target {targetPerson.name}");
            DroneTargetAssigner.ReleasePerson(targetPerson);
            targetPerson = null;
        }
        
        hasLanded = false;
        scanningEnabled = true;
        
        // Restart vision coroutine
        StopCoroutine(VisionUpdateCoroutine());
        StartCoroutine(VisionUpdateCoroutine());
    }
    
    private void CheckIfLanded()
    {
        if (droneController != null && targetPerson != null)
        {
            // Use the actual controller state instead of manual distance checking
            if (droneController.IsLanded && !hasLanded)
            {
                hasLanded = true;
                scanningEnabled = false; // STOP EVERYTHING - Stay with this person
                OnDroneLanded?.Invoke(droneController);
                
                Debug.Log($"Drone {gameObject.name} has successfully landed near {targetPerson.name} and will stay assigned");
                
                // Do NOT release the target - drone stays with this person permanently
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showVisionGizmos) return;
        
        // Draw vision range
        Gizmos.color = visionColor;
        Gizmos.DrawWireSphere(transform.position, visionRange);
        
        if (continuousRotation)
        {
            // Draw current vision cone
            Vector3 scanDirection = Quaternion.Euler(0, currentScanAngle, 0) * transform.forward;
            Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * scanDirection;
            Vector3 rightBoundary = Quaternion.Euler(0, visionAngle * 0.5f, 0) * scanDirection;
            
            Gizmos.DrawRay(transform.position, scanDirection * visionRange);
            Gizmos.DrawRay(transform.position, leftBoundary * visionRange);
            Gizmos.DrawRay(transform.position, rightBoundary * visionRange);
        }
        
        // Draw detections
        Gizmos.color = detectionColor;
        foreach (Transform person in detectedPersons)
        {
            if (person != null)
            {
                Gizmos.DrawLine(transform.position, person.position);
                Gizmos.DrawWireCube(person.position, Vector3.one);
            }
        }
        
        // Highlight current target
        if (targetPerson != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(targetPerson.position, Vector3.one * 2f);
        }
    }
}