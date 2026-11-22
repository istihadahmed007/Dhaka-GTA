import { AabbCollider, DistrictId, MissionDefinition, Vec2, VehicleKind } from './types';

export const WORLD_HALF_SIZE = 108; // Playable city bounds [-108..108]
export const RIVER_Z_START = 80; // Buriganga River begins at z = 80

export interface BuildingSpec {
  id: string;
  x: number;
  z: number;
  w: number;
  d: number;
  h: number;
  district: DistrictId;
  style: 'old_dhaka' | 'gulshan_tower' | 'residential' | 'warehouse' | 'police' | 'garage' | 'shop';
  color: number;
  accentColor: number;
  signText?: string;
  signBangla?: string;
  hasAwning?: boolean;
  hasWaterTank?: boolean;
}

export interface PoiLocation {
  id: 'garage' | 'shop' | 'police_hq' | 'tea_stall' | 'sadarghat_pier';
  name: string;
  banglaName: string;
  x: number;
  z: number;
  radius: number;
}

export interface RoadSegment {
  x1: number;
  z1: number;
  x2: number;
  z2: number;
  width: number;
}

export const DISTRICT_META: Record<DistrictId, { name: string; bangla: string }> = {
  old_dhaka: { name: 'Old Dhaka · Chawkbazar', bangla: 'পুরান ঢাকা · চকবাজার' },
  shapla_crossing: { name: 'Shapla Crossing', bangla: 'শাপলা চত্বর' },
  gulshan: { name: 'Gulshan · Banani Avenue', bangla: 'গুলশান · বনানী' },
  kawran_flyover: { name: 'Kawran Bazar · Flyover', bangla: 'কারওয়ান বাজার · ফ্লাইওভার' },
  sadarghat: { name: 'Sadarghat · Buriganga', bangla: 'সদরঘাট · বুড়িগঙ্গা' },
};

export function getDistrictAt(x: number, z: number): DistrictId {
  if (z >= 56) return 'sadarghat';
  if (Math.hypot(x, z) < 26) return 'shapla_crossing';
  if (x >= 10 && z < 26) return 'gulshan';
  if (x < -10 && z < 16) return 'kawran_flyover';
  return 'old_dhaka';
}

// Wide, clean Dhaka avenues:
// Main North-South Avenue: x = 0 (width 16)
// East-West Central Avenue: z = 0 (width 16)
// Outer Ring Avenues: x = -64, x = 64, z = -64, z = 64 (width 13)
export const ROAD_SEGMENTS: RoadSegment[] = [
  { x1: 0, z1: -92, x2: 0, z2: 72, width: 16 },
  { x1: -92, z1: 0, x2: 92, z2: 0, width: 16 },
  { x1: -64, z1: -72, x2: -64, z2: 72, width: 13 },
  { x1: 64, z1: -72, x2: 64, z2: 72, width: 13 },
  { x1: -72, z1: -64, x2: 72, z2: -64, width: 13 },
  { x1: -72, z1: 64, x2: 72, z2: 64, width: 13 },
];

export const POIS: PoiLocation[] = [
  { id: 'garage', name: 'Dhaka Customs Garage', banglaName: 'ঢাকা কাস্টমস গ্যারেজ', x: 34, z: -26, radius: 7.5 },
  { id: 'shop', name: "Babu's Hardware & Gear", banglaName: 'বাবু হার্ডওয়্যার ও শপ', x: -24, z: 24, radius: 6.0 },
  { id: 'police_hq', name: 'DMP Central Thana', banglaName: 'ডিএমপি থানা', x: 36, z: 26, radius: 6.5 },
  { id: 'tea_stall', name: 'Rafiq Bhai Cha Stall', banglaName: 'রফিক ভাইয়ের চা স্টল', x: -16, z: 42, radius: 5.5 },
  { id: 'sadarghat_pier', name: 'Sadarghat River Terminal', banglaName: 'সদরঘাট লঞ্চ ঘাট', x: 0, z: 76, radius: 8.0 },
];

