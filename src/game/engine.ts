import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import {
  AabbCollider,
  ActiveDialogue,
  EnemyPolicy,
  GamePhase,
  GraphicsQualityPreset,
  HudSnapshot,
  InteractModalKind,
  InventoryItem,
  LiveWeatherHudState,
  MinimapBlip,
  NpcState,
  OutfitId,
  PlayerState,
  RegionalPlacesHudState,
  SafehouseId,
  SafehouseSpec,
  DhakaSaveGameData,
  SideFareState,
  SideMissionKind,
  SideMissionState,
  InteractiveNpcConversation,
  TimePreset,
  VehicleCustomization,
  VehicleKind,
  VehicleState,
  VEHICLE_SPECS,
  WeaponId,
  WEAPON_SPECS,
  WeatherMode,
  CompanionState,
  StoreRobberyState,
  PoliceSearchRadiusState,
  PhoneContact,
} from './types';
import {
  addWantedHeat,
  applyGarageService,
  applyPlayerDamage,
  applyVehicleCustomization,
  buyShopItem,
  createInitialPlayerState,
  equipOrBuyOutfit,
  formatDhakaTime,
  purchaseSafehouse,
  refuelVehicle,
  resolveCircleCollisions,
  restAtSafehouse,
  stepVehicleKinematics,
  tickWantedCooldown,
  useInventoryItem,
} from './physics';
import {
  AVAILABLE_SIDE_MISSIONS,
  buildStaticColliders,
  DISTRICT_META,
  FARE_DESTINATIONS,
  getDistrictAt,
  INITIAL_VEHICLES,
  INTERACTIVE_NPCS,
  POIS,
  RIVER_Z_START,
  ROAD_SEGMENTS,
  SAFEHOUSE_SPECS,
  STORY_MISSIONS,
  TRAFFIC_ROUTES,
  WORLD_HALF_SIZE,
} from './worldData';
import {
  hasDhakaSaveGame,
  loadDhakaGame,
  saveDhakaGame,
} from './saveSystem';
import { getNearestStreetCorridor } from './dhakaGeoData';
import { fetchDhakaLiveWeather, OPEN_METEO_CACHE_TTL_MS } from '../api/openMeteoClient';
import { FALLBACK_DHAKA_REGIONAL_PLACES, fetchDhakaRegionalPlaces } from '../api/bdapisClient';
import { createDhakaTextures, DhakaTextureSet } from './textures';
import {
  createHumanoidRig,
  createVehicleRig,
  HumanoidRig,
  setHumanoidOutfit,
  setHumanoidWeaponMesh,
  upgradeHumanoidRigToGlb,
  VehicleRig,
} from './models';
import { buildDhakaWorld, WorldVisualHandles } from './worldBuilder';
import { DhakaAudioEngine } from './audio';
import { GraphicsQualityConfig, isMobileDevice, resolveGraphicsQualityConfig } from './graphicsQuality';
import {
  DhakaCharacterGlbTemplate,
  DhakaLandmarkGlbTemplate,
  DhakaSedanGlbTemplate,
  getAssetUrl,
  loadDhakaCharacterTemplate,
  loadDhakaLandmarkTemplate,
  loadDhakaSedanTemplate,
} from './assetLoader';
import {
  createTrafficLightVisuals,
  spawnTrafficVehicle,
  TrafficLightHandles,
  updateLaneAwareTrafficVehicle,
} from './trafficSystem';
import {
  updateIntelligentPedestrian,
  updatePedestrianAnimator,
} from './pedestrianSystem';
import {
  isWatercraftKind,
  BURIGANGA_WATER_LEVEL,
  BURIGANGA_MIN_Z,
  BURIGANGA_MAX_Z,
  RIVER_DOCKS,
  RiverDockBerth,
  getRiverWaveSample,
  isPointInBurigangaWater,
  getNearestRiverDock,
  dockWatercraftAtBerth,
} from './waterSystem';
import { NetworkClient } from './multiplayer/networkClient';
import { RemotePlayerRig } from './multiplayer/remotePlayerRig';
import { NetPlayerState, resolveServerUrl, SCombatEventMsg } from './multiplayer/protocol';
import { CameraRigController, CameraViewMode } from './cameraSystem';
import {
  CharacterSwitchState,
  createInitialCharacterSwitchState,
  createInitialProtagonistState,
  PROTAGONIST_PROFILES,
  ProtagonistId,
  ProtagonistInstanceState,
} from './protagonists';

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
const _zenithDay = new THREE.Color(0x2d74c4);
const _horizonDay = new THREE.Color(0xc2dceb);
const _zenithDawn = new THREE.Color(0x3b4d82);
const _horizonDawn = new THREE.Color(0xf0a672);
const _zenithSunset = new THREE.Color(0x2c3668);
const _horizonSunset = new THREE.Color(0xe8834a);
const _zenithNight = new THREE.Color(0x060b18);
const _horizonNight = new THREE.Color(0x13223a);
const _zenithStorm = new THREE.Color(0x1f2b38);
const _horizonStorm = new THREE.Color(0x4a5968);
const _sunColorDay = new THREE.Color(0xfff1d6);
const _sunColorSunset = new THREE.Color(0xff9a4d);
const _sunColorMoon = new THREE.Color(0x94b8e8);
const _lightningColor = new THREE.Color(0xe0f2fe);
const _tmpZenith = new THREE.Color();
const _tmpHorizon = new THREE.Color();
const _tmpSunColor = new THREE.Color();
const _tmpRoadColor = new THREE.Color();
const _dryRoadColor = new THREE.Color(0xffffff);
const _wetRoadColor = new THREE.Color(0xaab6c4);
const _sunDirVec = new THREE.Vector3();

export class DhakaGameEngine {
  private container: HTMLElement;
  private onHudUpdate: (snap: HudSnapshot) => void;
  private isSoftwareGl: boolean;
  private graphicsQuality: GraphicsQualityPreset;
  private qualityConfig: GraphicsQualityConfig;
  private frameCount = 0;
  private fpsWindowStartMs = performance.now();
  private lowFpsCounter = 0;
  private characterTemplate: DhakaCharacterGlbTemplate | null = null;
  private sedanTemplate: DhakaSedanGlbTemplate | null = null;
  private landmarkTemplate: DhakaLandmarkGlbTemplate | null = null;

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
  private playerSquashY = 1;
  private playerHitFlash = 0;

  private vehicles: VehicleState[] = [];
  private vehicleRigs = new Map<string, VehicleRig>();
  private trafficCounter = 0;
  private trafficLights: TrafficLightHandles;

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
  private activeSideMission: SideMissionState | null = null;
  private activeConversation: InteractiveNpcConversation | null = null;
  private activeSafehouseId: SafehouseId | null = null;
  private hasSaveGame = false;
  private dialogue: ActiveDialogue | null = null;
  private toastMsg: string | null = null;
  private toastTimer = 0;
  private playTimeSec = 0;
  private gameOverCause = '';
  private defeatDelayTimer = 0;

  // Environment & Weather
  private timeOfDay = 15.8; // Golden late-afternoon Dhaka light on boot
  private weather: WeatherMode = 'clear';
  private roadWetness = 0.0; // Dynamic monsoon puddle accumulation & drying
  private splashAnimTimer = 0.0;
  private lightningTimer = 6;
  private lightningFlash = 0;

  // Authoritative Multiplayer
  private netClient: NetworkClient | null = null;
  private remotePlayerRigs = new Map<string, RemotePlayerRig>();
  private killfeed: Array<{ id: string; text: string; timer: number }> = [];
  private chatMessages: Array<{ id: string; sender: string; text: string; isSystem: boolean }> = [];
  private multiplayerPing = 0;
  private multiplayerPlayerCount = 1;

  // Optional API-backed features (strictly opt-in; never block or freeze gameplay)
  private liveWeatherState: LiveWeatherHudState = {
    enabled: false,
    status: 'idle',
    conditionLabel: null,
    conditionBangla: null,
    temperatureC: null,
    humidityPct: null,
    windKmh: null,
    fromCache: false,
    error: null,
  };
  private liveWeatherLastCheckMs = 0;
  private regionalPlacesState: RegionalPlacesHudState = {
    enabled: false,
    status: 'idle',
    source: 'fallback',
    places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
    error: null,
  };

  // Camera & Rig Controller
  private cameraRig: CameraRigController;
  private cameraViewMode: CameraViewMode = 'third_person_close';
  private camYaw = 0; // 0 means looking north (-Z) behind player
  private camPitch = 0.28;
  private camPos = new THREE.Vector3(0, 7, 48);
  private camTarget = new THREE.Vector3(0, 2, 34);
  private titleAngle = 0;
  private trauma = 0;
  private hitstopTimer = 0;
  private raycaster = new THREE.Raycaster();

  // Dual Protagonist System (Tariq & Maya)
  private activeProtagonistId: ProtagonistId = 'tariq';
  private secondaryProtagonistState: ProtagonistInstanceState;
  private secondaryProtagonistRig: HumanoidRig | null = null;
  private characterSwitchState: CharacterSwitchState = createInitialCharacterSwitchState();
  private specialAbilityTimer = 0;
  private specialAbilityCooldown = 0;
  private specialAbilityActive = false;

  // GTA VI Core Systems: Weapon Wheel, Smartphone, Co-Op Companion & Robberies
  private weaponWheelOpen = false;
  private smartphoneOpen = false;
  private activeCompanion: CompanionState | null = null;
  private companionBanterTimer = 14.0;
  private companionAttackTimer = 0;
  private activeRobbery: StoreRobberyState | null = null;
  private policeSearchRadius: PoliceSearchRadiusState | null = null;

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

    const isMobile = isMobileDevice();
    this.graphicsQuality = this.isSoftwareGl ? 'low' : isMobile ? 'medium' : 'high';
    this.qualityConfig = resolveGraphicsQualityConfig(
      this.graphicsQuality,
      this.isSoftwareGl,
      window.devicePixelRatio || 1,
    );

