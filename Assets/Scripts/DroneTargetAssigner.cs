using System.Collections.Generic;
using UnityEngine;

public class DroneTargetAssigner : MonoBehaviour
{
    [Header("Drones Configuration")]
    public DroneLandingController[] drones;
    
    [Header("Vision Mode")]
    [SerializeField] private bool useAutonomousVision = true;
    [SerializeField] private bool useDirectAssignment = false;
    
    // Coordination system between drones
    private static HashSet<Transform> assignedPersons = new HashSet<Transform>();
    private static DroneTargetAssigner instance;

    void Awake()
    {
        instance = this;
        assignedPersons.Clear();
    }

    void Start()
    {
        SetupAutonomousVision();
    }

    private void SetupAutonomousVision()
    {
        foreach (DroneLandingController drone in drones)
        {
            if (drone != null)
            {
                // Add the vision system if it doesn't have one
                DroneVisionSystem visionSystem = drone.GetComponent<DroneVisionSystem>();
                if (visionSystem == null)
                {
                    visionSystem = drone.gameObject.AddComponent<DroneVisionSystem>();
                }
                
                // Configure vision events with coordination
                visionSystem.OnPersonDetected += (person) => 
                {
                    HandlePersonDetection(drone, person, visionSystem);
                };
                
                visionSystem.OnPersonLost += (person) => 
                {
                    // Person lost from sight - silent handling
                };
                
                // Event when the drone lands
                visionSystem.OnDroneLanded += (landedDrone) =>
                {
                    HandleDroneLanded(landedDrone);
                };
            }
        }
    }

    private void HandlePersonDetection(DroneLandingController drone, Transform person, DroneVisionSystem visionSystem)
    {
        // Check if this person is already assigned to another drone
        if (assignedPersons.Contains(person))
        {
            visionSystem.IgnorePerson(person); // Tell the drone to ignore this person
            return;
        }

        // If the drone already has a target, ignore new detections
        if (visionSystem.HasTarget)
        {
            return;
        }

        // Assign the person to this drone
        assignedPersons.Add(person);
        Debug.Log($"Drone {drone.name} assigned to {person.name}");
    }

    private void HandleDroneLanded(DroneLandingController drone)
    {
        DroneVisionSystem visionSystem = drone.GetComponent<DroneVisionSystem>();
        if (visionSystem != null)
        {
            visionSystem.StopScanning(); // Stop scanning
        }
    }

    // Static method so other scripts can verify if a person is assigned
    public static bool IsPersonAssigned(Transform person)
    {
        return assignedPersons.Contains(person);
    }

    // Method to release a person if the drone loses the target
    public static void ReleasePerson(Transform person)
    {
        if (assignedPersons.Contains(person))
        {
            assignedPersons.Remove(person);
        }
    }

    public void AssignTargets(List<GameObject> persons)
    {
        if (!useDirectAssignment) 
        {
            return;
        }

        if (drones == null || drones.Length == 0)
        {
            Debug.LogWarning("No drones assigned in DroneTargetAssigner.");
            return;
        }

        if (persons == null || persons.Count == 0)
        {
            Debug.LogWarning("There is not a single person to assign a drone to.");
            return;
        }

        for (int i = 0; i < drones.Length && i < persons.Count; i++)
        {
            if (drones[i] != null && persons[i] != null)
            {
                drones[i].SetTarget(persons[i].transform);
                assignedPersons.Add(persons[i].transform); // Mark as assigned
            }
        }
    }
    public void EnableAutonomousVision(bool enable)
    {
        useAutonomousVision = enable;
        if (enable)
        {
            SetupAutonomousVision();
        }
    }

    public void EnableDirectAssignment(bool enable)
    {
        useDirectAssignment = enable;
    }
    public void ResetAssignments()
    {
        assignedPersons.Clear();
    }
}
