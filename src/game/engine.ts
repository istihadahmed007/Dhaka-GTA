import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import {
  AabbCollider,
  ActiveDialogue,
  EnemyPolicy,
  GamePhase,
  HudSnapshot,
  InteractModalKind,
  MinimapBlip,
  NpcState,
  OutfitId,
  PlayerState,
  SideFareState,
  TimePreset,
  VehicleKind,
  VehicleState,
  VEHICLE_SPECS,
  WeaponId,
  WEAPON_SPECS,
  WeatherMode,
} from './types';
import {
  addWantedHeat,
  applyGarageService,
  applyPlayerDamage,
  buyShopItem,
  createInitialPlayerState,
  equipOrBuyOutfit,
  formatDhakaTime,
  resolveCircleCollisions,
  stepVehicleKinematics,
  tickWantedCooldown,
} from './physics';
import {
  buildStaticColliders,
  DISTRICT_META,
  FARE_DESTINATIONS,
  getDistrictAt,
  INITIAL_VEHICLES,
  POIS,
  STORY_MISSIONS,
  TRAFFIC_ROUTES,
} from './worldData';
import { createDhakaTextures, DhakaTextureSet } from './textures';
import {
  createHumanoidRig,
  createVehicleRig,
  HumanoidRig,
  setHumanoidOutfit,
  setHumanoidWeaponMesh,
  VehicleRig,
} from './models';
import { buildDhakaWorld, WorldVisualHandles } from './worldBuilder';
import { DhakaAudioEngine } from './audio';

interface ParticleSlot {
  active: boolean;
  x: number;
  y: number;
  z: number;
  vx: number;
  vy: number;
  vz: number;
  life: number;
  maxLife: number;
  scale: number;
  color: THREE.Color;
}

const _vecA = new THREE.Vector3();
const _vecB = new THREE.Vector3();
const _vecC = new THREE.Vector3();
const _mat4 = new THREE.Matrix4();
const _quat = new THREE.Quaternion();
const _scaleVec = new THREE.Vector3();
const _skyDay = new THREE.Color(0x8bc6e8);
const _skySunset = new THREE.Color(0xd97548);
const _skyNight = new THREE.Color(0x0c1526);
const _skyStorm = new THREE.Color(0x283544);
const _tmpColor = new THREE.Color();

export class DhakaGameEngine {
  private container: HTMLElement;
  private onHudUpdate: (snap: HudSnapshot) => void;
  private isSoftwareGl: boolean;

  private renderer: THREE.WebGLRenderer;
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private sunLight: THREE.DirectionalLight;
  private hemiLight: THREE.HemisphereLight;
  private textures: DhakaTextureSet;
  private world: WorldVisualHandles;
  private audio: DhakaAudioEngine;

  private colliders: AabbCollider[];
  private player: PlayerState;
  private playerRig: HumanoidRig;
  private playerWalkPhase = 0;
  private playerSquashY = 1;
  private playerAttackAnim = 0;
  private playerHitFlash = 0;

  private vehicles: VehicleState[] = [];
  private vehicleRigs = new Map<string, VehicleRig>();

  private npcs: NpcState[] = [];
  private npcRigs = new Map<string, HumanoidRig>();
  private npcCounter = 0;
  private policeSpawnTimer = 0;

  // Pooled particles (single InstancedMesh, zero per-frame allocations)
  private particlePool: ParticleSlot[] = [];
  private particleMesh: THREE.InstancedMesh;
  private particleCursor = 0;

  // Game State Machine & Story
  private phase: GamePhase = 'title';
  private modal: InteractModalKind = 'none';
  private missionIdx = 0;
  private stepIdx = 0;
  private stepKills = 0;
  private missionEntitiesSpawned = false;
  private fare: SideFareState = {
    active: false,
    passengerName: '',
    destX: 0,
    destZ: 0,
    destLabel: '',
    timer: 0,
    reward: 0,
  };
  private dialogue: ActiveDialogue | null = null;
  private toastMsg: string | null = null;
  private toastTimer = 0;
  private playTimeSec = 0;
  private gameOverCause = '';
  private defeatDelayTimer = 0;

  // Environment & Weather
  private timeOfDay = 15.8; // Golden late-afternoon Dhaka light on boot
  private weather: WeatherMode = 'clear';
  private lightningTimer = 6;
  private lightningFlash = 0;

  // Camera & Juice
  private camYaw = 0; // 0 means looking north (-Z) behind player
  private camPitch = 0.28;
  private camPos = new THREE.Vector3(0, 7, 48);
  private camTarget = new THREE.Vector3(0, 2, 34);
  private titleAngle = 0;
  private trauma = 0;
  private hitstopTimer = 0;
  private raycaster = new THREE.Raycaster();

  // Input State (separated held vs edge-triggered justPressed)
  private keysHeld = new Set<string>();
  private keysJustPressed = new Set<string>();
  private mouseHeld = false;
  private mouseJustPressed = false;
  private rightMouseDrag = false;
  private lastPointerX = 0;
  private lastPointerY = 0;

  // Loop & Lifecycle
  private frameId = 0;
  private lastTimeMs = 0;
  private hudEmitAccum = 0;
  private disposed = false;
  private resizeObserver: ResizeObserver;

  constructor(container: HTMLElement, onHudUpdate: (snap: HudSnapshot) => void) {
    this.container = container;
    this.onHudUpdate = onHudUpdate;

    this.isSoftwareGl = (() => {
      try {
        const probe = document.createElement('canvas').getContext('webgl2');
        if (!probe) return true;
        const ext = probe.getExtension('WEBGL_debug_renderer_info');
        const param = ext ? ext.UNMASKED_RENDERER_WEBGL : probe.RENDERER;
        const gpu = String(probe.getParameter(param));
        probe.getExtension('WEBGL_lose_context')?.loseContext();
        return /SwiftShader|llvmpipe|softpipe|Software|Basic Render/i.test(gpu);
      } catch {
        return true;
      }
    })();

    this.renderer = new THREE.WebGLRenderer({
      antialias: !this.isSoftwareGl,
      powerPreference: 'high-performance',
    });
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.12;
    this.renderer.setPixelRatio(this.isSoftwareGl ? 1 : Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = !this.isSoftwareGl;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const width = container.clientWidth || window.innerWidth;
    const height = container.clientHeight || window.innerHeight;
    this.renderer.setSize(width, height);
    container.innerHTML = '';
    container.appendChild(this.renderer.domElement);

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x9bc7e2);
    this.scene.fog = new THREE.FogExp2(0xb8c9d4, 0.0068);

    // Mandatory studio environment reflection map for PBR metals & car clearcoats
    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();

    this.camera = new THREE.PerspectiveCamera(58, width / height, 0.25, 420);

    // Warm/Cool lighting rig (single shadow caster with tight frustum)
    this.hemiLight = new THREE.HemisphereLight(0xaad4f5, 0x5d4b3c, 0.78);
    this.scene.add(this.hemiLight);

    this.sunLight = new THREE.DirectionalLight(0xffe1b8, 2.2);
    this.sunLight.position.set(48, 72, 34);
    if (!this.isSoftwareGl) {
      this.sunLight.castShadow = true;
      this.sunLight.shadow.mapSize.set(1024, 1024);
      const d = 68;
      this.sunLight.shadow.camera.left = -d;
      this.sunLight.shadow.camera.right = d;
      this.sunLight.shadow.camera.top = d;
      this.sunLight.shadow.camera.bottom = -d;
      this.sunLight.shadow.camera.near = 5;
      this.sunLight.shadow.camera.far = 190;
      this.sunLight.shadow.bias = -0.0005;
      this.sunLight.shadow.normalBias = 0.025;
    }
    this.scene.add(this.sunLight);
    this.scene.add(this.sunLight.target);

    this.audio = new DhakaAudioEngine();
    this.textures = createDhakaTextures(this.renderer.capabilities.getMaxAnisotropy());
    this.colliders = buildStaticColliders();
    this.world = buildDhakaWorld(this.scene, this.textures, !this.isSoftwareGl);

    // Pooled particle InstancedMesh (180 slots)
    const pGeo = new THREE.BoxGeometry(0.18, 0.18, 0.18);
    const pMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
    this.particleMesh = new THREE.InstancedMesh(pGeo, pMat, 180);
    this.particleMesh.frustumCulled = false;
    this.scene.add(this.particleMesh);
    for (let i = 0; i < 180; i++) {
      this.particlePool.push({
        active: false,
        x: 0,
        y: -50,
        z: 0,
        vx: 0,
        vy: 0,
        vz: 0,
        life: 0,
        maxLife: 1,
        scale: 0,
        color: new THREE.Color(0xffffff),
      });
      _mat4.makeScale(0, 0, 0);
      this.particleMesh.setMatrixAt(i, _mat4);
    }
    this.particleMesh.instanceMatrix.needsUpdate = true;

    // Player setup
    this.player = createInitialPlayerState();
    this.playerRig = createHumanoidRig(0x26466d, 0x1e2530, 0xe6a135, this.textures, { isPlayer: true });
    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);
    this.playerRig.group.position.set(this.player.x, 0, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;
    this.scene.add(this.playerRig.group);

    this.spawnInitialEntities();
    this.applyEnvironmentLighting(0);

    if (!this.isSoftwareGl) {
      this.renderer.compile(this.scene, this.camera);
    }

    this.bindEvents();
    this.resizeObserver = new ResizeObserver(() => this.handleResize());
    this.resizeObserver.observe(this.container);

    // Check ?autostart=1 query param
    const params = new URLSearchParams(window.location.search);
    if (params.get('autostart') === '1') {
      this.startGame();
    } else {
      document.body.dataset.state = 'title';
      this.emitHud();
    }

    this.lastTimeMs = performance.now();
    this.tick();
  }

