import { AabbCollider, DistrictId, MissionDefinition, Vec2, VehicleKind } from './types';

// Expanded ~2.5x linear dimensions from original (108 -> 270, river 80 -> 200)
export const WORLD_HALF_SIZE = 270; // Playable X: [-270..270] (540m), Z: [-270..200] (470m)
export const RIVER_Z_START = 200; // Buriganga River embankment begins at z = 200

export type BuildingStyle =
  | 'old_dhaka'
  | 'gulshan_tower'
  | 'motijheel_tower'
  | 'residential'
  | 'warehouse'
  | 'market_hall'
  | 'police'
  | 'garage'
  | 'shop';

export interface BuildingSpec {
  id: string;
  x: number;
  z: number;
  w: number;
  d: number;
  h: number;
  district: DistrictId;
  style: BuildingStyle;
  color: number;
  accentColor: number;
  signText?: string;
  signBangla?: string;
  hasAwning?: boolean;
  hasWaterTank?: boolean;
  isHero?: boolean;
}

export interface PoiLocation {
  id: string;
  kind: 'garage' | 'shop' | 'police_hq' | 'tea_stall' | 'sadarghat_pier';
  name: string;
  banglaName: string;
  x: number;
  z: number;
  radius: number;
}

export type RoadTier = 'avenue' | 'boulevard' | 'street' | 'alley';

export interface RoadSegment {
  id: string;
  x1: number;
  z1: number;
  x2: number;
  z2: number;
  width: number;
  tier: RoadTier;
}

export interface LakeZone {
  id: string;
  name: string;
  x: number;
  z: number;
  w: number;
  d: number;
}

export const DISTRICT_META: Record<DistrictId, { name: string; bangla: string }> = {
  shapla_crossing: { name: 'Shapla Crossing', bangla: 'শাপলা চত্বর' },
  old_dhaka: { name: 'Old Dhaka · Chawkbazar', bangla: 'পুরান ঢাকা · চকবাজার' },
  sadarghat: { name: 'Sadarghat · Buriganga', bangla: 'সদরঘাট · বুড়িগঙ্গা' },
  gulshan: { name: 'Gulshan · Banani Avenue', bangla: 'গুলশান · বনানী' },
  kawran_flyover: { name: 'Kawran Bazar · Flyover', bangla: 'কারওয়ান বাজার · ফ্লাইওভার' },
  motijheel: { name: 'Motijheel Commercial Hub', bangla: 'মতিঝিল বাণিজ্যিক এলাকা' },
  dhanmondi: { name: 'Dhanmondi · Lakefront', bangla: 'ধানমন্ডি · লেকপাড়' },
  uttara_north: { name: 'Banani North · Diplomatic', bangla: 'বনানী উত্তর · কূটনৈতিক এলাকা' },
  mirpur_market: { name: 'Mirpur · Bazaar Corridor', bangla: 'মিরপুর · বাজার রোড' },
};

export function getDistrictAt(x: number, z: number): DistrictId {
  if (z >= 164) return 'sadarghat';
  if (Math.hypot(x, z) < 28) return 'shapla_crossing';
  if (z < -150) {
    return x < -32 ? 'mirpur_market' : 'uttara_north';
  }
  if (x < -148) {
    return z >= 68 ? 'old_dhaka' : 'dhanmondi';
  }
  if (x >= 112 || (x >= 68 && z >= 10)) {
    return z < -68 ? 'gulshan' : 'motijheel';
  }
  if (x >= 10 && z < 12) return 'gulshan';
  if (x < -10 && z < 12) return 'kawran_flyover';
  return 'old_dhaka';
}

// Ornamental inland Dhaka water features (Dhanmondi Lake west + Hatirjheel Lake east-central)
export const LAKE_ZONES: LakeZone[] = [
  { id: 'dhanmondi_lake', name: 'Dhanmondi Lake', x: -184, z: -32, w: 48, d: 34 },
  { id: 'hatirjheel_lake', name: 'Hatirjheel Lake', x: 104, z: -32, w: 50, d: 34 },
];

