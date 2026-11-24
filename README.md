# Project Dhaka — GTA VI-Inspired Open-World Action Experience

[![TypeScript](https://img.shields.io/badge/TypeScript-5.9-blue.svg)](https://www.typescriptlang.org/)
[![Three.js](https://img.shields.io/badge/Three.js-r182-black.svg)](https://threejs.org/)
[![React](https://img.shields.io/badge/React-19-61dafb.svg)](https://react.dev/)
[![TailwindCSS](https://img.shields.io/badge/TailwindCSS-v4-38bdf8.svg)](https://tailwindcss.com/)
[![CI / CD](https://github.com/istihadahmed007/Dhaka-GTA/actions/workflows/deploy.yml/badge.svg)](https://github.com/istihadahmed007/Dhaka-GTA/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An original, open-world 3D action game set in the bustling, culturally rich metropolis of **Dhaka, Bangladesh**. Inspired by the gameplay depth, dual-protagonist narrative structure, cinematic camera choreography, and visual standards of **GTA VI**, built inside an optimized **React + TypeScript + Three.js** physically based rendering architecture.

---

## Highlights & Features

### 1. Dual-Protagonist Switching System (GTA VI-Inspired)
Play as two distinct protagonists with separate identities, stats, weapons, clothing, and game progression:
- **Tariq ("The Master Wheelman" / পুরান ঢাকার ওস্তাদ ড্রাইভার):**
  - Born in Chawkbazar, raised in Dholaikhal mechanics garages.
  - Higher drift handling bonus (+45%), higher physical melee shockwave (+45%), and collision resilience (+35%).
  - **Special Ability: Street Overdrive (`[X]` or `[Q]`)** — Max engine torque, instant drift stabilization, and heavy kinetic melee impact.
  - Default Outfit: Old Dhaka Indigo Jacket & Brass Buckle (`street`).
- **Maya ("The Undercover Operative" / গুলশানের আন্ডারকভার অপারেটিভ):**
  - High-tech investigative journalist and tactical operative tracking syndicate cartels between Dhanmondi and Gulshan.
  - Razor-sharp weapon precision (+65% tighter bullet cone, zero recoil), higher stamina (+30%), and stealth sprint speed.
  - **Special Ability: Focus Reflex (`[X]` or `[Q]`)** — Dilation of world physics time (bullet-time at 0.45x simulation rate) while maintaining full-speed aiming and movement.
  - Default Outfit: Sleek Crimson Tactical Blazer & Cyan Smartwatch (`gulshan`).
- **Cinematic 3-Stage Sky-Cam Transition (`[C]`):**
  - Seamlessly pans into Dhaka's tropical cloud layer, traverses metropolitan airspace with dynamic FOV, and swoops down into the target protagonist with full-screen location title banners.

### 2. GTA VI-Style Camera Rig Controller (`[V]`)
- **4 View Presets:** `third_person_close` (over-the-shoulder action), `third_person_far` (cinematic chase), `first_person` (true eye-level / cockpit view), and `cinematic` (dynamic camera orbit).
- **Physical Collision Occlusion:** Real-time raycasting against buildings, bridges, and trees smoothly draws camera distance forward to prevent wall clipping.
- **Dynamic Speed FOV & Banking:** Camera field-of-view expands dynamically during high-speed drifts, accompanied by centrifugal camera roll.
- **Trauma-Squared Camera Shake:** Procedural camera shake for gun recoil, vehicle collisions, and explosions that decays smoothly with non-linear spring damping.

### 3. Living Metropolitan Dhaka Environment
- **540m × 470m Explorable Map:** Built with 4-tier road hierarchies, dual riverbanks, flyovers, and spatial chunk streaming (`WorldChunkManager`).
- **Authentic Dhaka Districts:**
  - **Old Dhaka & Chawkbazar:** Historic narrow alleys, heritage shophouses, Shakhari Bazar, Lalbagh Fort, and Tara Masjid.
  - **Sadarghat & Buriganga Riverfront:** Sadarghat launch terminal, double-decker passenger launches, traditional wooden country boats, speedboats, and jetskis.
  - **Shapla Crossing & Motijheel:** 11.5m hero Shapla Water Lily Monument, Bangladesh Bank headquarters, and high-rise financial corporate towers.
  - **Gulshan & Banani:** High-rise glass curtain towers, luxury penthouses, boutique plazas, and diplomatic enclaves.
  - **Kawran Bazar & Elevated Express Flyover:** Multi-pillar concrete elevated flyovers, syndicate depots, and wholesale market halls.
  - **Dhanmondi & Hatirjheel:** Inland lake promenades, Satmasjid cultural centers, and bridges.
  - **Uttara & MRT-6 Metro Rail:** Concrete viaduct pillars, elevated metro tracks, and terminal stations.
- **Bilingual Signage & Props:** Authentic Bengali/English highway gantry signboards, tea stalls (*cha er dokan*), neon signboards, street vendors, and overhead telecom wire tangles.

### 4. AAA Physically Based Rendering & Monsoon Atmosphere
- **ACESFilmicToneMapping & sRGB Color Pipeline:** Natural tropical daylight, vibrant sunset gradients, and cinematic night lighting.
- **Dynamic Weather System (`[K]`):** Clear, overcast monsoon rain, and heavy thunderstorm.
- **Wet Road Reflections & Spray:** Asphalt shaders dynamically increase specularity and mirror riverbanks as road wetness rises, with procedural water spray mist emitted from vehicle tires.
- **Atmospheric Fog & Custom Sky Shader:** Mie-scattered sun discs with soft lower-horizon haze and procedural cloud-layer modulation.

### 5. Authentic Dhaka Traffic & Pedestrian AI
- **Varied Vehicle Fleet:** Green CNG auto-rickshaws, traditional passenger rickshaws, weathered colorful city buses, luxury sports sedans, police patrol interceptors, Buriganga passenger launches, speedboats, and water scooters.
- **Autonomous Pedestrians:** 35+ concurrent NPCs with pathfinding, distinct Dhaka attire (Panjabis, sarees, kurtas, denim), pedestrian daily routines, danger panic responses, and conversational dialogue.

### 6. Authoritative Dedicated Multiplayer Server
- **Tick-Synchronized Architecture:** 20 Hz authoritative state replication for 2–16 simultaneous players.
- **Cryptographic Security:** Salted SHA-256 PIN hashing with constant-time verification (`crypto.timingSafeEqual`) and 192-bit session reconnection tokens.
- **Server-Side Validation:** Authoritative validation of vehicle hijacking/entry range, economy transactions, weapon ammo, and damage registration.
- **Health Endpoint:** Dedicated `/health` endpoint returning JSON status, active players, and uptime for cloud orchestrators (Render, Docker, Railway, AWS ECS).

---

## Controls & Keybindings

| Action | Primary Key | Secondary / Gamepad |
| :--- | :--- | :--- |
| **Move / Steer** | `W` `A` `S` `D` / Arrow Keys | Left Stick |
| **Sprint** | `Shift` (Hold) | Left Stick Click |
| **Jump / Handbrake** | `Space` | A / Cross Button |
| **Switch Protagonist (GTA VI Sky-Cam)** | `C` | D-Pad Down |
| **Cycle Camera View (1st / 3rd Close / Far / Cinematic)** | `V` | Select / Back Button |
| **Protagonist Special Ability (Overdrive / Bullet-Time)** | `X` or `Q` | Both Sticks Click (L3+R3) |
| **Attack / Fire Weapon** | `Left Click` or `F` | Right Trigger |
| **Aim Weapon** | `Right Click` (Hold) | Left Trigger |
| **Enter / Exit Vehicle** | `E` or `Enter` | Y / Triangle Button |
| **Reload Weapon** | `R` | X / Square Button |
| **Weapon Slots** | `1` (Fists), `2` (Pistol), `3` (SMG), `4` (Shotgun) | D-Pad Left / Right |
| **Wardrobe / Outfit Closet** | `O` | Quick Menu |
| **CNG / Taxi Fare Side Hustle** | `N` | Mission Prompt |
| **Vehicle Horn / Siren** | `H` | Left Thumb Click |
| **Cycle Weather (Clear / Monsoon / Storm)** | `K` | Debug Key |
| **Cycle Time of Day (Dawn / Noon / Sunset / Night)** | `T` | Debug Key |

---

## Tech Stack & Architecture

- **Frontend & UI:** React 19, TypeScript 5.9, Tailwind CSS v4, Lucide Icons, Motion.
- **3D Graphics Engine:** Three.js r182, GLTFLoader, SkeletonUtils, BufferGeometryUtils.
- **Audio Engine:** Web Audio API with synthesized engine pitch oscillators, gunshot transience, tire screeches, monsoon rain ambient filters, and retro UI clicks.
- **Multiplayer Backend:** Node.js 22, `ws` (WebSockets), authoritative server state loop, JSON-based persistent account store.
- **Bundler & Build Tool:** Vite 8 (featuring Rolldown & LightningCSS).

---

## Local Development & Setup

### Prerequisites
- [Node.js](https://nodejs.org/) v22+
- `npm` v10+

### 1. Clone & Install
```bash
git clone https://github.com/istihadahmed007/Dhaka-GTA.git
cd Dhaka-GTA
npm ci
```

### 2. Run Single-Player Client (Dev Server)
```bash
npm run dev
```
Open [http://localhost:3000](http://localhost:3000) in your browser.

### 3. Run Dedicated Multiplayer Server
```bash
npm run server
```
The server will start listening on port `8080`:
- **WebSocket URL:** `ws://localhost:8080`
- **Health Check:** [http://localhost:8080/health](http://localhost:8080/health)

To connect the web client to your local server, set the environment variable in `.env.local`:
```env
VITE_MULTIPLAYER_SERVER_URL=ws://localhost:8080
```

### 4. Run Test Suite & Linter
```bash
# Typecheck
npm run lint

# Run all 77 unit & integration tests
npm test

# Production build
npm run build
```

---

## Deployment Guide

### Deploying the Web Client to GitHub Pages

The repository contains an automated GitHub Actions deployment workflow at `.github/workflows/deploy.yml`.

#### Step 1: Enable GitHub Pages in your repository settings
1. Go to your repository on GitHub: `https://github.com/istihadahmed007/Dhaka-GTA`
2. Navigate to **Settings** $\rightarrow$ **Pages** (under "Code and automation").
3. Under **Build and deployment**, set **Source** to **`GitHub Actions`** (do *not* select "Deploy from a branch").

#### Step 2: Trigger Deployment
- Pushing any commit to `main` automatically triggers `.github/workflows/deploy.yml`.
- You can also manually trigger the build from the **Actions** tab by selecting **Deploy to GitHub Pages** $\rightarrow$ **Run workflow**.

The workflow automatically configures repository subpaths (`VITE_BASE_PATH`), executes `npm run lint` and `npm test`, builds the optimized bundle into `dist/`, and uploads the Pages artifact.

---

### Deploying the Authoritative Multiplayer Server to Render

This project includes a ready-to-use `render.yaml` blueprint:

1. Connect your GitHub repository to [Render.com](https://render.com).
2. Create a new **Web Service** using the existing repository.
3. Render automatically reads `render.yaml`:
   - **Environment:** Node
   - **Build Command:** `npm ci`
   - **Start Command:** `npm run server`
   - **Health Check Path:** `/health`
   - **Default Port:** `10000`
4. Once deployed, Render will assign a public HTTPS/WSS URL (e.g. `wss://dhaka-gta-server.onrender.com`).
5. Configure your client's `VITE_MULTIPLAYER_SERVER_URL` with your Render WSS endpoint.

---

### Docker Deployment

A multi-stage `Dockerfile` is provided for containerized deployment (e.g. Docker Compose, AWS ECS, Fly.io, Railway):

```bash
# Build image
docker build -t dhaka-gta-server .

# Run container with persistent data volume
docker run -d -p 8080:8080 -v $(pwd)/data:/app/data --name dhaka-server dhaka-gta-server
```

---

## Attribution & Legal Notice

- **Original Project:** This is an original game project inspired by the open-world game design language of Rockstar Games' *Grand Theft Auto VI*. It does **not** contain or distribute any proprietary Rockstar Games source code, assets, audio, or trademarks.
- **Geographic Data:** City layout reference nodes are derived from [OpenStreetMap](https://www.openstreetmap.org/) contributors under the Open Database License (ODbL).
- **License:** MIT License. See [LICENSE](LICENSE) for details.