// Deterministic buildings placed strictly inside city blocks away from roads!
// Roads occupy:
// x in [-8, 8], [-70.5, -57.5], [57.5, 70.5]
// z in [-8, 8], [-70.5, -57.5], [57.5, 70.5]
// Roundabout occupies radius 11 around (0, 0)
export const BUILDINGS: BuildingSpec[] = [
  // === SOUTHWEST BLOCK: Old Dhaka / Chawkbazar (x: -54..-14, z: 14..54) ===
  {
    id: 'b_shop_babu',
    x: -28, z: 22, w: 14, d: 11, h: 9.5,
    district: 'old_dhaka', style: 'shop',
    color: 0xc97b4b, accentColor: 0x8f2d24,
    signText: "BABU'S GEAR", signBangla: 'বাবু হার্ডওয়্যার',
    hasAwning: true, hasWaterTank: true,
  },
  {
    id: 'b_old_1',
    x: -46, z: 22, w: 13, d: 13, h: 12,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xd49a6a, accentColor: 0x1d6f5f,
    signText: 'CHAWK SPICE', signBangla: 'চক মশলা ঘর',
    hasAwning: true, hasWaterTank: true,
  },
  {
    id: 'b_old_2',
    x: -46, z: 43, w: 15, d: 12, h: 10.5,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xb95c44, accentColor: 0xe6a135,
    signText: 'PURAN BIRIYANI', signBangla: 'হাজী বিরিয়ানি',
    hasAwning: true, hasWaterTank: true,
  },
  {
    id: 'b_old_3',
    x: -26, z: 45, w: 12, d: 10, h: 8.5,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xcfa97e, accentColor: 0x2a6041,
    signText: 'RAFIQ CHA', signBangla: 'চা-এর দোকান',
    hasAwning: true,
  },

  // === SOUTHEAST BLOCK: DMP Thana & Residential (x: 14..54, z: 14..54) ===
  {
    id: 'b_police_hq',
    x: 34, z: 22, w: 18, d: 12, h: 13,
    district: 'shapla_crossing', style: 'police',
    color: 0xdde5ed, accentColor: 0x1e3a8a,
    signText: 'DMP THANA', signBangla: 'ঢাকা মেট্রোপলিটন পুলিশ',
    hasWaterTank: true,
  },
  {
    id: 'b_res_se1',
    x: 24, z: 44, w: 14, d: 12, h: 15,
    district: 'old_dhaka', style: 'residential',
    color: 0xd6c7b2, accentColor: 0x9b3a2e,
    hasWaterTank: true,
  },
  {
    id: 'b_res_se2',
    x: 45, z: 44, w: 14, d: 13, h: 18,
    district: 'old_dhaka', style: 'residential',
    color: 0xc2b29b, accentColor: 0x1b5e4a,
    hasWaterTank: true,
  },

  // === NORTHEAST BLOCK: Gulshan & Banani Commercial + Garage (x: 14..54, z: -54..-14) ===
  {
    id: 'b_garage',
    x: 36, z: -20, w: 16, d: 12, h: 8.5,
    district: 'gulshan', style: 'garage',
    color: 0x334155, accentColor: 0xf59e0b,
    signText: 'DHAKA CUSTOMS', signBangla: 'ঢাকা গ্যারেজ',
    hasAwning: true,
  },
  {
    id: 'b_gulshan_1',
    x: 22, z: -43, w: 14, d: 14, h: 32,
    district: 'gulshan', style: 'gulshan_tower',
    color: 0x3b6e8c, accentColor: 0x94d2bd,
    signText: 'PADMA TOWER', signBangla: 'পদ্মা টাওয়ার',
    hasWaterTank: true,
  },
  {
    id: 'b_gulshan_2',
    x: 45, z: -43, w: 15, d: 15, h: 38,
    district: 'gulshan', style: 'gulshan_tower',
    color: 0x2c4969, accentColor: 0xe9c46a,
    signText: 'GULSHAN PLAZA', signBangla: 'গুলশান প্লাজা',
  },

  // === NORTHWEST BLOCK: Kawran Bazar & Warehouses (x: -54..-14, z: -54..-14) ===
  {
    id: 'b_kawran_1',
    x: -26, z: -23, w: 16, d: 13, h: 14,
    district: 'kawran_flyover', style: 'residential',
    color: 0xb8a996, accentColor: 0x0f766e,
    signText: 'KAWRAN BAZAR', signBangla: 'কারওয়ান বাজার',
    hasAwning: true, hasWaterTank: true,
  },
  {
    id: 'b_kawran_2',
    x: -46, z: -24, w: 14, d: 14, h: 11,
    district: 'kawran_flyover', style: 'warehouse',
    color: 0x7c6f64, accentColor: 0xb45309,
    signText: 'SYNDICATE DEPOT', signBangla: 'গুদাম ঘর',
  },
  {
    id: 'b_kawran_3',
    x: -26, z: -44, w: 15, d: 13, h: 22,
    district: 'kawran_flyover', style: 'gulshan_tower',
    color: 0x475569, accentColor: 0x38bdf8,
    hasWaterTank: true,
  },
  {
    id: 'b_kawran_4',
    x: -46, z: -45, w: 13, d: 12, h: 16,
    district: 'kawran_flyover', style: 'residential',
    color: 0xcab8a2, accentColor: 0x991b1b,
    hasWaterTank: true,
  },

  // === OUTER RING SKYLINE & PERIMETER BLOCKS (visual depth + city density) ===
  { id: 'b_out_ne1', x: 86, z: -42, w: 16, d: 18, h: 34, district: 'gulshan', style: 'gulshan_tower', color: 0x335c67, accentColor: 0xe09f3e },
  { id: 'b_out_ne2', x: 86, z: -18, w: 15, d: 14, h: 26, district: 'gulshan', style: 'gulshan_tower', color: 0x2b3e50, accentColor: 0x60a5fa },
  { id: 'b_out_se1', x: 86, z: 22, w: 16, d: 16, h: 18, district: 'old_dhaka', style: 'residential', color: 0xd1bfa7, accentColor: 0x9a3412, hasWaterTank: true },
  { id: 'b_out_se2', x: 86, z: 46, w: 15, d: 14, h: 14, district: 'old_dhaka', style: 'old_dhaka', color: 0xc47d57, accentColor: 0x15803d, hasAwning: true },

  { id: 'b_out_nw1', x: -86, z: -40, w: 16, d: 16, h: 20, district: 'kawran_flyover', style: 'warehouse', color: 0x6b7280, accentColor: 0xd97706 },
  { id: 'b_out_nw2', x: -86, z: -16, w: 15, d: 14, h: 15, district: 'kawran_flyover', style: 'residential', color: 0xb5a18c, accentColor: 0x0d9488 },
  { id: 'b_out_sw1', x: -86, z: 24, w: 16, d: 16, h: 13, district: 'old_dhaka', style: 'old_dhaka', color: 0xbf6a4b, accentColor: 0xd97706, hasAwning: true },
  { id: 'b_out_sw2', x: -86, z: 48, w: 15, d: 14, h: 11, district: 'old_dhaka', style: 'old_dhaka', color: 0xd4a373, accentColor: 0x991b1b, hasAwning: true },

  { id: 'b_out_n1', x: -34, z: -86, w: 18, d: 14, h: 25, district: 'kawran_flyover', style: 'gulshan_tower', color: 0x3b4d61, accentColor: 0xf59e0b },
  { id: 'b_out_n2', x: 34, z: -86, w: 18, d: 15, h: 42, district: 'gulshan', style: 'gulshan_tower', color: 0x1e3a5f, accentColor: 0x38bdf8 },
  { id: 'b_out_n3', x: 64, z: -88, w: 16, d: 14, h: 30, district: 'gulshan', style: 'gulshan_tower', color: 0x3f5e78, accentColor: 0xfbbf24 },
];