// Connected 4-tier Dhaka road hierarchy:
// Major North-South Avenues: x = 0 (16m), Boulevards: x = -64, +64, -144, +144, -224, +224 (13m)
// Major East-West Avenues: z = 0 (16m), Boulevards: z = -64, +64, -144, +144, -224 (13m), Riverfront z = 186 (14m)
// Secondary Streets & Old Dhaka / Motijheel / Mirpur connector alleys (8m)
export const ROAD_SEGMENTS: RoadSegment[] = [
  // Tier 1: Central Grand Avenues
  { id: 'ave_ns_central', x1: 0, z1: -244, x2: 0, z2: 192, width: 16, tier: 'avenue' },
  { id: 'ave_ew_central', x1: -244, z1: 0, x2: 244, z2: 0, width: 16, tier: 'avenue' },

  // Tier 2: Inner & Outer North-South Boulevards
  { id: 'blvd_ns_w1', x1: -64, z1: -232, x2: -64, z2: 192, width: 13, tier: 'boulevard' },
  { id: 'blvd_ns_e1', x1: 64, z1: -232, x2: 64, z2: 192, width: 13, tier: 'boulevard' },
  { id: 'blvd_ns_w2', x1: -144, z1: -232, x2: -144, z2: 192, width: 13, tier: 'boulevard' },
  { id: 'blvd_ns_e2', x1: 144, z1: -232, x2: 144, z2: 192, width: 13, tier: 'boulevard' },
  { id: 'blvd_ns_w3', x1: -224, z1: -232, x2: -224, z2: 192, width: 12, tier: 'boulevard' },
  { id: 'blvd_ns_e3', x1: 224, z1: -232, x2: 224, z2: 192, width: 12, tier: 'boulevard' },

  // Tier 2: Inner & Outer East-West Boulevards
  { id: 'blvd_ew_n1', x1: -232, z1: -64, x2: 232, z2: -64, width: 13, tier: 'boulevard' },
  { id: 'blvd_ew_s1', x1: -232, z1: 64, x2: 232, z2: 64, width: 13, tier: 'boulevard' },
  { id: 'blvd_ew_n2', x1: -232, z1: -144, x2: 232, z2: -144, width: 13, tier: 'boulevard' },
  { id: 'blvd_ew_s2', x1: -232, z1: 144, x2: 232, z2: 144, width: 13, tier: 'boulevard' },
  { id: 'blvd_ew_n3', x1: -232, z1: -224, x2: 232, z2: -224, width: 12, tier: 'boulevard' },

  // Riverfront Promenade Avenue (Sadarghat Launch & Ghat Road)
  { id: 'ave_sadarghat', x1: -232, z1: 186, x2: 232, z2: 186, width: 14, tier: 'avenue' },

  // Tier 3/4: Secondary District Streets & Old Dhaka Market Alleys
  { id: 'alley_old_dhaka_1', x1: -104, z1: 64, x2: -104, z2: 186, width: 8, tier: 'alley' },
  { id: 'alley_old_dhaka_2', x1: -144, z1: 104, x2: -64, z2: 104, width: 8, tier: 'alley' },
  { id: 'street_motijheel_1', x1: 104, z1: 0, x2: 104, z2: 144, width: 9, tier: 'street' },
  { id: 'street_motijheel_2', x1: 64, z1: 104, x2: 224, z2: 104, width: 9, tier: 'street' },
  { id: 'street_gulshan_n', x1: 104, z1: -224, x2: 104, z2: -64, width: 9, tier: 'street' },
  { id: 'street_mirpur_1', x1: -104, z1: -224, x2: -104, z2: -64, width: 9, tier: 'street' },
  { id: 'street_dhanmondi_s', x1: -224, z1: 104, x2: -144, z2: 104, width: 9, tier: 'street' },
];

export const POIS: PoiLocation[] = [
  // Preserved Core POIs
  { id: 'garage', kind: 'garage', name: 'Dhaka Customs Garage', banglaName: 'ঢাকা কাস্টমস গ্যারেজ', x: 34, z: -26, radius: 7.5 },
  { id: 'shop', kind: 'shop', name: "Babu's Hardware & Gear", banglaName: 'বাবু হার্ডওয়্যার ও শপ', x: -24, z: 24, radius: 6.0 },
  { id: 'police_hq', kind: 'police_hq', name: 'DMP Central Thana', banglaName: 'ডিএমপি থানা', x: 36, z: 26, radius: 6.5 },
  { id: 'tea_stall', kind: 'tea_stall', name: 'Rafiq Bhai Cha Stall', banglaName: 'রফিক ভাইয়ের চা স্টল', x: -16, z: 42, radius: 5.5 },

  // Expanded District POIs
  { id: 'sadarghat_pier', kind: 'sadarghat_pier', name: 'Sadarghat Launch Terminal', banglaName: 'সদরঘাট লঞ্চ টার্মিনাল', x: 0, z: 191, radius: 9.0 },
  { id: 'garage_motijheel', kind: 'garage', name: 'Motijheel Auto Works', banglaName: 'মতিঝিল অটো ওয়ার্কস', x: 162, z: 26, radius: 7.5 },
  { id: 'shop_dhanmondi', kind: 'shop', name: 'Dhanmondi Biriyani & Gear', banglaName: 'ধানমন্ডি বিরিয়ানি ও শপ', x: -166, z: 24, radius: 6.5 },
  { id: 'tea_sadarghat', kind: 'tea_stall', name: 'Buriganga Ghat Cha', banglaName: 'বুড়িগঙ্গা ঘাট চা স্টল', x: 24, z: 176, radius: 5.5 },
];

