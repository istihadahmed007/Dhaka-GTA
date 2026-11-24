import type { GraphicsQualityPreset } from './graphicsQuality';
export type { GraphicsQualityPreset } from './graphicsQuality';

export type GamePhase = 'title' | 'playing' | 'paused' | 'gameover' | 'victory';

export type DistrictId =
  | 'old_dhaka'
  | 'shapla_crossing'
  | 'gulshan'
  | 'kawran_flyover'
  | 'sadarghat'
  | 'motijheel'
  | 'dhanmondi'
  | 'uttara_north'
  | 'mirpur_market';

export type VehicleKind =
  | 'cng'
  | 'rickshaw'
  | 'sedan'
  | 'bus'
  | 'truck'
  | 'bike'
  | 'police'
  | 'launch'
  | 'speedboat'
  | 'jetski';

export function isWatercraftKind(kind: VehicleKind): boolean {
  return kind === 'launch' || kind === 'speedboat' || kind === 'jetski';
}

export type WeaponId = 'fists' | 'pistol' | 'smg' | 'shotgun';

export type WeatherMode = 'clear' | 'monsoon' | 'storm';

export type TimePreset = 'dawn' | 'day' | 'sunset' | 'night';

export type OutfitId = 'street' | 'panjabi' | 'gulshan' | 'monsoon';

export type EnemyPolicy = 'chaser' | 'ranged' | 'flanker';

export type InteractModalKind =
  | 'none'
  | 'garage'
  | 'shop'
  | 'wardrobe'
  | 'safehouse'
  | 'fuel_station'
  | 'dialogue'
  | 'multiplayer_lobby';

export type SafehouseId = 'old_dhaka_rooftop' | 'gulshan_penthouse' | 'dhanmondi_hideout';

export interface SafehouseSpec {
  id: SafehouseId;
  name: string;
  banglaName: string;
  district: DistrictId;
  x: number;
  z: number;
  radius: number;
  price: number;
  unlocked: boolean;
  description?: string;
}

export interface VehicleCustomization {
  engineStage: number; // 0..3 (+10%, +20%, +35% speed & accel)
  brakesStage: number; // 0..2 (+25%, +50% braking power)
  hasTurbo: boolean; // Turbo whistle & +15% torque
  hasSpoiler: boolean; // Downforce + improved drift stability
  tintLevel: number; // 0 = clear, 1 = smoke, 2 = limo dark
  neonColor?: number | null; // Underglow hex color
  plateJammer: boolean; // Clears heat on vehicle entry
}

export interface InventoryItem {
  id: string;
  name: string;
  banglaName: string;
  kind: 'food' | 'medical' | 'tool' | 'fuel' | 'contraband';
  count: number;
  value: number;
  description: string;
}

export type SideMissionKind = 'fare' | 'car_theft' | 'bounty' | 'courier';

export interface SideMissionState {
  id: string;
  kind: SideMissionKind;
  title: string;
  banglaTitle: string;
  giver: string;
  objective: string;
  targetX: number;
  targetZ: number;
  targetRadius: number;
  targetVehicleKind?: VehicleKind;
  targetKillsRequired?: number;
  rewardMoney: number;
  rewardItem?: InventoryItem;
  timer: number;
  maxTimer: number;
  step?: number;
}

export interface DialogueOption {
  id: string;
  label: string;
  banglaLabel: string;
  action: 'job' | 'rumor' | 'heal' | 'trade' | 'leave';
  param?: string;
}

export interface InteractiveNpcConversation {
  npcId: string;
  npcName: string;
  npcBanglaName: string;
  role: string;
  greetingBangla: string;
  greetingEnglish: string;
  options: DialogueOption[];
}