  private spawnInitialEntities(): void {
    // Clear existing vehicles & NPCs
    this.vehicleRigs.forEach((rig) => this.scene.remove(rig.group));
    this.vehicleRigs.clear();
    this.vehicles = [];

    this.npcRigs.forEach((rig) => this.scene.remove(rig.group));
    this.npcRigs.clear();
    this.npcs = [];

    // Spawn vehicles
    for (const vSpawn of INITIAL_VEHICLES) {
      const spec = VEHICLE_SPECS[vSpawn.kind];
      const state: VehicleState = {
        id: vSpawn.id,
        kind: vSpawn.kind,
        x: vSpawn.x,
        y: 0,
        z: vSpawn.z,
        heading: vSpawn.heading,
        speed: vSpawn.driver === 'traffic' ? spec.maxSpeed * 0.45 : 0,
        steerAngle: 0,
        hp: spec.maxHp,
        maxHp: spec.maxHp,
        color: vSpawn.color,
        driver: vSpawn.driver,
        routeIdx: vSpawn.routeIdx ?? 0,
        waypointIdx: vSpawn.waypointIdx ?? 0,
        honkTimer: 0,
        sirenOn: false,
        damagedSmoke: false,
      };
      this.vehicles.push(state);
      const rig = createVehicleRig(state.kind, state.color, this.textures, !this.isSoftwareGl);
      rig.group.position.set(state.x, 0, state.z);
      rig.group.rotation.y = state.heading;
      this.scene.add(rig.group);
      this.vehicleRigs.set(state.id, rig);
    }

    // Spawn key story allies (Rafiq Bhai at Cha Stall, Nadia near Gulshan Garage)
    this.spawnNpc({
      role: 'ally',
      policy: 'chaser',
      name: 'Rafiq Bhai',
      x: -16,
      z: 42,
      shirtColor: 0xe6a135,
      pantsColor: 0xf4ecd8,
      accentColor: 0x0f766e,
      hp: 200,
    });
    this.spawnNpc({
      role: 'ally',
      policy: 'chaser',
      name: 'Nadia Rahman',
      x: 31,
      z: -24,
      shirtColor: 0x0d9488,
      pantsColor: 0x1e293b,
      accentColor: 0xf59e0b,
      hp: 200,
    });

    // Spawn 18 Dhaka pedestrians across sidewalks in vibrant Bangladeshi attire
    const pedPalette = [0xc0392b, 0x16a085, 0xd4a017, 0x2980b9, 0x8e44ad, 0xe67e22, 0xf1f5f9, 0x1f6f54];
    const sidewalkSpots: Array<[number, number]> = [
      [-12, 26], [12, 32], [-12, 52], [12, 48],
      [-20, 12], [20, 12], [-40, 12], [40, 12],
      [-12, -18], [12, -18], [-12, -38], [12, -38],
      [-32, -12], [32, -12], [-22, 68], [22, 68],
      [-52, 28], [52, -28],
    ];
    sidewalkSpots.forEach(([px, pz], idx) => {
      this.spawnNpc({
        role: 'civilian',
        policy: 'chaser',
        x: px,
        z: pz,
        shirtColor: pedPalette[idx % pedPalette.length],
        pantsColor: idx % 2 === 0 ? 0x1e293b : 0x475569,
        accentColor: pedPalette[(idx + 3) % pedPalette.length],
        hp: 45,
      });
    });

    // Spawn 2 DMP Patrol Officers standing watch near DMP Thana
    this.spawnNpc({
      role: 'police',
      policy: 'chaser',
      x: 28,
      z: 24,
      shirtColor: 0x1e3a8a,
      pantsColor: 0x172554,
      accentColor: 0xfacc15,
      hp: 85,
    });
    this.spawnNpc({
      role: 'police',
      policy: 'ranged',
      x: 38,
      z: 30,
      shirtColor: 0x1e3a8a,
      pantsColor: 0x172554,
      accentColor: 0xfacc15,
      hp: 85,
    });
  }

  private spawnNpc(opts: {
    role: NpcState['role'];
    policy: EnemyPolicy;
    name?: string;
    x: number;
    z: number;
    shirtColor: number;
    pantsColor: number;
    accentColor: number;
    hp: number;
    missionTag?: string;
  }): NpcState {
    const id = `npc_${++this.npcCounter}`;
    const npc: NpcState = {
      id,
      role: opts.role,
      policy: opts.policy,
      name: opts.name,
      x: opts.x,
      y: 0,
      z: opts.z,
      vx: 0,
      vz: 0,
      heading: Math.random() * Math.PI * 2,
      hp: opts.hp,
      maxHp: opts.hp,
      speed: opts.role === 'civilian' ? 2.1 : opts.policy === 'flanker' ? 5.6 : 4.5,
      state: opts.role === 'ally' ? 'idle' : 'walk',
      stateTimer: 1 + Math.random() * 3,
      windupDuration: opts.policy === 'ranged' ? 0.55 : 0.42,
      attackCooldown: 0.6,
      hitFlashTimer: 0,
      koTimer: 0,
      shirtColor: opts.shirtColor,
      pantsColor: opts.pantsColor,
      targetX: opts.x + (Math.random() - 0.5) * 16,
      targetZ: opts.z + (Math.random() - 0.5) * 16,
      missionTag: opts.missionTag,
      hasAttackToken: false,
      strafeDir: Math.random() < 0.5 ? -1 : 1,
    };
    this.npcs.push(npc);
    const rig = createHumanoidRig(opts.shirtColor, opts.pantsColor, opts.accentColor, this.textures, {
      isPolice: opts.role === 'police',
      isBoss: opts.role === 'boss',
    });
    if (opts.role === 'police' || opts.role === 'syndicate' || opts.role === 'boss') {
      setHumanoidWeaponMesh(rig, opts.policy === 'ranged' ? 'pistol' : opts.role === 'boss' ? 'shotgun' : 'fists');
    }
    rig.group.position.set(npc.x, 0, npc.z);
    rig.group.rotation.y = npc.heading;
    this.scene.add(rig.group);
    this.npcRigs.set(id, rig);
    return npc;
  }

  // === PUBLIC API FOR REACT SHELL ===

  public startGame(): void {
    this.audio.unlock();
    if (this.phase === 'gameover' || this.phase === 'victory') {
      this.resetWorld();
    }
    this.phase = 'playing';
    this.modal = 'none';
    document.body.dataset.state = 'playing';
    this.clearInputs();
    this.camYaw = 0;
    this.camPitch = 0.28;
    // Immediately align camera behind player on start for crisp framing
    this.camPos.set(this.player.x, 4.4, this.player.z + 8.5);
    this.camTarget.set(this.player.x, 1.8, this.player.z - 6);
    this.camera.position.copy(this.camPos);
    this.camera.lookAt(this.camTarget);

    // Kick off Mission 1 dialogue on fresh game
    if (this.missionIdx === 0 && this.stepIdx === 0 && this.playTimeSec < 1) {
      this.triggerDialogue(
        'Rafiq Bhai',
        'তারেক! পুরান ঢাকা থেকে গুলশান—সব জায়গায় সিন্ডিকেট ঢুকে গেছে। আমার চা স্টলে আয়!',
        'Tariq! The Padma Syndicate is muscling in from Old Dhaka to Gulshan. Meet me at the Cha Stall!',
        7.5,
      );
    }
    this.emitHud();
  }

  public restartGame(): void {
    this.resetWorld();
    this.startGame();
  }

  public resetWorld(): void {
    this.player = createInitialPlayerState();
    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);
    this.playerRig.group.visible = true;
    this.playerRig.group.position.set(this.player.x, 0, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;

    this.missionIdx = 0;
    this.stepIdx = 0;
    this.stepKills = 0;
    this.missionEntitiesSpawned = false;
    this.fare.active = false;
    this.dialogue = null;
    this.toastMsg = null;
    this.toastTimer = 0;
    this.playTimeSec = 0;
    this.defeatDelayTimer = 0;
    this.gameOverCause = '';
    this.weather = 'clear';
    this.timeOfDay = 15.8;
    this.modal = 'none';
    this.spawnInitialEntities();
    this.applyEnvironmentLighting(0);
  }

  public togglePause(): void {
    if (this.modal !== 'none') {
      this.modal = 'none';
      this.emitHud();
      return;
    }
    if (this.phase === 'playing') {
      this.phase = 'paused';
      document.body.dataset.state = 'paused';
      this.clearInputs();
      this.audio.silenceLoops();
      this.emitHud();
    } else if (this.phase === 'paused') {
      this.phase = 'playing';
      document.body.dataset.state = 'playing';
      this.clearInputs();
      this.emitHud();
    }
  }

  public returnToTitle(): void {
    this.phase = 'title';
    this.modal = 'none';
    document.body.dataset.state = 'title';
    this.clearInputs();
    this.audio.silenceLoops();
    this.emitHud();
  }

  public closeModal(): void {
    this.modal = 'none';
    this.clearInputs();
    this.emitHud();
  }

  public openWardrobe(): void {
    if (this.phase !== 'playing') return;
    this.modal = this.modal === 'wardrobe' ? 'none' : 'wardrobe';
    this.clearInputs();
    this.emitHud();
  }

  public toggleMute(): void {
    this.audio.unlock();
    this.audio.toggleMute();
    this.emitHud();
  }

  public setTimePreset(preset: TimePreset): void {
    const map: Record<TimePreset, number> = {
      dawn: 6.4,
      day: 12.5,
      sunset: 17.6,
      night: 22.0,
    };
    this.timeOfDay = map[preset];
    this.applyEnvironmentLighting(0);
    this.showToast(`Dhaka Time: ${preset.toUpperCase()}`);
    this.emitHud();
  }

  public cycleTimePreset(): void {
    if (this.timeOfDay < 9) this.setTimePreset('day');
    else if (this.timeOfDay < 16.2) this.setTimePreset('sunset');
    else if (this.timeOfDay < 19.5) this.setTimePreset('night');
    else this.setTimePreset('dawn');
  }

  public setWeather(mode: WeatherMode): void {
    this.weather = mode;
    this.applyEnvironmentLighting(0);
    this.showToast(`Weather: ${mode === 'clear' ? 'Tropical Sun' : mode === 'monsoon' ? 'Monsoon Rain' : 'Dhaka Thunderstorm'}`);
    this.emitHud();
  }

  public cycleWeather(): void {
    if (this.weather === 'clear') this.setWeather('monsoon');
    else if (this.weather === 'monsoon') this.setWeather('storm');
    else this.setWeather('clear');
  }

  public selectWeapon(weapon: WeaponId): void {
    if (!this.player.unlockedWeapons.includes(weapon)) return;
    this.player.weapon = weapon;
    this.player.reloadTimer = 0;
    setHumanoidWeaponMesh(this.playerRig, weapon);
    this.emitHud();
  }

  public handleShopPurchase(item: 'biriyani' | 'armor' | WeaponId): void {
    const res = buyShopItem(this.player, item);
    if (res.ok) {
      this.audio.playChime(false);
      setHumanoidWeaponMesh(this.playerRig, this.player.weapon);
    }
    this.showToast(res.message);
    this.emitHud();
  }

  public handleGarageAction(service: 'repair' | 'clear_heat' | 'paint', paintColor?: number): void {
    const targetVeh = this.getActiveOrNearestVehicle(12);
    const res = applyGarageService(this.player, targetVeh, service, paintColor);
    if (res.ok) {
      this.audio.playChime(false);
      if (targetVeh && service === 'paint' && paintColor !== undefined) {
        const rig = this.vehicleRigs.get(targetVeh.id);
        rig?.bodyMaterials.forEach((m) => m.color.setHex(paintColor));
      }
    }
    this.showToast(res.message);
    this.emitHud();
  }

  public handleOutfitSelect(outfitId: OutfitId): void {
    const res = equipOrBuyOutfit(this.player, outfitId);
    if (res.ok) {
      this.audio.playChime(false);
      setHumanoidOutfit(this.playerRig, this.player.outfit);
    }
    this.showToast(res.message);
    this.emitHud();
  }

  public toggleSideFare(): void {
    if (this.fare.active) {
      this.fare.active = false;
      this.showToast('Fare job cancelled');
      this.emitHud();
      return;
    }
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    if (!veh) {
      this.showToast('Hop into a CNG, Rickshaw, or Sedan [F] to pick up Dhaka fares!');
      this.emitHud();
      return;
    }
    // Pick a destination at least 45m away
    const candidates = FARE_DESTINATIONS.filter((d) => Math.hypot(d.x - this.player.x, d.z - this.player.z) > 40);
    const pick = candidates[Math.floor(Math.random() * candidates.length)] ?? FARE_DESTINATIONS[0];
    const names = ['Karim Uncle', 'Farhana', 'Shafiq', 'Mina Apu', 'Imran'];
    this.fare = {
      active: true,
      passengerName: names[Math.floor(Math.random() * names.length)],
      destX: pick.x,
      destZ: pick.z,
      destLabel: pick.label,
      timer: 42,
      reward: 380 + Math.round(Math.hypot(pick.x - this.player.x, pick.z - this.player.z) * 3),
    };
    this.audio.playChime(false);
    this.showToast(`Passenger ${this.fare.passengerName}: Drop at ${pick.label}!`);
    this.emitHud();
  }