// Preserved & expanded hero core buildings (inside central blocks + signature district landmarks)
const HERO_BUILDINGS: BuildingSpec[] = [
  // === SOUTHWEST INNER BLOCK: Old Dhaka / Chawkbazar (x: -54..-14, z: 14..54) ===
  {
    id: 'b_shop_babu',
    x: -28, z: 22, w: 14, d: 11, h: 9.5,
    district: 'old_dhaka', style: 'shop',
    color: 0xc97b4b, accentColor: 0x8f2d24,
    signText: "BABU'S GEAR", signBangla: 'বাবু হার্ডওয়্যার',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_old_1',
    x: -46, z: 22, w: 13, d: 13, h: 12,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xd49a6a, accentColor: 0x1d6f5f,
    signText: 'CHAWK SPICE', signBangla: 'চক মশলা ঘর',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_old_2',
    x: -46, z: 43, w: 15, d: 12, h: 10.5,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xb95c44, accentColor: 0xe6a135,
    signText: 'PURAN BIRIYANI', signBangla: 'হাজী বিরিয়ানি',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_old_3',
    x: -26, z: 45, w: 12, d: 10, h: 8.5,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xcfa97e, accentColor: 0x2a6041,
    signText: 'RAFIQ CHA', signBangla: 'চা-এর দোকান',
    hasAwning: true, isHero: true,
  },

  // === SOUTHEAST INNER BLOCK: DMP Thana & Residential (x: 14..54, z: 14..54) ===
  {
    id: 'b_police_hq',
    x: 34, z: 22, w: 18, d: 12, h: 13,
    district: 'shapla_crossing', style: 'police',
    color: 0xdde5ed, accentColor: 0x1e3a8a,
    signText: 'DMP THANA', signBangla: 'ঢাকা মেট্রোপলিটন পুলিশ',
    hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_res_se1',
    x: 24, z: 44, w: 14, d: 12, h: 15,
    district: 'old_dhaka', style: 'residential',
    color: 0xd6c7b2, accentColor: 0x9b3a2e,
    hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_res_se2',
    x: 45, z: 44, w: 14, d: 13, h: 18,
    district: 'old_dhaka', style: 'residential',
    color: 0xc2b29b, accentColor: 0x1b5e4a,
    hasWaterTank: true, isHero: true,
  },

  // === NORTHEAST INNER BLOCK: Gulshan Commercial + Dhaka Customs Garage ===
  {
    id: 'b_garage',
    x: 36, z: -20, w: 16, d: 12, h: 8.5,
    district: 'gulshan', style: 'garage',
    color: 0x334155, accentColor: 0xf59e0b,
    signText: 'DHAKA CUSTOMS', signBangla: 'ঢাকা গ্যারেজ',
    hasAwning: true, isHero: true,
  },
  {
    id: 'b_gulshan_1',
    x: 22, z: -43, w: 14, d: 14, h: 34,
    district: 'gulshan', style: 'gulshan_tower',
    color: 0x3b6e8c, accentColor: 0x94d2bd,
    signText: 'PADMA TOWER', signBangla: 'পদ্মা টাওয়ার',
    hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_gulshan_2',
    x: 45, z: -43, w: 15, d: 15, h: 42,
    district: 'gulshan', style: 'gulshan_tower',
    color: 0x2c4969, accentColor: 0xe9c46a,
    signText: 'GULSHAN PLAZA', signBangla: 'গুলশান প্লাজা',
    isHero: true,
  },

  // === NORTHWEST INNER BLOCK: Kawran Bazar & Warehouses ===
  {
    id: 'b_kawran_1',
    x: -26, z: -23, w: 16, d: 13, h: 14,
    district: 'kawran_flyover', style: 'market_hall',
    color: 0xb8a996, accentColor: 0x0f766e,
    signText: 'KAWRAN BAZAR', signBangla: 'কারওয়ান বাজার',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_kawran_2',
    x: -46, z: -24, w: 14, d: 14, h: 11,
    district: 'kawran_flyover', style: 'warehouse',
    color: 0x7c6f64, accentColor: 0xb45309,
    signText: 'SYNDICATE DEPOT', signBangla: 'গুদাম ঘর',
    isHero: true,
  },
  {
    id: 'b_kawran_3',
    x: -26, z: -44, w: 15, d: 13, h: 24,
    district: 'kawran_flyover', style: 'gulshan_tower',
    color: 0x475569, accentColor: 0x38bdf8,
    hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_kawran_4',
    x: -46, z: -45, w: 13, d: 12, h: 16,
    district: 'kawran_flyover', style: 'residential',
    color: 0xcab8a2, accentColor: 0x991b1b,
    hasWaterTank: true, isHero: true,
  },

  // === EXPANDED DISTRICT HERO LANDMARKS ===
  // Motijheel Commercial Hub (East)
  {
    id: 'b_motijheel_bank',
    x: 126, z: 32, w: 18, d: 18, h: 52,
    district: 'motijheel', style: 'motijheel_tower',
    color: 0x264653, accentColor: 0xe9c46a,
    signText: 'BENGAL BANK', signBangla: 'বেঙ্গল ব্যাংক ভবন',
    isHero: true,
  },
  {
    id: 'b_motijheel_plaza',
    x: 182, z: 32, w: 20, d: 16, h: 46,
    district: 'motijheel', style: 'motijheel_tower',
    color: 0x334e68, accentColor: 0xf59e0b,
    signText: 'MOTIJHEEL TOWER', signBangla: 'মতিঝিল টাওয়ার',
    isHero: true,
  },
  {
    id: 'b_motijheel_garage',
    x: 162, z: 18, w: 15, d: 11, h: 8.5,
    district: 'motijheel', style: 'garage',
    color: 0x374151, accentColor: 0xf59e0b,
    signText: 'MOTIJHEEL AUTO', signBangla: 'মতিঝিল অটো ওয়ার্কস',
    hasAwning: true, isHero: true,
  },

  // Dhanmondi Shop & Cultural Hall (West)
  {
    id: 'b_dhanmondi_shop',
    x: -168, z: 18, w: 15, d: 11, h: 10,
    district: 'dhanmondi', style: 'shop',
    color: 0xc8845c, accentColor: 0x0f766e,
    signText: 'DHANMONDI GEAR', signBangla: 'ধানমন্ডি বিরিয়ানি',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
  {
    id: 'b_dhanmondi_center',
    x: -184, z: 42, w: 20, d: 14, h: 16,
    district: 'dhanmondi', style: 'residential',
    color: 0xd8c3a5, accentColor: 0x9b2226,
    signText: 'SATMASJID PLAZA', signBangla: 'সাতমসজিদ প্লাজা',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },

  // Sadarghat River Terminal Building (South)
  {
    id: 'b_sadarghat_terminal',
    x: 26, z: 166, w: 24, d: 14, h: 11.5,
    district: 'sadarghat', style: 'market_hall',
    color: 0xc86d48, accentColor: 0x0f766e,
    signText: 'SADARGHAT PORT', signBangla: 'সদরঘাট লঞ্চ ঘাট',
    hasAwning: true, isHero: true,
  },
  {
    id: 'b_old_dhaka_bazar_hall',
    x: -34, z: 164, w: 22, d: 14, h: 10.5,
    district: 'old_dhaka', style: 'old_dhaka',
    color: 0xb85d43, accentColor: 0xe6a135,
    signText: 'SHAKHARI BAZAR', signBangla: 'শাঁখারী বাজার',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },

  // Banani North Diplomatic Tower (North)
  {
    id: 'b_banani_north_tower',
    x: 34, z: -184, w: 20, d: 18, h: 48,
    district: 'uttara_north', style: 'gulshan_tower',
    color: 0x1d3557, accentColor: 0x38bdf8,
    signText: 'BANANI HEIGHTS', signBangla: 'বনানী হাইটস',
    isHero: true,
  },

  // Mirpur Bazaar Emporium (Northwest)
  {
    id: 'b_mirpur_market',
    x: -124, z: -184, w: 20, d: 16, h: 15,
    district: 'mirpur_market', style: 'market_hall',
    color: 0xb59c84, accentColor: 0xd97706,
    signText: 'MIRPUR BAZAAR', signBangla: 'মিরপুর বাজার',
    hasAwning: true, hasWaterTank: true, isHero: true,
  },
];

// Deterministic PRNG (Mulberry32) so expanded city layout is 100% reproducible
function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Checks whether an axis-aligned footprint [x - w/2, x + w/2] x [z - d/2, z + d/2]
 * clears all roads, lakes, roundabout, flyover piers, POIs, and existing hero buildings.
 */
export function isFootprintClearOfRoads(
  x: number,
  z: number,
  w: number,
  d: number,
  margin = 3.2,
): boolean {
  const minX = x - w / 2 - margin;
  const maxX = x + w / 2 + margin;
  const minZ = z - d / 2 - margin;
  const maxZ = z + d / 2 + margin;

  // Keep inside playable area and north of riverfront road
  if (minX < -WORLD_HALF_SIZE + 8 || maxX > WORLD_HALF_SIZE - 8) return false;
  if (minZ < -WORLD_HALF_SIZE + 8 || maxZ > RIVER_Z_START - 20) return false;

  // Keep clear of central Shapla roundabout
  if (Math.hypot(x, z) < 22 + Math.max(w, d) * 0.5) return false;

  // Check every axis-aligned road segment
  for (const seg of ROAD_SEGMENTS) {
    const rMinX = Math.min(seg.x1, seg.x2) - seg.width / 2;
    const rMaxX = Math.max(seg.x1, seg.x2) + seg.width / 2;
    const rMinZ = Math.min(seg.z1, seg.z2) - seg.width / 2;
    const rMaxZ = Math.max(seg.z1, seg.z2) + seg.width / 2;
    if (maxX > rMinX && minX < rMaxX && maxZ > rMinZ && minZ < rMaxZ) {
      return false;
    }
  }

  // Keep clear of Dhanmondi Lake
  for (const lake of LAKE_ZONES) {
    if (
      maxX > lake.x - lake.w / 2 - 4 &&
      minX < lake.x + lake.w / 2 + 4 &&
      maxZ > lake.z - lake.d / 2 - 4 &&
      minZ < lake.z + lake.d / 2 + 4
    ) {
      return false;
    }
  }

  // Keep clear of elevated flyover corridor (x = -56.2, z = -195..125)
  if (maxX > -61 && minX < -51 && maxZ > -198 && minZ < 128) return false;

  return true;
}

function generateExpandedCityBuildings(): BuildingSpec[] {
  const rand = mulberry32(20260711);
  const all: BuildingSpec[] = [...HERO_BUILDINGS];

  const oldDhakaPalette = [0xc97b4b, 0xb95c44, 0xd49a6a, 0xcfa97e, 0xa8553b, 0xd9a066];
  const oldDhakaAccents = [0x1d6f5f, 0x8f2d24, 0xe6a135, 0x2a6041, 0x9a3412];
  const gulshanPalette = [0x2b4c70, 0x3b6e8c, 0x243b53, 0x335c67, 0x1e3a5f, 0x3f5e78];
  const motijheelPalette = [0x334155, 0x264653, 0x475569, 0x1e3a5f, 0x52606d];
  const resPalette = [0xd6c7b2, 0xc2b29b, 0xd4c2aa, 0xb8a996, 0xc9b59c, 0xded2c1];
  const resAccents = [0x9b3a2e, 0x0f766e, 0xb45309, 0x1b5e4a, 0x475569];

  // Grid cell centers between avenues/boulevards:
  // X spans: [-256..-230], [-218..-150], [-138..-70], [-58..-8], [8..58], [70..138], [150..218], [230..256]
  // Z spans: [-256..-230], [-218..-150], [-138..-70], [-58..-8], [8..58], [70..138], [150..178]
  const xBands: Array<[number, number]> = [
    [-256, -234],
    [-212, -154],
    [-132, -74],
    [-50, -14],
    [14, 52],
    [74, 132],
    [154, 212],
    [234, 256],
  ];
  const zBands: Array<[number, number]> = [
    [-256, -234],
    [-212, -154],
    [-132, -74],
    [-52, -14],
    [14, 52],
    [74, 132],
    [154, 174],
  ];

  let genIdx = 0;

  for (const [bx1, bx2] of xBands) {
    for (const [bz1, bz2] of zBands) {
      // Skip the 4 inner hero blocks around Shapla Crossing (already hand-crafted in HERO_BUILDINGS)
      const isInnerCore = bx1 >= -52 && bx2 <= 54 && bz1 >= -54 && bz2 <= 54;
      if (isInnerCore) continue;

      const blockW = bx2 - bx1;
      const blockD = bz2 - bz1;
      const cols = blockW > 42 ? 2 : 1;
      const rows = blockD > 42 ? 2 : 1;

      for (let c = 0; c < cols; c++) {
        for (let r = 0; r < rows; r++) {
          const cellW = blockW / cols;
          const cellD = blockD / rows;
          const cx = bx1 + (c + 0.5) * cellW + (rand() - 0.5) * 3.5;
          const cz = bz1 + (r + 0.5) * cellD + (rand() - 0.5) * 3.5;

          const district = getDistrictAt(cx, cz);

          // Determine authentic Dhaka architectural typology per district
          let style: BuildingStyle = 'residential';
          let w = 13 + rand() * 7;
          let d = 12 + rand() * 6;
          let h = 12 + rand() * 10;
          let color = resPalette[Math.floor(rand() * resPalette.length)];
          let accentColor = resAccents[Math.floor(rand() * resAccents.length)];
          let hasAwning = false;
          let hasWaterTank = rand() > 0.3;

          if (district === 'old_dhaka' || district === 'sadarghat') {
            style = rand() < 0.78 ? 'old_dhaka' : 'market_hall';
            w = 12 + rand() * 6;
            d = 11 + rand() * 5;
            h = 7.5 + rand() * 7.5;
            color = oldDhakaPalette[Math.floor(rand() * oldDhakaPalette.length)];
            accentColor = oldDhakaAccents[Math.floor(rand() * oldDhakaAccents.length)];
            hasAwning = rand() < 0.72;
          } else if (district === 'gulshan' || district === 'uttara_north') {
            style = rand() < 0.74 ? 'gulshan_tower' : 'residential';
            w = 14 + rand() * 6;
            d = 13 + rand() * 6;
            h = style === 'gulshan_tower' ? 24 + rand() * 24 : 15 + rand() * 10;
            color =
              style === 'gulshan_tower'
                ? gulshanPalette[Math.floor(rand() * gulshanPalette.length)]
                : resPalette[Math.floor(rand() * resPalette.length)];
            accentColor = rand() < 0.5 ? 0x38bdf8 : 0xe9c46a;
            hasWaterTank = style !== 'gulshan_tower' || rand() < 0.4;
          } else if (district === 'motijheel') {
            style = rand() < 0.68 ? 'motijheel_tower' : 'residential';
            w = 14 + rand() * 7;
            d = 13 + rand() * 6;
            h = style === 'motijheel_tower' ? 26 + rand() * 26 : 16 + rand() * 9;
            color = motijheelPalette[Math.floor(rand() * motijheelPalette.length)];
            accentColor = rand() < 0.5 ? 0xf59e0b : 0x94d2bd;
          } else if (district === 'kawran_flyover' || district === 'mirpur_market') {
            const roll = rand();
            style = roll < 0.35 ? 'warehouse' : roll < 0.65 ? 'market_hall' : 'residential';
            w = 13 + rand() * 7;
            d = 12 + rand() * 6;
            h = style === 'warehouse' ? 9 + rand() * 5 : 13 + rand() * 11;
            color = style === 'warehouse' ? 0x736b63 : resPalette[Math.floor(rand() * resPalette.length)];
            hasAwning = style === 'market_hall';
          } else if (district === 'dhanmondi') {
            style = rand() < 0.8 ? 'residential' : 'old_dhaka';
            w = 13 + rand() * 5;
            d = 12 + rand() * 5;
            h = 13 + rand() * 11;
            color = resPalette[Math.floor(rand() * resPalette.length)];
            hasAwning = rand() < 0.25;
          }

          // Clamp size nicely inside cell
          w = Math.min(w, Math.max(9, cellW - 5));
          d = Math.min(d, Math.max(9, cellD - 5));

          if (!isFootprintClearOfRoads(cx, cz, w, d, 3.6)) continue;

          // Ensure no overlap with any already placed building or POI
          let overlapsExisting = false;
          for (const ex of all) {
            if (
              Math.abs(cx - ex.x) < (w + ex.w) * 0.5 + 3.2 &&
              Math.abs(cz - ex.z) < (d + ex.d) * 0.5 + 3.2
            ) {
              overlapsExisting = true;
              break;
            }
          }
          if (overlapsExisting) continue;

          for (const poi of POIS) {
            if (Math.hypot(cx - poi.x, cz - poi.z) < Math.max(w, d) * 0.5 + poi.radius + 2) {
              overlapsExisting = true;
              break;
            }
          }
          if (overlapsExisting) continue;

          all.push({
            id: `b_gen_${++genIdx}`,
            x: Math.round(cx * 10) / 10,
            z: Math.round(cz * 10) / 10,
            w: Math.round(w * 10) / 10,
            d: Math.round(d * 10) / 10,
            h: Math.round(h * 10) / 10,
            district,
            style,
            color,
            accentColor,
            hasAwning,
            hasWaterTank,
            isHero: false,
          });
        }
      }
    }
  }

  return all;
}

export const BUILDINGS: BuildingSpec[] = generateExpandedCityBuildings();

// Extended Elevated Dhaka Express Flyover pillars along x = -56.2 (Kawran -> Mirpur -> Old Dhaka edge)
export const FLYOVER_PILLARS: Vec2[] = [
  { x: -56.2, z: -182 },
  { x: -56.2, z: -156 },
  { x: -56.2, z: -128 },
  { x: -56.2, z: -100 },
  { x: -56.2, z: -76 },
  { x: -56.2, z: -50 },
  { x: -56.2, z: -24 },
  { x: -56.2, z: 16 },
  { x: -56.2, z: 42 },
  { x: -56.2, z: 80 },
  { x: -56.2, z: 108 },
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

  // Dhanmondi Lake water barrier collider (keep cars out of the lake; footbridge spans center)
  for (const lake of LAKE_ZONES) {
    list.push({
      id: `collider_${lake.id}`,
      minX: lake.x - lake.w / 2,
      maxX: lake.x + lake.w / 2,
      minZ: lake.z - lake.d / 2,
      maxZ: lake.z + lake.d / 2,
      height: 2,
      kind: 'wall',
    });
  }

  // Flyover concrete piers
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

// 8 Connected Dhaka traffic loops covering core avenues, inner ring, outer Motijheel/Dhanmondi/Banani ring & Sadarghat
export const TRAFFIC_ROUTES: Vec2[][] = [
  // 0: Inner Counter-Clockwise Ring Road
  [
    { x: 61, z: 61 },
    { x: 61, z: -61 },
    { x: -61, z: -61 },
    { x: -61, z: 61 },
  ],
  // 1: Inner Clockwise Ring Road
  [
    { x: -67, z: 67 },
    { x: -67, z: -67 },
    { x: 67, z: -67 },
    { x: 67, z: 67 },
  ],
  // 2: Central North-South Grand Avenue (Sadarghat <-> Shapla <-> Banani North)
  [
    { x: 4.2, z: 180 },
    { x: 4.2, z: 12 },
    { x: 10, z: 6 },
    { x: 10, z: -6 },
    { x: 4.2, z: -12 },
    { x: 4.2, z: -218 },
    { x: -4.2, z: -218 },
    { x: -4.2, z: -12 },
    { x: -10, z: -6 },
    { x: -10, z: 6 },
    { x: -4.2, z: 12 },
    { x: -4.2, z: 180 },
  ],
  // 3: Central East-West Grand Avenue (Dhanmondi <-> Shapla <-> Motijheel)
  [
    { x: -218, z: 4.2 },
    { x: -12, z: 4.2 },
    { x: -6, z: 10 },
    { x: 6, z: 10 },
    { x: 12, z: 4.2 },
    { x: 218, z: 4.2 },
    { x: 218, z: -4.2 },
    { x: 12, z: -4.2 },
    { x: 6, z: -10 },
    { x: -6, z: -10 },
    { x: -12, z: -4.2 },
    { x: -218, z: -4.2 },
  ],
  // 4: Mid-Outer Dhaka Ring (Motijheel <-> Gulshan North <-> Mirpur <-> Old Dhaka)
  [
    { x: 141, z: 141 },
    { x: 141, z: -141 },
    { x: -141, z: -141 },
    { x: -141, z: 141 },
  ],
  // 5: Sadarghat Riverfront & Old Dhaka Loop
  [
    { x: -141, z: 183 },
    { x: 141, z: 183 },
    { x: 141, z: 147 },
    { x: -141, z: 147 },
  ],
  // 6: Outer Grand Perimeter (Dhanmondi <-> Banani North <-> Motijheel East <-> Sadarghat)
  [
    { x: 221, z: 183 },
    { x: 221, z: -221 },
    { x: -221, z: -221 },
    { x: -221, z: 183 },
  ],
  // 7: Motijheel Commercial & Gulshan Shuttle
  [
    { x: 67, z: -141 },
    { x: 147, z: -141 },
    { x: 147, z: 141 },
    { x: 67, z: 141 },
  ],
];

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
  // Hero parked vehicles cleanly curbside near player spawn (-2.2, 35.2), clear of moving lanes
  { id: 'v_starter_cng', kind: 'cng', x: -6.4, z: 33.8, heading: Math.PI, color: 0x0f6a44, driver: 'none' },
  { id: 'v_starter_sedan', kind: 'sedan', x: 6.4, z: 27.5, heading: Math.PI, color: 0xe2e8f0, driver: 'none' },
  { id: 'v_starter_rickshaw', kind: 'rickshaw', x: -12, z: 39, heading: Math.PI * 0.5, color: 0xc62828, driver: 'none' },
  { id: 'v_starter_bike', kind: 'bike', x: 6.6, z: 35.5, heading: Math.PI, color: 0xdc2626, driver: 'none' },

  // Parked vehicles across districts (Garages, Sadarghat, Motijheel, Dhanmondi, Police HQ)
  { id: 'v_garage_sedan', kind: 'sedan', x: 25, z: -26, heading: Math.PI * 0.5, color: 0x1d4ed8, driver: 'none' },
  { id: 'v_sadarghat_truck', kind: 'truck', x: -18, z: 184, heading: Math.PI * 0.5, color: 0xd97706, driver: 'none' },
  { id: 'v_bus_stop', kind: 'bus', x: 22, z: 5.8, heading: -Math.PI * 0.5, color: 0xb81d24, driver: 'none' },
  { id: 'v_police_hq_1', kind: 'police', x: 24, z: 26, heading: Math.PI, color: 0xf1f5f9, driver: 'none' },
  { id: 'v_motijheel_sedan', kind: 'sedan', x: 152, z: 24, heading: Math.PI, color: 0x0f172a, driver: 'none' },
  { id: 'v_dhanmondi_cng', kind: 'cng', x: -156, z: 22, heading: 0, color: 0x0f6a44, driver: 'none' },
  { id: 'v_banani_bike', kind: 'bike', x: 18, z: -148, heading: Math.PI * 0.5, color: 0x0284c7, driver: 'none' },
  { id: 'v_sadarghat_rickshaw', kind: 'rickshaw', x: 16, z: 182, heading: -Math.PI * 0.5, color: 0xd97706, driver: 'none' },

  // Live moving traffic across inner & expanded Dhaka avenues (20 active traffic vehicles)
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

  // Expanded outer ring & Sadarghat / Motijheel / Dhanmondi traffic
  { id: 't_bus_outer1', kind: 'bus', x: 141, z: 45, heading: Math.PI, color: 0xb81d24, driver: 'traffic', routeIdx: 4, waypointIdx: 1 },
  { id: 't_cng_outer1', kind: 'cng', x: 40, z: -141, heading: -Math.PI * 0.5, color: 0x0f6a44, driver: 'traffic', routeIdx: 4, waypointIdx: 2 },
  { id: 't_truck_outer1', kind: 'truck', x: -141, z: -20, heading: 0, color: 0xd97706, driver: 'traffic', routeIdx: 4, waypointIdx: 3 },
  { id: 't_sedan_outer1', kind: 'sedan', x: -45, z: 141, heading: Math.PI * 0.5, color: 0xe2e8f0, driver: 'traffic', routeIdx: 4, waypointIdx: 0 },
  { id: 't_cng_river1', kind: 'cng', x: -45, z: 183, heading: Math.PI * 0.5, color: 0x0f6a44, driver: 'traffic', routeIdx: 5, waypointIdx: 1 },
  { id: 't_rick_river1', kind: 'rickshaw', x: 60, z: 147, heading: -Math.PI * 0.5, color: 0xc62828, driver: 'traffic', routeIdx: 5, waypointIdx: 3 },
  { id: 't_bus_grand1', kind: 'bus', x: 221, z: -60, heading: Math.PI, color: 0x15803d, driver: 'traffic', routeIdx: 6, waypointIdx: 1 },
  { id: 't_sedan_grand1', kind: 'sedan', x: -221, z: 30, heading: 0, color: 0x1d4ed8, driver: 'traffic', routeIdx: 6, waypointIdx: 3 },
  { id: 't_bike_moti1', kind: 'bike', x: 147, z: -30, heading: 0, color: 0xdc2626, driver: 'traffic', routeIdx: 7, waypointIdx: 2 },
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
        dialogueEnglish: 'Time to remind them whose streets these are.',
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
        title: 'Reach Sadarghat Launch Terminal',
        banglaTitle: 'সদরঘাট লঞ্চ ঘাটে যাও',
        objective: 'Drive south down the avenue to Sadarghat Riverfront before the Syndicate moves the truck',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'তারেক, সদরঘাটে ওদের একটা ট্রাক ঢুকেছে। ওটা দখল করতে পারলে সব প্রমাণ পাবো।',
        dialogueEnglish: "Tariq, their cargo truck just docked at Sadarghat. Seize it and we'll have the proof.",
        targetX: -8,
        targetZ: 184,
        radius: 11,
        kind: 'goto',
      },
      {
        id: 'm2_guards',
        title: 'Neutralize Pier Guards',
        banglaTitle: 'ঘাটের পাহারাদারদের সরাও',
        objective: 'Eliminate the 4 Syndicate guards protecting the cargo truck at Sadarghat',
        targetX: -14,
        targetZ: 184,
        radius: 16,
        kind: 'eliminate',
        requiredKills: 4,
      },
      {
        id: 'm2_deliver',
        title: 'Deliver the Cargo Truck',
        banglaTitle: 'ট্রাকটি গ্যারেজে নিয়ে যাও',
        objective: 'Get into the Bengal Cargo Truck [F] and drive it north to Dhaka Customs Garage',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'দারুণ! পুলিশ আসার আগেই ট্রাকটা নিয়ে গ্যারেজে ঢুকে পড়ো।',
        dialogueEnglish: 'Nice work! Get that truck up to Dhaka Customs before DMP locks down the avenue.',
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
    id: 'm4_motijheel_vault',
    index: 3,
    title: 'Motijheel Reckoning',
    banglaTitle: 'মতিঝিল অভিযান',
    district: 'motijheel',
    rewardMoney: 5500,
    steps: [
      {
        id: 'm4_moti_arrive',
        title: 'Infiltrate Motijheel Hub',
        banglaTitle: 'মতিঝিল বাণিজ্যিক এলাকায় যাও',
        objective: 'Drive east into Motijheel Commercial Hub to cut off the Syndicate cash line',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'ওদের আসল টাকার উৎস মতিঝিলে! বেঙ্গল ব্যাংক প্লাজার সামনে ওদের গার্ড আছে।',
        dialogueEnglish: 'Their real money flows through Motijheel! Syndicate guards are holding Bengal Bank Plaza.',
        targetX: 144,
        targetZ: 34,
        radius: 12,
        kind: 'goto',
      },
      {
        id: 'm4_moti_clear',
        title: 'Break the Motijheel Guard',
        banglaTitle: 'সিন্ডিকেট গার্ডদের পরাস্ত করো',
        objective: 'Defeat the 5 Syndicate enforcers protecting the Motijheel financial plaza',
        targetX: 144,
        targetZ: 34,
        radius: 18,
        kind: 'eliminate',
        requiredKills: 5,
      },
    ],
  },
  {
    id: 'm5_king_of_dhaka',
    index: 4,
    title: 'King of Dhaka',
    banglaTitle: 'ঢাকার রাজা',
    district: 'gulshan',
    rewardMoney: 9000,
    steps: [
      {
        id: 'm5_plaza',
        title: 'Storm Banani Heights',
        banglaTitle: 'বনানী হাইটসে অভিযান',
        objective: 'Race north to Banani Heights for the final showdown with the Padma Boss',
        dialogueSpeaker: 'Nadia Rahman',
        dialogueBangla: 'শেষ লড়াই, তারেক! বনানী হাইটসে ওদের বস পালানোর চেষ্টা করছে।',
        dialogueEnglish: 'Final showdown, Tariq! The Padma Boss is cornered up at Banani Heights.',
        targetX: 12,
        targetZ: -178,
        radius: 13,
        kind: 'goto',
      },
      {
        id: 'm5_showdown',
        title: 'Dismantle the Syndicate',
        banglaTitle: 'সিন্ডিকেটের পতন ঘটাও',
        objective: 'Defeat the Padma Syndicate Boss and his elite guards',
        targetX: 12,
        targetZ: -178,
        radius: 20,
        kind: 'eliminate',
        requiredKills: 5,
      },
      {
        id: 'm5_shapla',
        title: 'Claim Shapla Crossing',
        banglaTitle: 'শাপলা চত্বরে বিজয়',
        objective: 'Return to Shapla Monument Crossing to reclaim the city',
        dialogueSpeaker: 'Rafiq Bhai',
        dialogueBangla: 'পুরো ঢাকা আজ মুক্ত! শাপলা চত্বরে আয়, সবাই তোর জন্য অপেক্ষা করছে।',
        dialogueEnglish: 'Dhaka is free! Roll back into Shapla Crossing—the whole city is yours.',
        targetX: 0,
        targetZ: 14,
        radius: 10,
        kind: 'goto',
      },
    ],
  },
];

export const FARE_DESTINATIONS: Array<{ label: string; x: number; z: number }> = [
  { label: 'Gulshan Plaza', x: 64, z: -48 },
  { label: 'Sadarghat Launch Terminal', x: 0, z: 186 },
  { label: 'Motijheel Bank Plaza', x: 144, z: 36 },
  { label: 'Dhanmondi Lakefront', x: -144, z: -32 },
  { label: 'Kawran Bazar Flyover', x: -64, z: -24 },
  { label: 'Chawkbazar Spice Alley', x: -104, z: 104 },
  { label: 'Banani North Diplomatic', x: 64, z: -180 },
  { label: 'Mirpur Bazaar Corridor', x: -144, z: -180 },
  { label: 'Shakhari Bazar Old Dhaka', x: -64, z: 144 },
  { label: 'Motijheel East Ring', x: 224, z: 64 },
  { label: 'DMP Central Crossing', x: 64, z: 24 },
  { label: 'Dhanmondi Satmasjid Road', x: -224, z: 48 },
  { label: 'Hatirjheel Overpass', x: 104, z: -64 },
];