export interface DhakaSaveGameData {
  version: number;
  timestamp: number;
  player: {
    x: number;
    y: number;
    z: number;
    heading: number;
    hp: number;
    armor: number;
    stamina: number;
    money: number;
    wantedLevel: number;
    weapon: WeaponId;
    unlockedWeapons: WeaponId[];
    ammo: Record<WeaponId, number>;
    outfit: OutfitId;
    unlockedOutfits: OutfitId[];
    unlockedSafehouses: SafehouseId[];
    inventory: InventoryItem[];
  };
  missions: {
    missionIdx: number;
    stepIdx: number;
    missionsCompleted: number;
    faresCompleted: number;
    bountiesCompleted: number;
    carTheftsCompleted: number;
  };
  world: {
    timeOfDay: number;
    weather: WeatherMode;
    playTimeSec: number;
  };
  ownedVehicles: Array<{
    kind: VehicleKind;
    color: number;
    customization: VehicleCustomization;
  }>;
}

export interface Vec2 {
  x: number;
  z: number;
}

export interface AabbCollider {
  id: string;
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
  height: number;
  kind: 'building' | 'pillar' | 'monument' | 'wall' | 'stall';
}

export interface VehicleSpec {
  kind: VehicleKind;
  name: string;
  banglaName: string;
  maxSpeed: number; // m/s
  reverseSpeed: number;
  accel: number;
  brake: number;
  turnRate: number;
  drag: number;
  maxHp: number;
  mass: number;
  halfWidth: number;
  halfLength: number;
  defaultColor: number;
  canDrift: boolean;
}

export interface WeaponSpec {
  id: WeaponId;
  name: string;
  role: string;
  key: string;
  damage: number;
  range: number;
  cooldown: number;
  magSize: number;
  reloadTime: number;
  spread: number;
  pellets: number;
  price: number;
  ammoPrice: number;
}

export interface OutfitSpec {
  id: OutfitId;
  name: string;
  banglaName: string;
  description: string;
  torsoColor: number;
  pantsColor: number;
  accentColor: number;
  price: number;
}

export interface VehicleState {
  id: string;
  kind: VehicleKind;
  x: number;
  y: number;
  z: number;
  heading: number;
  speed: number;
  steerAngle: number;
  hp: number;
  maxHp: number;
  color: number;
  driver: 'none' | 'player' | 'traffic' | 'police';
  routeIdx: number;
  waypointIdx: number;
  honkTimer: number;
  sirenOn: boolean;
  damagedSmoke: boolean;
  // --- Enhanced realistic vehicle dynamics ---
  vx?: number; // World X velocity (m/s)
  vz?: number; // World Z velocity (m/s)
  lateralSlip?: number; // Lateral tire slip velocity (m/s)
  pitch?: number; // Suspension pitch angle (rad, squat/dive)
  roll?: number; // Suspension roll angle (rad, body lean / bike banking)
  wheelRotation?: number; // Cumulative wheel spin angle (rad)
  isBraking?: boolean; // True when braking or handbrake active
  isReversing?: boolean; // True when reverse gear active
  isDrifting?: boolean; // True when tire slip exceeds grip threshold
  // --- Believable living traffic simulation fields ---
  laneOffset?: number; // Current lateral lane offset in road corridor (-1 = left slow shoulder, 0 = main lane, +1 = overtaking right)
  targetLaneOffset?: number; // Desired lane offset for smooth lane-change transitions
  isOvertaking?: boolean; // Currently performing an overtaking maneuver around a slower vehicle
  overtakeTimer?: number; // Time elapsed in current overtake maneuver
  waitTimer?: number; // Time paused (at traffic light, bus stop, or in congestion queue)
  busStopTimer?: number; // For city buses paused at designated passenger stops
  stoppedAtRed?: boolean; // True when halted at red signal
  // --- Watercraft dynamics & docking ---
  buoyancyVy?: number; // Vertical buoyancy velocity (m/s)
  isWatercraft?: boolean; // True for launches, speedboats, and jet skis
  isPlaning?: boolean; // True when hull lifts out of water at high speed
  dockedAt?: string | null; // Id of berth if currently docked
  wakeTimer?: number; // Internal timer for wake and splash generation
  // --- Fuel & Customization ---
  fuel?: number; // 0..100
  maxFuel?: number; // 100
  fuelType?: 'petrol' | 'cng' | 'diesel';
  customization?: VehicleCustomization;
  isStalled?: boolean;
  stoleFromNpc?: boolean;
}

