using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimulationControlUI : MonoBehaviour
{
    [Header("UI References")]
    public Button pauseButton;
    public Button restartButton;
    public Button speedButton;
    
    [Header("Control References")]
    public DroneTargetAssigner droneAssigner;
    
    private bool isPaused = false;
    private float currentTimeScale = 1f;
    private readonly float[] speedLevels = { 0.5f, 1f, 2f, 4f };
    private int currentSpeedIndex = 1;
    
    void Start()
    {
        SetupButtons();
    }
    
    void SetupButtons()
    {
        // Setup pause button
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
            UpdatePauseButtonText();
        }
        
        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartSimulation);
            restartButton.GetComponentInChildren<TextMeshProUGUI>().text = "Restart";
        }
        
        // Setup speed button
        if (speedButton != null)
        {
            speedButton.onClick.AddListener(ChangeSpeed);
            UpdateSpeedButtonText();
        }
    }
    
    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : currentTimeScale;
        UpdatePauseButtonText();
        
        Debug.Log(isPaused ? "Simulation paused" : "Simulation resumed");
    }
    
    public void RestartSimulation()
    {
        // Reset time scales
        Time.timeScale = 1f;
        currentTimeScale = 1f;
        currentSpeedIndex = 1;
        isPaused = false;
        
        // Reset assignments
        if (droneAssigner != null)
        {
            droneAssigner.ResetAssignments();
        }
        
        // Reload the scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
        
        Debug.Log("Simulation restarted");
    }
    
    public void ChangeSpeed()
    {
        currentSpeedIndex = (currentSpeedIndex + 1) % speedLevels.Length;
        currentTimeScale = speedLevels[currentSpeedIndex];
        
        if (!isPaused)
        {
            Time.timeScale = currentTimeScale;
        }
        
        UpdateSpeedButtonText();
        Debug.Log($"Speed changed to {currentTimeScale}x");
    }
    
    private void UpdatePauseButtonText()
    {
        if (pauseButton != null)
        {
            var text = pauseButton.GetComponentInChildren<TextMeshProUGUI>();
            text.text = isPaused ? "Resume" : "Pause";
        }
    }
    
    private void UpdateSpeedButtonText()
    {
        if (speedButton != null)
        {
            var text = speedButton.GetComponentInChildren<TextMeshProUGUI>();
            text.text = $"{currentTimeScale}x";
        }
    }
}