// Flyover concrete pillars along the western highway x = -64 (elevated at y = 9.5)
// Placed cleanly on the median edge x = -56.5 so road remains fully drivable!
export const FLYOVER_PILLARS: Vec2[] = [
  { x: -56.2, z: -76 },
  { x: -56.2, z: -50 },
  { x: -56.2, z: -24 },
  { x: -56.2, z: 16 },
  { x: -56.2, z: 42 },
];

export function buildStaticColliders(): AabbCollider[] {
  const list: AabbCollider[] = [];

  for (const b of BUILDINGS) {
    list.push({
      id: b.id,
      minX: b.x - b.w / 2,
      maxX: b.x + b.w / 2,
      minZ: b.z - b.d / 2,
      maxZ: b.z + b.d / 2,
      height: b.h,
      kind: 'building',
    });
  }

  // Central Shapla Monument Roundabout pedestal
  list.push({
    id: 'shapla_monument',
    minX: -4.2,
    maxX: 4.2,
    minZ: -4.2,
    maxZ: 4.2,
    height: 12,
    kind: 'monument',
  });

  // Flyover pillars
  FLYOVER_PILLARS.forEach((p, idx) => {
    list.push({
      id: `flyover_pillar_${idx}`,
      minX: p.x - 1.1,
      maxX: p.x + 1.1,
      minZ: p.z - 1.1,
      maxZ: p.z + 1.1,
      height: 10,
      kind: 'pillar',
    });
  });

  return list;
}