export interface PlayerState {
  x: number;
  y: number;
  z: number;
  vx: number;
  vy: number;
  vz: number;
  heading: number;
  grounded: boolean;
  coyoteTimer: number;
  jumpBufferTimer: number;
  hp: number;
  maxHp: number;
  armor: number;
  maxArmor: number;
  stamina: number;
  maxStamina: number;
  money: number;
  wantedLevel: number; // 0..5
  wantedHeat: number; // 0..100
  wantedCooldown: number;
  vehicleId: string | null;
  weapon: WeaponId;
  unlockedWeapons: WeaponId[];
  ammo: Record<WeaponId, number>;
  magAmmo: Record<WeaponId, number>;
  reloadTimer: number;
  attackCooldown: number;
  outfit: OutfitId;
  unlockedOutfits: OutfitId[];
  unlockedSafehouses: SafehouseId[];
  inventory: InventoryItem[];
  kills: number;
  missionsCompleted: number;
  faresCompleted: number;
  bountiesCompleted: number;
  carTheftsCompleted: number;
  distanceDriven: number;
  isSwimming?: boolean;
}

export interface NpcState {
  id: string;
  role: 'civilian' | 'police' | 'syndicate' | 'boss' | 'ally';
  policy: EnemyPolicy;
  name?: string;
  x: number;
  y: number;
  z: number;
  vx: number;
  vz: number;
  heading: number;
  hp: number;
  maxHp: number;
  speed: number;
  state:
  | 'idle'
  | 'walk'
  | 'flee'
  | 'chase'
  | 'windup'
  | 'attack'
  | 'ko'
  | 'waiting_to_cross'
  | 'crossing'
  | 'chatting'
  | 'browsing_shop'
  | 'waiting_bus';
  stateTimer: number;
  windupDuration: number;
  attackCooldown: number;
  hitFlashTimer: number;
  koTimer: number;
  shirtColor: number;
  pantsColor: number;
  targetX: number;
  targetZ: number;
  missionTag?: string;
  hasAttackToken: boolean;
  strafeDir: number;
  // --- Believable living pedestrian simulation fields ---
  pedestrianZone?: 'sidewalk' | 'crossing' | 'plaza' | 'shop' | 'bazaar';
  socialPartnerId?: string; // ID of partner when chatting
  crossLookTimer?: number; // Timer for turning head to check traffic
  hurrying?: boolean; // Jogging/hurrying when car approaches
  dangerSourceX?: number; // Origin of gunfire or explosion
  dangerSourceZ?: number;
  dangerLevel?: number;
}

export interface MissionStep {
  id: string;
  title: string;
  banglaTitle: string;
  objective: string;
  dialogueSpeaker?: string;
  dialogueBangla?: string;
  dialogueEnglish?: string;
  targetX: number;
  targetZ: number;
  radius: number;
  kind: 'goto' | 'eliminate' | 'steal_deliver' | 'escape_police' | 'interact';
  requiredKills?: number;
  vehicleKindRequired?: VehicleKind;
}

export interface MissionDefinition {
  id: string;
  index: number;
  title: string;
  banglaTitle: string;
  district: DistrictId;
  rewardMoney: number;
  unlockWeapon?: WeaponId;
  weatherOverride?: WeatherMode;
  steps: MissionStep[];
}

export interface SideFareState {
  active: boolean;
  passengerName: string;
  destX: number;
  destZ: number;
  destLabel: string;
  timer: number;
  reward: number;
}

export interface ActiveDialogue {
  speaker: string;
  bangla: string;
  english: string;
  timer: number;
}