  // === INPUT HANDLING ===

  private bindEvents(): void {
    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('blur', this.onBlur);
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    const dom = this.renderer.domElement;
    dom.addEventListener('mousedown', this.onMouseDown);
    window.addEventListener('mouseup', this.onMouseUp);
    window.addEventListener('mousemove', this.onMouseMove);
    dom.addEventListener('contextmenu', this.onContextMenu);
  }

  private onContextMenu = (e: MouseEvent) => e.preventDefault();

  private onKeyDown = (e: KeyboardEvent) => {
    const code = e.code;

    // Title screen start handshake on Enter / Space
    if (this.phase === 'title' && (code === 'Enter' || code === 'Space')) {
      e.preventDefault();
      this.startGame();
      return;
    }

    if (code === 'Escape' || code === 'KeyP') {
      if (this.phase === 'playing' || this.phase === 'paused') {
        e.preventDefault();
        this.togglePause();
        return;
      }
    }

    if (code === 'KeyM') {
      this.toggleMute();
      return;
    }

    if (this.phase !== 'playing' || this.modal !== 'none') return;

    if (!this.keysHeld.has(code)) {
      this.keysJustPressed.add(code);
    }
    this.keysHeld.add(code);

    if (code === 'Space' || code.startsWith('Arrow')) {
      e.preventDefault();
    }
  };

  private onKeyUp = (e: KeyboardEvent) => {
    this.keysHeld.delete(e.code);
    // Variable jump height cut when releasing jump early
    if (e.code === 'Space' && !this.player.grounded && this.player.vy > 2.2) {
      this.player.vy *= 0.48;
    }
  };

  private onMouseDown = (e: MouseEvent) => {
    this.audio.unlock();
    if (this.phase !== 'playing' || this.modal !== 'none') return;
    if (e.button === 2) {
      this.rightMouseDrag = true;
      this.lastPointerX = e.clientX;
      this.lastPointerY = e.clientY;
    } else if (e.button === 0) {
      this.mouseHeld = true;
      this.mouseJustPressed = true;
      this.lastPointerX = e.clientX;
      this.lastPointerY = e.clientY;
    }
  };

  private onMouseUp = (e: MouseEvent) => {
    if (e.button === 2) this.rightMouseDrag = false;
    if (e.button === 0) this.mouseHeld = false;
  };

  private onMouseMove = (e: MouseEvent) => {
    if (this.phase !== 'playing' || this.modal !== 'none') return;
    if (this.rightMouseDrag) {
      const dx = e.clientX - this.lastPointerX;
      const dy = e.clientY - this.lastPointerY;
      this.lastPointerX = e.clientX;
      this.lastPointerY = e.clientY;
      this.camYaw -= dx * 0.0055;
      this.camPitch = THREE.MathUtils.clamp(this.camPitch + dy * 0.004, 0.1, 0.72);
    }
  };

  private onBlur = () => {
    this.clearInputs();
  };

  private onVisibilityChange = () => {
    if (document.hidden) this.clearInputs();
  };

  private clearInputs(): void {
    this.keysHeld.clear();
    this.keysJustPressed.clear();
    this.mouseHeld = false;
    this.mouseJustPressed = false;
    this.rightMouseDrag = false;
  }

  private handleResize(): void {
    if (this.disposed) return;
    const width = this.container.clientWidth || window.innerWidth;
    const height = this.container.clientHeight || window.innerHeight;
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  }

  // === MAIN SIMULATION LOOP ===

  private afterDraw(cb: () => void): void {
    const gl = this.renderer.getContext() as WebGL2RenderingContext;
    if (!gl || typeof gl.fenceSync !== 'function') {
      this.frameId = window.setTimeout(cb, 80);
      return;
    }
    const sync = gl.fenceSync(gl.SYNC_GPU_COMMANDS_COMPLETE, 0);
    gl.flush();
    const poll = () => {
      if (this.disposed) return;
      const state = sync && gl.getSyncParameter(sync, gl.SYNC_STATUS);
      if (state === gl.UNSIGNALED) {
        this.frameId = window.setTimeout(poll, 5);
      } else {
        if (sync) gl.deleteSync(sync);
        this.frameId = window.setTimeout(cb, 80);
      }
    };
    this.frameId = window.setTimeout(poll, 5);
  }

  private tick = (): void => {
    if (this.disposed) return;
    const now = performance.now();
    const rawDt = (now - this.lastTimeMs) / 1000;
    this.lastTimeMs = now;
    const dt = Math.min(Math.max(rawDt, 0.001), 0.05);

    this.updateFrame(dt);
    this.renderer.render(this.scene, this.camera);

    if (this.isSoftwareGl) {
      this.afterDraw(this.tick);
    } else {
      this.frameId = requestAnimationFrame(this.tick);
    }
  };

  private updateFrame(dt: number): void {
    // Visual animations (boats, rain, particles, hitstop decay, camera shake) tick in real dt
    this.updateWorldVisuals(dt);
    this.updateParticles(dt);

    if (this.trauma > 0) {
      this.trauma = Math.max(0, this.trauma - dt * 2.2);
    }

    if (this.phase === 'title') {
      this.titleAngle += dt * 0.14;
      this.updateTraffic(dt * 0.8);
      this.updateTitleCamera();
      this.keysJustPressed.clear();
      this.mouseJustPressed = false;
      return;
    }

    if (this.phase === 'paused' || this.phase === 'gameover' || this.phase === 'victory') {
      this.keysJustPressed.clear();
      this.mouseJustPressed = false;
      return;
    }

    // Handle short defeat beat before showing Wasted modal
    if (this.defeatDelayTimer > 0) {
      this.defeatDelayTimer -= dt;
      this.updateGameplayCamera(dt);
      if (this.defeatDelayTimer <= 0) {
        this.phase = 'gameover';
        document.body.dataset.state = 'gameover';
        this.audio.silenceLoops();
        this.emitHud();
      }
      return;
    }

    // Modal open freezes action cleanly without dropping render
    if (this.modal !== 'none') {
      this.updateGameplayCamera(dt);
      this.keysJustPressed.clear();
      this.mouseJustPressed = false;
      return;
    }

    // Hitstop pauses gameplay physics briefly on heavy impact while camera shake renders
    let simDt = dt;
    if (this.hitstopTimer > 0) {
      this.hitstopTimer = Math.max(0, this.hitstopTimer - dt);
      simDt = 0;
    }

    if (simDt > 0) {
      this.playTimeSec += simDt;
      // Gentle real-time Dhaka day/night cycle (1 full day ~ 8 minutes)
      this.timeOfDay = (this.timeOfDay + simDt * 0.05) % 24;
      this.applyEnvironmentLighting(simDt);

      this.handleEdgeInputs();
      if (this.player.vehicleId) {
        this.updateDrivingPlayer(simDt);
      } else {
        this.updateOnFootPlayer(simDt);
      }

      this.updateTraffic(simDt);
      this.updateNpcs(simDt);
      this.updatePoliceEscalation(simDt);
      this.updateMissionsAndFare(simDt);

      if (this.dialogue) {
        this.dialogue.timer -= simDt;
        if (this.dialogue.timer <= 0) this.dialogue = null;
      }
      if (this.toastTimer > 0) {
        this.toastTimer -= simDt;
        if (this.toastTimer <= 0) this.toastMsg = null;
      }

      // Check player defeat condition (reachable, never clamped to 1)
      if (this.player.hp <= 0 && this.defeatDelayTimer <= 0) {
        this.defeatDelayTimer = 0.55;
        this.trauma = Math.min(1, this.trauma + 0.85);
        this.audio.playImpact(true);
        this.gameOverCause = this.player.wantedLevel > 0 ? 'Busted by DMP Patrol' : 'Wasted in Dhaka';
      }
    }

    this.updateGameplayCamera(dt);

    // Update continuous audio loops
    const activeVeh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const speedRatio = activeVeh ? Math.min(1, Math.abs(activeVeh.speed) / VEHICLE_SPECS[activeVeh.kind].maxSpeed) : 0;
    const rainIntensity = this.weather === 'storm' ? 1 : this.weather === 'monsoon' ? 0.65 : 0;
    this.audio.updateAmbient(
      dt,
      Boolean(activeVeh),
      activeVeh?.kind ?? null,
      speedRatio,
      rainIntensity,
      this.player.wantedLevel > 0,
    );

    // Clear single-frame edge triggers
    this.keysJustPressed.clear();
    this.mouseJustPressed = false;

    // Throttle HUD state pushes to <= 10Hz
    this.hudEmitAccum += dt;
    if (this.hudEmitAccum >= 0.1) {
      this.hudEmitAccum = 0;
      this.emitHud();
    }
  }

  private handleEdgeInputs(): void {
    // Enter / Exit nearest vehicle [F]
    if (this.keysJustPressed.has('KeyF')) {
      this.tryToggleVehicle();
    }

    // Interact with Garage / Shop / Wardrobe [G]
    if (this.keysJustPressed.has('KeyG')) {
      this.tryInteractPoi();
    }

    // Quick Outfit Wardrobe [C]
    if (this.keysJustPressed.has('KeyC')) {
      this.openWardrobe();
    }

    // Toggle CNG/Taxi Fare Side Hustle [N]
    if (this.keysJustPressed.has('KeyN')) {
      this.toggleSideFare();
    }

    // Cycle Time of Day [T] & Weather [V]
    if (this.keysJustPressed.has('KeyT')) this.cycleTimePreset();
    if (this.keysJustPressed.has('KeyV')) this.cycleWeather();

    // Weapon hotkeys [1..4]
    if (this.keysJustPressed.has('Digit1')) this.selectWeapon('fists');
    if (this.keysJustPressed.has('Digit2')) this.selectWeapon('pistol');
    if (this.keysJustPressed.has('Digit3')) this.selectWeapon('smg');
    if (this.keysJustPressed.has('Digit4')) this.selectWeapon('shotgun');

    // Reload [R]
    if (this.keysJustPressed.has('KeyR') && !this.player.vehicleId) {
      this.startReload();
    }

    // Horn [H]
    if (this.keysJustPressed.has('KeyH') && this.player.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      if (veh) {
        this.audio.playHorn(veh.kind);
        // Nearby pedestrians scatter slightly when honked at
        this.npcs.forEach((n) => {
          if (n.role === 'civilian' && Math.hypot(n.x - veh.x, n.z - veh.z) < 10) {
            n.state = 'flee';
            n.stateTimer = 2.5;
          }
        });
      }
    }

    // Jump buffer [Space]
    if (this.keysJustPressed.has('Space') && !this.player.vehicleId) {
      this.player.jumpBufferTimer = 0.13;
    }
  }