// Traffic loop routes (right-hand / left-hand Dhaka style lanes staying clear of buildings and Shapla island)
export const TRAFFIC_ROUTES: Vec2[][] = [
  // Outer Counter-Clockwise Ring Road
  [
    { x: 61, z: 61 },
    { x: 61, z: -61 },
    { x: -61, z: -61 },
    { x: -61, z: 61 },
  ],
  // Outer Clockwise Ring Road
  [
    { x: -67, z: 67 },
    { x: -67, z: -67 },
    { x: 67, z: -67 },
    { x: 67, z: 67 },
  ],
  // North-South Central Corridor + Loop
  [
    { x: 4.2, z: 60 },
    { x: 4.2, z: 12 },
    { x: 10, z: 6 },
    { x: 10, z: -6 },
    { x: 4.2, z: -12 },
    { x: 4.2, z: -60 },
    { x: -4.2, z: -60 },
    { x: -4.2, z: -12 },
    { x: -10, z: -6 },
    { x: -10, z: 6 },
    { x: -4.2, z: 12 },
    { x: -4.2, z: 60 },
  ],
  // East-West Central Corridor + Loop
  [
    { x: -60, z: 4.2 },
    { x: -12, z: 4.2 },
    { x: -6, z: 10 },
    { x: 6, z: 10 },
    { x: 12, z: 4.2 },
    { x: 60, z: 4.2 },
    { x: 60, z: -4.2 },
    { x: 12, z: -4.2 },
    { x: 6, z: -10 },
    { x: -6, z: -10 },
    { x: -12, z: -4.2 },
    { x: -60, z: -4.2 },
  ],
];

// Parked starter vehicles placed cleanly along boulevards & near spawn so player can hop right in
export interface InitialVehicleSpawn {
  id: string;
  kind: VehicleKind;
  x: number;
  z: number;
  heading: number;
  color: number;
  driver: 'none' | 'traffic';
  routeIdx?: number;
  waypointIdx?: number;
}