export interface MinimapBlip {
  x: number;
  z: number;
  kind: 'mission' | 'garage' | 'shop' | 'police' | 'enemy' | 'vehicle' | 'fare' | 'safehouse' | 'fuel';
}

export interface LiveWeatherHudState {
  enabled: boolean;
  status: 'idle' | 'loading' | 'live' | 'error';
  conditionLabel: string | null;
  conditionBangla: string | null;
  temperatureC: number | null;
  humidityPct: number | null;
  windKmh: number | null;
  fromCache: boolean;
  error: string | null;
}

export interface RegionalPlaceItem {
  name: string;
  banglaName: string;
  upazilas: string[];
}

export interface RegionalPlacesHudState {
  enabled: boolean;
  status: 'idle' | 'loading' | 'ready' | 'error';
  source: 'bdapis' | 'fallback';
  places: RegionalPlaceItem[];
  error: string | null;
}

export interface HudSnapshot {
  phase: GamePhase;
  modal: InteractModalKind;
  districtName: string;
  districtBangla: string;
  streetName: string | null;
  streetBangla: string | null;
  timeOfDay: number; // 0..24 hours
  timeLabel: string;
  weather: WeatherMode;
  graphicsQuality: GraphicsQualityPreset;
  characterRigMode: 'glb' | 'procedural';
  liveWeather: LiveWeatherHudState;
  regionalPlaces: RegionalPlacesHudState;
  hp: number;
  maxHp: number;
  armor: number;
  maxArmor: number;
  stamina: number;
  money: number;
  wantedLevel: number;
  wantedCooldownRatio: number;
  inVehicle: boolean;
  vehicleName: string;
  vehicleBangla: string;
  vehicleKind: VehicleKind | null;
  vehicleSpeedKmh: number;
  vehicleHp: number;
  vehicleMaxHp: number;
  fuel: number;
  maxFuel: number;
  fuelRatio: number;
  isLowFuel: boolean;
  isWatercraft?: boolean;
  isSwimming?: boolean;
  nearDockName?: string | null;
  weapon: WeaponId;
  unlockedWeapons: WeaponId[];
  magAmmo: number;
  reserveAmmo: number;
  reloading: boolean;
  outfit: OutfitId;
  unlockedOutfits: OutfitId[];
  unlockedSafehouses: SafehouseId[];
  inventory: InventoryItem[];
  interactPrompt: string | null;
  interactKey: string | null;
  missionTitle: string | null;
  missionBangla: string | null;
  missionObjective: string | null;
  missionProgressText: string | null;
  fareActive: boolean;
  fareText: string | null;
  fareTimer: number;
  activeSideMission: SideMissionState | null;
  activeConversation: InteractiveNpcConversation | null;
  safehouseName?: string | null;
  hasSaveGame: boolean;
  dialogue: ActiveDialogue | null;
  toast: string | null;
  muted: boolean;
  multiplayerActive: boolean;
  multiplayerPing: number;
  multiplayerPlayerCount: number;
  multiplayerKillfeed: Array<{ id: string; text: string; timer: number }>;
  multiplayerChat: Array<{ id: string; sender: string; text: string; isSystem: boolean }>;
  playerX: number;
  playerZ: number;
  playerHeading: number;
  blips: MinimapBlip[];
  stats: {
    kills: number;
    missionsCompleted: number;
    totalMissions: number;
    faresCompleted: number;
    bountiesCompleted: number;
    carTheftsCompleted: number;
    playTimeSec: number;
    cause: string;
  };
}