  private tryToggleVehicle(): void {
    if (this.player.vehicleId) {
      // Exit vehicle cleanly on the driver's left side
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      this.player.vehicleId = null;
      if (veh) {
        veh.driver = 'none';
        veh.sirenOn = false;
        const exitX = veh.x - Math.cos(veh.heading) * (VEHICLE_SPECS[veh.kind].halfWidth + 1.15);
        const exitZ = veh.z + Math.sin(veh.heading) * (VEHICLE_SPECS[veh.kind].halfWidth + 1.15);
        const safe = resolveCircleCollisions(exitX, exitZ, 0.5, this.colliders);
        this.player.x = safe.x;
        this.player.z = safe.z;
        this.player.y = 0;
        this.player.heading = veh.heading;
      }
      this.playerRig.group.visible = true;
      this.audio.playImpact(false);
      this.emitHud();
      return;
    }

    const nearest = this.getActiveOrNearestVehicle(5.6);
    if (!nearest) return;

    // Hijacking a moving traffic or police vehicle adds minor Wanted Heat
    if (nearest.driver === 'traffic' || nearest.kind === 'police') {
      const heat = nearest.kind === 'police' ? 30 : 14;
      addWantedHeat(this.player, heat);
      this.showToast(
        nearest.kind === 'police' ? 'Hijacked DMP Patrol! Police alerted!' : `Took control of ${VEHICLE_SPECS[nearest.kind].name}`,
      );
    }

    nearest.driver = 'player';
    nearest.sirenOn = nearest.kind === 'police';
    this.player.vehicleId = nearest.id;
    this.playerRig.group.visible = false;
    this.audio.playHorn(nearest.kind);
    this.emitHud();
  }

  private tryInteractPoi(): void {
    for (const poi of POIS) {
      if (Math.hypot(poi.x - this.player.x, poi.z - this.player.z) <= poi.radius + 2.5) {
        if (poi.id === 'garage') {
          this.modal = 'garage';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.id === 'shop') {
          this.modal = 'shop';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.id === 'tea_stall') {
          this.player.hp = Math.min(this.player.maxHp, this.player.hp + 25);
          this.audio.playChime(false);
          this.showToast('Hot Masala Cha from Rafiq Bhai (+25 HP)');
          this.emitHud();
          return;
        }
      }
    }
  }

  private getActiveOrNearestVehicle(maxDist: number): VehicleState | null {
    if (this.player.vehicleId) {
      return this.vehicles.find((v) => v.id === this.player.vehicleId) ?? null;
    }
    let best: VehicleState | null = null;
    let bestDist = maxDist;
    for (const v of this.vehicles) {
      if (v.hp <= 0) continue;
      const d = Math.hypot(v.x - this.player.x, v.z - this.player.z);
      if (d < bestDist) {
        bestDist = d;
        best = v;
      }
    }
    return best;
  }

  // === ON-FOOT PLAYER CONTROLLER & COMBAT ===

