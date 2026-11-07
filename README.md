[README.md](https://github.com/user-attachments/files/34512076/README.md)
# 🌌 OOYL: Respawn Sci-Fi

![Unity](https://img.shields.io/badge/Engine-Unity%202D%20(URP)-000000?style=for-the-badge&logo=unity&logoColor=white)
![Language](https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Genre](https://img.shields.io/badge/Genre-Tactical%20Top--Down%20Shooter-blueviolet?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-Active%20Development-success?style=for-the-badge)

**OOYL: Respawn Sci-Fi** is a 2D top-down tactical sci-fi shooter and geometric action-puzzle game built in Unity using the Universal Render Pipeline (URP) 2D. Set inside an isometric sci-fi spaceport complex, players must master mathematical ricochet ballistics, real-time trajectory telemetry, and localized time dilation to outsmart enemy patrols, defeat bosses, and rescue the Princess across 6 challenging sectors.

---

## ✨ Key Gameplay Features

* **📐 Mathematical Ricochet Ballistics:** Projectiles utilize a custom `PerfectMathBounce` 2D physics material and `RicochetBullet` logic, enabling precise bank shots off spaceport walls and collision boundaries.
* **🔬 Geometric Laser Sight & Telemetry HUD:** Features a real-time raycast trajectory predictor (`GeometricLaserSight.cs`) paired with a `MathTelemetryHUD` that calculates and displays reflection angles and bounce vectors before firing.
* **⏱️ Tactical Time Dilation:** Manipulate the flow of combat using the `TimeDilation` system to slow down incoming `EnemyRicochetBullet` barrages and line up pixel-perfect trick shots.
* **🤖 Dynamic Enemy AI & Boss Encounters:** Engage patrolling sci-fi adversaries powered by `EnemyAI.cs`, culminating in multi-directional combat encounters managed by `BossController.cs`.
* **👑 Escort & Rescue Mechanics:** Locate and interact with the Princess (`PrincessController.cs`) while navigating hazards to trigger sector transitions (`LevelTransition.cs`).
* **🎬 Cinematic Intro & 6 Unique Sectors:** Begins with an integrated cinematic briefing (`SolarisStartVid.mp4` via `IntroVideoPlayer.cs`) followed by 6 distinct levels (`Level1` through `Level6`).
* **🛠️ Custom Unity Editor Tooling:** Includes dedicated automated scene and prefab setup builders in `Assets/Scripts/Editor/` for rapid prototyping and one-click component configuration.

---

## 🗂️ Repository Architecture

```text
OOYL-Respawn-SciFi/
├── Assets/
│   ├── BGscenes/                  # Multi-layered level backgrounds (Level 1 & Level 2 spaceport art)
│   ├── Prefabs/                   # Core gameplay prefabs
│   │   ├── Enemy.prefab
│   │   ├── EnemyRicochetBullet.prefab
│   │   └── RicochetBullet.prefab
│   ├── Scenes/                    # Game progression scenes
│   │   ├── Intro.unity            # Cinematic intro scene
│   │   ├── Level1.unity - Level6.unity
│   │   └── Collision bonds.prefab # Level boundary colliders
│   ├── Scripts/                   # Core C# Gameplay & Systems Architecture
│   │   ├── Editor/                # Custom Unity Editor automation builders
│   │   │   ├── HeroShootSetupBuilder.cs
│   │   │   ├── PrincessSetupBuilder.cs
│   │   │   ├── TimeDilationSetupBuilder.cs
│   │   │   ├── UICanvasBuilder.cs
│   │   │   └── VillanSetupBuilder.cs
│   │   ├── BossController.cs      # Boss behavior and attack patterns
│   │   ├── CameraFollow.cs        # Smooth 2D camera tracking
│   │   ├── DamageReceiver.cs      # Modular health and hit detection
│   │   ├── EnemyAI.cs             # NPC pathfinding, sightlines, and shooting
│   │   ├── GameManager.cs         # Global game state, respawn, and level flow
│   │   ├── GeometricLaserSight.cs # Multi-bounce raycast aiming line renderer
│   │   ├── IntroVideoPlayer.cs    # Video playback & scene transition handler
│   │   ├── LevelTransition.cs     # Sector completion triggers
│   │   ├── MathTelemetryHUD.cs    # Real-time angle/vector UI telemetry
│   │   ├── PlayerController.cs    # Hero movement, aiming, and weapon firing
│   │   ├── PrincessController.cs  # Rescue target AI and animation states
│   │   ├── RicochetBullet.cs      # Projectile reflection & lifecycle physics
│   │   └── TimeDilation.cs        # Bullet-time timescale controller
│   ├── Settings/                  # URP 2D Renderer, Volume Profiles, & Input System Actions
│   ├── Sprites/                   # 4-directional character sprite sheets & Animator Controllers
│   │   ├── Hero/                  # Sci-gun & Saber animations
│   │   ├── Princess/              # Directional movement & idle animations
│   │   ├── Villan/                # Boss animations & saber/shoot sheets
│   │   └── npc/                   # Standard enemy walk/shoot animations
│   └── Videos/
│       └── SolarisStartVid.mp4    # Opening cinematic video
├── Packages/                      # Unity Package Manager dependencies
└── ProjectSettings/               # Physics2D, Tag, Input, and URP project configurations
```

---

## 🎮 Controls

The project uses Unity's **New Input System** (`InputSystem_Actions.inputactions`) alongside standard keyboard/mouse bindings:

| Action | Input Binding | Description |
| :--- | :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` / `Arrow Keys` | 4-directional character movement |
| **Aim** | `Mouse Cursor` | Rotate geometric laser sight and telemetry vector |
| **Fire Ricochet Shot** | `Left Mouse Button (LMB)` | Fire a high-velocity bouncing projectile |
| **Time Dilation** | `Right Mouse Button (RMB)` / `Shift` | Activate/Deactivate tactical slow-motion |
| **Skip Intro** | `Space` / `Enter` | Skip the opening Solaris cinematic video |

---

## 🚀 Getting Started

### Prerequisites
* **Unity Engine:** Unity `6000.x` (Unity 6) or Unity `2022.3 LTS`+ with **Universal Render Pipeline (URP)** support.
* **IDE:** Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit.

### Installation & Setup
1. **Clone the Repository:**
   ```bash
   git clone https://github.com/your-username/OOYL-Respawn-SciFi.git
   cd OOYL-Respawn-SciFi
   ```
2. **Open in Unity Hub:**
   * Launch **Unity Hub** and click **Add** -> **Add project from disk**.
   * Select the cloned `OOYL-Respawn-SciFi` root folder.
3. **Load the Starting Scene:**
   * In the Project window, navigate to `Assets/Scenes/`.
   * Open `Intro.unity` for the full experience, or `Level1.unity` to jump straight into gameplay.
4. **Press Play:**
   * Click the **Play** button in the top toolbar to start testing in the Editor.

---

## 🛠️ Custom Editor Builders

This repository includes custom Editor scripts located in `Assets/Scripts/Editor/` to automate scene setup and character configuration without manual Inspector wiring:

* **`HeroShootSetupBuilder`**: Automatically configures the player's shooting components, laser sight references, and projectile spawn points.
* **`VillanSetupBuilder` & `PrincessSetupBuilder`**: Generates and links Animator Controllers, sprites, and colliders for NPCs.
* **`TimeDilationSetupBuilder`**: Sets up global post-processing volume hooks and time-scaling managers in the active scene.
* **`UICanvasBuilder`**:的程序化构建 (Programmatically constructs) the HUD canvas, health displays, and the `MathTelemetryHUD` overlay.

---

## 🧮 Physics & Math Telemetry System

A core pillar of **OOYL: Respawn Sci-Fi** is its deterministic bounce physics:
1. **Incident & Reflection Vectors:** Both `GeometricLaserSight` and `RicochetBullet` compute reflection vectors using the surface normal $\vec{n}$ of the 2D collider:
   $$\vec{R} = \vec{I} - 2(\vec{I} \cdot \vec{n})\vec{n}$$
2. **Zero-Friction Elasticity:** The `PerfectMathBounce.physicsMaterial2D` asset enforces $0$ friction and $1.0$ bounciness so bullets maintain constant kinetic speed across multiple wall bounces.
3. **HUD Readout:** `MathTelemetryHUD.cs` hooks into the player's aiming system to display real-time trajectory data on screen.

---

## 📜 Credits & Attributions

* **Character Sprites:** Generated and customized using the Universal LPC Spritesheet Character Generator (`Assets/Sprites/Hero/credits/credits.txt`).
* **Fonts & UI:** Liberation Sans (SIL Open Font License) & EmojiOne assets via Unity TextMesh Pro.
* **Environment Art:** Custom Isometric Sci-Fi Spaceport Complex backgrounds and level layouts.

---

## 📄 License

This project is licensed under the MIT License — see the credits files inside `Assets/Sprites/` for individual asset attribution requirements.
