# Autonomous Drone Multi-Agent System

[![Unity](https://img.shields.io/badge/Unity-2025.1-blue.svg)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-9.0-green.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> A sophisticated multi-agent system simulation featuring autonomous drones with vision-based person detection and coordination capabilities.

## Project Overview

This project implements an intelligent drone system that autonomously searches for and lands near people in a 3D park environment. Multiple drones coordinate to efficiently cover the area while avoiding conflicts through a centralized assignment system.

### Key Features
- **Autonomous Vision System**: Physics-based detection with obstacle handling
- **Multi-Drone Coordination**: Prevents targeting conflicts between agents
- **Real-time UI Controls**: Pause, restart, and speed control functionality
- **Live Status Monitoring**: Track each drone's state and target assignments
- **Realistic Environment**: 3D park with obstacles and line-of-sight challenges

## 🔧 Installation

### Prerequisites
- Unity Hub
- Unity 2025.1 or later

### Setup Instructions

1. **Download the Unity Package**
   ```
   [https://drive.google.com/drive/folders/1e5msq0kcWxq--s3bpFjkQ7nxeX3J-crL?usp=sharing]
   ```

2. **Create Unity Project**
   - Open Unity Hub
   - Create new Universal 3D project

3. **Import Package**
   - Open the Assets tab and choose the Import Package option
   - Look for the unity package and open it
   - Choose the "All" option and import it
   - When pop-up appears, press "Reload"

4. **Configure Layers** ⚠️ *Important*
   - Add layers to the Unity project with correct index and identical names:
     - Layer 3: `Person`
     - Layer 6: `Obstacle`

5. **Configure Drone Settings**
   - Select drone objects in Hierarchy
   - In Inspector, modify: `Ground Mask = Ground`
   - Repeat for each drone

6. **Create Tag**
   - Create a new tag called `"Person"`
   - Apply to person objects in the scene

7. **Run Simulation**
   - Press Play in Unity
   - Enjoy the autonomous drone simulation!

### Main Interface
The simulation features a comprehensive graphical interface with:

### Control Panel
- **Pause/Resume**: Pause or resume the simulation
- **Restart**: Reset the simulation to initial state  
- **Speed Control**: Adjust simulation speed (0.5x, 1x, 2x, 4x)

### Drone Status Panel
- **Real-time Status**: Monitor each drone's current state:
  - `[SEARCHING]` - Looking for targets
  - `[FLYING TO TARGET]` - Moving towards assigned person
  - `[LANDING]` - Descending to target location
  - `[ASSIGNED TO PERSON]` - Successfully landed and assigned

### Automatic Operation
- Drones autonomously search for and assign themselves to people
- Multiple drones coordinate to avoid targeting the same person
- Each drone maintains its assignment once successfully landed

## 👥 Team Members

| Student ID | Name
| A01639866 | Ana Elena Velasco García
| A01643496 | Baltazar Servín Riveroll
| A01644781 | Emilio Pardo Gutiérrez
| A01644644 | Jozef David Hernández Campos
| A01639205 | Maria José Medina Calderón

## 📋 Technical Architecture

### Core Components
- **DroneVisionSystem**: Handles autonomous target detection and tracking
- **DroneLandingController**: Manages flight patterns and landing procedures  
- **DroneTargetAssigner**: Coordinates assignments between multiple drones
- **UI Systems**: Real-time status display and simulation controls

### Multi-Agent Behavior
- Each drone operates independently with its own vision and decision-making
- Centralized coordination prevents multiple drones targeting the same person
- Intelligent obstacle avoidance and alternative search patterns

## 📚 Documentation

- [Review 1 PDF](Review1.pdf)
- [Review 2 PDF](Review2.pdf)
- [Review 3 PDF](Review3.pdf)