export const INITIAL_VEHICLES: InitialVehicleSpawn[] = [
  // Hero parked vehicles cleanly curbside near player spawn (0, 36), clear of moving lanes
  { id: 'v_starter_cng', kind: 'cng', x: -6.4, z: 33.8, heading: Math.PI, color: 0x0f6a44, driver: 'none' },
  { id: 'v_starter_sedan', kind: 'sedan', x: 6.4, z: 27.5, heading: Math.PI, color: 0xe2e8f0, driver: 'none' },
  { id: 'v_starter_rickshaw', kind: 'rickshaw', x: -12, z: 39, heading: Math.PI * 0.5, color: 0xc62828, driver: 'none' },
  { id: 'v_starter_bike', kind: 'bike', x: 6.6, z: 35.5, heading: Math.PI, color: 0xdc2626, driver: 'none' },
  // Parked around districts
  { id: 'v_garage_sedan', kind: 'sedan', x: 25, z: -26, heading: Math.PI * 0.5, color: 0x1d4ed8, driver: 'none' },
  { id: 'v_sadarghat_truck', kind: 'truck', x: -16, z: 68, heading: Math.PI * 0.5, color: 0xd97706, driver: 'none' },
  { id: 'v_bus_stop', kind: 'bus', x: 22, z: 5.8, heading: -Math.PI * 0.5, color: 0xb81d24, driver: 'none' },
  { id: 'v_police_hq_1', kind: 'police', x: 24, z: 26, heading: Math.PI, color: 0xf1f5f9, driver: 'none' },

  // Live moving traffic vehicles
  { id: 't_cng_1', kind: 'cng', x: 61, z: 30, heading: Math.PI, color: 0x0e7448, driver: 'traffic', routeIdx: 0, waypointIdx: 1 },
  { id: 't_bus_1', kind: 'bus', x: 61, z: -30, heading: Math.PI, color: 0x15803d, driver: 'traffic', routeIdx: 0, waypointIdx: 1 },
  { id: 't_truck_1', kind: 'truck', x: -20, z: -61, heading: -Math.PI * 0.5, color: 0xea580c, driver: 'traffic', routeIdx: 0, waypointIdx: 2 },
  { id: 't_cng_2', kind: 'cng', x: -61, z: 10, heading: 0, color: 0x0f6a44, driver: 'traffic', routeIdx: 0, waypointIdx: 3 },
  { id: 't_sedan_1', kind: 'sedan', x: -67, z: -20, heading: Math.PI, color: 0x334155, driver: 'traffic', routeIdx: 1, waypointIdx: 1 },
  { id: 't_rick_1', kind: 'rickshaw', x: 25, z: -67, heading: Math.PI * 0.5, color: 0x0284c7, driver: 'traffic', routeIdx: 1, waypointIdx: 2 },
  { id: 't_bike_1', kind: 'bike', x: 67, z: 18, heading: 0, color: 0x111827, driver: 'traffic', routeIdx: 1, waypointIdx: 3 },
  { id: 't_cng_3', kind: 'cng', x: 4.2, z: -35, heading: Math.PI, color: 0x0f6a44, driver: 'traffic', routeIdx: 2, waypointIdx: 5 },
  { id: 't_sedan_2', kind: 'sedan', x: -4.2, z: 20, heading: 0, color: 0xb91c1c, driver: 'traffic', routeIdx: 2, waypointIdx: 11 },
  { id: 't_bus_2', kind: 'bus', x: -35, z: 4.2, heading: Math.PI * 0.5, color: 0xb81d24, driver: 'traffic', routeIdx: 3, waypointIdx: 1 },
  { id: 't_rick_2', kind: 'rickshaw', x: 38, z: -4.2, heading: -Math.PI * 0.5, color: 0x7c3aed, driver: 'traffic', routeIdx: 3, waypointIdx: 7 },
];