    this.renderer = new THREE.WebGLRenderer({
      antialias: !this.isSoftwareGl,
      powerPreference: 'high-performance',
    });
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.12;
    this.renderer.setPixelRatio(this.qualityConfig.pixelRatio);
    this.renderer.shadowMap.enabled = this.qualityConfig.shadowsEnabled;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const width = container.clientWidth || window.innerWidth;
    const height = container.clientHeight || window.innerHeight;
    this.renderer.setSize(width, height);
    container.innerHTML = '';
    container.appendChild(this.renderer.domElement);

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x9bc7e2);
    this.scene.fog = new THREE.FogExp2(0xb8c9d4, 0.0044);

    // Mandatory studio environment reflection map for PBR metals & car clearcoats
    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();

    this.camera = new THREE.PerspectiveCamera(58, width / height, 0.25, 780);

    // Warm/Cool lighting rig (single shadow caster with balanced frustum)
    this.hemiLight = new THREE.HemisphereLight(0xaad4f5, 0x5d4b3c, 0.78);
    this.scene.add(this.hemiLight);

    this.sunLight = new THREE.DirectionalLight(0xffe1b8, 2.2);
    this.sunLight.position.set(54, 78, 38);
    this.sunLight.castShadow = this.qualityConfig.shadowsEnabled;
    this.sunLight.shadow.mapSize.set(this.qualityConfig.shadowMapSize, this.qualityConfig.shadowMapSize);
    const d = 84;
    this.sunLight.shadow.camera.left = -d;
    this.sunLight.shadow.camera.right = d;
    this.sunLight.shadow.camera.top = d;
    this.sunLight.shadow.camera.bottom = -d;
    this.sunLight.shadow.camera.near = 5;
    this.sunLight.shadow.camera.far = 225;
    this.sunLight.shadow.bias = -0.0005;
    this.sunLight.shadow.normalBias = 0.025;
    this.scene.add(this.sunLight);
    this.scene.add(this.sunLight.target);

    this.audio = new DhakaAudioEngine();
    this.textures = createDhakaTextures(this.renderer.capabilities.getMaxAnisotropy());
    this.colliders = buildStaticColliders();
    this.world = buildDhakaWorld(this.scene, this.textures, !this.isSoftwareGl);
    this.applyQualityHandles();

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

    // Camera Rig Setup
    this.cameraRig = new CameraRigController(this.camPos, this.camTarget);

    // Player setup (instant sculpted procedural rig, smoothly upgraded to rigged GLB once loaded)
    this.player = createInitialPlayerState();
    const tariqProf = PROTAGONIST_PROFILES.tariq;
    this.playerRig = createHumanoidRig(
      tariqProf.defaultColors.torso,
      tariqProf.defaultColors.pants,
      tariqProf.defaultColors.accent,
      this.textures,
      {
        isPlayer: true,
        castShadow: !this.isSoftwareGl,
      },
    );
    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);
    this.playerRig.group.position.set(this.player.x, 0, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;
    this.scene.add(this.playerRig.group);

    // Secondary Protagonist Setup (Maya stationed in Gulshan as ambient ally)
    const mayaProf = PROTAGONIST_PROFILES.maya;
    this.secondaryProtagonistState = createInitialProtagonistState('maya', 115, -120);
    this.secondaryProtagonistRig = createHumanoidRig(
      mayaProf.defaultColors.torso,
      mayaProf.defaultColors.pants,
      mayaProf.defaultColors.accent,
      this.textures,
      {
        isPlayer: false,
        castShadow: !this.isSoftwareGl,
      },
    );
    setHumanoidOutfit(this.secondaryProtagonistRig, mayaProf.defaultOutfit);
    setHumanoidWeaponMesh(this.secondaryProtagonistRig, mayaProf.preferredWeapon);
    this.secondaryProtagonistRig.group.position.set(
      this.secondaryProtagonistState.x,
      0,
      this.secondaryProtagonistState.z,
    );
    this.scene.add(this.secondaryProtagonistRig.group);

    this.trafficLights = createTrafficLightVisuals();
    this.scene.add(this.trafficLights.group);

    this.spawnInitialEntities();
    this.applyEnvironmentLighting(0);
    void this.loadCharacterGlbAsync();
    void this.loadSedanGlbAsync();
    void this.loadLandmarkGlbAsync();

    if (!this.isSoftwareGl) {
      this.renderer.compile(this.scene, this.camera);
    }

    this.bindEvents();
    this.resizeObserver = new ResizeObserver(() => this.handleResize());
    this.resizeObserver.observe(this.container);

    this.hasSaveGame = hasDhakaSaveGame();

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

    this.npcRigs.forEach((rig) => {
      rig.animator.disposeMixer();
      this.scene.remove(rig.group);
    });
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
        fuel: 100,
        maxFuel: 100,
        fuelType: vSpawn.kind === 'cng' ? 'cng' : vSpawn.kind === 'truck' || vSpawn.kind === 'bus' || vSpawn.kind === 'launch' ? 'diesel' : 'petrol',
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

    // Spawn 32 Dhaka pedestrians across inner & expanded district sidewalks
    const pedPalette = [0xc0392b, 0x16a085, 0xd4a017, 0x2980b9, 0x8e44ad, 0xe67e22, 0xf1f5f9, 0x1f6f54];
    const sidewalkSpots: Array<[number, number]> = [
      // Core Shapla / Old Dhaka / Gulshan / Kawran
      [-12, 26], [12, 32], [-12, 52], [12, 48],
      [-20, 12], [20, 12], [-40, 12], [40, 12],
      [-12, -18], [12, -18], [-12, -38], [12, -38],
      [-32, -12], [32, -12], [-22, 74], [22, 74],
      [-52, 28], [52, -28],
      // Expanded districts: Motijheel, Old Dhaka South, Sadarghat, Dhanmondi, Mirpur, Banani North
      [114, 24], [156, 52], [116, 114], [172, 114],
      [-114, 92], [-88, 132], [-156, 116], [-12, 174],
      [28, 174], [-156, -14], [-156, -54], [-116, -120],
      [116, -120], [52, -172],
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

    // Spawn 4 DMP Patrol Officers standing watch across Central Thana, Motijheel & Sadarghat
    const policePosts: Array<{ x: number; z: number; policy: EnemyPolicy }> = [
      { x: 28, z: 24, policy: 'chaser' },
      { x: 38, z: 30, policy: 'ranged' },
      { x: 132, z: 12, policy: 'ranged' },
      { x: -16, z: 174, policy: 'chaser' },
    ];
    for (const post of policePosts) {
      this.spawnNpc({
        role: 'police',
        policy: post.policy,
        x: post.x,
        z: post.z,
        shirtColor: 0x1e3a8a,
        pantsColor: 0x172554,
        accentColor: 0xfacc15,
        hp: 85,
      });
    }
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
      castShadow: !this.isSoftwareGl,
    });
    if (this.characterTemplate) {
      upgradeHumanoidRigToGlb(rig, this.characterTemplate, this.textures, !this.isSoftwareGl);
    }
    if (opts.role === 'police' || opts.role === 'syndicate' || opts.role === 'boss') {
      setHumanoidWeaponMesh(rig, opts.policy === 'ranged' ? 'pistol' : opts.role === 'boss' ? 'shotgun' : 'fists');
    }
    rig.group.position.set(npc.x, 0, npc.z);
    rig.group.rotation.y = npc.heading;
    this.scene.add(rig.group);
    this.npcRigs.set(id, rig);
    return npc;
  }

  private async loadCharacterGlbAsync(): Promise<void> {
    const template = await loadDhakaCharacterTemplate(getAssetUrl('models/dhaka_character.glb'));
    if (this.disposed || !template) return;
    this.characterTemplate = template;

    // Smoothly upgrade player & active NPCs to skinned GLB characters in-place
    upgradeHumanoidRigToGlb(this.playerRig, template, this.textures, !this.isSoftwareGl);
    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);

    if (this.secondaryProtagonistRig) {
      upgradeHumanoidRigToGlb(this.secondaryProtagonistRig, template, this.textures, !this.isSoftwareGl);
    }

    this.remotePlayerRigs.forEach((r) => {
      upgradeHumanoidRigToGlb(r.rig, template, this.textures, !this.isSoftwareGl);
    });

    this.npcRigs.forEach((rig) => {
      upgradeHumanoidRigToGlb(rig, template, this.textures, !this.isSoftwareGl);
    });
    this.emitHud();
  }

  private async loadSedanGlbAsync(): Promise<void> {
    const template = await loadDhakaSedanTemplate(getAssetUrl('models/dhaka_sedan.glb'));
    if (this.disposed || !template) return;
    this.sedanTemplate = template;

    // Smoothly upgrade any active procedural sedans to the high-detail GLB model
    for (const v of this.vehicles) {
      if (v.kind === 'sedan') {
        const oldRig = this.vehicleRigs.get(v.id);
        if (oldRig && !oldRig.isGlb) {
          this.scene.remove(oldRig.group);
          const newRig = createVehicleRig(v.kind, v.color, this.textures, !this.isSoftwareGl);
          newRig.group.position.set(v.x, v.y, v.z);
          newRig.group.rotation.y = v.heading;
          this.scene.add(newRig.group);
          this.vehicleRigs.set(v.id, newRig);
        }
      }
    }
  }

  private async loadLandmarkGlbAsync(): Promise<void> {
    const template = await loadDhakaLandmarkTemplate(getAssetUrl('models/dhaka_landmark_ahsan_manzil.glb'));
    if (this.disposed || !template) return;
    this.landmarkTemplate = template;
  }

  public alertNearbyPedestriansOfDanger(x: number, z: number, radius = 35): void {
    for (const n of this.npcs) {
      if (n.role === 'civilian' && Math.hypot(n.x - x, n.z - z) < radius) {
        n.dangerSourceX = x;
        n.dangerSourceZ = z;
      }
    }
  }

  private spawnStreamingPedestrian(): NpcState | null {
    const angle = Math.random() * Math.PI * 2;
    const dist = 32 + Math.random() * 36;
    const px = this.player.x + Math.sin(angle) * dist;
    const pz = this.player.z + Math.cos(angle) * dist;

    if (Math.abs(px) > WORLD_HALF_SIZE - 25 || pz > RIVER_Z_START - 12 || pz < -(WORLD_HALF_SIZE - 25)) {
      return null;
    }
    const safe = resolveCircleCollisions(px, pz, 0.5, this.colliders);

    const pedPalette = [0xc0392b, 0x16a085, 0xd4a017, 0x2980b9, 0x8e44ad, 0xe67e22, 0xf1f5f9, 0x1f6f54, 0x0f766e];
    const shirt = pedPalette[Math.floor(Math.random() * pedPalette.length)];
    const pants = Math.random() < 0.5 ? 0x1e293b : 0x475569;
    const accent = pedPalette[Math.floor(Math.random() * pedPalette.length)];

    return this.spawnNpc({
      role: 'civilian',
      policy: 'chaser',
      x: safe.x,
      z: safe.z,
      shirtColor: shirt,
      pantsColor: pants,
      accentColor: accent,
      hp: 45,
    });
  }

  private spawnStreamingVehicle(): VehicleState | null {
    const routeIdx = Math.floor(Math.random() * TRAFFIC_ROUTES.length);
    const route = TRAFFIC_ROUTES[routeIdx];
    let bestWpIdx = -1;
    let bestDistScore = 999;
    for (let i = 0; i < route.length; i++) {
      const d = Math.hypot(route[i].x - this.player.x, route[i].z - this.player.z);
      if (d >= 40 && d <= 95) {
        const score = Math.abs(d - 65);
        if (score < bestDistScore) {
          bestDistScore = score;
          bestWpIdx = i;
        }
      }
    }
    if (bestWpIdx === -1) return null;

    const kindWeights: VehicleKind[] = [
      'cng', 'cng', 'cng',
      'rickshaw', 'rickshaw', 'rickshaw',
      'sedan', 'sedan',
      'bike', 'bike',
      'bus',
      'truck',
    ];
    const kind = kindWeights[Math.floor(Math.random() * kindWeights.length)];
    const id = `t_stream_${++this.trafficCounter}`;
    const v = spawnTrafficVehicle(id, kind, routeIdx, bestWpIdx, 'traffic');

    for (const other of this.vehicles) {
      if (Math.hypot(other.x - v.x, other.z - v.z) < 8.0) return null;
    }

    this.vehicles.push(v);
    const rig = createVehicleRig(v.kind, v.color, this.textures, !this.isSoftwareGl);
    rig.group.position.set(v.x, 0, v.z);
    rig.group.rotation.y = v.heading;
    this.scene.add(rig.group);
    this.vehicleRigs.set(v.id, rig);
    return v;
  }

  private applyQualityHandles(): void {
    const cfg = this.qualityConfig;
    if (this.world.chunkManager && cfg.lodDistances) {
      this.world.chunkManager.setLodDistances(
        cfg.lodDistances.near,
        cfg.lodDistances.med,
        cfg.lodDistances.far,
      );
    }
    this.world.haloInst.visible = cfg.lampHalosEnabled;
    this.world.poolInst.visible = cfg.lampHalosEnabled;
    const rainCount = Math.max(120, Math.round(this.world.rainMaxCount * cfg.rainDensityScale));
    this.world.rainPoints.geometry.setDrawRange(0, rainCount);
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

  public continueGame(): boolean {
    this.audio.unlock();
    const ok = this.loadGame();
    if (ok) {
      this.phase = 'playing';
      this.modal = 'none';
      document.body.dataset.state = 'playing';
      this.clearInputs();
      this.camYaw = 0;
      this.camPitch = 0.28;
      this.camPos.set(this.player.x, 4.4, this.player.z + 8.5);
      this.camTarget.set(this.player.x, 1.8, this.player.z - 6);
      this.camera.position.copy(this.camPos);
      this.camera.lookAt(this.camTarget);
      this.emitHud();
      return true;
    }
    this.startGame();
    return false;
  }

  public saveGame(): boolean {
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const savePayload: DhakaSaveGameData = {
      version: 1,
      timestamp: Date.now(),
      player: {
        x: this.player.x,
        y: this.player.y ?? 0,
        z: this.player.z,
        heading: this.player.heading,
        hp: this.player.hp,
        armor: this.player.armor,
        stamina: this.player.stamina,
        money: this.player.money,
        wantedLevel: this.player.wantedLevel,
        weapon: this.player.weapon,
        unlockedWeapons: [...this.player.unlockedWeapons],
        ammo: { ...this.player.ammo },
        outfit: this.player.outfit,
        unlockedOutfits: [...this.player.unlockedOutfits],
        unlockedSafehouses: [...this.player.unlockedSafehouses],
        inventory: this.player.inventory.map((i) => ({ ...i })),
      },
      missions: {
        missionIdx: this.missionIdx,
        stepIdx: this.stepIdx,
        missionsCompleted: this.player.missionsCompleted,
        faresCompleted: this.player.faresCompleted,
        bountiesCompleted: this.player.bountiesCompleted ?? 0,
        carTheftsCompleted: this.player.carTheftsCompleted ?? 0,
      },
      world: {
        timeOfDay: this.timeOfDay,
        weather: this.weather,
        playTimeSec: this.playTimeSec,
      },
      ownedVehicles: veh
        ? [
            {
              kind: veh.kind,
              color: veh.color,
              customization: veh.customization ?? {
                engineStage: 0,
                brakesStage: 0,
                hasTurbo: false,
                hasSpoiler: false,
                tintLevel: 0,
                neonColor: null,
                plateJammer: false,
              },
            },
          ]
        : [],
    };

    const ok = saveDhakaGame(savePayload);
    if (ok) {
      this.hasSaveGame = true;
    }
    return ok;
  }

  public loadGame(): boolean {
    const loaded = loadDhakaGame();
    if (!loaded) return false;

    this.playTimeSec = loaded.world.playTimeSec;
    this.player.x = loaded.player.x;
    this.player.y = loaded.player.y ?? 0;
    this.player.z = loaded.player.z;
    this.player.heading = loaded.player.heading;
    this.player.hp = loaded.player.hp;
    this.player.armor = loaded.player.armor;
    this.player.stamina = loaded.player.stamina;
    this.player.money = loaded.player.money;
    this.player.wantedLevel = loaded.player.wantedLevel;
    this.player.weapon = loaded.player.weapon;
    this.player.unlockedWeapons = [...loaded.player.unlockedWeapons];
    this.player.ammo = { ...loaded.player.ammo };
    this.player.outfit = loaded.player.outfit;
    this.player.unlockedOutfits = [...loaded.player.unlockedOutfits];
    this.player.unlockedSafehouses = [...loaded.player.unlockedSafehouses];
    this.player.inventory = loaded.player.inventory.map((i) => ({ ...i }));

    this.missionIdx = loaded.missions.missionIdx;
    this.stepIdx = loaded.missions.stepIdx;
    this.player.missionsCompleted = loaded.missions.missionsCompleted;
    this.player.faresCompleted = loaded.missions.faresCompleted;
    this.player.bountiesCompleted = loaded.missions.bountiesCompleted;
    this.player.carTheftsCompleted = loaded.missions.carTheftsCompleted;

    this.timeOfDay = loaded.world.timeOfDay;
    this.weather = loaded.world.weather;

    this.playerRig.group.position.set(this.player.x, 0, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;
    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);
    this.applyEnvironmentLighting(0);

    if (loaded.ownedVehicles && loaded.ownedVehicles.length > 0) {
      const near = this.getActiveOrNearestVehicle(14);
      if (near) {
        near.customization = { ...loaded.ownedVehicles[0].customization };
        near.color = loaded.ownedVehicles[0].color;
      }
    }

    this.showToast('Game loaded successfully!');
    this.emitHud();
    return true;
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

  public openMultiplayerLobby(): void {
    if (this.phase !== 'playing' && this.phase !== 'title') return;
    this.modal = this.modal === 'multiplayer_lobby' ? 'none' : 'multiplayer_lobby';
    this.clearInputs();
    this.emitHud();
  }

  public connectMultiplayer(
    serverUrl = resolveServerUrl(),
    callsign = 'DhakaPlayer',
    pin?: string,
  ): void {
    if (this.netClient) {
      this.disconnectMultiplayer();
    }

    if (this.phase === 'title') {
      this.startGame();
    }

    this.showToast('Connecting to Dhaka Multiplayer...');
    this.netClient = new NetworkClient(serverUrl, {
      onConnect: () => {
        this.showToast('Connected to Dhaka Server');
        this.emitHud();
      },
      onDisconnect: (reason: string) => {
        this.showToast(`Multiplayer disconnected: ${reason}`);
        this.cleanupRemoteRigs();
        this.emitHud();
      },
      onAuthOk: (player: NetPlayerState) => {
        this.showToast(`Logged in as ${player.name} (৳${player.money})`);
        this.player.money = player.money;
        this.player.hp = player.hp;
        this.player.armor = player.armor;
        this.player.x = player.transform.x;
        this.player.z = player.transform.z;
        this.modal = 'none';
        this.emitHud();
      },
      onCombatEvent: (evt: SCombatEventMsg) => {
        if (evt.lethal) {
          const killText = `${evt.attackerName || 'Player'} defeated ${evt.victimName || 'Player'} [${evt.weapon}]`;
          this.killfeed.unshift({
            id: `${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
            text: killText,
            timer: 5.0,
          });
          if (this.killfeed.length > 6) this.killfeed.pop();
          this.audio.playImpact(true);
        } else {
          this.audio.playImpact(false);
        }
        this.emitHud();
      },
      onChat: (sender: string, text: string, isSystem: boolean) => {
        this.chatMessages.push({
          id: `${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
          sender,
          text,
          isSystem,
        });
        if (this.chatMessages.length > 50) this.chatMessages.shift();
        this.emitHud();
      },
      onPlayerJoined: (p: NetPlayerState) => {
        this.chatMessages.push({
          id: `${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
          sender: 'SYSTEM',
          text: `${p.name} arrived in Dhaka.`,
          isSystem: true,
        });
        this.emitHud();
      },
      onPlayerLeft: (id: string, name: string) => {
        this.chatMessages.push({
          id: `${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
          sender: 'SYSTEM',
          text: `${name || id} left Dhaka.`,
          isSystem: true,
        });
        const rig = this.remotePlayerRigs.get(id);
        if (rig) {
          this.scene.remove(rig.group);
          rig.dispose();
          this.remotePlayerRigs.delete(id);
        }
        this.emitHud();
      },
      onError: (err: string) => {
        this.showToast(`Server error: ${err}`);
      },
    });

    this.netClient.connect(callsign, this.player.outfit, undefined, pin);
  }

  public disconnectMultiplayer(): void {
    if (this.netClient) {
      this.netClient.disconnect();
      this.netClient = null;
    }
    this.cleanupRemoteRigs();
    this.multiplayerPing = 0;
    this.multiplayerPlayerCount = 1;
    this.showToast('Returned to offline single-player mode');
    this.emitHud();
  }

  public sendChatMessage(text: string): void {
    if (this.netClient && this.netClient.isConnected() && text.trim().length > 0) {
      this.netClient.sendChat(text.trim());
    }
  }

  private cleanupRemoteRigs(): void {
    for (const [, rig] of this.remotePlayerRigs) {
      this.scene.remove(rig.group);
      rig.dispose();
    }
    this.remotePlayerRigs.clear();
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
    // Manual weather selection always works and cleanly overrides live weather sync
    if (this.liveWeatherState.enabled) {
      this.liveWeatherState.enabled = false;
    }
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

  public setGraphicsQuality(preset: GraphicsQualityPreset): void {
    this.graphicsQuality = preset;
    this.qualityConfig = resolveGraphicsQualityConfig(
      preset,
      this.isSoftwareGl,
      window.devicePixelRatio || 1,
    );
    const cfg = this.qualityConfig;
    this.renderer.setPixelRatio(cfg.pixelRatio);
    this.renderer.shadowMap.enabled = cfg.shadowsEnabled;
    this.sunLight.castShadow = cfg.shadowsEnabled;
    if (
      this.sunLight.shadow.mapSize.x !== cfg.shadowMapSize ||
      this.sunLight.shadow.mapSize.y !== cfg.shadowMapSize
    ) {
      this.sunLight.shadow.mapSize.set(cfg.shadowMapSize, cfg.shadowMapSize);
      if (this.sunLight.shadow.map) {
        this.sunLight.shadow.map.dispose();
        this.sunLight.shadow.map = null;
      }
    }
    this.applyQualityHandles();
    this.applyEnvironmentLighting(0);
    this.showToast(`Graphics Quality: ${preset.toUpperCase()}`);
    this.emitHud();
  }

  public setLiveWeatherEnabled(enabled: boolean): void {
    this.liveWeatherState.enabled = enabled;
    if (!enabled) {
      this.liveWeatherState.status = 'idle';
      this.liveWeatherState.error = null;
      this.showToast('Switched to Manual Dhaka Weather');
      this.emitHud();
      return;
    }
    void this.syncLiveWeather(false);
  }

  public refreshLiveWeather(): void {
    this.liveWeatherState.enabled = true;
    void this.syncLiveWeather(true);
  }

  private async syncLiveWeather(forceRefresh: boolean): Promise<void> {
    this.liveWeatherState.status = 'loading';
    this.liveWeatherState.error = null;
    this.liveWeatherLastCheckMs = Date.now();
    this.emitHud();

    const result = await fetchDhakaLiveWeather({ forceRefresh });
    if (this.disposed || !this.liveWeatherState.enabled) return;

    if (result.ok && result.reading) {
      const r = result.reading;
      this.liveWeatherState = {
        enabled: true,
        status: 'live',
        conditionLabel: r.conditionLabel,
        conditionBangla: r.conditionBangla,
        temperatureC: r.temperatureC,
        humidityPct: r.humidityPct,
        windKmh: r.windKmh,
        fromCache: r.fromCache,
        error: null,
      };
      this.weather = r.mappedMode;
      this.applyEnvironmentLighting(0);
      this.showToast(`Live Dhaka Weather: ${r.temperatureC}°C · ${r.conditionLabel}`);
    } else {
      this.liveWeatherState = {
        ...this.liveWeatherState,
        enabled: true,
        status: 'error',
        error: result.error ?? 'Could not fetch Dhaka weather — manual mode still works',
      };
      this.showToast('Live weather unavailable — keeping current weather');
    }
    this.emitHud();
  }

  public setRegionalPlacesEnabled(enabled: boolean): void {
    this.regionalPlacesState.enabled = enabled;
    if (!enabled) {
      this.emitHud();
      return;
    }
    void this.loadRegionalPlaces();
  }

  private async loadRegionalPlaces(): Promise<void> {
    this.regionalPlacesState.status = 'loading';
    this.regionalPlacesState.error = null;
    this.emitHud();

    const result = await fetchDhakaRegionalPlaces();
    if (this.disposed) return;

    this.regionalPlacesState = {
      enabled: this.regionalPlacesState.enabled,
      status: result.ok ? 'ready' : 'error',
      source: result.source,
      places: result.places,
      error: result.error,
    };
    this.emitHud();
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

  public handleSafehouseAction(action: 'rest' | 'save' | 'purchase' | 'armory' | 'wardrobe', safehouseId?: SafehouseId): void {
    const id = safehouseId ?? this.activeSafehouseId ?? 'old_dhaka_rooftop';
    const isOwned = this.player.unlockedSafehouses.includes(id);

    if (action === 'purchase') {
      const res = purchaseSafehouse(this.player, id);
      if (res.ok) {
        this.audio.playChime(false);
        this.saveGame();
      }
      this.showToast(res.message);
      this.emitHud();
      return;
    }

    if (!isOwned) {
      this.showToast('You must purchase this safehouse deed first!');
      this.emitHud();
      return;
    }

    if (action === 'rest') {
      this.timeOfDay = (this.timeOfDay + 6) % 24;
      const res = restAtSafehouse(this.player, 6);
      this.audio.playChime(false);
      this.applyEnvironmentLighting(0);
      this.saveGame();
      this.showToast(res.message);
      this.emitHud();
      return;
    }

    if (action === 'save') {
      this.saveGame();
      this.showToast('Game saved to Dhaka Cloud Storage!');
      this.emitHud();
      return;
    }

    if (action === 'armory') {
      let refilled = false;
      for (const w of this.player.unlockedWeapons) {
        const spec = WEAPON_SPECS[w];
        if (spec.magSize > 0) {
          this.player.magAmmo[w] = spec.magSize;
          this.player.ammo[w] = spec.magSize * 4;
          refilled = true;
        }
      }
      this.audio.playChime(false);
      this.showToast(refilled ? 'Armory: All weapons fully reloaded!' : 'Armory accessed.');
      this.emitHud();
      return;
    }

    if (action === 'wardrobe') {
      this.modal = 'wardrobe';
      this.emitHud();
      return;
    }
  }

  public handleRefuelAction(action: 'refuel' | 'jerrycan'): void {
    const veh = this.getActiveOrNearestVehicle(12);
    if (action === 'refuel') {
      const res = refuelVehicle(this.player, veh);
      if (res.ok) {
        this.audio.playChime(false);
        this.saveGame();
      }
      this.showToast(res.message);
      this.emitHud();
      return;
    }

    if (action === 'jerrycan') {
      const cost = 250;
      if (this.player.money < cost) {
        this.showToast('Need ৳250 to buy an emergency Jerrycan');
        return;
      }
      this.player.money -= cost;
      const existing = this.player.inventory.find((i) => i.id === 'item_jerrycan');
      if (existing) {
        existing.count++;
      } else {
        this.player.inventory.push({
          id: 'item_jerrycan',
          name: 'Emergency Octane Jerrycan (10L)',
          banglaName: 'অকটেন জেরিক্যান',
          kind: 'fuel',
          count: 1,
          value: 200,
          description: 'Contains 10L high-octane fuel (+35% tank). Use when stalled.',
        });
      }
      this.audio.playChime(false);
      this.showToast('Bought 10L Emergency Octane Jerrycan!');
      this.emitHud();
      return;
    }
  }

  public handleVehicleUpgrade(option: 'engine' | 'brakes' | 'turbo' | 'spoiler' | 'tint' | 'neon' | 'jammer', val?: any): void {
    const targetVeh = this.getActiveOrNearestVehicle(14);
    const res = applyVehicleCustomization(this.player, targetVeh, option, val);
    if (res.ok) {
      this.audio.playChime(false);
      this.saveGame();
    }
    this.showToast(res.message);
    this.emitHud();
  }

  public handleUseItem(itemId: string): void {
    const veh = this.getActiveOrNearestVehicle(6);
    const res = useInventoryItem(this.player, veh, itemId);
    if (res.ok) {
      this.audio.playChime(false);
    }
    this.showToast(res.message);
    this.emitHud();
  }

  public startSideMission(kindOrId: SideMissionKind | string): void {
    const spec = AVAILABLE_SIDE_MISSIONS.find((m) => m.id === kindOrId || m.kind === kindOrId);
    if (!spec) return;

    this.activeSideMission = {
      ...spec,
      step: 1,
      timer: spec.maxTimer,
    };

    if (spec.id === 'side_bounty_mirpur' || spec.kind === 'bounty') {
      this.spawnNpc({
        role: 'syndicate',
        policy: 'flanker',
        name: 'Tariq "Chhuri"',
        x: -95,
        z: -175,
        shirtColor: 0x7c2d12,
        pantsColor: 0x1c1917,
        accentColor: 0xef4444,
        hp: 130,
        missionTag: 'side_bounty_target',
      });
    }

    this.audio.playChime(false);
    this.showToast(`Started Side Mission: ${spec.title}`);
    this.closeModal();
    this.emitHud();
  }

  public cancelSideMission(): void {
    if (!this.activeSideMission) return;
    this.activeSideMission = null;
    this.showToast('Side mission cancelled');
    this.emitHud();
  }

  public handleDialogueOption(optionId: string): void {
    if (!this.activeConversation) return;
    const option = this.activeConversation.options.find((o) => o.id === optionId);
    if (!option) return;

    if (option.action === 'job') {
      const targetParam = option.param;
      const mission = AVAILABLE_SIDE_MISSIONS.find(
        (m) => m.kind === targetParam || m.id === targetParam,
      );
      if (mission) {
        this.startSideMission(mission.id);
      } else {
        this.showToast('No contracts currently available.');
      }
      return;
    }

    if (option.action === 'heal') {
      const cost = option.param ? parseInt(option.param, 10) : 40;
      if (this.player.money < cost) {
        this.showToast(`Not enough Taka for fresh Cha (৳${cost})`);
        return;
      }
      this.player.money -= cost;
      this.player.hp = Math.min(this.player.maxHp, this.player.hp + 35);
      this.player.stamina = Math.min(this.player.maxStamina, this.player.stamina + 45);
      this.audio.playChime(false);
      this.showToast('Drank fresh Old Dhaka Cardamom Cha (+35 HP, +45 Stamina)!');
      this.emitHud();
      return;
    }

    if (option.action === 'trade') {
      if (option.param === 'shop') {
        this.modal = 'shop';
        this.clearInputs();
        this.emitHud();
        return;
      }
      if (option.param === 'safehouse') {
        this.modal = 'safehouse';
        this.clearInputs();
        this.emitHud();
        return;
      }
      const price = 200;
      if (this.player.money < price) {
        this.showToast(`Need ৳${price} for this item`);
        return;
      }
      this.player.money -= price;
      const existing = this.player.inventory.find((i) => i.id === 'item_jerrycan');
      if (existing) {
        existing.count++;
      } else {
        this.player.inventory.push({
          id: 'item_jerrycan',
          name: 'Emergency Octane Jerrycan (10L)',
          banglaName: 'অকটেন জেরিক্যান',
          kind: 'fuel',
          count: 1,
          value: 200,
          description: 'Contains 10L high-octane fuel (+35% tank). Use when stalled.',
        });
      }
      this.audio.playChime(false);
      this.showToast('Purchased Octane Jerrycan (+1 in inventory)!');
      this.emitHud();
      return;
    }

    if (option.action === 'rumor') {
      this.audio.playChime(false);
      if (this.activeConversation.npcId === 'npc_rafiq') {
        this.showToast('Rafiq Bhai: "Padma syndicate enforcers patrol Mirpur and Kawran Flyover at night."');
      } else {
        this.showToast('Nadia: "DMP patrol units are heavily concentrated near Thana and Motijheel."');
      }
      this.emitHud();
      return;
    }

    if (option.action === 'leave') {
      this.closeModal();
      return;
    }
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
      this.showToast('Hop into a CNG, Rickshaw, Sedan, or Boat [F] to pick up Dhaka fares!');
      this.emitHud();
      return;
    }

    if (isWatercraftKind(veh.kind)) {
      const candidateDocks = RIVER_DOCKS.filter((d) => Math.hypot(d.x - this.player.x, d.z - this.player.z) > 65);
      const pickDock = candidateDocks[Math.floor(Math.random() * candidateDocks.length)] ?? RIVER_DOCKS[4];
      const dist = Math.hypot(pickDock.x - this.player.x, pickDock.z - this.player.z);
      const riverPassengerNames = ['Faruk Majhi', 'Alamgir Master', 'Rehana Begum', 'Kamal Mia', 'Dr. Subir Das'];
      this.fare = {
        active: true,
        passengerName: riverPassengerNames[Math.floor(Math.random() * riverPassengerNames.length)],
        destX: pickDock.x,
        destZ: pickDock.z,
        destLabel: `${pickDock.name} (${pickDock.banglaName})`,
        timer: Math.max(45, Math.round(dist * 0.52 + 24)),
        reward: 750 + Math.round(dist * 5.2),
      };
      this.audio.playChime(false);
      this.showToast(`Buriganga Ferry Fare: Cross river to ${pickDock.banglaName}!`);
      this.emitHud();
      return;
    }

    // Pick a destination at least 60m away across the expanded city
    const candidates = FARE_DESTINATIONS.filter((d) => Math.hypot(d.x - this.player.x, d.z - this.player.z) > 60);
    const pick = candidates[Math.floor(Math.random() * candidates.length)] ?? FARE_DESTINATIONS[0];
    const dist = Math.hypot(pick.x - this.player.x, pick.z - this.player.z);
    const names = ['Karim Uncle', 'Farhana', 'Shafiq', 'Mina Apu', 'Imran', 'Tanvir', 'Nusrat'];
    this.fare = {
      active: true,
      passengerName: names[Math.floor(Math.random() * names.length)],
      destX: pick.x,
      destZ: pick.z,
      destLabel: pick.label,
      timer: Math.max(36, Math.round(dist * 0.42 + 18)),
      reward: 360 + Math.round(dist * 3.5),
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

    if (code === 'Escape') {
      if (this.phase === 'playing' || this.phase === 'paused') {
        e.preventDefault();
        this.togglePause();
        return;
      }
    }

    // GTA VI Smartphone [P]
    if (code === 'KeyP') {
      if (this.phase === 'playing' && this.modal === 'none') {
        e.preventDefault();
        this.toggleSmartphone();
        return;
      }
    }

    // GTA VI Radial Weapon Wheel [Tab] (holds bullet-time slowdown)
    if (code === 'Tab') {
      if (this.phase === 'playing' && this.modal === 'none') {
        e.preventDefault();
        this.toggleWeaponWheel(true);
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
    if (e.code === 'Tab' && this.weaponWheelOpen) {
      this.toggleWeaponWheel(false);
    }
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

    // Dynamic performance monitoring & graceful downscaling for sustained 60 FPS
    this.frameCount++;
    if (now - this.fpsWindowStartMs >= 2200) {
      const avgFps = (this.frameCount * 1000) / (now - this.fpsWindowStartMs);
      this.frameCount = 0;
      this.fpsWindowStartMs = now;

      if (!this.isSoftwareGl && avgFps < 38) {
        this.lowFpsCounter++;
        if (this.lowFpsCounter >= 2) {
          this.lowFpsCounter = 0;
          if (this.graphicsQuality === 'high') {
            this.setGraphicsQuality('medium');
            this.showToast('Adjusted to MEDIUM graphics for smooth 60 FPS');
          } else if (this.graphicsQuality === 'medium') {
            this.setGraphicsQuality('low');
            this.showToast('Adjusted to LOW graphics for smooth 60 FPS');
          }
        }
      } else {
        this.lowFpsCounter = 0;
      }
    }

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
      this.playerRig.animator.update(dt, { speed: 0, isGrounded: true, weapon: this.player.weapon });
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
    // Focus Reflex (Maya) slows world simulation time to 0.45x while camera remains fluid
    // GTA VI Radial Weapon Wheel slows world simulation time to 0.12x (bullet-time)
    const isFocusReflex = this.specialAbilityActive && this.activeProtagonistId === 'maya';
    const timeScale = this.weaponWheelOpen ? 0.12 : (isFocusReflex ? 0.45 : 1.0);
    let simDt = dt * timeScale;
    if (this.hitstopTimer > 0) {
      this.hitstopTimer = Math.max(0, this.hitstopTimer - dt);
      simDt = 0;
    }

    // Tick active special ability timer and cooldown
    if (this.specialAbilityActive) {
      this.specialAbilityTimer -= dt;
      if (this.specialAbilityTimer <= 0) {
        this.specialAbilityActive = false;
        this.specialAbilityTimer = 0;
        this.showToast('Special Ability Ended');
        this.emitHud();
      }
    }
    if (this.specialAbilityCooldown > 0) {
      this.specialAbilityCooldown = Math.max(0, this.specialAbilityCooldown - dt);
    }

    if (simDt > 0) {
      this.playTimeSec += simDt;
      // Gentle real-time Dhaka day/night cycle (1 full day ~ 8 minutes)
      this.timeOfDay = (this.timeOfDay + simDt * 0.05) % 24;
      this.applyEnvironmentLighting(simDt);

      // Infrequent 30-minute Open-Meteo refresh ONLY when explicitly enabled
      if (
        this.liveWeatherState.enabled &&
        this.liveWeatherState.status !== 'loading' &&
        Date.now() - this.liveWeatherLastCheckMs >= OPEN_METEO_CACHE_TTL_MS
      ) {
        void this.syncLiveWeather(false);
      }

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
      this.updateCompanion(simDt);
      this.updateRobbery(simDt);
      this.updatePoliceSearchRadius(simDt);

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

    // Authoritative multiplayer networking & remote rigs update
    if (this.netClient && this.netClient.isConnected()) {
      this.netClient.update(dt);
      this.multiplayerPing = this.netClient.getPing();
      const localState = this.netClient.getLocalPlayerState();
      if (localState) {
        this.player.hp = localState.hp;
        this.player.armor = localState.armor;
        this.player.money = localState.money;
      }

      const { players: remotePlayers } = this.netClient.getInterpolatedEntities();
      this.multiplayerPlayerCount = remotePlayers.length;
      const activeIds = new Set<string>();

      for (const p of remotePlayers) {
        if (p.id === this.netClient.getLocalPlayerId()) continue;
        activeIds.add(p.id);

        let rig = this.remotePlayerRigs.get(p.id);
        if (!rig) {
          rig = new RemotePlayerRig(
            p.id,
            `Player_${p.id.slice(0, 5)}`,
            'street',
            0xffffff,
            this.textures,
            this.characterTemplate,
            !this.isSoftwareGl,
          );
          this.scene.add(rig.group);
          this.remotePlayerRigs.set(p.id, rig);
        }

        const remoteSpeed = Math.hypot(p.vx, p.vz);
        rig.updateInterpolated(p.x, p.y, p.z, p.heading, remoteSpeed, p.weapon, p.hp, p.armor, p.state, dt);
      }

      for (const [id, rig] of this.remotePlayerRigs) {
        if (!activeIds.has(id)) {
          this.scene.remove(rig.group);
          rig.dispose();
          this.remotePlayerRigs.delete(id);
        }
      }

      for (let i = this.killfeed.length - 1; i >= 0; i--) {
        this.killfeed[i].timer -= dt;
        if (this.killfeed[i].timer <= 0) {
          this.killfeed.splice(i, 1);
        }
      }
    }

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
    // Enter / Exit nearest vehicle or watercraft [F] or [E]
    if (
      this.keysJustPressed.has('KeyF') ||
      (this.keysJustPressed.has('KeyE') && (this.player.vehicleId || this.getActiveOrNearestVehicle(5.6)))
    ) {
      this.tryToggleVehicle();
    }

    // Dock watercraft at river berth or Interact with Garage / Shop / Wardrobe [G]
    if (this.keysJustPressed.has('KeyG')) {
      if (this.player.vehicleId) {
        const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
        if (veh && isWatercraftKind(veh.kind)) {
          const dock = getNearestRiverDock(veh.x, veh.z, 16.0);
          if (dock) {
            dockWatercraftAtBerth(veh, dock);
            this.audio.playChime(true);
            this.showToast(`Berth Secured at ${dock.name} (${dock.banglaName})`);
            this.emitHud();
            return;
          }
        }
      }
      this.tryInteractPoi();
    }

    // GTA VI Dual Protagonist Switch [C]
    if (this.keysJustPressed.has('KeyC')) {
      this.switchProtagonist();
    }

    // GTA VI Smartphone [P]
    if (this.keysJustPressed.has('KeyP')) {
      this.toggleSmartphone();
    }

    // GTA VI Companion Tag-Team Rally [Y]
    if (this.keysJustPressed.has('KeyY')) {
      this.toggleCompanion();
    }

    // Toggle Camera View Mode [V] (First Person Cockpit / 3rd Close / 3rd Chase / Cinematic)
    if (this.keysJustPressed.has('KeyV')) {
      this.cycleCameraView();
    }

    // Toggle Protagonist Special Ability [X] or [Q] (Street Overdrive / Focus Reflex)
    if (this.keysJustPressed.has('KeyX') || this.keysJustPressed.has('KeyQ')) {
      this.toggleSpecialAbility();
    }

    // Quick Outfit Wardrobe [O]
    if (this.keysJustPressed.has('KeyO')) {
      this.openWardrobe();
    }

    // Toggle CNG/Taxi/Boat Fare Side Hustle [N]
    if (this.keysJustPressed.has('KeyN')) {
      this.toggleSideFare();
    }

    // Cycle Time of Day [T] & Weather [K]
    if (this.keysJustPressed.has('KeyT')) this.cycleTimePreset();
    if (this.keysJustPressed.has('KeyK')) this.cycleWeather();

    // Weapon hotkeys [1..4]
    if (this.keysJustPressed.has('Digit1')) this.selectWeapon('fists');
    if (this.keysJustPressed.has('Digit2')) this.selectWeapon('pistol');
    if (this.keysJustPressed.has('Digit3')) this.selectWeapon('smg');
    if (this.keysJustPressed.has('Digit4')) this.selectWeapon('shotgun');

    // Reload [R]
    if (this.keysJustPressed.has('KeyR') && !this.player.vehicleId) {
      this.startReload();
    }

    // Horn [H] in Vehicle or Robbery Step [H] on foot
    if (this.keysJustPressed.has('KeyH')) {
      if (this.player.vehicleId) {
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
      } else {
        this.triggerStoreRobberyStep();
      }
    }

    // Jump buffer [Space]
    if (this.keysJustPressed.has('Space') && !this.player.vehicleId) {
      this.player.jumpBufferTimer = 0.13;
    }
  }

  private tryToggleVehicle(): void {
    if (this.player.vehicleId) {
      if (this.netClient && this.netClient.isConnected()) {
        this.netClient.sendVehicleAction('exit', this.player.vehicleId);
      }
      // Exit vehicle cleanly on the driver's left side or onto dock pier
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      this.player.vehicleId = null;
      if (veh) {
        veh.driver = 'none';
        veh.sirenOn = false;
        if (isWatercraftKind(veh.kind)) {
          const dock = getNearestRiverDock(veh.x, veh.z, 18.0);
          if (dock) {
            this.player.x = dock.x;
            this.player.z = dock.z;
            this.player.y = 0;
            this.player.vy = 0;
            this.player.isSwimming = false;
            this.player.grounded = true;
          } else {
            const exitDist = VEHICLE_SPECS[veh.kind].halfWidth + 1.6;
            this.player.x = veh.x - Math.cos(veh.heading) * exitDist;
            this.player.z = veh.z + Math.sin(veh.heading) * exitDist;
            const wave = getRiverWaveSample(this.player.x, this.player.z, this.playTimeSec);
            this.player.y = wave.height + 0.12;
            this.player.vy = 0;
            this.player.isSwimming = true;
            this.player.grounded = true;
            this.audio.playWaterSplash(0.5);
          }
        } else {
          const exitX = veh.x - Math.cos(veh.heading) * (VEHICLE_SPECS[veh.kind].halfWidth + 1.15);
          const exitZ = veh.z + Math.sin(veh.heading) * (VEHICLE_SPECS[veh.kind].halfWidth + 1.15);
          const safe = resolveCircleCollisions(exitX, exitZ, 0.5, this.colliders);
          this.player.x = safe.x;
          this.player.z = safe.z;
          this.player.y = 0;
          this.player.heading = veh.heading;
          this.player.isSwimming = false;
        }
      }
      this.playerRig.group.visible = true;
      this.audio.playImpact(false);
      this.emitHud();
      return;
    }

    const nearest = this.getActiveOrNearestVehicle(5.6);
    if (!nearest) return;

    if (this.netClient && this.netClient.isConnected()) {
      this.netClient.sendVehicleAction('enter', nearest.id);
    }

    // Hijacking a moving traffic or police vehicle adds Wanted Heat & triggers carjacking reaction
    const wasOccupied = nearest.driver === 'traffic' || nearest.driver === 'police';
    if (wasOccupied) {
      nearest.stoleFromNpc = true;
      const isPolice = nearest.kind === 'police' || nearest.driver === 'police';
      const heat = isPolice ? 45 : 20;
      addWantedHeat(this.player, heat);
      this.player.carTheftsCompleted = (this.player.carTheftsCompleted || 0) + 1;

      // Pull out driver: spawn punch impact visual & sound
      this.spawnBurst(nearest.x, 1.2, nearest.z, 0xff5533, 12, 4.0);
      this.audio.playImpact(true);

      // Spawn fleeing civilian driver running away from car
      this.spawnFleeingDriver(nearest.x, nearest.z, nearest.heading);

      this.showToast(
        isPolice
          ? 'Carjacked DMP Patrol Unit! 2-Star Police Pursuit Triggered!'
          : `Carjacked ${VEHICLE_SPECS[nearest.kind].name}! (+${heat} Crime Heat)`,
      );

      // Check active side mission progress if targeting this car
      this.checkSideMissionTheftProgress(nearest);
    } else if (isWatercraftKind(nearest.kind)) {
      this.showToast(
        `Boarded ${VEHICLE_SPECS[nearest.kind].name} (${nearest.kind === 'launch' ? 'যাত্রীবাহী লঞ্চ' : nearest.kind === 'speedboat' ? 'স্পিডবোট' : 'জেট স্কি'})`,
      );
    }

    nearest.driver = 'player';
    nearest.sirenOn = nearest.kind === 'police';
    this.player.vehicleId = nearest.id;
    this.player.isSwimming = false;
    this.playerRig.group.visible = false;
    this.audio.playHorn(nearest.kind);
    this.emitHud();
  }

  private tryInteractPoi(): void {
    for (const poi of POIS) {
      if (Math.hypot(poi.x - this.player.x, poi.z - this.player.z) <= poi.radius + 2.5) {
        if (poi.kind === 'garage') {
          this.modal = 'garage';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.kind === 'shop') {
          this.modal = 'shop';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.kind === 'safehouse') {
          this.activeSafehouseId = poi.id as SafehouseId;
          this.modal = 'safehouse';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.kind === 'fuel_station') {
          this.modal = 'fuel_station';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.kind === 'dialogue_npc') {
          const npcId = poi.id.replace('_poi', '');
          this.activeConversation = INTERACTIVE_NPCS.find((n) => n.npcId === npcId) ?? null;
          this.modal = 'dialogue';
          this.clearInputs();
          this.emitHud();
          return;
        }
        if (poi.kind === 'tea_stall') {
          this.player.hp = Math.min(this.player.maxHp, this.player.hp + 25);
          this.audio.playChime(false);
          this.showToast(`Hot Masala Cha at ${poi.name} (+25 HP)`);
          this.emitHud();
          return;
        }
      }
    }
  }

  private spawnFleeingDriver(x: number, z: number, heading: number): void {
    const doorX = x - Math.cos(heading) * 1.6;
    const doorZ = z + Math.sin(heading) * 1.6;
    const civilian = this.spawnNpc({
      role: 'civilian',
      policy: 'chaser',
      name: 'Panicked Driver',
      x: doorX,
      z: doorZ,
      shirtColor: 0xe0e7ff,
      pantsColor: 0x334155,
      accentColor: 0xf43f5e,
      hp: 40,
    });
    civilian.state = 'flee';
    civilian.dangerSourceX = x;
    civilian.dangerSourceZ = z;
    civilian.dangerLevel = 1.0;
    civilian.stateTimer = 8.0;
  }

  private checkSideMissionTheftProgress(veh: VehicleState): void {
    if (!this.activeSideMission) return;
    if (this.activeSideMission.id === 'side_theft_sedan' && veh.kind === 'sedan') {
      this.activeSideMission.step = 2;
      this.activeSideMission.objective = 'Deliver Sedan to Babu Chop Shop in Old Dhaka (-36, 58)';
      this.activeSideMission.targetX = -36;
      this.activeSideMission.targetZ = 58;
      this.showToast('Sedan acquired! Deliver to Babu at Old Dhaka Chop Shop!');
    } else if (this.activeSideMission.id === 'side_theft_police' && veh.kind === 'police') {
      this.activeSideMission.step = 2;
      this.activeSideMission.objective = 'Evade DMP Pursuit until Wanted Stars expire!';
      this.showToast('DMP Cruiser hijacked! Evade the police chase!');
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
      const effectiveMax = isWatercraftKind(v.kind) ? (v.kind === 'launch' ? maxDist + 6.0 : maxDist + 2.0) : maxDist;
      const d = Math.hypot(v.x - this.player.x, v.z - this.player.z);
      if (d < effectiveMax && d < bestDist) {
        bestDist = d;
        best = v;
      }
    }
    return best;
  }

  // === ON-FOOT PLAYER CONTROLLER & COMBAT ===

  private updateOnFootPlayer(dt: number): void {
    const inWater = isPointInBurigangaWater(this.player.x, this.player.z);
    this.player.isSwimming = inWater;

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
      this.player.stamina = Math.max(0, this.player.stamina - dt * (inWater ? 32 : 26));
    } else {
      this.player.stamina = Math.min(this.player.maxStamina, this.player.stamina + dt * 18);
    }

    const targetSpeed = inWater ? (sprinting ? 2.8 : 1.6) : (sprinting ? 9.6 : 5.8);
    if (moveLen > 0.01) {
      moveX /= moveLen;
      moveZ /= moveLen;
      const accel = 1 - Math.exp((inWater ? -7 : -14) * dt);
      this.player.vx += (moveX * targetSpeed - this.player.vx) * accel;
      this.player.vz += (moveZ * targetSpeed - this.player.vz) * accel;
      const targetHeading = Math.atan2(moveX, moveZ);
      let diff = targetHeading - this.player.heading;
      while (diff > Math.PI) diff -= Math.PI * 2;
      while (diff < -Math.PI) diff += Math.PI * 2;
      this.player.heading += diff * (1 - Math.exp(-13 * dt));
    } else {
      const decel = 1 - Math.exp((inWater ? -6 : -12) * dt);
      this.player.vx *= 1 - decel;
      this.player.vz *= 1 - decel;
    }

    if (this.netClient && this.netClient.isConnected()) {
      this.netClient.sendMovementInput(
        dt,
        moveX,
        moveZ,
        this.player.heading,
        sprinting,
        this.player.jumpBufferTimer > 0,
      );
    }

    // Vertical physics & Swimming Buoyancy
    if (inWater) {
      const wave = getRiverWaveSample(this.player.x, this.player.z, this.playTimeSec);
      this.player.y = wave.height + 0.1;
      this.player.vy = 0;
      this.player.grounded = true;
      this.player.coyoteTimer = 0;
      this.player.jumpBufferTimer = 0;
      if (moveLen > 0.05 && Math.random() < 0.22) {
        this.spawnBurst(this.player.x, wave.height + 0.05, this.player.z, 0x67e8f9, 2, 1.2);
      }
    } else {
      // Coyote time & jump buffering on land
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
    }

    // Horizontal motion + AABB resolution (allows swimming in open Buriganga river)
    const res = resolveCircleCollisions(
      this.player.x + this.player.vx * dt,
      this.player.z + this.player.vz * dt,
      0.52,
      this.colliders,
      inWater,
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

    // Animate rigged GLB (or sculpted procedural fallback) player via smooth CharacterAnimator
    const horizSpeed = Math.hypot(this.player.vx, this.player.vz);
    this.playerSquashY += (1 - this.playerSquashY) * (1 - Math.exp(-12 * dt));
    if (this.playerHitFlash > 0) {
      this.playerHitFlash -= dt;
      this.playerRig.torsoMat.emissive.setHex(this.playerHitFlash > 0 ? 0xff4444 : 0x000000);
    }

    const invSq = 1 / Math.sqrt(this.playerSquashY);
    const baseS = this.playerRig.baseScale;
    this.playerRig.bodyRoot.scale.set(invSq * baseS, this.playerSquashY * baseS, invSq * baseS);
    this.playerRig.group.position.set(this.player.x, this.player.y, this.player.z);
    this.playerRig.group.rotation.y = this.player.heading;

    this.playerRig.animator.update(dt, {
      speed: horizSpeed,
      velocity: { x: this.player.vx, y: this.player.vy, z: this.player.vz },
      heading: this.player.heading,
      maxWalkSpeed: inWater ? 1.8 : 5.8,
      maxRunSpeed: inWater ? 3.0 : 9.6,
      isGrounded: this.player.grounded,
      isJumping: !this.player.grounded && this.player.vy > 0.5,
      isFalling: !this.player.grounded && this.player.vy < -0.5,
      verticalVelocity: this.player.vy,
      isSwimming: inWater,
      weapon: this.player.weapon,
      isAiming: this.player.weapon !== 'fists' && this.mouseHeld,
    });
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
    this.playerRig.animator.triggerAttack();
    this.audio.playWeapon(weaponId);

    if (this.netClient && this.netClient.isConnected()) {
      this.netClient.sendAttack(weaponId, this.player.heading);
    }

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
      this.alertNearbyPedestriansOfDanger(this.player.x, this.player.z, 40);
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
    this.npcRigs.get(npc.id)?.animator.triggerHit();
    this.spawnBurst(npc.x, 1.3, npc.z, 0xf97316, heavy ? 12 : 6, 4.5);
    this.audio.playImpact(heavy);
    this.alertNearbyPedestriansOfDanger(npc.x, npc.z, 28);

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

    if (this.netClient && this.netClient.isConnected()) {
      this.netClient.sendVehicleInput(
        throttle,
        steer,
        handbrake,
        this.keysHeld.has('KeyH'),
      );
    }

    const prevX = veh.x;
    const prevZ = veh.z;

    // Street Overdrive ability (Tariq): extra torque, enhanced drift bite and speed boost
    if (this.specialAbilityActive && this.activeProtagonistId === 'tariq') {
      if (Math.abs(veh.speed) > 1.0) {
        veh.speed *= 1.014;
      }
    }

    const res = stepVehicleKinematics(veh, { throttle, steer, handbrake }, dt, this.colliders);

    // Tire smoke on handbrake drifting
    if (handbrake && Math.abs(veh.speed) > 3.5 && !isWatercraftKind(veh.kind)) {
      const sternDist = VEHICLE_SPECS[veh.kind].halfLength * 0.85;
      const sternX = veh.x - Math.sin(veh.heading) * sternDist;
      const sternZ = veh.z - Math.cos(veh.heading) * sternDist;
      this.spawnBurst(sternX, 0.12, sternZ, 0xdfe6e9, 3, 2.2);
    }

    // Dynamic monsoon wet road spray particles
    if (this.roadWetness > 0.15 && Math.abs(veh.speed) > 3.0 && !isWatercraftKind(veh.kind)) {
      const sternDist = VEHICLE_SPECS[veh.kind].halfLength * 0.85;
      const sternX = veh.x - Math.sin(veh.heading) * sternDist;
      const sternZ = veh.z - Math.cos(veh.heading) * sternDist;
      this.spawnBurst(sternX, 0.15, sternZ, 0xcfd8dc, 2, 2.0);
    }

    this.player.distanceDriven += Math.hypot(veh.x - prevX, veh.z - prevZ);
    this.player.x = veh.x;
    this.player.z = veh.z;
    this.player.heading = veh.heading;

    if (isWatercraftKind(veh.kind)) {
      this.player.y = veh.y;
      veh.wakeTimer = (veh.wakeTimer ?? 0) + dt;
      if (veh.wakeTimer >= 0.055 && Math.abs(veh.speed) > 1.8) {
        veh.wakeTimer = 0;
        const sternDist = VEHICLE_SPECS[veh.kind].halfLength * 0.92;
        const sternX = veh.x - Math.sin(veh.heading) * sternDist;
        const sternZ = veh.z - Math.cos(veh.heading) * sternDist;
        this.spawnBurst(sternX, veh.y + 0.04, sternZ, 0xe0f2fe, 3, 1.4);
        if (veh.isPlaning || Math.abs(veh.speed) > 11) {
          this.spawnBurst(sternX, veh.y + 0.12, sternZ, 0xf0f9ff, 5, 3.2);
        }
      }
    } else {
      this.player.y = 0;
    }

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
          this.alertNearbyPedestriansOfDanger(veh.x, veh.z, 32);
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
    this.trafficLights.update(this.playTimeSec);

    const crossingPeds = this.npcs
      .filter((n) => n.state === 'crossing')
      .map((n) => ({ x: n.x, z: n.z, isCrossing: true }));

    for (let i = this.vehicles.length - 1; i >= 0; i--) {
      const v = this.vehicles[i];
      if (v.driver === 'player') continue;

      const distP = Math.hypot(v.x - this.player.x, v.z - this.player.z);

      // Despawn streamed traffic vehicles that wander too far from player (> 165m)
      if (distP > 165 && v.id.startsWith('t_stream_') && v.driver === 'traffic') {
        const rig = this.vehicleRigs.get(v.id);
        if (rig) {
          this.scene.remove(rig.group);
          this.vehicleRigs.delete(v.id);
        }
        this.vehicles.splice(i, 1);
        continue;
      }

      if (v.driver === 'traffic') {
        updateLaneAwareTrafficVehicle(
          v,
          dt,
          this.vehicles,
          this.player.x,
          this.player.z,
          this.player.vehicleId,
          this.playTimeSec,
          this.colliders,
          crossingPeds,
        );

        if (v.honkTimer && v.honkTimer > 4.3) {
          this.audio.playHorn(v.kind);
        }
      } else if (v.driver === 'police') {
        if (this.player.wantedLevel > 0) {
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
          v.sirenOn = true;
          stepVehicleKinematics(v, { throttle, steer, handbrake: dist < 8 }, dt, this.colliders);
        } else {
          v.sirenOn = false;
          v.driver = 'traffic';
          updateLaneAwareTrafficVehicle(
            v,
            dt,
            this.vehicles,
            this.player.x,
            this.player.z,
            this.player.vehicleId,
            this.playTimeSec,
            this.colliders,
            crossingPeds,
          );
        }
      } else {
        // Parked vehicle rolls to a stop smoothly
        if (Math.abs(v.speed) > 0.05) {
          stepVehicleKinematics(v, { throttle: 0, steer: 0, handbrake: true }, dt, this.colliders);
        }
      }

      this.syncVehicleRig(v, dt);
    }

    // Dynamic traffic vehicle streaming: maintain 20-28 active traffic vehicles around player
    const trafficCount = this.vehicles.filter((v) => v.driver === 'traffic').length;
    if (trafficCount < 22) {
      this.spawnStreamingVehicle();
    }
  }

  private syncVehicleRig(v: VehicleState, dt: number): void {
    const rig = this.vehicleRigs.get(v.id);
    if (!rig) return;
    rig.group.position.set(v.x, v.y, v.z);
    rig.group.rotation.y = v.heading;

    // Pitch and Roll dynamics for watercraft and banking terrestrial vehicles
    rig.group.rotation.x = v.pitch ?? 0;
    rig.group.rotation.z = v.roll ?? 0;

    // Spin propellers for watercraft
    if (rig.propeller) {
      const propSpeed = Math.max(2.8, Math.abs(v.speed) * 4.6);
      rig.propeller.rotation.z += propSpeed * dt;
    }

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

      // KO topple / rigged fall animation
      if (npc.state === 'ko') {
        npc.koTimer -= dt;
        rig.telegraphRing.visible = false;
        rig.animator.update(dt, { speed: 0, isKo: true });
        if (npc.koTimer <= 0) {
          rig.animator.disposeMixer();
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
        // Face player when nearby and breathe gently in Idle clip
        if (distP < 12) {
          npc.heading = Math.atan2(dxP, dzP);
        }
        rig.group.rotation.y = npc.heading;
        if (distP < this.qualityConfig.npcAnimationMaxDist) {
          rig.animator.update(dt, { speed: 0, isGrounded: true });
        }
        continue;
      }

      if (npc.role === 'civilian') {
        // Despawn streamed civilians that wander too far from player (> 105m)
        if (distP > 105 && npc.id.startsWith('npc_')) {
          rig.animator.disposeMixer();
          this.scene.remove(rig.group);
          this.npcRigs.delete(npc.id);
          this.npcs.splice(i, 1);
          continue;
        }

        updateIntelligentPedestrian(
          npc,
          dt,
          this.npcs,
          this.vehicles,
          this.player.x,
          this.player.z,
          this.colliders,
        );

        rig.group.position.set(npc.x, 0, npc.z);
        rig.group.rotation.y = npc.heading;
        if (distP < this.qualityConfig.npcAnimationMaxDist) {
          updatePedestrianAnimator(rig.animator, npc, dt);
        }
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
          if (npc.stateTimer <= 0) {
            rig.telegraphRing.visible = false;
            rig.animator.triggerAttack();
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
          const boundX = WORLD_HALF_SIZE - 25;
          const boundZMin = -(WORLD_HALF_SIZE - 25);
          const boundZMax = RIVER_Z_START - 12;
          npc.targetX = THREE.MathUtils.clamp(npc.x + (Math.random() - 0.5) * 32, -boundX, boundX);
          npc.targetZ = THREE.MathUtils.clamp(npc.z + (Math.random() - 0.5) * 32, boundZMin, boundZMax);
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
      if (distP < this.qualityConfig.npcAnimationMaxDist) {
        const spd = Math.hypot(npc.vx, npc.vz);
        rig.animator.update(dt, {
          speed: spd,
          maxWalkSpeed: 3.4,
          maxRunSpeed: 5.8,
          isGrounded: true,
          isWindup: npc.state === 'windup',
          isAiming: npc.policy === 'ranged' && isHostile,
          weapon: npc.policy === 'ranged' ? 'pistol' : npc.role === 'boss' ? 'shotgun' : 'fists',
        });
      }
    }

    // Dynamic pedestrian streaming: maintain 32-42 active pedestrians around player
    const civilianCount = this.npcs.filter((n) => n.role === 'civilian').length;
    if (civilianCount < 36) {
      this.spawnStreamingPedestrian();
    }
  }

  private executeEnemyAttack(npc: NpcState, distP: number, dxP: number, dzP: number): void {
    const isRanged = npc.policy === 'ranged' || npc.role === 'boss';
    const rig = this.npcRigs.get(npc.id);
    if (isRanged) {
      rig?.animator.triggerShoot();
      this.alertNearbyPedestriansOfDanger(npc.x, npc.z, 36);
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
    if (!this.player.vehicleId) {
      this.playerRig.animator.triggerHit();
    }
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
      const policeBoundX = WORLD_HALF_SIZE - 30;
      const sx = THREE.MathUtils.clamp(this.player.x + Math.cos(angle) * 32, -policeBoundX, policeBoundX);
      const sz = THREE.MathUtils.clamp(this.player.z + Math.sin(angle) * 32, -policeBoundX, RIVER_Z_START - 12);
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
      const beaconY = this.fare.destZ >= RIVER_Z_START ? BURIGANGA_WATER_LEVEL + 0.35 : 0;
      this.world.fareBeacon.position.set(this.fare.destX, beaconY, this.fare.destZ);
      this.world.fareBeacon.rotation.y += dt * 1.4;

      const inWatercraft = Boolean(
        this.player.vehicleId &&
        this.vehicles.some((v) => v.id === this.player.vehicleId && isWatercraftKind(v.kind)),
      );
      const arrivalRadius = inWatercraft ? 14.0 : 6.5;

      if (Math.hypot(this.player.x - this.fare.destX, this.player.z - this.fare.destZ) < arrivalRadius) {
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

    // Side mission progression
    if (this.activeSideMission) {
      const sm = this.activeSideMission;
      if (sm.timer !== undefined) {
        sm.timer -= dt;
        if (sm.timer <= 0) {
          this.activeSideMission = null;
          this.showToast('Side Mission Failed: Time Expired');
          this.emitHud();
        }
      }

      if (this.activeSideMission) {
        let smCompleted = false;

        if (sm.id === 'side_theft_sedan' && sm.step === 2) {
          const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
          if (veh && veh.kind === 'sedan' && Math.hypot(this.player.x - sm.targetX, this.player.z - sm.targetZ) < 14) {
            smCompleted = true;
          }
        } else if (sm.id === 'side_theft_police' && sm.step === 2) {
          if (this.player.wantedLevel === 0) {
            smCompleted = true;
          }
        } else if (sm.id === 'side_bounty_mirpur') {
          const bountyNpc = this.npcs.find((n) => n.missionTag === 'side_bounty_target');
          if (bountyNpc && bountyNpc.state === 'ko') {
            smCompleted = true;
          }
        } else if (sm.id === 'side_courier_buriganga') {
          const inWatercraft = Boolean(
            this.player.vehicleId &&
            this.vehicles.some((v) => v.id === this.player.vehicleId && isWatercraftKind(v.kind)),
          );
          if (inWatercraft && Math.hypot(this.player.x - sm.targetX, this.player.z - sm.targetZ) < 16) {
            smCompleted = true;
          }
        }

        if (smCompleted) {
          this.player.money += sm.rewardMoney;
          if (sm.kind === 'bounty') {
            this.player.bountiesCompleted = (this.player.bountiesCompleted || 0) + 1;
          } else if (sm.kind === 'car_theft') {
            this.player.carTheftsCompleted = (this.player.carTheftsCompleted || 0) + 1;
          } else if (sm.kind === 'courier') {
            this.player.faresCompleted = (this.player.faresCompleted || 0) + 1;
          }
          this.audio.playChime(true);
          this.showToast(`Side Mission Passed: ${sm.title} (+৳${sm.rewardMoney})!`);
          this.activeSideMission = null;
          this.saveGame();
          this.emitHud();
        }
      }
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
    this.saveGame();

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

    // Sub-pixel shadow camera snapping to eliminate directional shadow shimmer while moving
    const shadowFrustum = 168; // 2 * d (d = 84)
    const texelSize = shadowFrustum / Math.max(512, this.qualityConfig.shadowMapSize);
    const snapX = Math.round(this.player.x / texelSize) * texelSize;
    const snapZ = Math.round(this.player.z / texelSize) * texelSize;

    if (!isNight) {
      const sunAngle = dayPhase;
      const sx = Math.cos(sunAngle) * 78;
      const sy = Math.max(14, Math.sin(sunAngle) * 86);
      const sz = 32;
      this.sunLight.position.set(snapX + sx, sy, snapZ + sz);
      this.sunLight.target.position.set(snapX, 0, snapZ);
      _sunDirVec.set(sx, Math.max(4, Math.sin(sunAngle) * 86), sz).normalize();
    } else {
      // Cool moonlight from high above
      this.sunLight.position.set(snapX - 40, 68, snapZ - 25);
      this.sunLight.target.position.set(snapX, 0, snapZ);
      _sunDirVec.set(-40, 68, -25).normalize();
    }

    // Blend Sky Dome zenith & horizon palettes smoothly across Dawn, Day, Sunset, and Night
    if (h >= 5 && h < 7.8) {
      const t = (h - 5) / 2.8;
      _tmpZenith.lerpColors(_zenithNight, _zenithDawn, t);
      _tmpHorizon.lerpColors(_horizonNight, _horizonDawn, t);
      _tmpSunColor.lerpColors(_sunColorMoon, _sunColorSunset, t);
    } else if (h >= 7.8 && h < 10.5) {
      const t = (h - 7.8) / 2.7;
      _tmpZenith.lerpColors(_zenithDawn, _zenithDay, t);
      _tmpHorizon.lerpColors(_horizonDawn, _horizonDay, t);
      _tmpSunColor.lerpColors(_sunColorSunset, _sunColorDay, t);
    } else if (h >= 10.5 && h < 15.8) {
      _tmpZenith.copy(_zenithDay);
      _tmpHorizon.copy(_horizonDay);
      _tmpSunColor.copy(_sunColorDay);
    } else if (h >= 15.8 && h < 18.6) {
      const t = (h - 15.8) / 2.8;
      _tmpZenith.lerpColors(_zenithDay, _zenithSunset, t);
      _tmpHorizon.lerpColors(_horizonDay, _horizonSunset, t);
      _tmpSunColor.lerpColors(_sunColorDay, _sunColorSunset, t);
    } else if (h >= 18.6 && h < 20.8) {
      const t = (h - 18.6) / 2.2;
      _tmpZenith.lerpColors(_zenithSunset, _zenithNight, t);
      _tmpHorizon.lerpColors(_horizonSunset, _horizonNight, t);
      _tmpSunColor.lerpColors(_sunColorSunset, _sunColorMoon, t);
    } else {
      _tmpZenith.copy(_zenithNight);
      _tmpHorizon.copy(_horizonNight);
      _tmpSunColor.copy(_sunColorMoon);
    }

    // Dynamic monsoon wetness accumulation & drying
    const isRaining = this.weather === 'monsoon' || this.weather === 'storm';
    const targetWetness = this.weather === 'storm' ? 1.0 : this.weather === 'monsoon' ? 0.78 : 0.0;
    if (targetWetness > this.roadWetness) {
      this.roadWetness = Math.min(targetWetness, this.roadWetness + (dt > 0 ? dt / 7.0 : 0.0));
    } else {
      this.roadWetness = Math.max(0.0, this.roadWetness - (dt > 0 ? dt / 35.0 : 0.0));
    }
    const rainWetness = this.roadWetness;

    // Blend monsoon/storm cloud cover over zenith & horizon
    if (this.weather === 'monsoon') {
      _tmpZenith.lerp(_zenithStorm, 0.58);
      _tmpHorizon.lerp(_horizonStorm, 0.56);
    } else if (this.weather === 'storm') {
      _tmpZenith.lerp(_zenithStorm, 0.84);
      _tmpHorizon.lerp(_horizonStorm, 0.82);
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
        _tmpZenith.lerp(_lightningColor, this.lightningFlash * 0.55);
        _tmpHorizon.lerp(_lightningColor, this.lightningFlash * 0.7);
      }
    } else {
      this.lightningFlash = 0;
    }

    // Push unified atmospheric colors to Sky Dome shader, Scene background & Horizon Fog
    const skyU = this.world.skyUniforms;
    skyU.uZenithColor.value.copy(_tmpZenith);
    skyU.uHorizonColor.value.copy(_tmpHorizon);
    skyU.uSunColor.value.copy(_tmpSunColor);
    skyU.uSunDir.value.copy(_sunDirVec);
    skyU.uSunGlow.value = (isNight ? 0.28 : 1.0) * (1.0 - rainWetness * 0.72);

    (this.scene.background as THREE.Color).copy(_tmpHorizon);
    if (this.scene.fog instanceof THREE.FogExp2) {
      this.scene.fog.color.copy(_tmpHorizon);
      const baseFog = isNight ? 0.0038 : (h < 7.8 || h > 16.5) ? 0.0034 : 0.0031;
      const stormFog = this.weather === 'storm' ? 0.0072 : this.weather === 'monsoon' ? 0.0055 : baseFog;
      this.scene.fog.density = THREE.MathUtils.lerp(baseFog, stormFog, Math.max(rainWetness, isRaining ? 1.0 : 0.0));
    }

    const nightFactor = isNight ? 1 : THREE.MathUtils.clamp(1 - sunElev * 2.8, 0, 1);
    const stormDim = this.weather === 'storm' ? 0.58 : this.weather === 'monsoon' ? 0.76 : 1.0;

    // Dynamically scale PBR studio IBL environment intensity so Dhaka Night is truly dark & atmospheric!
    const baseEnv = THREE.MathUtils.lerp(0.78, 0.16, nightFactor);
    this.scene.environmentIntensity = (baseEnv + rainWetness * 0.08) * stormDim * this.qualityConfig.envIntensityMultiplier;

    this.sunLight.intensity = (isNight ? 0.36 : 2.15 * Math.max(0.42, sunElev)) * stormDim + this.lightningFlash * 2.8;
    this.sunLight.color.copy(_tmpSunColor);

    // Dynamic Hemisphere bounce colors (warm golden-terracotta sunset, deep indigo night, tropical daylight)
    this.hemiLight.color.copy(_tmpHorizon).lerp(_tmpZenith, 0.4);
    this.hemiLight.groundColor.setHex(
      isNight ? 0x111827 : sunElev < 0.32 ? 0x6e4a38 : rainWetness > 0 ? 0x2f3944 : 0x5d4f40,
    );
    this.hemiLight.intensity = THREE.MathUtils.lerp(0.84, 0.26, nightFactor) * stormDim;

    // Balanced filmic exposure across bright daylight and moody night streets
    this.renderer.toneMappingExposure = 1.06 - rainWetness * 0.04;

    // Streetlights, neon signs, building windows & vehicle headlights respond to dusk/night/monsoon
    const glow = 0.2 + nightFactor * 2.3 + rainWetness * 0.55;
    this.world.lampGlowMaterials.forEach((m) => (m.emissiveIntensity = glow));
    this.world.signMaterials.forEach((m) => (m.emissiveIntensity = 0.08 + nightFactor * 1.1 + rainWetness * 0.2));

    const winGlow = 0.04 + nightFactor * 1.48 + rainWetness * 0.22;
    this.world.nightWindowMaterials.forEach((m) => (m.emissiveIntensity = winGlow));

    // Soft additive sodium streetlamp halos & warm road light pools (zero extra PointLights)
    const lampAlpha = THREE.MathUtils.clamp(nightFactor * 0.95 + rainWetness * 0.28 - 0.14, 0, 0.95);
    this.world.lampHaloMat.opacity = lampAlpha * 0.9;
    this.world.lampPoolMat.opacity = lampAlpha * (0.42 + rainWetness * 0.26);

    // Wet Dhaka monsoon road sheen (matte clean tarmac when dry; glossy wet mirror sheen with puddle normal rims when wet!)
    const isWet = rainWetness > 0.04;
    const targetRoadRough = THREE.MathUtils.lerp(0.88, 0.18, rainWetness);
    const targetRoadMetal = THREE.MathUtils.lerp(0.04, 0.42, rainWetness);
    const targetEnvBoost = THREE.MathUtils.lerp(0.65, 2.4, rainWetness);
    _tmpRoadColor.lerpColors(_dryRoadColor, _wetRoadColor, rainWetness);
    for (const rMat of this.world.roadMaterials) {
      const desiredRoughMap = isWet ? (rMat.userData.wetRoughnessMap as THREE.Texture | null) ?? null : (rMat.userData.dryRoughnessMap as THREE.Texture | null) ?? null;
      const desiredNormalMap = isWet ? (rMat.userData.wetNormalMap as THREE.Texture | null) ?? null : (rMat.userData.dryNormalMap as THREE.Texture | null) ?? null;
      if (rMat.roughnessMap !== desiredRoughMap) {
        rMat.roughnessMap = desiredRoughMap;
        rMat.needsUpdate = true;
      }
      if (rMat.normalMap !== desiredNormalMap && desiredNormalMap !== null) {
        rMat.normalMap = desiredNormalMap;
        rMat.needsUpdate = true;
      }
      rMat.roughness = targetRoadRough;
      rMat.metalness = targetRoadMetal;
      rMat.envMapIntensity = targetEnvBoost;
      if (rMat.map) {
        rMat.color.copy(_tmpRoadColor);
      }
    }

    this.vehicleRigs.forEach((rig) => {
      rig.headlights.forEach((hl) => (hl.emissiveIntensity = 0.45 + nightFactor * 2.1 + rainWetness * 0.5));
    });
  }

  private updateWorldVisuals(dt: number): void {
    // Keep procedural Sky Dome centered on the active camera
    this.world.skyDome.position.copy(this.camera.position);

    // Dynamically stream world chunks, apply frustum culling, and update LODs
    if (this.world.chunkManager) {
      this.world.chunkManager.update(this.camera.position, this.camera);
    }

    // Gentle Buriganga boat bobbing & animated river/lake water ripple current
    const t = performance.now() * 0.001;
    for (const b of this.world.boats) {
      b.group.position.y = -0.92 + Math.sin(t * 1.7 + b.phase) * 0.08;
      b.group.rotation.z = Math.sin(t * 1.3 + b.phase) * 0.04;
    }
    this.world.riverMat.emissiveIntensity = 0.08 + Math.sin(t * 1.1) * 0.035;
    for (const wMat of this.world.waterMaterials) {
      if (wMat.bumpMap) {
        wMat.bumpMap.offset.x = (wMat.bumpMap.offset.x + dt * 0.032) % 1;
        wMat.bumpMap.offset.y = (wMat.bumpMap.offset.y + dt * 0.015) % 1;
      }
      if (wMat.normalMap) {
        wMat.normalMap.offset.x = (wMat.normalMap.offset.x + dt * 0.024) % 1;
        wMat.normalMap.offset.y = (wMat.normalMap.offset.y + dt * 0.018) % 1;
      }
    }

    // Tropical Dhaka vegetation wind sway (intensifying during monsoon and storms)
    const windFreq = this.weather === 'storm' ? 3.4 : this.weather === 'monsoon' ? 2.1 : 1.1;
    const windAmp = this.weather === 'storm' ? 0.08 : this.weather === 'monsoon' ? 0.045 : 0.02;
    if (this.world.palmFrondInst) {
      this.world.palmFrondInst.rotation.z = Math.sin(t * windFreq * 0.9) * windAmp * 1.6;
      this.world.palmFrondInst.rotation.x = Math.cos(t * windFreq * 0.7) * windAmp * 1.2;
    }
    if (this.world.kcCanopyInst) {
      this.world.kcCanopyInst.rotation.z = Math.sin(t * windFreq * 1.1 + 0.5) * windAmp * 0.9;
    }
    if (this.world.canopyInst) {
      this.world.canopyInst.rotation.z = Math.sin(t * windFreq + 1.0) * windAmp * 0.7;
    }

    // Directional rain streaks, droplet particles & ground puddle ripples around active camera
    const raining = this.weather === 'monsoon' || this.weather === 'storm';
    this.world.rainPoints.visible = raining;
    if (this.world.rainStreaks) {
      this.world.rainStreaks.visible = raining;
    }
    if (this.world.rainSplashInst) {
      this.world.rainSplashInst.visible = raining || this.roadWetness > 0.15;
    }

    if (raining) {
      const arr = this.world.rainPositions;
      const fallSpeed = this.weather === 'storm' ? 32 : 22;
      const windX = this.weather === 'storm' ? 5.8 : 2.0;
      const cx = this.camera.position.x;
      const cz = this.camera.position.z;
      for (let i = 0; i < arr.length; i += 3) {
        arr[i] += windX * dt;
        arr[i + 1] -= fallSpeed * dt;
        if (arr[i + 1] < 0) {
          arr[i] = cx + (Math.random() - 0.5) * 72;
          arr[i + 1] = 24 + Math.random() * 10;
          arr[i + 2] = cz + (Math.random() - 0.5) * 72;
        }
      }
      (this.world.rainPoints.geometry.attributes.position as THREE.BufferAttribute).needsUpdate = true;

      if (this.world.rainStreaks) {
        const posAttr = this.world.rainStreaks.geometry.attributes.position as THREE.BufferAttribute;
        const sArr = posAttr.array as Float32Array;
        const streakFall = this.weather === 'storm' ? 44 : 29;
        const streakWind = this.weather === 'storm' ? 8.2 : 2.8;
        for (let i = 0; i < sArr.length; i += 6) {
          sArr[i] += streakWind * dt;
          sArr[i + 1] -= streakFall * dt;
          sArr[i + 3] += streakWind * dt;
          sArr[i + 4] -= streakFall * dt;
          if (sArr[i + 4] < 0) {
            const rx = cx + (Math.random() - 0.5) * 76;
            const ry = 22 + Math.random() * 8;
            const rz = cz + (Math.random() - 0.5) * 76;
            const len = 0.85 + Math.random() * 0.65;
            sArr[i] = rx;
            sArr[i + 1] = ry;
            sArr[i + 2] = rz;
            sArr[i + 3] = rx + streakWind * 0.025;
            sArr[i + 4] = ry - len;
            sArr[i + 5] = rz;
          }
        }
        posAttr.needsUpdate = true;
      }
    }

    // Animate ground puddle ripples
    if (this.world.rainSplashInst && (raining || this.roadWetness > 0.15)) {
      this.splashAnimTimer += dt;
      const count = this.world.rainSplashInst.count;
      const px = this.player.x;
      const pz = this.player.z;
      for (let i = 0; i < count; i++) {
        const phase = (this.splashAnimTimer * 2.4 + i * 0.17) % 1.0;
        const s = THREE.MathUtils.lerp(0.2, 1.35, phase);
        const sx = px + Math.sin(i * 3.7) * (6 + (i % 24) * 0.8);
        const sz = pz + Math.cos(i * 5.1) * (6 + (i % 24) * 0.8);
        _vecA.set(sx, 0.056, sz);
        _scaleVec.set(s, 1, s);
        _mat4.compose(_vecA, _quat, _scaleVec);
        this.world.rainSplashInst.setMatrixAt(i, _mat4);
      }
      this.world.rainSplashInst.instanceMatrix.needsUpdate = true;
      const splashMat = this.world.rainSplashInst.material as THREE.MeshBasicMaterial;
      splashMat.opacity = Math.min(0.65, this.roadWetness * 0.75);
    }
  }

  private updateTitleCamera(): void {
    // Wide cinematic flyover showcasing Shapla Crossing, Dhaka Express Flyover, and expanded Motijheel/Gulshan skylines
    const r = 56;
    const cx = Math.sin(this.titleAngle) * r;
    const cz = Math.cos(this.titleAngle) * r * 0.84 + 14;
    this.camera.position.set(cx, 22.5 + Math.sin(this.titleAngle * 1.4) * 3.0, cz);
    this.camera.lookAt(4, 7.0, -10);
  }

  private updateGameplayCamera(dt: number): void {
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const isAiming = !veh && (this.mouseHeld && this.player.weapon !== 'fists');

    const camRes = this.cameraRig.step({
      dt,
      playerPos: { x: this.player.x, y: this.player.y, z: this.player.z },
      playerHeading: this.player.heading,
      playerVelocity: { x: this.player.vx, y: this.player.vy, z: this.player.vz },
      camYaw: this.camYaw,
      camPitch: this.camPitch,
      viewMode: this.cameraViewMode,
      inVehicle: Boolean(veh),
      vehicleSpeed: veh ? veh.speed : 0,
      vehicleSteerAngle: veh ? veh.steerAngle : 0,
      vehicleKind: veh ? veh.kind : null,
      isAiming,
      trauma: this.trauma,
      occluders: this.world.cameraOccluders,
      characterSwitch: this.characterSwitchState,
      focusReflexActive: this.specialAbilityActive && this.activeProtagonistId === 'maya',
    });

    this.camera.position.copy(camRes.camPos);
    this.camera.lookAt(camRes.camTarget);
    if (Math.abs(this.camera.fov - camRes.fov) > 0.05) {
      this.camera.fov = camRes.fov;
      this.camera.updateProjectionMatrix();
    }
    this.camera.rotation.z = camRes.rollAngle;
  }

  public getActiveProtagonist(): ProtagonistId {
    return this.activeProtagonistId;
  }

  public getCameraViewMode(): CameraViewMode {
    return this.cameraViewMode;
  }

  public cycleCameraView(): void {
    const modes: CameraViewMode[] = ['third_person_close', 'third_person_far', 'first_person', 'cinematic'];
    const nextIdx = (modes.indexOf(this.cameraViewMode) + 1) % modes.length;
    this.cameraViewMode = modes[nextIdx];
    this.audio.playClick();
    const labels: Record<CameraViewMode, string> = {
      third_person_close: 'Camera: Action 3rd-Person Close',
      third_person_far: 'Camera: Wide 3rd-Person Chase',
      first_person: 'Camera: 1st-Person Cockpit View',
      cinematic: 'Camera: Cinematic Bumper Angle',
    };
    this.showToast(labels[this.cameraViewMode]);
    this.emitHud();
  }

  public toggleSpecialAbility(): void {
    const profile = PROTAGONIST_PROFILES[this.activeProtagonistId];
    if (this.specialAbilityActive) return;
    if (this.specialAbilityCooldown > 0) {
      this.showToast(`${profile.abilityName} Cooldown: ${Math.ceil(this.specialAbilityCooldown)}s`);
      return;
    }
    this.specialAbilityActive = true;
    this.specialAbilityTimer = profile.abilityDuration;
    this.specialAbilityCooldown = profile.abilityCooldown;
    this.audio.playChime(true);
    this.trauma = Math.min(1, this.trauma + 0.35);
    this.showToast(`ACTIVATED: ${profile.abilityName} (${profile.abilityBangla})!`);
    this.emitHud();
  }

  public switchProtagonist(): void {
    if (this.characterSwitchState.active) return;
    if (this.player.hp <= 0) return;

    const fromId = this.activeProtagonistId;
    const toId: ProtagonistId = fromId === 'tariq' ? 'maya' : 'tariq';
    const targetProfile = PROTAGONIST_PROFILES[toId];

    // Pack current player state
    const currentPack: ProtagonistInstanceState = {
      id: fromId,
      x: this.player.x,
      y: this.player.y,
      z: this.player.z,
      heading: this.player.heading,
      hp: this.player.hp,
      armor: this.player.armor,
      stamina: this.player.stamina,
      money: this.player.money,
      weapon: this.player.weapon,
      unlockedWeapons: [...this.player.unlockedWeapons],
      ammo: { ...this.player.ammo },
      magAmmo: { ...this.player.magAmmo },
      outfit: this.player.outfit,
      abilityTimer: this.specialAbilityTimer,
      abilityCooldownTimer: this.specialAbilityCooldown,
      abilityActive: this.specialAbilityActive,
    };

    // Unpack target protagonist
    const targetState = this.secondaryProtagonistState;
    this.secondaryProtagonistState = currentPack;
    this.activeProtagonistId = toId;

    // Reposition secondary rig to old spot
    if (this.secondaryProtagonistRig) {
      this.secondaryProtagonistRig.group.position.set(currentPack.x, currentPack.y, currentPack.z);
      this.secondaryProtagonistRig.group.rotation.y = currentPack.heading;
      setHumanoidOutfit(this.secondaryProtagonistRig, currentPack.outfit);
      setHumanoidWeaponMesh(this.secondaryProtagonistRig, currentPack.weapon);
    }

    // Apply target attributes to player
    this.player.x = targetState.x;
    this.player.y = targetState.y;
    this.player.z = targetState.z;
    this.player.vx = 0;
    this.player.vy = 0;
    this.player.vz = 0;
    this.player.heading = targetState.heading;
    this.player.hp = THREE.MathUtils.clamp(targetState.hp, 20, targetProfile.attributes.maxHp);
    this.player.maxHp = targetProfile.attributes.maxHp;
    this.player.armor = targetState.armor;
    this.player.maxArmor = targetProfile.attributes.maxArmor;
    this.player.stamina = targetProfile.attributes.maxStamina;
    this.player.maxStamina = targetProfile.attributes.maxStamina;
    this.player.money = targetState.money;
    this.player.weapon = targetState.weapon;
    this.player.unlockedWeapons = targetState.unlockedWeapons;
    this.player.ammo = targetState.ammo;
    this.player.magAmmo = targetState.magAmmo;
    this.player.outfit = targetState.outfit;
    this.player.vehicleId = null;
    this.playerRig.group.visible = true;

    this.specialAbilityActive = false;
    this.specialAbilityTimer = 0;
    this.specialAbilityCooldown = targetState.abilityCooldownTimer;

    setHumanoidOutfit(this.playerRig, this.player.outfit);
    setHumanoidWeaponMesh(this.playerRig, this.player.weapon);

    // 3-phase Sky-Cam transition
    this.characterSwitchState.active = true;
    this.characterSwitchState.phase = 'ascend';
    this.characterSwitchState.timer = 0;
    this.characterSwitchState.duration = 2.4;
    this.characterSwitchState.fromId = fromId;
    this.characterSwitchState.toId = toId;
    this.characterSwitchState.fromPos = { x: currentPack.x, y: currentPack.y, z: currentPack.z };
    this.characterSwitchState.toPos = { x: targetState.x, y: targetState.y, z: targetState.z };
    this.characterSwitchState.skyAltitude = 58;
    this.characterSwitchState.bannerText = `${targetProfile.name.toUpperCase()} — ${targetProfile.district.toUpperCase()}`;
    this.characterSwitchState.bannerBangla = `${targetProfile.banglaName} — ${targetProfile.banglaDistrict}`;

    this.audio.playChime(true);
    this.showToast(`Switched to ${targetProfile.name} (${targetProfile.banglaName})`);
    this.emitHud();
  }

  // === GTA VI CORE MECHANICS (WEAPON WHEEL, SMARTPHONE, COMPANION CO-OP & STORE ROBBERIES) ===

  public toggleWeaponWheel(open?: boolean): void {
    this.weaponWheelOpen = open !== undefined ? open : !this.weaponWheelOpen;
    this.emitHud();
  }

  public toggleSmartphone(open?: boolean): void {
    this.smartphoneOpen = open !== undefined ? open : !this.smartphoneOpen;
    this.emitHud();
  }

  public captureSnapshot(): void {
    this.audio.playClick();
    this.showToast('📸 Photo Captured & Saved to DhakaOS Gallery!');
  }

  public handleDepositMoney(amt: number): void {
    if (this.player.money >= amt) {
      this.player.money -= amt;
      this.showToast(`৳${amt.toLocaleString()} deposited into bKash Vault!`);
      this.emitHud();
    }
  }

  public toggleCompanion(): void {
    if (this.activeCompanion) {
      this.activeCompanion = null;
      if (this.secondaryProtagonistRig) {
        this.secondaryProtagonistRig.group.visible = false;
      }
      this.showToast('Partner backup dismissed');
      this.emitHud();
      return;
    }

    const partnerId: ProtagonistId = this.activeProtagonistId === 'tariq' ? 'maya' : 'tariq';
    const partnerProf = PROTAGONIST_PROFILES[partnerId];

    this.activeCompanion = {
      id: partnerId,
      name: partnerProf.name,
      banglaName: partnerProf.banglaName,
      hp: partnerProf.attributes.maxHp,
      maxHp: partnerProf.attributes.maxHp,
      state: this.player.vehicleId ? 'passenger' : 'following',
      weapon: partnerProf.preferredWeapon,
    };

    if (this.secondaryProtagonistRig) {
      this.secondaryProtagonistRig.group.visible = true;
      this.secondaryProtagonistRig.group.position.set(
        this.player.x + 2,
        this.player.y,
        this.player.z + 2
      );
      setHumanoidWeaponMesh(this.secondaryProtagonistRig, partnerProf.preferredWeapon);
      setHumanoidOutfit(this.secondaryProtagonistRig, partnerProf.defaultOutfit);
    }

    this.showToast(`🔥 ${partnerProf.name} (${partnerProf.banglaName}) joined as Tag-Team Partner!`);
    this.triggerDialogue(
      partnerProf.name,
      partnerId === 'maya' ? 'তারেক, আমি কভার দিচ্ছি! চলো বের হই!' : 'মায়া, আমি সাথে আছি! ড্রিফট শুরু করো!',
      partnerId === 'maya' ? "Tariq, I've got your back! Let's roll!" : "Maya, I'm riding shotgun! Let's hit the gas!",
      5.5
    );
    this.emitHud();
  }

  public handleCallContact(contact: PhoneContact): void {
    if (this.player.money < contact.cost) {
      this.showToast(`Need ৳${contact.cost} to call ${contact.name}`);
      return;
    }
    this.player.money -= contact.cost;

    if (contact.serviceType === 'vehicle_drop') {
      const dropX = this.player.x + Math.sin(this.player.heading) * 6;
      const dropZ = this.player.z + Math.cos(this.player.heading) * 6;
      const vehSpawn: VehicleState = {
        id: `drop_sedan_${Date.now()}`,
        kind: 'sedan',
        x: dropX,
        y: 0,
        z: dropZ,
        heading: this.player.heading,
        speed: 0,
        steerAngle: 0,
        hp: 200,
        maxHp: 200,
        color: 0x0f172a,
        driver: 'none',
        routeIdx: 0,
        waypointIdx: 0,
        honkTimer: 0,
        sirenOn: false,
        damagedSmoke: false,
        fuel: 100,
        maxFuel: 100,
        fuelType: 'petrol',
        customization: {
          engineStage: 3,
          brakesStage: 2,
          hasTurbo: true,
          hasSpoiler: true,
          tintLevel: 2,
          plateJammer: true,
        },
      };
      this.vehicles.push(vehSpawn);
      this.spawnBurst(dropX, 0.5, dropZ, 0x38bdf8, 8, 2.5);
      this.showToast('🚗 Shofi Bhai delivered a tuned Gulshan GT Sedan!');
    } else if (contact.serviceType === 'clear_heat') {
      this.player.wantedLevel = 0;
      this.player.wantedHeat = 0;
      this.player.wantedCooldown = 0;
      this.policeSearchRadius = null;
      this.showToast('👮 Inspector Rehan wiped your DMP wanted record!');
    } else if (contact.serviceType === 'weapons') {
      if (!this.player.unlockedWeapons.includes('shotgun')) {
        this.player.unlockedWeapons.push('shotgun');
      }
      this.player.weapon = 'shotgun';
      this.player.ammo.shotgun = (this.player.ammo.shotgun ?? 0) + 36;
      this.player.armor = this.player.maxArmor;
      this.showToast('📦 Master Kabir delivered 12G Shotgun & Heavy Armor!');
    } else if (contact.serviceType === 'companion') {
      this.toggleCompanion();
    }
    this.emitHud();
  }

  private triggerStoreRobberyStep(): void {
    if (this.player.vehicleId || this.player.weapon === 'fists') return;
    if (this.activeRobbery) {
      this.activeRobbery.intimidation = Math.min(100, this.activeRobbery.intimidation + 30);
      this.audio.playWeapon('pistol');
      this.showToast('Intimidation boosted! Cashier bagging faster!');
      return;
    }
    const nearShop = POIS.find((p) => p.kind === 'shop' && Math.hypot(p.x - this.player.x, p.z - this.player.z) <= p.radius + 3.0);
    if (!nearShop) return;

    this.activeRobbery = {
      storeId: nearShop.id,
      storeName: nearShop.name,
      storeBangla: nearShop.banglaName,
      district: getDistrictAt(nearShop.x, nearShop.z),
      x: nearShop.x,
      z: nearShop.z,
      intimidation: 55,
      cashBagged: 0,
      targetCash: 6000 + Math.floor(Math.random() * 5000),
      state: 'intimidation',
      timer: 0,
    };
    this.audio.playImpact(true);
    this.showToast(`🚨 Robbery Started at ${nearShop.name}! Keep weapon drawn!`);
    this.triggerDialogue('Shopkeeper', 'ভাই সব নিয়া যান, গুলি কইরেন না!', 'Take everything, please don’t shoot!');
    this.emitHud();
  }

  private updateRobbery(dt: number): void {
    if (!this.activeRobbery) return;
    const rob = this.activeRobbery;
    const dist = Math.hypot(this.player.x - rob.x, this.player.z - rob.z);
    if (dist > 15.0) {
      this.showToast('Robbery Abandoned: Moved too far from the cash counter.');
      this.activeRobbery = null;
      this.emitHud();
      return;
    }

    rob.timer += dt;
    // Decaying intimidation if player holsters weapon
    if (this.player.weapon === 'fists') {
      rob.intimidation = Math.max(0, rob.intimidation - dt * 25);
    } else {
      rob.intimidation = Math.min(100, rob.intimidation + dt * 8);
    }

    if (rob.intimidation < 10) {
      this.showToast('Clerk panicked and hit silent alarm! Robbery failed!');
      this.player.wantedLevel = Math.max(this.player.wantedLevel, 2);
      this.activeRobbery = null;
      this.emitHud();
      return;
    }

    // Cashier bags cash
    const baggingRate = (rob.intimidation / 100) * 1200;
    rob.cashBagged = Math.min(rob.targetCash, rob.cashBagged + dt * baggingRate);

    if (rob.cashBagged >= rob.targetCash) {
      const reward = Math.round(rob.targetCash);
      this.player.money += reward;
      this.player.wantedLevel = Math.max(this.player.wantedLevel, 2);
      this.audio.playChime(true);
      this.showToast(`💰 Robbery Successful! Stashed ৳${reward.toLocaleString()}! Police dispatched!`);
      this.policeSearchRadius = {
        x: rob.x,
        z: rob.z,
        radius: 45.0,
        active: true,
        searchTimer: 25.0,
      };
      this.activeRobbery = null;
      this.emitHud();
    }
  }

  private updateCompanion(dt: number): void {
    if (!this.activeCompanion || !this.secondaryProtagonistRig) return;
    const comp = this.activeCompanion;
    const rig = this.secondaryProtagonistRig;

    this.companionBanterTimer -= dt;
    if (this.companionBanterTimer <= 0) {
      this.companionBanterTimer = 22.0 + Math.random() * 10.0;
      this.triggerCompanionBanter();
    }

    // If player is in a vehicle
    if (this.player.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      if (veh) {
        comp.state = 'passenger';
        const rightX = Math.cos(veh.heading);
        const rightZ = -Math.sin(veh.heading);
        const fwdX = -Math.sin(veh.heading);
        const fwdZ = -Math.cos(veh.heading);

        const pX = veh.x + rightX * 0.52 - fwdX * 0.15;
        const pZ = veh.z + rightZ * 0.52 - fwdZ * 0.15;
        const pY = veh.y + 0.15;

        rig.group.position.set(pX, pY, pZ);
        rig.group.rotation.y = veh.heading;
        rig.group.visible = true;

        // Drive-by auto cover fire against pursuers
        this.companionAttackTimer -= dt;
        if (this.companionAttackTimer <= 0) {
          const target = this.npcs.find(
            (n) => (n.role === 'police' || n.role === 'syndicate') && n.hp > 0 && Math.hypot(n.x - veh.x, n.z - veh.z) < 28
          );
          if (target) {
            this.companionAttackTimer = 0.85;
            target.hp -= 20;
            this.spawnBurst(target.x, 1.2, target.z, 0xff3333, 3, 1.5);
            this.audio.playWeapon('smg');
            if (target.hp <= 0) {
              target.state = 'ko';
              this.showToast(`${comp.name} took out a hostile!`);
            }
          }
        }
      }
    } else {
      // On foot: companion follows player smoothly
      comp.state = 'following';
      rig.group.visible = true;

      const targetFollowX = this.player.x - Math.sin(this.player.heading) * 1.8 + Math.cos(this.player.heading) * 1.2;
      const targetFollowZ = this.player.z - Math.cos(this.player.heading) * 1.8 - Math.sin(this.player.heading) * 1.2;

      const dx = targetFollowX - rig.group.position.x;
      const dz = targetFollowZ - rig.group.position.z;
      const dist = Math.hypot(dx, dz);

      if (dist > 0.4) {
        const moveSpd = Math.min(8.5, dist * 3.5);
        rig.group.position.x += (dx / dist) * moveSpd * dt;
        rig.group.position.z += (dz / dist) * moveSpd * dt;
        rig.group.position.y = this.player.y;
        rig.group.rotation.y = Math.atan2(-dx, -dz);
        rig.animator.update(dt, { speed: moveSpd, isGrounded: true, weapon: comp.weapon });
      } else {
        rig.animator.update(dt, { speed: 0, isGrounded: true, weapon: comp.weapon });
      }

      // Foot combat cover fire
      this.companionAttackTimer -= dt;
      if (this.companionAttackTimer <= 0) {
        const target = this.npcs.find(
          (n) => (n.role === 'police' || n.role === 'syndicate') && n.hp > 0 && Math.hypot(n.x - this.player.x, n.z - this.player.z) < 20
        );
        if (target) {
          this.companionAttackTimer = 0.9;
          target.hp -= 18;
          this.spawnBurst(target.x, 1.2, target.z, 0xff3333, 3, 1.5);
          this.audio.playWeapon('pistol');
          if (target.hp <= 0) {
            target.state = 'ko';
            this.showToast(`${comp.name} eliminated an attacker!`);
          }
        }
      }
    }
  }

  private triggerCompanionBanter(): void {
    if (!this.activeCompanion) return;
    const isTariqActive = this.activeProtagonistId === 'tariq';
    const lines = isTariqActive
      ? [
          { speaker: 'Maya', bangla: 'তারেক, পেছনে খেয়াল রাখো! পুলিশ কিন্তু পিছু ছাড়বে না!', english: 'Tariq, watch your six! Police won’t let up easily!' },
          { speaker: 'Tariq', bangla: 'চকবাজারের গলিতে আমাদের ধরতে পারবে না, ভরসা রাখো!', english: 'They can’t catch us in these Old Dhaka alleys, trust me!' },
          { speaker: 'Maya', bangla: 'আমার গান রেডি আছে। তুমি শুধু এক্সিলারেটর চেপে রাখো!', english: 'My weapon is locked and loaded. Just keep your foot on the gas!' },
          { speaker: 'Tariq', bangla: 'ধোলাইখালের আসল ড্রিফট এখনও দেখোই নাই!', english: 'You haven’t even seen real Dholaikhal drifting yet!' },
        ]
      : [
          { speaker: 'Tariq', bangla: 'মায়া, ফুল স্পিডে চলো! সিন্ডিকেটের খবর আমরা আজই বের করবো!', english: 'Maya, floor it! We’re busting this syndicate network today!' },
          { speaker: 'Maya', bangla: 'তাদের সার্ভার ড্রাইভ আমার কাছে। আমাদের শুধু সদরে পৌঁছাতে হবে।', english: 'I have their server drive. We just need to make it to Sadarghat.' },
          { speaker: 'Tariq', bangla: 'কোন চিন্তা নাই, আমি সামনে ক্লিয়ার করছি!', english: 'No worries, clearing the road ahead!' },
        ];
    const pick = lines[Math.floor(Math.random() * lines.length)];
    this.triggerDialogue(pick.speaker, pick.bangla, pick.english, 5.5);
  }

  private updatePoliceSearchRadius(dt: number): void {
    if (!this.policeSearchRadius || !this.policeSearchRadius.active) return;
    this.policeSearchRadius.searchTimer -= dt;
    if (this.policeSearchRadius.searchTimer <= 0) {
      this.policeSearchRadius.active = false;
      this.policeSearchRadius = null;
      if (this.player.wantedLevel > 0) {
        this.player.wantedLevel = Math.max(0, this.player.wantedLevel - 1);
        this.showToast('Police lost your trail! Wanted heat cooling down.');
      }
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
    if (this.activeRobbery) {
      return {
        prompt: `Robbing ${this.activeRobbery.storeName} — Keep Weapon Drawn [H] (৳${Math.round(this.activeRobbery.cashBagged).toLocaleString()} / ৳${this.activeRobbery.targetCash.toLocaleString()})`,
        key: 'H',
      };
    }
    for (const poi of POIS) {
      if (Math.hypot(poi.x - this.player.x, poi.z - this.player.z) <= poi.radius + 2.0) {
        if (poi.kind === 'garage') return { prompt: `Open ${poi.name}`, key: 'G' };
        if (poi.kind === 'shop') {
          if (this.player.weapon !== 'fists' && !this.player.vehicleId) {
            return { prompt: `Hold [H] to Rob ${poi.name}`, key: 'H' };
          }
          return { prompt: `Enter ${poi.name}`, key: 'G' };
        }
        if (poi.kind === 'safehouse') {
          const safeId = (poi.safehouseId ?? poi.id) as SafehouseId;
          const spec = SAFEHOUSE_SPECS.find((s) => s.id === safeId);
          const isOwned = this.player.unlockedSafehouses.includes(safeId);
          return { prompt: `${isOwned ? 'Enter Safehouse' : 'Inspect Safehouse'}: ${spec?.name ?? poi.name}`, key: 'G' };
        }
        if (poi.kind === 'fuel_station') return { prompt: `Refuel at ${poi.name}`, key: 'G' };
        if (poi.kind === 'dialogue_npc') return { prompt: poi.name, key: 'G' };
        if (poi.kind === 'tea_stall') return { prompt: `Drink Masala Cha at ${poi.name} (+25 HP)`, key: 'G' };
      }
    }
    if (this.player.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === this.player.vehicleId);
      if (veh && isWatercraftKind(veh.kind)) {
        const dock = getNearestRiverDock(veh.x, veh.z, 16.0);
        if (dock) {
          return { prompt: `Secure Berth at ${dock.name} (${dock.banglaName})`, key: 'G' };
        }
      }
      const nearFuel = POIS.find((p) => p.kind === 'fuel_station' && Math.hypot(p.x - this.player.x, p.z - this.player.z) <= p.radius + 6.0);
      if (nearFuel) {
        return { prompt: `Pull into ${nearFuel.name} to Refuel`, key: 'G' };
      }
      return { prompt: isWatercraftKind(veh?.kind ?? 'sedan') ? 'Disembark [F]' : 'Exit Vehicle [F]', key: 'F' };
    } else {
      const nearVeh = this.getActiveOrNearestVehicle(5.6);
      if (nearVeh) {
        const action = isWatercraftKind(nearVeh.kind) ? 'Board' : nearVeh.driver === 'traffic' ? 'Carjack' : 'Drive';
        return { prompt: `${action} ${VEHICLE_SPECS[nearVeh.kind].name}`, key: 'F' };
      }
    }
    return { prompt: null, key: null };
  }

  private emitHud(): void {
    const distId = getDistrictAt(this.player.x, this.player.z);
    const distMeta = DISTRICT_META[distId];
    const streetMeta = getNearestStreetCorridor(this.player.x, this.player.z, ROAD_SEGMENTS);
    const veh = this.player.vehicleId ? this.vehicles.find((v) => v.id === this.player.vehicleId) : null;
    const mission = STORY_MISSIONS[this.missionIdx];
    const step = mission?.steps[this.stepIdx];
    const { prompt, key } = this.computeInteractPrompt();

    const fuel = Math.round(veh ? (veh.fuel ?? 100) : 100);
    const maxFuel = Math.round(veh ? (veh.maxFuel ?? 100) : 100);
    const fuelRatio = veh ? (veh.fuel ?? 100) / (veh.maxFuel ?? 100) : 1.0;
    const isLowFuel = Boolean(veh && (veh.fuel ?? 100) < 20);

    const blips: MinimapBlip[] = [];
    POIS.forEach((p) => {
      if (p.kind === 'garage') blips.push({ x: p.x, z: p.z, kind: 'garage' });
      if (p.kind === 'shop') blips.push({ x: p.x, z: p.z, kind: 'shop' });
      if (p.kind === 'safehouse') blips.push({ x: p.x, z: p.z, kind: 'safehouse' });
      if (p.kind === 'fuel_station') blips.push({ x: p.x, z: p.z, kind: 'fuel' });
    });
    if (step) {
      blips.push({ x: step.targetX, z: step.targetZ, kind: 'mission' });
    }
    if (this.fare.active) {
      blips.push({ x: this.fare.destX, z: this.fare.destZ, kind: 'fare' });
    }
    if (this.activeSideMission) {
      blips.push({ x: this.activeSideMission.targetX, z: this.activeSideMission.targetZ, kind: 'mission' });
    }
    for (const n of this.npcs) {
      if (n.state === 'ko') continue;
      if (n.role === 'police' && this.player.wantedLevel > 0) {
        blips.push({ x: n.x, z: n.z, kind: 'police' });
      } else if (n.role === 'syndicate' || n.role === 'boss' || n.missionTag === 'side_bounty_target') {
        blips.push({ x: n.x, z: n.z, kind: 'enemy' });
      }
    }

    const w = this.player.weapon;
    const maxWantedCd = Math.max(1, 14 + this.player.wantedLevel * 4);
    const currentProf = PROTAGONIST_PROFILES[this.activeProtagonistId];

    this.onHudUpdate({
      phase: this.phase,
      modal: this.modal,
      districtName: distMeta.name,
      districtBangla: distMeta.bangla,
      streetName: streetMeta?.name ?? null,
      streetBangla: streetMeta?.banglaName ?? null,
      timeOfDay: this.timeOfDay,
      timeLabel: formatDhakaTime(this.timeOfDay),
      weather: this.weather,
      graphicsQuality: this.graphicsQuality,
      characterRigMode: this.playerRig.isGlbBacked ? 'glb' : 'procedural',
      activeProtagonist: this.activeProtagonistId,
      protagonistName: currentProf.name,
      protagonistBangla: currentProf.banglaName,
      protagonistAbilityName: currentProf.abilityName,
      protagonistAbilityBangla: currentProf.abilityBangla,
      protagonistAbilityActive: this.specialAbilityActive,
      protagonistAbilityTimerRatio: this.specialAbilityActive
        ? THREE.MathUtils.clamp(this.specialAbilityTimer / currentProf.abilityDuration, 0, 1)
        : 0,
      protagonistAbilityCooldownRatio:
        this.specialAbilityCooldown > 0
          ? THREE.MathUtils.clamp(this.specialAbilityCooldown / currentProf.abilityCooldown, 0, 1)
          : 0,
      cameraViewMode: this.cameraViewMode,
      characterSwitching: this.characterSwitchState.active,
      characterSwitchBanner: this.characterSwitchState.bannerText || null,
      characterSwitchBangla: this.characterSwitchState.bannerBangla || null,
      liveWeather: { ...this.liveWeatherState },
      regionalPlaces: {
        ...this.regionalPlacesState,
        places: this.regionalPlacesState.places.map((p) => ({ ...p, upazilas: [...p.upazilas] })),
      },
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
      fuel,
      maxFuel,
      fuelRatio,
      isLowFuel,
      isWatercraft: Boolean(veh && isWatercraftKind(veh.kind)),
      isSwimming: Boolean(this.player.isSwimming),
      nearDockName: getNearestRiverDock(this.player.x, this.player.z, 24.0)?.banglaName ?? null,
      weapon: w,
      unlockedWeapons: [...this.player.unlockedWeapons],
      magAmmo: this.player.magAmmo[w] ?? 0,
      reserveAmmo: this.player.ammo[w] ?? 0,
      reloading: this.player.reloadTimer > 0,
      outfit: this.player.outfit,
      unlockedOutfits: [...this.player.unlockedOutfits],
      unlockedSafehouses: [...this.player.unlockedSafehouses],
      inventory: this.player.inventory.map((i) => ({ ...i })),
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
      activeSideMission: this.activeSideMission ? { ...this.activeSideMission } : null,
      activeConversation: this.activeConversation ? { ...this.activeConversation } : null,
      safehouseName: this.activeSafehouseId ? SAFEHOUSE_SPECS.find((s) => s.id === this.activeSafehouseId)?.name ?? null : null,
      hasSaveGame: this.hasSaveGame,
      dialogue: this.dialogue ? { ...this.dialogue } : null,
      toast: this.toastMsg,
      muted: this.audio.isMuted(),
      multiplayerActive: Boolean(this.netClient && this.netClient.isConnected()),
      multiplayerPing: this.multiplayerPing,
      multiplayerPlayerCount: this.multiplayerPlayerCount,
      multiplayerKillfeed: this.killfeed.map((k) => ({ ...k })),
      multiplayerChat: this.chatMessages.map((c) => ({ ...c })),
      playerX: this.player.x,
      playerZ: this.player.z,
      playerHeading: this.player.heading,
      blips,
      weaponWheelOpen: this.weaponWheelOpen,
      smartphoneOpen: this.smartphoneOpen,
      activeCompanion: this.activeCompanion
        ? {
            ...this.activeCompanion,
            x: this.secondaryProtagonistRig ? this.secondaryProtagonistRig.group.position.x : undefined,
            z: this.secondaryProtagonistRig ? this.secondaryProtagonistRig.group.position.z : undefined,
          }
        : null,
      activeRobbery: this.activeRobbery ? { ...this.activeRobbery } : null,
      policeSearchRadius: this.policeSearchRadius ? { ...this.policeSearchRadius } : null,
      stats: {
        kills: this.player.kills,
        missionsCompleted: this.player.missionsCompleted,
        totalMissions: STORY_MISSIONS.length,
        faresCompleted: this.player.faresCompleted,
        bountiesCompleted: this.player.bountiesCompleted ?? 0,
        carTheftsCompleted: this.player.carTheftsCompleted ?? 0,
        playTimeSec: Math.round(this.playTimeSec),
        cause: this.gameOverCause,
      },
    });
  }

  public dispose(): void {
    this.disposed = true;
    this.disconnectMultiplayer();
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
    this.playerRig.animator.disposeMixer();
    this.npcRigs.forEach((rig) => {
      this.scene.remove(rig.group);
      rig.animator.disposeMixer();
    });
    this.vehicleRigs.forEach((rig) => {
      this.scene.remove(rig.group);
    });
    this.world.disposables.forEach((d) => d.dispose());
    this.trafficLights.dispose();
    this.textures.dispose();
    this.audio.dispose();
    this.scene.clear();
    this.renderer.dispose();
    if (this.renderer.domElement && this.renderer.domElement.parentElement) {
      this.renderer.domElement.parentElement.removeChild(this.renderer.domElement);
    }
  }
}
