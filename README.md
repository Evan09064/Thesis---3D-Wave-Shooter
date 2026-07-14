# Real-Time Adaptive Game Difficulty

**Optimising Player Flow and Skill Evolution**

MSc thesis project — Leiden University (Computer Science, AI specialisation), 2025.
Supervised by Dr. M. Preuss and Giulio Barbero.

A 3D wave shooter built in Unity that measures how you actually play and adapts to
you in real time — not by scaling a single "difficulty" number, but by identifying
your *playstyle* and responding to it with targeted interventions.

---

## The idea

Most games adjust difficulty on one axis: enemies get more health, or they hit
harder. That treats every struggling player the same, and every strong player the
same.

This project asks a different question: *what kind of player are you, and what
would actually keep you in flow?* A low-skill player who never stops moving needs
something different from a low-skill player who stands still. A high-skill player
who hoards one weapon needs a different challenge from one who constantly switches.

So the system separates **how good you are** from **how you play**, and picks its
response from the combination.

---

## How it works

### 1. Telemetry

The game continuously records player performance during each wave:

- **Shooting** — shots fired, shots hit, accuracy (per wave and overall)
- **Movement** — distance travelled, time spent moving vs idle
- **Survivability** — damage taken, damage per minute
- **Wave timing** — completion time per wave, rolling average
- **Weapon behaviour** — usage counts and durations per weapon, switch frequency

(`Managers/PerformanceStats.cs`, `Managers/WeaponUsageStats.cs`)

### 2. Composite Skill Index

Every two waves, four metrics — accuracy, total round time, average round time and
distance travelled — are converted to **z-scores** against means and standard
deviations gathered in a pilot study, then combined into a single weighted
composite skill index (PI):

| Metric | Weight | Direction |
|---|---|---|
| Accuracy | 0.30 | higher is better |
| Total round time | 0.25 | lower is better |
| Average round time | 0.25 | lower is better |
| Distance travelled | 0.20 | higher is better |

The player is classified **high-** or **low-skill** against an empirically derived
cut-point. Standardising each metric first means one noisy signal can't dominate
the classification.

(`ComputeCompositeSkill()` in `Managers/GameManager.cs`)

### 3. Playstyle axes

In parallel, three behavioural ratios are computed over the same two-wave window
and thresholded against pilot-derived cut-points:

- **Move / idle ratio** — restless or stationary?
- **Damage per minute** — reckless or cautious?
- **Weapon switch rate** — adaptable or entrenched?

### 4. The twelve countermeasure policies

Each behavioural axis is crossed with the skill classification, producing four
quadrants per axis, and every quadrant has its own intervention — **assist** the
player who is struggling, **disrupt** the player who is coasting.

| Axis | Low skill (assist) | High skill (disrupt) |
|---|---|---|
| **High move** | spawn cover crates | spread enemies pairwise |
| **Low move** | speed-boost pickups | fast "dart" enemies |
| **High damage taken** | reduce spawn rate | slow the player on hit |
| **Low damage taken** | damage-buff pickups | spawn a mini-tank |
| **High switching** | weapon-swap bonus | temporary weapon lockout |
| **Low switching** | free ammo | ammo limits |

Twelve policies in total (3 axes × 4 quadrants). The interventions are applied live
between waves, so the game reshapes itself around the player without ever showing a
difficulty slider.

---

## Design decisions worth noting

- **Skill and playstyle are measured separately.** Collapsing them into one
  difficulty number is what makes conventional DDA blunt; keeping them orthogonal
  is what lets the same "low skill" reading produce two different responses.
- **Standardised inputs.** z-scoring each metric against pilot data keeps the
  composite index stable across metrics measured in wildly different units
  (seconds, percentages, Unity distance units).
- **Assist / disrupt symmetry.** Every quadrant has a defined response, so the
  policy space is complete rather than a pile of special cases — which also makes
  the tuning regression-testable: change a cut-point, and you know exactly which
  policies shift.
- **A two-wave evaluation window** trades responsiveness for stability. One wave is
  too noisy to classify a player on; two smooths the signal without making the
  adaptation feel laggy.

---

## Running it

Built with **Unity** (C#). The adaptive system is engine-side, no external ML
runtime required.

1. Clone the repo
2. Open the project folder in Unity Hub (Unity 6 / 2022 LTS or newer)
3. Open a scene from `Assets/3DWaveShooter/Scenes/`
4. Press Play

Adaptation decisions are logged to the Unity console (`[PI Evaluation]` and the
quadrant hits), so you can watch the system classify and respond as you play.

### Where the interesting code lives

```
Assets/3DWaveShooter/
├── Managers/
│   ├── GameManager.cs        ← skill index, playstyle axes, all 12 policies
│   ├── PerformanceStats.cs   ← accuracy, timing, damage telemetry
│   └── WeaponUsageStats.cs   ← weapon usage and switch tracking
└── Scripts/
    ├── Enemy/                ← enemy AI and navigation
    ├── Player/               ← movement, attack, weapons
    └── ...
```

---

## Things to Improve

- **Learn the weights instead of deriving them.** The composite index weights and
  cut-points come from a pilot study; with enough play data they could be fitted
  instead.
- **Continuous rather than binary classification.** High/low thresholds are a
  deliberate simplification whereas a graded response would adapt more smoothly at the
  boundaries.
- **Longer-horizon modelling.** The system reacts to the last two waves; it doesn't
  yet model a player's *improvement trajectory* over a whole session.

---

*Thesis: "Real-Time Adaptive Game Difficulty: Optimising Player Flow and Skill
Evolution" — Leiden University, 2025.*