export const VEHICLE_SPECS: Record<VehicleKind, VehicleSpec> = {
  cng: {
    kind: 'cng',
    name: 'Dhaka CNG Auto',
    banglaName: 'সিএনজি অটোরিকশা',
    maxSpeed: 19,
    reverseSpeed: 7,
    accel: 14,
    brake: 20,
    turnRate: 2.55,
    drag: 2.2,
    maxHp: 110,
    mass: 1.0,
    halfWidth: 0.92,
    halfLength: 1.55,
    defaultColor: 0x0f6a44,
    canDrift: false,
  },
  rickshaw: {
    kind: 'rickshaw',
    name: 'Cycle Rickshaw',
    banglaName: 'রিকশা',
    maxSpeed: 12.5,
    reverseSpeed: 4,
    accel: 10,
    brake: 18,
    turnRate: 2.9,
    drag: 2.8,
    maxHp: 75,
    mass: 0.65,
    halfWidth: 0.78,
    halfLength: 1.45,
    defaultColor: 0xc62828,
    canDrift: false,
  },
  sedan: {
    kind: 'sedan',
    name: 'Gulshan GT Sedan',
    banglaName: 'প্রিমিও সেডান',
    maxSpeed: 29,
    reverseSpeed: 10,
    accel: 19,
    brake: 26,
    turnRate: 2.25,
    drag: 1.8,
    maxHp: 165,
    mass: 1.6,
    halfWidth: 1.05,
    halfLength: 2.25,
    defaultColor: 0xd9e1e8,
    canDrift: true,
  },
  bike: {
    kind: 'bike',
    name: 'Dhaka 150R Moto',
    banglaName: 'মোটরসাইকেল',
    maxSpeed: 31,
    reverseSpeed: 5,
    accel: 23,
    brake: 28,
    turnRate: 2.85,
    drag: 1.9,
    maxHp: 90,
    mass: 0.75,
    halfWidth: 0.55,
    halfLength: 1.25,
    defaultColor: 0xb91c1c,
    canDrift: true,
  },
  bus: {
    kind: 'bus',
    name: 'Dhaka Metro Bus',
    banglaName: 'সিটি বাস',
    maxSpeed: 18,
    reverseSpeed: 6,
    accel: 8.5,
    brake: 15,
    turnRate: 1.45,
    drag: 1.6,
    maxHp: 340,
    mass: 4.0,
    halfWidth: 1.35,
    halfLength: 4.15,
    defaultColor: 0xb81d24,
    canDrift: false,
  },
  truck: {
    kind: 'truck',
    name: 'Bengal Cargo Truck',
    banglaName: 'মালবাহী ট্রাক',
    maxSpeed: 20,
    reverseSpeed: 7,
    accel: 10.5,
    brake: 16,
    turnRate: 1.65,
    drag: 1.7,
    maxHp: 280,
    mass: 3.2,
    halfWidth: 1.25,
    halfLength: 3.15,
    defaultColor: 0xd97706,
    canDrift: false,
  },
  police: {
    kind: 'police',
    name: 'DMP Patrol Pickup',
    banglaName: 'ডিএমপি পুলিশ পিকআপ',
    maxSpeed: 27,
    reverseSpeed: 9,
    accel: 18,
    brake: 25,
    turnRate: 2.15,
    drag: 1.8,
    maxHp: 210,
    mass: 2.1,
    halfWidth: 1.12,
    halfLength: 2.45,
    defaultColor: 0xf1f5f9,
    canDrift: true,
  },
  launch: {
    kind: 'launch',
    name: 'Sadarghat Passenger Launch',
    banglaName: 'সদরঘাট যাত্রীবাহী লঞ্চ',
    maxSpeed: 12.0,
    reverseSpeed: 4.5,
    accel: 2.8,
    brake: 4.8,
    turnRate: 0.52,
    drag: 0.85,
    maxHp: 850,
    mass: 14.0,
    halfWidth: 3.4,
    halfLength: 11.0,
    defaultColor: 0x0f5132,
    canDrift: false,
  },
  speedboat: {
    kind: 'speedboat',
    name: 'Buriganga River Speedboat',
    banglaName: 'বুড়িগঙ্গা স্পিডবোট',
    maxSpeed: 24.5,
    reverseSpeed: 7.0,
    accel: 16.5,
    brake: 22.0,
    turnRate: 1.95,
    drag: 1.15,
    maxHp: 220,
    mass: 1.4,
    halfWidth: 1.35,
    halfLength: 3.4,
    defaultColor: 0xd97706,
    canDrift: true,
  },
  jetski: {
    kind: 'jetski',
    name: 'Wave Runner Jet Ski',
    banglaName: 'ওয়েভরানার জেট স্কি',
    maxSpeed: 29.0,
    reverseSpeed: 6.0,
    accel: 21.0,
    brake: 26.0,
    turnRate: 2.65,
    drag: 1.35,
    maxHp: 160,
    mass: 0.55,
    halfWidth: 0.75,
    halfLength: 1.65,
    defaultColor: 0x2563eb,
    canDrift: true,
  },
};