  private updateOnFootPlayer(dt: number): void {
    // Q / E keyboard camera orbit
    if (this.keysHeld.has('KeyQ')) this.camYaw += dt * 1.9;
    if (this.keysHeld.has('KeyE')) this.camYaw -= dt * 1.9;

    const fwd = (this.keysHeld.has('KeyW') || this.keysHeld.has('ArrowUp') ? 1 : 0) -
      (this.keysHeld.has('KeyS') || this.keysHeld.has('ArrowDown') ? 1 : 0);
    const strafe = (this.keysHeld.has('KeyD') || this.keysHeld.has('ArrowRight') ? 1 : 0) -
      (this.keysHeld.has('KeyA') || this.keysHeld.has('ArrowLeft') ? 1 : 0);

    // Camera-relative horizontal basis (camYaw = 0 faces -Z)
    const camFwdX = -Math.sin(this.camYaw);
    const camFwdZ = -Math.cos(this.camYaw);
    const camRightX = Math.cos(this.camYaw);
    const camRightZ = -Math.sin(this.camYaw);

    let moveX = camFwdX * fwd + camRightX * strafe;
    let moveZ = camFwdZ * fwd + camRightZ * strafe;
    const moveLen = Math.hypot(moveX, moveZ);

    const wantsSprint = (this.keysHeld.has('ShiftLeft') || this.keysHeld.has('ShiftRight')) && moveLen > 0.1;
    const sprinting = wantsSprint && this.player.stamina > 6;
    if (sprinting) {
      this.player.stamina = Math.max(0, this.player.stamina - dt * 26);
    } else {
      this.player.stamina = Math.min(this.player.maxStamina, this.player.stamina + dt * 18);
    }

    const targetSpeed = sprinting ? 9.6 : 5.8;
    if (moveLen > 0.01) {
      moveX /= moveLen;
      moveZ /= moveLen;
      const accel = 1 - Math.exp(-14 * dt);
      this.player.vx += (moveX * targetSpeed - this.player.vx) * accel;
      this.player.vz += (moveZ * targetSpeed - this.player.vz) * accel;
      const targetHeading = Math.atan2(moveX, moveZ);
      let diff = targetHeading - this.player.heading;
      while (diff > Math.PI) diff -= Math.PI * 2;
      while (diff < -Math.PI) diff += Math.PI * 2;
      this.player.heading += diff * (1 - Math.exp(-13 * dt));
    } else {
      const decel = 1 - Math.exp(-12 * dt);
      this.player.vx *= 1 - decel;
      this.player.vz *= 1 - decel;
    }

    // Coyote time & jump buffering
    if (this.player.grounded) {
      this.player.coyoteTimer = 0.1;
    } else {
      this.player.coyoteTimer = Math.max(0, this.player.coyoteTimer - dt);
    }
    if (this.player.jumpBufferTimer > 0) {
      this.player.jumpBufferTimer = Math.max(0, this.player.jumpBufferTimer - dt);
      if (this.player.coyoteTimer > 0) {
        this.player.vy = 7.4;
        this.player.grounded = false;
        this.player.coyoteTimer = 0;
        this.player.jumpBufferTimer = 0;
        this.playerSquashY = 1.22; // takeoff stretch
        this.spawnBurst(this.player.x, 0.15, this.player.z, 0xd6cfc2, 5, 2.5);
        this.audio.playImpact(false);
      }
    }

    // Vertical physics
    this.player.vy -= 21 * dt;
    this.player.y += this.player.vy * dt;
    if (this.player.y <= 0) {
      if (!this.player.grounded && this.player.vy < -3) {
        this.playerSquashY = 0.78; // landing squash
        this.spawnBurst(this.player.x, 0.12, this.player.z, 0xb8b0a4, 4, 2.0);
      }
      this.player.y = 0;
      this.player.vy = 0;
      this.player.grounded = true;
    }

    // Horizontal motion + AABB resolution
    const res = resolveCircleCollisions(
      this.player.x + this.player.vx * dt,
      this.player.z + this.player.vz * dt,
      0.52,
      this.colliders,
    );
    this.player.x = res.x;
    this.player.z = res.z;

    // Weapon cooldown & reload
    if (this.player.attackCooldown > 0) {
      this.player.attackCooldown = Math.max(0, this.player.attackCooldown - dt);
    }
    if (this.player.reloadTimer > 0) {
      this.player.reloadTimer -= dt;
      if (this.player.reloadTimer <= 0) {
        this.finishReload();
      }
    }

    // Attack input (Left Click or J key)
    const spec = WEAPON_SPECS[this.player.weapon];
    const autoFire = this.player.weapon === 'smg' && (this.mouseHeld || this.keysHeld.has('KeyJ'));
    const edgeFire = this.mouseJustPressed || this.keysJustPressed.has('KeyJ');
    if ((autoFire || edgeFire) && this.player.attackCooldown <= 0 && this.player.reloadTimer <= 0) {
      this.performPlayerAttack(spec.id);
    }

    // Animate articulated player rig synced to distance traveled
    const horizSpeed = Math.hypot(this.player.vx, this.player.vz);
    this.playerWalkPhase += horizSpeed * dt * 2.1;
    this.playerSquashY += (1 - this.playerSquashY) * (1 - Math.exp(-12 * dt));
    if (this.playerAttackAnim > 0) this.playerAttackAnim = Math.max(0, this.playerAttackAnim - dt * 5);
    if (this.playerHitFlash > 0) {
      this.playerHitFlash -= dt;
      this.playerRig.torsoMat.emissive.setHex(this.playerHitFlash > 0 ? 0xff4444 : 0x000000);
    }

    const invSq = 1 / Math.sqrt(this.playerSquashY);
    this.playerRig.bodyRoot.scale.set(invSq, this.playerSquashY, invSq);
    this.playerRig.group.position.set(this.player.x, this.player.y, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;

    const swing = horizSpeed > 0.3 ? Math.sin(this.playerWalkPhase) * Math.min(0.85, horizSpeed * 0.1) : 0;
    this.playerRig.leftLeg.rotation.x = swing;
    this.playerRig.rightLeg.rotation.x = -swing;
    this.playerRig.leftArm.rotation.x = -swing * 0.85;
    if (this.playerAttackAnim > 0) {
      this.playerRig.rightArm.rotation.x = -Math.PI * 0.48 - this.playerAttackAnim * 0.45;
    } else if (this.player.weapon !== 'fists') {
      this.playerRig.rightArm.rotation.x = -Math.PI * 0.35;
    } else {
      this.playerRig.rightArm.rotation.x = swing * 0.85;
    }
  }

  private startReload(): void {
    const w = this.player.weapon;
    const spec = WEAPON_SPECS[w];
    if (spec.magSize <= 0 || this.player.reloadTimer > 0) return;
    if (this.player.magAmmo[w] >= spec.magSize || this.player.ammo[w] <= 0) return;
    this.player.reloadTimer = spec.reloadTime;
    this.showToast(`Reloading ${spec.name}...`);
    this.emitHud();
  }

  private finishReload(): void {
    const w = this.player.weapon;
    const spec = WEAPON_SPECS[w];
    const needed = spec.magSize - this.player.magAmmo[w];
    const take = Math.min(needed, this.player.ammo[w]);
    this.player.magAmmo[w] += take;
    this.player.ammo[w] -= take;
    this.player.reloadTimer = 0;
    this.emitHud();
  }

  private performPlayerAttack(weaponId: WeaponId): void {
    const spec = WEAPON_SPECS[weaponId];
    if (spec.magSize > 0) {
      if (this.player.magAmmo[weaponId] <= 0) {
        if (this.player.ammo[weaponId] > 0) {
          this.startReload();
        } else {
          this.showToast('Out of ammo! Visit Babu’s Shop or switch to [1] Fists');
        }
        return;
      }
      this.player.magAmmo[weaponId] -= 1;
    }

    this.player.attackCooldown = spec.cooldown;
    this.playerAttackAnim = 1;
    this.audio.playWeapon(weaponId);

    // Face camera forward direction when firing a gun
    if (weaponId !== 'fists') {
      this.player.heading = Math.atan2(-Math.sin(this.camYaw), -Math.cos(this.camYaw));
      this.trauma = Math.min(1, this.trauma + (weaponId === 'shotgun' ? 0.35 : 0.12));
    }

    const dirX = Math.sin(this.player.heading);
    const dirZ = Math.cos(this.player.heading);

    // Muzzle flash / fist burst
    this.spawnBurst(
      this.player.x + dirX * 0.9,
      1.35,
      this.player.z + dirZ * 0.9,
      weaponId === 'fists' ? 0xfbbf24 : 0xffe49e,
      weaponId === 'shotgun' ? 12 : 5,
      weaponId === 'fists' ? 2.8 : 7.5,
    );

    // Firing firearms in public near civilians alerts police slightly
    if (weaponId !== 'fists') {
      const policeNear = this.npcs.some((n) => n.role === 'police' && n.state !== 'ko' && Math.hypot(n.x - this.player.x, n.z - this.player.z) < 28);
      if (policeNear) addWantedHeat(this.player, 12);
    }

    // Hit detection against NPCs in forward cone/ray
    let anyHit = false;
    for (let p = 0; p < spec.pellets; p++) {
      const spreadAngle = (Math.random() - 0.5) * spec.spread;
      const rx = Math.sin(this.player.heading + spreadAngle);
      const rz = Math.cos(this.player.heading + spreadAngle);

      let bestNpc: NpcState | null = null;
      let bestDist = spec.range;
      for (const npc of this.npcs) {
        if (npc.state === 'ko' || npc.role === 'ally') continue;
        const dx = npc.x - this.player.x;
        const dz = npc.z - this.player.z;
        const dist = Math.hypot(dx, dz);
        if (dist > bestDist || dist < 0.01) continue;
        const dot = (dx * rx + dz * rz) / dist;
        const minDot = weaponId === 'fists' ? 0.45 : 0.88;
        if (dot >= minDot) {
          bestDist = dist;
          bestNpc = npc;
        }
      }

      if (bestNpc) {
        anyHit = true;
        this.damageNpc(bestNpc, spec.damage, rx, rz, weaponId === 'shotgun' || weaponId === 'fists');
      }
    }

    if (anyHit && weaponId !== 'smg') {
      this.hitstopTimer = 0.04;
    }
    this.emitHud();
  }

  private damageNpc(npc: NpcState, damage: number, knockX: number, knockZ: number, heavy: boolean): void {
    npc.hp = Math.max(0, npc.hp - damage);
    npc.hitFlashTimer = 0.07;
    npc.vx += knockX * (heavy ? 6.5 : 3.2);
    npc.vz += knockZ * (heavy ? 6.5 : 3.2);
    this.spawnBurst(npc.x, 1.3, npc.z, 0xf97316, heavy ? 12 : 6, 4.5);
    this.audio.playImpact(heavy);

    if (npc.role === 'civilian') {
      addWantedHeat(this.player, 22);
      npc.state = 'flee';
      npc.stateTimer = 6;
    } else if (npc.role === 'police') {
      addWantedHeat(this.player, 28);
    }

    if (npc.hp <= 0 && npc.state !== 'ko') {
      npc.state = 'ko';
      npc.koTimer = 0.48;
      npc.hasAttackToken = false;
      this.player.kills += 1;
      this.hitstopTimer = 0.055;
      this.trauma = Math.min(1, this.trauma + 0.3);
      this.spawnBurst(npc.x, 1.1, npc.z, 0xfacc15, 16, 5.5);

      const cashDrop = npc.role === 'boss' ? 900 : npc.role === 'syndicate' ? 180 : 75;
      this.player.money += cashDrop;
      if (npc.missionTag === 'target') {
        this.stepKills += 1;
      }
    }
  }

  // === DRIVING CONTROLLER ===

  private updateDrivingPlayer(dt: number): void {
    const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
    if (!veh) {
      this.player.vehicleId = null;
      this.playerRig.group.visible = true;
      return;
    }

    const throttle = (this.keysHeld.has('KeyW') || this.keysHeld.has('ArrowUp') ? 1 : 0) -
      (this.keysHeld.has('KeyS') || this.keysHeld.has('ArrowDown') ? 1 : 0);
    const steer = (this.keysHeld.has('KeyA') || this.keysHeld.has('ArrowLeft') ? 1 : 0) -
      (this.keysHeld.has('KeyD') || this.keysHeld.has('ArrowRight') ? 1 : 0);
    const handbrake = this.keysHeld.has('Space');

    const prevX = veh.x;
    const prevZ = veh.z;
    const res = stepVehicleKinematics(veh, { throttle, steer, handbrake }, dt, this.colliders);

    this.player.distanceDriven += Math.hypot(veh.x - prevX, veh.z - prevZ);
    this.player.x = veh.x;
    this.player.z = veh.z;
    this.player.y = 0;
    this.player.heading = veh.heading;

    if (res.collided && res.impactSpeed > 6) {
      this.trauma = Math.min(1, this.trauma + Math.min(0.6, res.impactSpeed * 0.03));
      this.hitstopTimer = 0.035;
      this.audio.playImpact(true);
      this.spawnBurst(veh.x, 0.9, veh.z, 0xfbbf24, 14, 6);
    }

    // Vehicle-vs-Vehicle bumps & Vehicle-vs-NPC knockdowns
    this.resolveVehicleInteractions(veh, dt);

    // If vehicle is destroyed, eject player safely with minor blast damage
    if (veh.hp <= 0) {
      this.spawnBurst(veh.x, 1.2, veh.z, 0xff5722, 26, 9);
      this.audio.playImpact(true);
      this.trauma = 0.9;
      applyPlayerDamage(this.player, 24);
      this.tryToggleVehicle();
      this.showToast('Vehicle wrecked!');
    }

    // Gently align camera behind vehicle when moving forward
    if (!this.rightMouseDrag && Math.abs(veh.speed) > 1.2) {
      const desiredYaw = Math.atan2(-Math.sin(veh.heading), -Math.cos(veh.heading));
      let diff = desiredYaw - this.camYaw;
      while (diff > Math.PI) diff -= Math.PI * 2;
      while (diff < -Math.PI) diff += Math.PI * 2;
      this.camYaw += diff * (1 - Math.exp(-3.6 * dt));
    }

    this.syncVehicleRig(veh, dt);
  }

  private resolveVehicleInteractions(veh: VehicleState, dt: number): void {
    const specA = VEHICLE_SPECS[veh.kind];
    const radA = (specA.halfWidth + specA.halfLength) * 0.45;

    for (const other of this.vehicles) {
      if (other.id === veh.id) continue;
      const specB = VEHICLE_SPECS[other.kind];
      const radB = (specB.halfWidth + specB.halfLength) * 0.45;
      const dx = veh.x - other.x;
      const dz = veh.z - other.z;
      const dist = Math.hypot(dx, dz);
      const minD = radA + radB;
      if (dist < minD && dist > 0.01) {
        const nx = dx / dist;
        const nz = dz / dist;
        const push = (minD - dist) * 0.55;
        veh.x += nx * push;
        veh.z += nz * push;
        other.x -= nx * push;
        other.z -= nz * push;
        if (Math.abs(veh.speed) > 7) {
          this.audio.playImpact(true);
          this.trauma = Math.min(1, this.trauma + 0.28);
          this.spawnBurst((veh.x + other.x) * 0.5, 0.8, (veh.z + other.z) * 0.5, 0xfbbf24, 10, 5);
          veh.speed *= 0.45;
          other.hp = Math.max(0, other.hp - 18);
          if (other.kind === 'police') addWantedHeat(this.player, 24);
        }
      }
    }

    // Check high-speed hits against NPCs
    if (Math.abs(veh.speed) > 4.5) {
      for (const npc of this.npcs) {
        if (npc.state === 'ko' || npc.role === 'ally') continue;
        if (Math.hypot(npc.x - veh.x, npc.z - veh.z) < radA + 0.65) {
          this.damageNpc(npc, Math.round(Math.abs(veh.speed) * 4.5), Math.sin(veh.heading), Math.cos(veh.heading), true);
        }
      }
    }

    // Damaged engine smoke particles
    if (veh.damagedSmoke && Math.random() < dt * 8) {
      this.spawnBurst(veh.x + Math.sin(veh.heading) * 1.1, 1.3, veh.z + Math.cos(veh.heading) * 1.1, 0x475569, 2, 1.5);
    }
  }

  // === TRAFFIC AI ===

  private updateTraffic(dt: number): void {
    for (const v of this.vehicles) {
      if (v.driver === 'player') continue;

      if (v.driver === 'traffic') {
        const route = TRAFFIC_ROUTES[v.routeIdx % TRAFFIC_ROUTES.length];
        const wp = route[v.waypointIdx % route.length];
        const dx = wp.x - v.x;
        const dz = wp.z - v.z;
        const dist = Math.hypot(dx, dz);
        if (dist < 4.5) {
          v.waypointIdx = (v.waypointIdx + 1) % route.length;
        }

        const desiredHeading = Math.atan2(dx, dz);
        let diff = desiredHeading - v.heading;
        while (diff > Math.PI) diff -= Math.PI * 2;
        while (diff < -Math.PI) diff += Math.PI * 2;
        const steer = THREE.MathUtils.clamp(diff * 1.8, -1, 1);

        // Brake if player or another vehicle is directly ahead
        const fx = Math.sin(v.heading);
        const fz = Math.cos(v.heading);
        let blocked = false;
        const pDx = this.player.x - v.x;
        const pDz = this.player.z - v.z;
        const pDist = Math.hypot(pDx, pDz);
        if (pDist < 7.5 && (pDx * fx + pDz * fz) / Math.max(0.1, pDist) > 0.72) {
          blocked = true;
        }
        if (!blocked) {
          for (const other of this.vehicles) {
            if (other.id === v.id) continue;
            const odx = other.x - v.x;
            const odz = other.z - v.z;
            const od = Math.hypot(odx, odz);
            if (od < 8.5 && (odx * fx + odz * fz) / Math.max(0.1, od) > 0.75) {
              blocked = true;
              break;
            }
          }
        }

        const targetRatio = blocked ? 0 : 0.42;
        const spec = VEHICLE_SPECS[v.kind];
        const throttle = blocked ? -0.9 : v.speed < spec.maxSpeed * targetRatio ? 0.55 : 0;
        stepVehicleKinematics(v, { throttle, steer, handbrake: blocked }, dt, this.colliders);
      } else if (v.driver === 'police') {
        // Active DMP patrol pursuit vehicle
        const dx = this.player.x - v.x;
        const dz = this.player.z - v.z;
        const dist = Math.hypot(dx, dz);
        const desiredHeading = Math.atan2(dx, dz);
        let diff = desiredHeading - v.heading;
        while (diff > Math.PI) diff -= Math.PI * 2;
        while (diff < -Math.PI) diff += Math.PI * 2;
        const steer = THREE.MathUtils.clamp(diff * 2.1, -1, 1);
        const throttle = dist > 9.5 ? 0.78 : -0.4;
        v.sirenOn = this.player.wantedLevel > 0;
        if (this.player.wantedLevel === 0) {
          v.driver = 'traffic';
        }
        stepVehicleKinematics(v, { throttle, steer, handbrake: dist < 8 }, dt, this.colliders);
      } else {
        // Parked vehicle rolls to a stop smoothly
        if (Math.abs(v.speed) > 0.05) {
          stepVehicleKinematics(v, { throttle: 0, steer: 0, handbrake: true }, dt, this.colliders);
        }
      }

      this.syncVehicleRig(v, dt);
    }
  }

  private syncVehicleRig(v: VehicleState, dt: number): void {
    const rig = this.vehicleRigs.get(v.id);
    if (!rig) return;
    rig.group.position.set(v.x, v.y, v.z);
    rig.group.rotation.y = v.heading;

    const wheelSpin = v.speed * dt * 2.2;
    rig.wheels.forEach((w, idx) => {
      w.rotation.x += wheelSpin;
      if (idx === 0 || (v.kind !== 'cng' && v.kind !== 'rickshaw' && v.kind !== 'bike' && idx < 2)) {
        w.rotation.y = v.steerAngle;
      }
    });

    if (rig.sirenRed && rig.sirenBlue) {
      if (v.sirenOn) {
        const flash = Math.floor(this.playTimeSec * 8) % 2 === 0;
        rig.sirenRed.emissiveIntensity = flash ? 3.2 : 0.2;
        rig.sirenBlue.emissiveIntensity = flash ? 0.2 : 3.2;
      } else {
        rig.sirenRed.emissiveIntensity = 0.2;
        rig.sirenBlue.emissiveIntensity = 0.2;
      }
    }
  }

  // === LIVING CITY NPC & ENEMY COMBAT AI (3 DISTINCT POLICIES + ATTACK TOKENS + TELEGRAPHS) ===

  private updateNpcs(dt: number): void {
    // Count currently active attack tokens (max 3 simultaneous attackers for fair readability)
    let activeTokens = 0;
    for (const n of this.npcs) {
      if (n.hasAttackToken && n.state !== 'ko') activeTokens++;
    }

    for (let i = this.npcs.length - 1; i >= 0; i--) {
      const npc = this.npcs[i];
      const rig = this.npcRigs.get(npc.id);
      if (!rig) continue;

      if (npc.hitFlashTimer > 0) {
        npc.hitFlashTimer -= dt;
        rig.torsoMat.emissive.setHex(npc.hitFlashTimer > 0 ? 0xffffff : 0x000000);
      }

      // KO topple dissolve animation
      if (npc.state === 'ko') {
        npc.koTimer -= dt;
        rig.telegraphRing.visible = false;
        const p = 1 - Math.max(0, npc.koTimer / 0.48);
        rig.bodyRoot.rotation.x = p * (Math.PI / 2);
        rig.bodyRoot.position.y = -p * 0.45;
        if (npc.koTimer <= 0) {
          this.scene.remove(rig.group);
          this.npcRigs.delete(npc.id);
          this.npcs.splice(i, 1);
        }
        continue;
      }

      if (npc.attackCooldown > 0) npc.attackCooldown -= dt;

      const dxP = this.player.x - npc.x;
      const dzP = this.player.z - npc.z;
      const distP = Math.hypot(dxP, dzP);
      const isHostile =
        npc.role === 'syndicate' ||
        npc.role === 'boss' ||
        (npc.role === 'police' && this.player.wantedLevel > 0);

      if (npc.role === 'ally') {
        // Face player when nearby
        if (distP < 12) {
          npc.heading = Math.atan2(dxP, dzP);
        }
        rig.group.rotation.y = npc.heading;
        continue;
      }

      if (isHostile && distP < 42) {
        // 3 distinct combat behavior policies:
        // 1. chaser: rushes into melee range (2.2m) and delivers telegraphed heavy strike
        // 2. ranged: maintains 10-16m standoff distance, strafes, shows red aim windup before firing
        // 3. flanker: orbits laterally at high speed then bursts in with lunge
        if (npc.state === 'windup') {
          npc.stateTimer -= dt;
          npc.heading = Math.atan2(dxP, dzP);
          rig.telegraphRing.visible = true;
          const pulse = 0.85 + Math.sin(this.playTimeSec * 24) * 0.18;
          rig.telegraphRing.scale.setScalar(pulse);
          rig.rightArm.rotation.x = -Math.PI * 0.65;
          if (npc.stateTimer <= 0) {
            rig.telegraphRing.visible = false;
            this.executeEnemyAttack(npc, distP, dxP, dzP);
            npc.state = 'chase';
            npc.attackCooldown = npc.role === 'boss' ? 1.1 : 1.55;
            npc.hasAttackToken = false;
            activeTokens = Math.max(0, activeTokens - 1);
          }
        } else {
          rig.telegraphRing.visible = false;
          npc.heading = Math.atan2(dxP, dzP);
          let desiredVx = 0;
          let desiredVz = 0;
          const nx = dxP / Math.max(0.01, distP);
          const nz = dzP / Math.max(0.01, distP);

          if (npc.policy === 'ranged') {
            if (distP > 15) {
              desiredVx = nx * npc.speed;
              desiredVz = nz * npc.speed;
            } else if (distP < 8) {
              desiredVx = -nx * npc.speed * 0.7;
              desiredVz = -nz * npc.speed * 0.7;
            } else {
              // Lateral strafing while lining up shot
              desiredVx = -nz * npc.strafeDir * npc.speed * 0.55;
              desiredVz = nx * npc.strafeDir * npc.speed * 0.55;
            }
            if (distP <= 18 && npc.attackCooldown <= 0 && activeTokens < 3) {
              npc.state = 'windup';
              npc.stateTimer = npc.windupDuration;
              npc.hasAttackToken = true;
              activeTokens++;
              this.audio.playTelegraphCue();
            }
          } else if (npc.policy === 'flanker') {
            // Circles laterally until close, then closes fast
            const orbitWeight = distP > 5.5 ? 0.65 : 0.15;
            desiredVx = (nx * (1 - orbitWeight) - nz * npc.strafeDir * orbitWeight) * npc.speed;
            desiredVz = (nz * (1 - orbitWeight) + nx * npc.strafeDir * orbitWeight) * npc.speed;
            if (distP <= 2.7 && npc.attackCooldown <= 0 && activeTokens < 3) {
              npc.state = 'windup';
              npc.stateTimer = npc.windupDuration;
              npc.hasAttackToken = true;
              activeTokens++;
              this.audio.playTelegraphCue();
            }
          } else {
            // Chaser policy
            if (distP > 2.0) {
              desiredVx = nx * npc.speed;
              desiredVz = nz * npc.speed;
            }
            if (distP <= 2.6 && npc.attackCooldown <= 0 && activeTokens < 3) {
              npc.state = 'windup';
              npc.stateTimer = npc.windupDuration;
              npc.hasAttackToken = true;
              activeTokens++;
              this.audio.playTelegraphCue();
            }
          }

          npc.vx += (desiredVx - npc.vx) * Math.min(1, dt * 8);
          npc.vz += (desiredVz - npc.vz) * Math.min(1, dt * 8);
        }
      } else if (npc.state === 'flee') {
        npc.stateTimer -= dt;
        const nx = -dxP / Math.max(0.01, distP);
        const nz = -dzP / Math.max(0.01, distP);
        npc.vx = nx * 4.4;
        npc.vz = nz * 4.4;
        npc.heading = Math.atan2(npc.vx, npc.vz);
        if (npc.stateTimer <= 0) npc.state = 'walk';
      } else {
        // Calm Dhaka sidewalk strolling
        rig.telegraphRing.visible = false;
        const tx = npc.targetX - npc.x;
        const tz = npc.targetZ - npc.z;
        const td = Math.hypot(tx, tz);
        if (td < 1.5 || npc.stateTimer <= 0) {
          npc.targetX = THREE.MathUtils.clamp(npc.x + (Math.random() - 0.5) * 24, -75, 75);
          npc.targetZ = THREE.MathUtils.clamp(npc.z + (Math.random() - 0.5) * 24, -75, 72);
          npc.stateTimer = 4 + Math.random() * 4;
        } else {
          npc.stateTimer -= dt;
          npc.vx = (tx / td) * npc.speed * 0.75;
          npc.vz = (tz / td) * npc.speed * 0.75;
          npc.heading = Math.atan2(npc.vx, npc.vz);
        }
      }

      const resolved = resolveCircleCollisions(npc.x + npc.vx * dt, npc.z + npc.vz * dt, 0.48, this.colliders);
      npc.x = resolved.x;
      npc.z = resolved.z;

      rig.group.position.set(npc.x, 0, npc.z);
      rig.group.rotation.y = npc.heading;
      const spd = Math.hypot(npc.vx, npc.vz);
      const gait = spd > 0.2 ? Math.sin(this.playTimeSec * 7 + i) * 0.65 : 0;
      rig.leftLeg.rotation.x = gait;
      rig.rightLeg.rotation.x = -gait;
      rig.leftArm.rotation.x = -gait * 0.7;
      if (npc.state !== 'windup') {
        rig.rightArm.rotation.x = npc.policy === 'ranged' && isHostile ? -Math.PI * 0.38 : gait * 0.7;
      }
    }
  }

  private executeEnemyAttack(npc: NpcState, distP: number, dxP: number, dzP: number): void {
    const isRanged = npc.policy === 'ranged' || npc.role === 'boss';
    if (isRanged) {
      this.audio.playWeapon(npc.role === 'boss' ? 'shotgun' : 'pistol');
      this.spawnBurst(npc.x + Math.sin(npc.heading) * 0.7, 1.35, npc.z + Math.cos(npc.heading) * 0.7, 0xffa726, 6, 5);
      // Slight inaccuracy so moving/dodging player can evade shots
      const hitChance = distP < 10 ? 0.72 : 0.48;
      if (distP < 22 && Math.random() < hitChance) {
        this.hurtPlayer(npc.role === 'boss' ? 16 : 11);
      }
    } else if (distP < 3.1) {
      this.audio.playWeapon('fists');
      this.hurtPlayer(npc.role === 'boss' ? 18 : 12);
      const len = Math.max(0.1, distP);
      this.player.vx += (dxP / len) * 4;
      this.player.vz += (dzP / len) * 4;
    }
  }

  private hurtPlayer(rawDamage: number): void {
    if (this.player.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      if (veh) {
        veh.hp = Math.max(0, veh.hp - rawDamage * 0.8);
        veh.damagedSmoke = veh.hp < veh.maxHp * 0.4;
      }
      applyPlayerDamage(this.player, Math.ceil(rawDamage * 0.35));
    } else {
      applyPlayerDamage(this.player, rawDamage);
    }
    this.playerHitFlash = 0.14;
    this.trauma = Math.min(1, this.trauma + 0.32);
    this.spawnBurst(this.player.x, 1.3, this.player.z, 0xef4444, 8, 4);
    this.audio.playImpact(false);
    this.emitHud();
  }

  // === POLICE CHASE & WANTED SYSTEM ===

  private updatePoliceEscalation(dt: number): void {
    if (this.player.wantedLevel <= 0) return;

    const policeNear = this.npcs.some(
      (n) => n.role === 'police' && n.state !== 'ko' && Math.hypot(n.x - this.player.x, n.z - this.player.z) < 28,
    );
    tickWantedCooldown(this.player, policeNear, dt);

    // Dispatch additional DMP officers up to wantedLevel + 1
    const activePolice = this.npcs.filter((n) => n.role === 'police' && n.state !== 'ko').length;
    const desiredPolice = Math.min(5, this.player.wantedLevel + 1);
    this.policeSpawnTimer -= dt;
    if (activePolice < desiredPolice && this.policeSpawnTimer <= 0) {
      this.policeSpawnTimer = 5.5;
      const angle = Math.random() * Math.PI * 2;
      const sx = THREE.MathUtils.clamp(this.player.x + Math.cos(angle) * 28, -72, 72);
      const sz = THREE.MathUtils.clamp(this.player.z + Math.sin(angle) * 28, -72, 68);
      const safe = resolveCircleCollisions(sx, sz, 0.8, this.colliders);
      const policies: EnemyPolicy[] = ['chaser', 'ranged', 'flanker'];
      this.spawnNpc({
        role: 'police',
        policy: policies[activePolice % policies.length],
        x: safe.x,
        z: safe.z,
        shirtColor: 0x1e3a8a,
        pantsColor: 0x172554,
        accentColor: 0xfacc15,
        hp: 80 + this.player.wantedLevel * 10,
      });

      // Activate DMP Patrol Pickup pursuit at 2+ Stars
      if (this.player.wantedLevel >= 2) {
        const patrol = this.vehicles.find((v) => v.kind === 'police' && v.driver !== 'player');
        if (patrol) {
          patrol.driver = 'police';
          patrol.sirenOn = true;
        }
      }
    }
  }

  // === STORYLINE MISSIONS & SIDE FARES ===

  private updateMissionsAndFare(dt: number): void {
    // Side fare delivery timer
    if (this.fare.active) {
      this.fare.timer -= dt;
      this.world.fareBeacon.visible = true;
      this.world.fareBeacon.position.set(this.fare.destX, 0, this.fare.destZ);
      this.world.fareBeacon.rotation.y += dt * 1.4;

      if (Math.hypot(this.player.x - this.fare.destX, this.player.z - this.fare.destZ) < 6.5) {
        this.player.money += this.fare.reward;
        this.player.faresCompleted += 1;
        this.fare.active = false;
        this.world.fareBeacon.visible = false;
        this.audio.playChime(true);
        this.showToast(`Fare Complete! Earned ৳${this.fare.reward}`);
        this.emitHud();
      } else if (this.fare.timer <= 0) {
        this.fare.active = false;
        this.world.fareBeacon.visible = false;
        this.showToast('Fare timed out — passenger hopped out!');
        this.emitHud();
      }
    } else {
      this.world.fareBeacon.visible = false;
    }

    // Active story mission progression
    const mission = STORY_MISSIONS[this.missionIdx];
    if (!mission) {
      this.world.missionBeacon.visible = false;
      return;
    }
    const step = mission.steps[this.stepIdx];
    if (!step) return;

    this.world.missionBeacon.visible = true;
    this.world.missionBeacon.position.set(step.targetX, 0, step.targetZ);
    this.world.missionBeacon.rotation.y += dt * 1.2;

    if (mission.weatherOverride && this.stepIdx === 0 && !this.missionEntitiesSpawned) {
      this.weather = mission.weatherOverride;
    }

    // Spawn mission enemies once when entering an eliminate step
    if (step.kind === 'eliminate' && !this.missionEntitiesSpawned) {
      this.missionEntitiesSpawned = true;
      this.stepKills = 0;
      const count = step.requiredKills ?? 3;
      const policies: EnemyPolicy[] = ['chaser', 'ranged', 'flanker'];
      for (let i = 0; i < count; i++) {
        const ang = (i / count) * Math.PI * 2;
        const ex = step.targetX + Math.cos(ang) * 4.5;
        const ez = step.targetZ + Math.sin(ang) * 4.5;
        const safe = resolveCircleCollisions(ex, ez, 0.7, this.colliders);
        const isBoss = i === count - 1 && mission.index >= 2;
        this.spawnNpc({
          role: isBoss ? 'boss' : 'syndicate',
          policy: isBoss ? 'flanker' : policies[i % policies.length],
          x: safe.x,
          z: safe.z,
          shirtColor: isBoss ? 0x7f1d1d : 0x991b1b,
          pantsColor: 0x18181b,
          accentColor: isBoss ? 0xfbbf24 : 0xef4444,
          hp: isBoss ? 170 : 65 + mission.index * 12,
          missionTag: 'target',
        });
      }
    }

    const distToStep = Math.hypot(this.player.x - step.targetX, this.player.z - step.targetZ);
    let stepComplete = false;

    if (step.kind === 'goto' && distToStep <= step.radius) {
      stepComplete = true;
    } else if (step.kind === 'eliminate' && this.stepKills >= (step.requiredKills ?? 3)) {
      stepComplete = true;
    } else if (step.kind === 'steal_deliver') {
      const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
      if (veh && (!step.vehicleKindRequired || veh.kind === step.vehicleKindRequired) && distToStep <= step.radius) {
        stepComplete = true;
      }
    }

    if (stepComplete) {
      this.advanceMissionStep(mission);
    }
  }

  private advanceMissionStep(mission: (typeof STORY_MISSIONS)[number]): void {
    this.stepIdx += 1;
    this.missionEntitiesSpawned = false;
    this.stepKills = 0;

    if (this.stepIdx < mission.steps.length) {
      const nextStep = mission.steps[this.stepIdx];
      this.audio.playChime(false);
      if (nextStep.dialogueSpeaker && nextStep.dialogueBangla && nextStep.dialogueEnglish) {
        this.triggerDialogue(nextStep.dialogueSpeaker, nextStep.dialogueBangla, nextStep.dialogueEnglish, 7);
      }
      // Mission 2 steal step raises 2-star DMP heat for dramatic chase
      if (nextStep.kind === 'steal_deliver') {
        addWantedHeat(this.player, 35);
      }
      this.emitHud();
      return;
    }

    // Complete entire mission!
    this.player.money += mission.rewardMoney;
    this.player.missionsCompleted += 1;
    this.audio.playChime(true);

    let rewardText = `MISSION PASSED: ${mission.title} (+৳${mission.rewardMoney})`;
    if (mission.unlockWeapon && !this.player.unlockedWeapons.includes(mission.unlockWeapon)) {
      const w = mission.unlockWeapon;
      const spec = WEAPON_SPECS[w];
      this.player.unlockedWeapons.push(w);
      this.player.magAmmo[w] = spec.magSize;
      this.player.ammo[w] = spec.magSize * 4;
      this.player.weapon = w;
      setHumanoidWeaponMesh(this.playerRig, w);
      rewardText += ` · Unlocked ${spec.name}!`;
    }
    this.showToast(rewardText);

    this.missionIdx += 1;
    this.stepIdx = 0;

    if (this.missionIdx >= STORY_MISSIONS.length) {
      // Storyline Victory!
      this.phase = 'victory';
      document.body.dataset.state = 'gameover';
      this.audio.silenceLoops();
    } else {
      const nextM = STORY_MISSIONS[this.missionIdx];
      const firstStep = nextM.steps[0];
      if (firstStep.dialogueSpeaker && firstStep.dialogueBangla && firstStep.dialogueEnglish) {
        this.triggerDialogue(firstStep.dialogueSpeaker, firstStep.dialogueBangla, firstStep.dialogueEnglish, 7.5);
      }
    }
    this.emitHud();
  }

  // === DYNAMIC ENVIRONMENT, LIGHTING, MONSOON & CAMERA ===

  private applyEnvironmentLighting(dt: number): void {
    const h = this.timeOfDay;
    // Compute sun arc (6:00 sunrise -> 12:30 zenith -> 18:30 sunset -> night)
    const dayPhase = ((h - 6) / 12) * Math.PI;
    const sunElev = Math.sin(dayPhase);
    const isNight = sunElev < -0.05;

    if (!isNight) {
      const sunAngle = dayPhase;
      this.sunLight.position.set(
        this.player.x + Math.cos(sunAngle) * 75,
        Math.max(14, Math.sin(sunAngle) * 85),
        this.player.z + 32,
      );
      this.sunLight.target.position.set(this.player.x, 0, this.player.z);
    } else {
      // Cool moonlight from high above
      this.sunLight.position.set(this.player.x - 40, 68, this.player.z - 25);
      this.sunLight.target.position.set(this.player.x, 0, this.player.z);
    }

    // Blend sky & fog palette smoothly
    if (h >= 5 && h < 8) {
      _tmpColor.lerpColors(_skyNight, _skySunset, (h - 5) / 3);
    } else if (h >= 8 && h < 16) {
      _tmpColor.lerpColors(_skySunset, _skyDay, Math.min(1, (h - 8) / 2.5));
    } else if (h >= 16 && h < 19) {
      _tmpColor.lerpColors(_skyDay, _skySunset, (h - 16) / 2.4);
    } else if (h >= 19 && h < 21) {
      _tmpColor.lerpColors(_skySunset, _skyNight, (h - 19) / 2);
    } else {
      _tmpColor.copy(_skyNight);
    }

    if (this.weather === 'monsoon') {
      _tmpColor.lerp(_skyStorm, 0.55);
    } else if (this.weather === 'storm') {
      _tmpColor.lerp(_skyStorm, 0.8);
    }

    // Thunderstorm lightning flash
    if (this.weather === 'storm' && dt > 0) {
      this.lightningTimer -= dt;
      if (this.lightningTimer <= 0) {
        this.lightningTimer = 7 + Math.random() * 9;
        this.lightningFlash = 1.0;
        this.audio.playThunder();
      }
      if (this.lightningFlash > 0) {
        this.lightningFlash = Math.max(0, this.lightningFlash - dt * 4.5);
        _tmpColor.lerp(new THREE.Color(0xdbeafe), this.lightningFlash * 0.65);
      }
    } else {
      this.lightningFlash = 0;
    }

    (this.scene.background as THREE.Color).copy(_tmpColor);
    if (this.scene.fog instanceof THREE.FogExp2) {
      this.scene.fog.color.copy(_tmpColor);
      this.scene.fog.density = this.weather === 'storm' ? 0.0115 : this.weather === 'monsoon' ? 0.0092 : 0.0064;
    }

    const nightFactor = isNight ? 1 : THREE.MathUtils.clamp(1 - sunElev * 3, 0, 1);
    const stormDim = this.weather === 'storm' ? 0.62 : this.weather === 'monsoon' ? 0.78 : 1.0;

    this.sunLight.intensity = (isNight ? 0.75 : 2.15 * Math.max(0.45, sunElev)) * stormDim + this.lightningFlash * 2.5;
    this.sunLight.color.setHex(isNight ? 0x7da4d8 : sunElev < 0.35 ? 0xffb370 : 0xffe8c4);
    this.hemiLight.intensity = (isNight ? 0.52 : 0.82) * stormDim;

    // Streetlights, neon signs & vehicle headlights glow brighter at night / monsoon
    const glow = 0.35 + nightFactor * 1.9 + (this.weather !== 'clear' ? 0.45 : 0);
    this.world.lampGlowMaterials.forEach((m) => (m.emissiveIntensity = glow));
    this.world.signMaterials.forEach((m) => (m.emissiveIntensity = 0.4 + nightFactor * 0.95));
    this.vehicleRigs.forEach((rig) => {
      rig.headlights.forEach((hl) => (hl.emissiveIntensity = 0.45 + nightFactor * 1.8));
    });
  }

  private updateWorldVisuals(dt: number): void {
    // Gentle Buriganga boat bobbing
    const t = performance.now() * 0.001;
    for (const b of this.world.boats) {
      b.group.position.y = -0.92 + Math.sin(t * 1.7 + b.phase) * 0.08;
      b.group.rotation.z = Math.sin(t * 1.3 + b.phase) * 0.04;
    }

    // Monsoon rain streaks around active camera
    const raining = this.weather === 'monsoon' || this.weather === 'storm';
    this.world.rainPoints.visible = raining;
    if (raining) {
      const arr = this.world.rainPositions;
      const fallSpeed = this.weather === 'storm' ? 28 : 20;
      const windX = this.weather === 'storm' ? 4.5 : 1.5;
      const cx = this.camera.position.x;
      const cz = this.camera.position.z;
      for (let i = 0; i < arr.length; i += 3) {
        arr[i] += windX * dt;
        arr[i + 1] -= fallSpeed * dt;
        if (arr[i + 1] < 0) {
          arr[i] = cx + (Math.random() - 0.5) * 60;
          arr[i + 1] = 22 + Math.random() * 8;
          arr[i + 2] = cz + (Math.random() - 0.5) * 60;
        }
      }
      (this.world.rainPoints.geometry.attributes.position as THREE.BufferAttribute).needsUpdate = true;
    }
  }

  private updateTitleCamera(): void {
    // Cinematic flyover framing Shapla Monument Crossing, Gulshan skyline, and lively Dhaka avenues
    const r = 42;
    const cx = Math.sin(this.titleAngle) * r;
    const cz = Math.cos(this.titleAngle) * r * 0.82 + 10;
    this.camera.position.set(cx, 18.5 + Math.sin(this.titleAngle * 1.5) * 2.5, cz);
    this.camera.lookAt(-6, 5.5, -4);
  }

  private updateGameplayCamera(dt: number): void {
    const inVeh = Boolean(this.player.vehicleId);
    const dist = inVeh ? 10.6 : 7.2;
    const height = inVeh ? 4.2 : 3.2;

    const targetX = this.player.x;
    const targetY = this.player.y + (inVeh ? 1.65 : 1.55);
    const targetZ = this.player.z;

    const desiredX = targetX + Math.sin(this.camYaw) * Math.cos(this.camPitch) * dist;
    const desiredY = targetY + Math.sin(this.camPitch) * dist + height * 0.32;
    const desiredZ = targetZ + Math.cos(this.camYaw) * Math.cos(this.camPitch) * dist;

    // Raycast from player anchor to desired camera position to pull in before clipping walls
    _vecA.set(targetX, targetY, targetZ);
    _vecB.set(desiredX, desiredY, desiredZ);
    _vecC.subVectors(_vecB, _vecA);
    const desiredLen = _vecC.length();
    if (desiredLen > 0.1) {
      _vecC.divideScalar(desiredLen);
      this.raycaster.set(_vecA, _vecC);
      this.raycaster.far = desiredLen;
      const hits = this.raycaster.intersectObjects(this.world.cameraOccluders, false);
      if (hits.length > 0 && hits[0].distance < desiredLen) {
        const pulled = Math.max(2.2, hits[0].distance - 0.65);
        _vecB.copy(_vecA).addScaledVector(_vecC, pulled);
      }
    }

    const damp = 1 - Math.exp(-9.5 * dt);
    this.camPos.lerp(_vecB, damp);
    this.camTarget.lerp(_vecA, 1 - Math.exp(-14 * dt));

    // Trauma-squared camera shake
    const shake = this.trauma * this.trauma;
    const sx = shake > 0.001 ? (Math.random() - 0.5) * shake * 0.85 : 0;
    const sy = shake > 0.001 ? (Math.random() - 0.5) * shake * 0.65 : 0;

    this.camera.position.set(this.camPos.x + sx, Math.max(1.2, this.camPos.y + sy), this.camPos.z);
    this.camera.lookAt(this.camTarget.x, this.camTarget.y, this.camTarget.z);

    // Subtle speed FOV kick when driving fast
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const targetFov = veh ? 58 + Math.min(14, Math.abs(veh.speed) * 0.45) : 58;
    if (Math.abs(this.camera.fov - targetFov) > 0.1) {
      this.camera.fov += (targetFov - this.camera.fov) * (1 - Math.exp(-6 * dt));
      this.camera.updateProjectionMatrix();
    }
  }

  // === POOLED PARTICLE BURSTS ===

  private spawnBurst(x: number, y: number, z: number, colorHex: number, count: number, speed: number): void {
    for (let i = 0; i < count; i++) {
      const slot = this.particlePool[this.particleCursor];
      this.particleCursor = (this.particleCursor + 1) % this.particlePool.length;
      slot.active = true;
      slot.x = x;
      slot.y = y;
      slot.z = z;
      const a = Math.random() * Math.PI * 2;
      const spd = speed * (0.4 + Math.random() * 0.8);
      slot.vx = Math.cos(a) * spd;
      slot.vy = 1.5 + Math.random() * speed * 0.65;
      slot.vz = Math.sin(a) * spd;
      slot.maxLife = 0.25 + Math.random() * 0.35;
      slot.life = slot.maxLife;
      slot.scale = 0.6 + Math.random() * 0.9;
      slot.color.setHex(colorHex);
      this.particleMesh.setColorAt(this.particleCursor === 0 ? this.particlePool.length - 1 : this.particleCursor - 1, slot.color);
    }
    if (this.particleMesh.instanceColor) this.particleMesh.instanceColor.needsUpdate = true;
  }

  private updateParticles(dt: number): void {
    let anyChanged = false;
    for (let i = 0; i < this.particlePool.length; i++) {
      const p = this.particlePool[i];
      if (!p.active) continue;
      anyChanged = true;
      p.life -= dt;
      if (p.life <= 0) {
        p.active = false;
        _mat4.makeScale(0, 0, 0);
        this.particleMesh.setMatrixAt(i, _mat4);
        continue;
      }
      p.vy -= 14 * dt;
      p.x += p.vx * dt;
      p.y = Math.max(0.05, p.y + p.vy * dt);
      p.z += p.vz * dt;
      const s = p.scale * (p.life / p.maxLife);
      _vecA.set(p.x, p.y, p.z);
      _scaleVec.set(s, s, s);
      _mat4.compose(_vecA, _quat, _scaleVec);
      this.particleMesh.setMatrixAt(i, _mat4);
    }
    if (anyChanged) this.particleMesh.instanceMatrix.needsUpdate = true;
  }

  private triggerDialogue(speaker: string, bangla: string, english: string, duration = 6.5): void {
    this.dialogue = { speaker, bangla, english, timer: duration };
  }

  private showToast(msg: string): void {
    this.toastMsg = msg;
    this.toastTimer = 3.2;
  }

  // === HUD SNAPSHOT ===

  private computeInteractPrompt(): { prompt: string | null; key: string | null } {
    for (const poi of POIS) {
      if (Math.hypot(poi.x - this.player.x, poi.z - this.player.z) <= poi.radius + 2.0) {
        if (poi.id === 'garage') return { prompt: 'Open Dhaka Customs Garage', key: 'G' };
        if (poi.id === 'shop') return { prompt: "Enter Babu's Hardware & Gear", key: 'G' };
        if (poi.id === 'tea_stall') return { prompt: 'Drink Masala Cha (+25 HP)', key: 'G' };
      }
    }
    if (!this.player.vehicleId) {
      const nearVeh = this.getActiveOrNearestVehicle(5.6);
      if (nearVeh) {
        return { prompt: `Drive ${VEHICLE_SPECS[nearVeh.kind].name}`, key: 'F' };
      }
    }
    return { prompt: null, key: null };
  }

  private emitHud(): void {
    const distId = getDistrictAt(this.player.x, this.player.z);
    const distMeta = DISTRICT_META[distId];
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const mission = STORY_MISSIONS[this.missionIdx];
    const step = mission?.steps[this.stepIdx];
    const { prompt, key } = this.computeInteractPrompt();

    const blips: MinimapBlip[] = [];
    POIS.forEach((p) => {
      if (p.id === 'garage') blips.push({ x: p.x, z: p.z, kind: 'garage' });
      if (p.id === 'shop') blips.push({ x: p.x, z: p.z, kind: 'shop' });
    });
    if (step) {
      blips.push({ x: step.targetX, z: step.targetZ, kind: 'mission' });
    }
    if (this.fare.active) {
      blips.push({ x: this.fare.destX, z: this.fare.destZ, kind: 'fare' });
    }
    for (const n of this.npcs) {
      if (n.state === 'ko') continue;
      if (n.role === 'police' && this.player.wantedLevel > 0) {
        blips.push({ x: n.x, z: n.z, kind: 'police' });
      } else if (n.role === 'syndicate' || n.role === 'boss') {
        blips.push({ x: n.x, z: n.z, kind: 'enemy' });
      }
    }

    const w = this.player.weapon;
    const maxWantedCd = Math.max(1, 14 + this.player.wantedLevel * 4);

    this.onHudUpdate({
      phase: this.phase,
      modal: this.modal,
      districtName: distMeta.name,
      districtBangla: distMeta.bangla,
      timeOfDay: this.timeOfDay,
      timeLabel: formatDhakaTime(this.timeOfDay),
      weather: this.weather,
      hp: Math.round(this.player.hp),
      maxHp: this.player.maxHp,
      armor: Math.round(this.player.armor),
      maxArmor: this.player.maxArmor,
      stamina: Math.round(this.player.stamina),
      money: Math.round(this.player.money),
      wantedLevel: this.player.wantedLevel,
      wantedCooldownRatio: this.player.wantedLevel > 0 ? this.player.wantedCooldown / maxWantedCd : 0,
      inVehicle: Boolean(veh),
      vehicleName: veh ? VEHICLE_SPECS[veh.kind].name : '',
      vehicleBangla: veh ? VEHICLE_SPECS[veh.kind].banglaName : '',
      vehicleKind: veh ? veh.kind : null,
      vehicleSpeedKmh: veh ? Math.round(Math.abs(veh.speed) * 3.6) : 0,
      vehicleHp: veh ? Math.round(veh.hp) : 0,
      vehicleMaxHp: veh ? veh.maxHp : 100,
      weapon: w,
      unlockedWeapons: [...this.player.unlockedWeapons],
      magAmmo: this.player.magAmmo[w] ?? 0,
      reserveAmmo: this.player.ammo[w] ?? 0,
      reloading: this.player.reloadTimer > 0,
      outfit: this.player.outfit,
      unlockedOutfits: [...this.player.unlockedOutfits],
      interactPrompt: prompt,
      interactKey: key,
      missionTitle: mission ? mission.title : null,
      missionBangla: mission ? mission.banglaTitle : null,
      missionObjective: step ? step.objective : 'Free Roam Dhaka — Press [N] in a vehicle for Fare Runs',
      missionProgressText:
        step && step.kind === 'eliminate' ? `${this.stepKills} / ${step.requiredKills ?? 3} Defeated` : null,
      fareActive: this.fare.active,
      fareText: this.fare.active ? `Drop ${this.fare.passengerName} at ${this.fare.destLabel}` : null,
      fareTimer: Math.ceil(this.fare.timer),
      dialogue: this.dialogue ? { ...this.dialogue } : null,
      toast: this.toastMsg,
      muted: this.audio.isMuted(),
      playerX: this.player.x,
      playerZ: this.player.z,
      playerHeading: this.player.heading,
      blips,
      stats: {
        kills: this.player.kills,
        missionsCompleted: this.player.missionsCompleted,
        totalMissions: STORY_MISSIONS.length,
        faresCompleted: this.player.faresCompleted,
        playTimeSec: Math.round(this.playTimeSec),
        cause: this.gameOverCause,
      },
    });
  }

  public dispose(): void {
    this.disposed = true;
    cancelAnimationFrame(this.frameId);
    clearTimeout(this.frameId);
    this.resizeObserver.disconnect();
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('blur', this.onBlur);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    window.removeEventListener('mouseup', this.onMouseUp);
    window.removeEventListener('mousemove', this.onMouseMove);
    this.renderer.domElement.removeEventListener('mousedown', this.onMouseDown);
    this.renderer.domElement.removeEventListener('contextmenu', this.onContextMenu);
    this.world.disposables.forEach((d) => d.dispose());
    this.textures.dispose();
    this.audio.dispose();
    this.renderer.dispose();
  }
}