export const STORY_MISSIONS: MissionDefinition[] = [
  {
    id: 'm1_chawkbazar',
    index: 0,
    title: 'Chanda at Chawkbazar',
    banglaTitle: 'চকবাজারে চাঁদাবাজি',
    district: 'old_dhaka',
    rewardMoney: 1500,
    unlockWeapon: 'pistol',
    steps: [
      {
        id: 'm1_meet',
        title: 'Meet Rafiq Bhai',
        banglaTitle: 'রফিক ভাইয়ের সাথে দেখা করো',
        objective: 'Go to Rafiq Bhai at Old Dhaka Cha Stall (follow gold marker)',
        dialogueSpeaker: 'Rafiq Bhai',
        dialogueBangla: 'তারেক, শোন! পদ্মা সিন্ডিকেটের লোকেরা চকবাজারের দোকানদারদের জ্বালাচ্ছে।',
        dialogueEnglish: 'Tariq, listen! Padma Syndicate thugs are shaking down Chawkbazar shopkeepers.',
        targetX: -16,
        targetZ: 42,
        radius: 6.5,
        kind: 'goto',
      },
      {
        id: 'm1_clear',
        title: 'Clear the Extortion Crew',
        banglaTitle: 'চাঁদাবাজদের তাড়াও',
        objective: 'Defeat the 3 Syndicate thugs in Chawkbazar alley (Left Click or J to attack)',
        dialogueSpeaker: 'Tariq',
        dialogueBangla: 'ওদের আজ একটা উচিত শিক্ষা দিতে হবে।',
        dialogueEnglish: "Time to remind them whose streets these are.",
        targetX: -36,
        targetZ: 34,
        radius: 14,
        kind: 'eliminate',
        requiredKills: 3,
      },
      {
        id: 'm1_return',
        title: 'Report to Dhaka Customs',
        banglaTitle: 'গ্যারেজে ফিরে যাও',
        objective: 'Drive or run to Dhaka Customs Garage in Gulshan to claim your reward',
        dialogueSpeaker: 'Rafiq Bhai',
        dialogueBangla: 'সাবাশ ভাই! গ্যারেজে আয়, তোর জন্য একটা পিস্তল আর টাকা রেখেছি।',
        dialogueEnglish: 'Well done! Head up to Dhaka Customs Garage—I stashed cash and a 9mm for you.',
        targetX: 34,
        targetZ: -26,
        radius: 8,
        kind: 'goto',
      },
    ],
  },
  {
    id: 'm2_buriganga',
    index: 1,
    title: 'Buriganga Run',
    banglaTitle: 'বুড়িগঙ্গার চালান',
    district: 'sadarghat',
    rewardMoney: 2800,
    unlockWeapon: 'smg',
    steps: [
      {
        id: 'm2_pier',
        title: 'Reach Sadarghat Pier',
        banglaTitle: 'সদরঘাট ঘাটে যাও',
        objective: 'Head south to Sadarghat Riverfront before the Syndicate moves the truck',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'তারেক, সদরঘাটে ওদের একটা ট্রাক ঢুকেছে। ওটা দখল করতে পারলে সব প্রমাণ পাবো।',
        dialogueEnglish: "Tariq, their cargo truck just docked at Sadarghat. Seize it and we'll have the proof.",
        targetX: -8,
        targetZ: 70,
        radius: 10,
        kind: 'goto',
      },
      {
        id: 'm2_guards',
        title: 'Neutralize Pier Guards',
        banglaTitle: 'ঘাটের পাহারাদারদের সরাও',
        objective: 'Eliminate the 4 Syndicate guards protecting the cargo truck',
        targetX: -12,
        targetZ: 68,
        radius: 16,
        kind: 'eliminate',
        requiredKills: 4,
      },
      {
        id: 'm2_deliver',
        title: 'Deliver the Cargo Truck',
        banglaTitle: 'ট্রাকটি গ্যারেজে নিয়ে যাও',
        objective: 'Get into the Bengal Cargo Truck [F] and drive it to Dhaka Customs Garage',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'দারুণ! পুলিশ আসার আগেই ট্রাকটা নিয়ে গ্যারেজে ঢুকে পড়ো।',
        dialogueEnglish: 'Nice work! Get that truck to Dhaka Customs before DMP locks down the avenue.',
        targetX: 34,
        targetZ: -26,
        radius: 9,
        kind: 'steal_deliver',
        vehicleKindRequired: 'truck',
      },
    ],
  },
  {
    id: 'm3_monsoon',
    index: 2,
    title: 'Monsoon Convoy',
    banglaTitle: 'বর্ষার অভিযান',
    district: 'kawran_flyover',
    rewardMoney: 4200,
    unlockWeapon: 'shotgun',
    weatherOverride: 'storm',
    steps: [
      {
        id: 'm3_ambush',
        title: 'Ambush Under the Flyover',
        banglaTitle: 'ফ্লাইওভারের নিচে অভিযান',
        objective: 'Drive to Kawran Bazar Flyover depot in the monsoon storm',
        dialogueSpeaker: 'Tariq',
        dialogueBangla: 'এই ঝড়ের রাতেই ফ্লাইওভারের নিচে ওদের লিডার লুকিয়ে আছে।',
        dialogueEnglish: "Storm's rolling in—the Syndicate Lieutenant is hiding under the Kawran Flyover.",
        targetX: -48,
        targetZ: -35,
        radius: 12,
        kind: 'goto',
      },
      {
        id: 'm3_boss',
        title: 'Defeat the Flyover Crew',
        banglaTitle: 'সিন্ডিকেট লিডারকে পরাস্ত করো',
        objective: 'Take down the Syndicate Lieutenant and his 4 enforcers',
        targetX: -46,
        targetZ: -34,
        radius: 18,
        kind: 'eliminate',
        requiredKills: 5,
      },
    ],
  },
  {
    id: 'm4_king_of_dhaka',
    index: 3,
    title: 'King of Dhaka',
    banglaTitle: 'ঢাকার রাজা',
    district: 'gulshan',
    rewardMoney: 8000,
    steps: [
      {
        id: 'm4_plaza',
        title: 'Storm Gulshan Plaza',
        banglaTitle: 'গুলশান প্লাজায় অভিযান',
        objective: 'Advance to Gulshan Plaza for the final showdown with the Padma Boss',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'শেষ লড়াই, তারেক! গুলশান প্লাজায় ওদের বস পালানোর চেষ্টা করছে।',
        dialogueEnglish: 'Final showdown, Tariq! The Padma Boss is trying to flee from Gulshan Plaza.',
        targetX: 34,
        targetZ: -44,
        radius: 12,
        kind: 'goto',
      },
      {
        id: 'm4_showdown',
        title: 'Dismantle the Syndicate',
        banglaTitle: 'সিন্ডিকেটের পতন ঘটাও',
        objective: 'Defeat the Padma Syndicate Boss and his elite guards',
        targetX: 34,
        targetZ: -44,
        radius: 18,
        kind: 'eliminate',
        requiredKills: 5,
      },
      {
        id: 'm4_shapla',
        title: 'Claim Shapla Crossing',
        banglaTitle: 'শাপলা চত্বরে বিজয়',
        objective: 'Return to Shapla Monument Crossing to reclaim the city',
        dialogueSpeaker: 'Rafiq Bhai',
        dialogueBangla: 'পুরো ঢাকা আজ মুক্ত! শাপলা চত্বরে আয়, সবাই তোর জন্য অপেক্ষা করছে।',
        dialogueEnglish: 'Dhaka is free! Roll back into Shapla Crossing—the whole city is yours.',
        targetX: 0,
        targetZ: 14,
        radius: 9,
        kind: 'goto',
      },
    ],
  },
];

export const FARE_DESTINATIONS: Array<{ label: string; x: number; z: number }> = [
  { label: 'Gulshan Plaza', x: 34, z: -56 },
  { label: 'Sadarghat Terminal', x: 0, z: 68 },
  { label: 'Kawran Bazar Flyover', x: -58, z: -24 },
  { label: 'Chawkbazar Spice Market', x: -34, z: 58 },
  { label: 'DMP Central Crossing', x: 48, z: 6 },
];