export const WEAPON_SPECS: Record<WeaponId, WeaponSpec> = {
  fists: {
    id: 'fists',
    name: 'Street Brawler',
    role: 'Close Melee Combo',
    key: '1',
    damage: 24,
    range: 2.5,
    cooldown: 0.36,
    magSize: 0,
    reloadTime: 0,
    spread: 0,
    pellets: 1,
    price: 0,
    ammoPrice: 0,
  },
  pistol: {
    id: 'pistol',
    name: '9mm Service Pistol',
    role: 'Accurate Sidearm',
    key: '2',
    damage: 32,
    range: 34,
    cooldown: 0.28,
    magSize: 12,
    reloadTime: 1.2,
    spread: 0.03,
    pellets: 1,
    price: 900,
    ammoPrice: 120,
  },
  smg: {
    id: 'smg',
    name: 'Buriganga SMG',
    role: 'Rapid Suppression',
    key: '3',
    damage: 18,
    range: 28,
    cooldown: 0.11,
    magSize: 30,
    reloadTime: 1.5,
    spread: 0.08,
    pellets: 1,
    price: 2200,
    ammoPrice: 200,
  },
  shotgun: {
    id: 'shotgun',
    name: '12G Street Sweeper',
    role: 'Heavy Close Spread',
    key: '4',
    damage: 16,
    range: 17,
    cooldown: 0.72,
    magSize: 6,
    reloadTime: 1.8,
    spread: 0.15,
    pellets: 5,
    price: 3400,
    ammoPrice: 260,
  },
};

export const OUTFIT_SPECS: Record<OutfitId, OutfitSpec> = {
  street: {
    id: 'street',
    name: 'Old Dhaka Denim',
    banglaName: 'পুরান ঢাকা স্ট্রিট',
    description: 'Indigo jacket, white tee, dark jeans',
    torsoColor: 0x26466d,
    pantsColor: 0x1e2530,
    accentColor: 0xe6a135,
    price: 0,
  },
  panjabi: {
    id: 'panjabi',
    name: 'Crimson Panjabi & Vest',
    banglaName: 'লাল পাঞ্জাবি',
    description: 'Festive deep crimson panjabi with gold trim',
    torsoColor: 0x9b1c24,
    pantsColor: 0xf0ead6,
    accentColor: 0xd4a017,
    price: 600,
  },
  gulshan: {
    id: 'gulshan',
    name: 'Gulshan Noir Jacket',
    banglaName: 'গুলশান ব্ল্যাক',
    description: 'Charcoal leather blazer & emerald collar',
    torsoColor: 0x181b20,
    pantsColor: 0x121418,
    accentColor: 0x10b981,
    price: 1200,
  },
  monsoon: {
    id: 'monsoon',
    name: 'Monsoon Tactical Shell',
    banglaName: 'বর্ষা রেইনকোট',
    description: 'Saffron-ochre weatherproof street shell',
    torsoColor: 0xc26d18,
    pantsColor: 0x24303c,
    accentColor: 0x38bdf8,
    price: 850,
  },
};
