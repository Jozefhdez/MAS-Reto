using UnityEngine;
using TMPro;
using System.Text;

public class DroneStatusUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI statusText;
    
    [Header("Drone References")]
    public DroneLandingController[] drones;
    
    private StringBuilder stringBuilder = new StringBuilder();
    
    void Start()
    {
        if (statusText == null)
            statusText = GetComponent<TextMeshProUGUI>();
            
        if (statusText != null)
        {
            statusText.richText = true;
        }
            
        InvokeRepeating(nameof(UpdateStatus), 0f, 0.5f);
    }
    
    void UpdateStatus()
    {
        stringBuilder.Clear();
        stringBuilder.AppendLine("<color=#00FF00>DRONE STATUS</color>");
        
        for (int i = 0; i < drones.Length; i++)
        {
            if (drones[i] == null) continue;
            
            DroneVisionSystem vision = drones[i].GetComponent<DroneVisionSystem>();
            string droneName = $"Drone {i + 1}";
            string status = GetDroneStatus(vision);
            string target = GetDroneTarget(vision);
            
            stringBuilder.AppendLine($"{droneName}: {status}");
            stringBuilder.AppendLine($"Target: {target}");
            stringBuilder.AppendLine();
        }
        
        statusText.text = stringBuilder.ToString();
    }
    
    private string GetDroneStatus(DroneVisionSystem vision)
    {
        if (vision == null) return "<color=#808080>? No System</color>";
        
        // Get the drone landing controller to check real state
        DroneLandingController controller = vision.GetComponent<DroneLandingController>();
        
        if (controller != null)
        {
            switch (controller.CurrentState)
            {
                case DroneLandingController.DroneState.Landed:
                        return "<color=#00FF00>[LANDED]</color>";
                
                case DroneLandingController.DroneState.Descend:
                    return "<color=#FFA500>[LANDING]</color>";
                
                case DroneLandingController.DroneState.FlyHorizontal:
                case DroneLandingController.DroneState.Takeoff:
                    if (vision.HasTarget)
                        return "<color=#FFFF00>[FLYING TO TARGET]</color>";
                    else
                        return "<color=#FFFF00>[FLYING]</color>";
                
                case DroneLandingController.DroneState.Hover:
                    return "<color=#00FFFF>[HOVERING]</color>";
                
                case DroneLandingController.DroneState.Idle:
                default:
                    if (vision.HasTarget)
                        return "<color=#FFFF00>[PREPARING]</color>";
                    else
                        return "<color=#808080>[SEARCHING]</color>";
            }
        }
        
        // Fallback to vision-based detection
        if (vision.HasTarget)
        {
            return "<color=#FFFF00>[MOVING TO TARGET]</color>";
        }
        else
        {
            return "<color=#808080>[SEARCHING]</color>";
        }
    }
    
    private string GetDroneTarget(DroneVisionSystem vision)
    {
        if (vision == null || !vision.HasTarget)
            return "[SEARCHING TARGET]";
        
        return $"<color=#00FFFF>{vision.CurrentTarget.name}</color>";
    }
}
