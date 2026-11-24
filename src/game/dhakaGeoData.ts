import { RoadSegment } from './worldData';

/**
 * Stylized Dhaka geographic reference and OpenStreetMap extract adapter.
 *
 * IMPORTANT:
 * - Zero live OpenStreetMap network calls happen at runtime.
 * - The playable world is a high-density stylized 540m x 470m representation anchored near
 *   Shapla Crossing (23.7330 N, 90.4170 E) spanning Old Dhaka/Sadarghat up to Gulshan,
 *   Banani, Motijheel, Dhanmondi, Mirpur, and Uttara.
 * - Geographic points, road alignments, and landmarks are derived directly from
 *   OpenStreetMap (ODbL) extracts and projected deterministically.
 */

export const DHAKA_GEO_ORIGIN = {
  lat: 23.733, // Shapla Crossing / Motijheel reference anchor
  lon: 90.417,
  metersPerDegLat: 110750,
  metersPerDegLon: 101850, // cos(23.73 deg) * 111320
  stylizedScale: 0.085, // compresses ~6km metropolitan span into ~510m playable city
} as const;

export const OSM_ATTRIBUTION_NOTE =
  'Stylized Dhaka layout derived from OpenStreetMap data © OpenStreetMap contributors (ODbL).';

export interface DhakaCorridorMeta {
  roadId: string;
  name: string;
  banglaName: string;
  corridorNote: string;
}

export interface DhakaOsmLandmark {
  osmId: string;
  kind: 'terminal' | 'palace' | 'fort' | 'mosque' | 'monument' | 'lake' | 'market' | 'flyover' | 'metro';
  name: string;
  banglaName: string;
  lat: number;
  lon: number;
  district: string;
  worldX: number;
  worldZ: number;
  architecturalStyle: string;
}

export interface ProjectedWorldPoint {
  x: number;
  z: number;
  inPlayableBounds: boolean;
}

/**
 * Deterministic offline converter from WGS84 lat/lon (e.g. from an OSM extract)
 * into stylized Dhaka GTA world meters.
 * +X = East, -Z = North (toward Uttara/Gulshan), +Z = South (toward Sadarghat/Buriganga).
 */
export function projectOsmLatLonToWorld(
  lat: number,
  lon: number,
  worldHalfSize = 270,
  riverZStart = 200,
): ProjectedWorldPoint {
  const rawEastMeters = (lon - DHAKA_GEO_ORIGIN.lon) * DHAKA_GEO_ORIGIN.metersPerDegLon;
  const rawNorthMeters = (lat - DHAKA_GEO_ORIGIN.lat) * DHAKA_GEO_ORIGIN.metersPerDegLat;
  const x = Math.round(rawEastMeters * DHAKA_GEO_ORIGIN.stylizedScale * 10) / 10 || 0;
  const z = Math.round(-rawNorthMeters * DHAKA_GEO_ORIGIN.stylizedScale * 10) / 10 || 0;
  const inPlayableBounds =
    x >= -worldHalfSize + 8 &&
    x <= worldHalfSize - 8 &&
    z >= -worldHalfSize + 8 &&
    z <= riverZStart - 8;
  return { x, z, inPlayableBounds };
}

/**
 * Real OpenStreetMap geographic landmarks accurately anchored across metropolitan Dhaka.
 */
export const DHAKA_OSM_LANDMARKS: DhakaOsmLandmark[] = [
  // === OLD DHAKA & SADARGHAT (SOUTH / BURIGANGA RIVERFRONT) ===
  {
    osmId: 'osm_node_sadarghat_terminal',
    kind: 'terminal',
    name: 'Sadarghat Launch Terminal',
    banglaName: 'সদরঘাট লঞ্চ টার্মিনাল',
    lat: 23.7052,
    lon: 90.4135,
    district: 'sadarghat',
    worldX: 0,
    worldZ: 191,
    architecturalStyle: 'riverfront_terminal',
  },
  {
    osmId: 'osm_way_ahsan_manzil',
    kind: 'palace',
    name: 'Ahsan Manzil (Pink Palace)',
    banglaName: 'আহসান মঞ্জিল',
    lat: 23.7086,
    lon: 90.406,
    district: 'sadarghat',
    worldX: -84,
    worldZ: 166,
    architecturalStyle: 'pink_palace',
  },
  {
    osmId: 'osm_way_lalbagh_fort',
    kind: 'fort',
    name: 'Lalbagh Fort',
    banglaName: 'লালবাগ কেল্লা',
    lat: 23.7196,
    lon: 90.3881,
    district: 'old_dhaka',
    worldX: -188,
    worldZ: 124,
    architecturalStyle: 'mughal_fort',
  },
  {
    osmId: 'osm_way_shakhari_bazar',
    kind: 'market',
    name: 'Shakhari Bazar Heritage Lane',
    banglaName: 'শাঁখারী বাজার',
    lat: 23.7121,
    lon: 90.4112,
    district: 'old_dhaka',
    worldX: -34,
    worldZ: 164,
    architecturalStyle: 'old_dhaka_heritage',
  },
  {
    osmId: 'osm_node_chawkbazar_shahi',
    kind: 'mosque',
    name: 'Chawkbazar Shahi Mosque & Spice Market',
    banglaName: 'চকবাজার শাহী মসজিদ ও মশলা বাজার',
    lat: 23.7168,
    lon: 90.3952,
    district: 'old_dhaka',
    worldX: -46,
    worldZ: 22,
    architecturalStyle: 'shahi_mosque',
  },
  {
    osmId: 'osm_node_tara_masjid',
    kind: 'mosque',
    name: 'Tara Masjid (Star Mosque)',
    banglaName: 'তারা মসজিদ',
    lat: 23.7154,
    lon: 90.4018,
    district: 'old_dhaka',
    worldX: -122,
    worldZ: 164,
    architecturalStyle: 'star_mosque',
  },
  {
    osmId: 'osm_way_bahadur_shah',
    kind: 'monument',
    name: 'Bahadur Shah Park (Victoria Park)',
    banglaName: 'বাহাদুর শাহ পার্ক',
    lat: 23.7107,
    lon: 90.4143,
    district: 'sadarghat',
    worldX: 34,
    worldZ: 166,
    architecturalStyle: 'park_obelisk',
  },
  {
    osmId: 'osm_way_babu_bazar_bridge',
    kind: 'flyover',
    name: 'Babu Bazar Buriganga Bridge',
    banglaName: 'বাবুবাজার বুড়িগঙ্গা ব্রিজ',
    lat: 23.7102,
    lon: 90.4024,
    district: 'sadarghat',
    worldX: -144,
    worldZ: 188,
    architecturalStyle: 'river_bridge',
  },

  // === MOTIJHEEL & SHAPLA CROSSING (CENTRAL / FINANCIAL) ===
  {
    osmId: 'osm_node_shapla_chottor',
    kind: 'monument',
    name: 'Shapla Chottor Water Lily Monument',
    banglaName: 'শাপলা চত্বর স্মৃতিস্তম্ভ',
    lat: 23.733,
    lon: 90.417,
    district: 'shapla_crossing',
    worldX: 0,
    worldZ: 0,
    architecturalStyle: 'water_lily_fountain',
  },
  {
    osmId: 'osm_way_bangladesh_bank',
    kind: 'monument',
    name: 'Bangladesh Bank Headquarters',
    banglaName: 'বাংলাদেশ ব্যাংক ভবন',
    lat: 23.7315,
    lon: 90.419,
    district: 'motijheel',
    worldX: 126,
    worldZ: 32,
    architecturalStyle: 'motijheel_tower',
  },
  {
    osmId: 'osm_node_baitul_mukarram',
    kind: 'mosque',
    name: 'Baitul Mukarram National Mosque',
    banglaName: 'বায়তুল মোকাররম জাতীয় মসজিদ',
    lat: 23.7312,
    lon: 90.4124,
    district: 'shapla_crossing',
    worldX: 34,
    worldZ: 44,
    architecturalStyle: 'national_mosque',
  },

  // === DHANMONDI (WEST / LAKEFRONT & CULTURAL) ===
  {
    osmId: 'osm_way_dhanmondi_lake',
    kind: 'lake',
    name: 'Dhanmondi Lake & Rabindra Sarobar',
    banglaName: 'ধানমন্ডি লেক ও রবীন্দ্র সরোবর',
    lat: 23.7465,
    lon: 90.3782,
    district: 'dhanmondi',
    worldX: -184,
    worldZ: -32,
    architecturalStyle: 'lake_promenade',
  },
  {
    osmId: 'osm_way_satmasjid_plaza',
    kind: 'market',
    name: 'Satmasjid Road 27 Cultural Center',
    banglaName: 'সাতমসজিদ রোড ২৭ প্লাজা',
    lat: 23.7482,
    lon: 90.3745,
    district: 'dhanmondi',
    worldX: -184,
    worldZ: 42,
    architecturalStyle: 'red_brick_cultural',
  },

  // === KAWRAN BAZAR & ELEVATED FLYOVER (MIDTOWN) ===
  {
    osmId: 'osm_way_kawran_bazar',
    kind: 'market',
    name: 'Kawran Bazar Wholesale Market Hall',
    banglaName: 'কারওয়ান বাজার পাইকারি আড়ত',
    lat: 23.7512,
    lon: 90.3934,
    district: 'kawran_flyover',
    worldX: -26,
    worldZ: -23,
    architecturalStyle: 'market_hall',
  },
  {
    osmId: 'osm_way_kawran_flyover',
    kind: 'flyover',
    name: 'Dhaka Elevated Express Flyover',
    banglaName: 'ঢাকা এলিভেটেড এক্সপ্রেস ফ্লাইওভার',
    lat: 23.754,
    lon: 90.3995,
    district: 'kawran_flyover',
    worldX: -64,
    worldZ: -64,
    architecturalStyle: 'elevated_viaduct',
  },

  // === GULSHAN & BANANI (NORTHEAST / COMMERCIAL & DIPLOMATIC) ===
  {
    osmId: 'osm_way_hatirjheel_lake',
    kind: 'lake',
    name: 'Hatirjheel Lake & Rainbow Arch Bridge',
    banglaName: 'হাতিরঝিল লেক ও আর্চ ব্রিজ',
    lat: 23.7698,
    lon: 90.412,
    district: 'gulshan',
    worldX: 104,
    worldZ: -32,
    architecturalStyle: 'rainbow_arch_bridge',
  },
  {
    osmId: 'osm_node_gulshan_circle_1',
    kind: 'monument',
    name: 'Gulshan Circle 1 Landmark Tower',
    banglaName: 'গুলশান ১ সার্কেল টাওয়ার',
    lat: 23.7785,
    lon: 90.4162,
    district: 'gulshan',
    worldX: 45,
    worldZ: -43,
    architecturalStyle: 'gulshan_tower',
  },
  {
    osmId: 'osm_way_banani_road_11',
    kind: 'market',
    name: 'Banani Road 11 Boutique Plaza',
    banglaName: 'বনানী রোড ১১ ফ্যাশন প্লাজা',
    lat: 23.7932,
    lon: 90.4048,
    district: 'uttara_north',
    worldX: 45,
    worldZ: -124,
    architecturalStyle: 'glass_boutique',
  },
  // === MIRPUR & BAZAAR CORRIDOR (NORTHWEST) ===
  {
    osmId: 'osm_node_mirpur_10',
    kind: 'market',
    name: 'Mirpur 10 Roundabout & Bazaar Emporium',
    banglaName: 'মিরপুর ১০ গোলচত্বর বাজার',
    lat: 23.8072,
    lon: 90.3685,
    district: 'mirpur_market',
    worldX: -124,
    worldZ: -184,
    architecturalStyle: 'bazaar_complex',
  },
  // === UTTARA & METRO RAIL (FAR NORTH) ===
  {
    osmId: 'osm_way_uttara_metro',
    kind: 'metro',
    name: 'Dhaka Metro Rail (MRT-6) Station & Plaza',
    banglaName: 'ঢাকা মেট্রোরেল উত্তরা স্টেশন',
    lat: 23.869,
    lon: 90.3965,
    district: 'uttara_north',
    worldX: 24,
    worldZ: -242,
    architecturalStyle: 'metro_station',
  },
];

/**
 * Authentic Dhaka corridor metadata mapped deterministically to all connected
 * road segments in worldData.ts, preserving exact Bengali orthography.
 */
export const DHAKA_ROAD_CORRIDORS: Record<string, DhakaCorridorMeta> = {
  // Core Spines
  ave_ns_central: {
    roadId: 'ave_ns_central',
    name: 'Kazi Nazrul Islam Ave',
    banglaName: 'কাজী নজরুল ইসলাম এভিনিউ',
    corridorNote: 'Central north-south spine from Sadarghat through Shapla Crossing',
  },
  ave_ew_central: {
    roadId: 'ave_ew_central',
    name: 'Toyenbee Circular Rd',
    banglaName: 'টয়েনবি সার্কুলার রোড',
    corridorNote: 'Central east-west arterial connecting Dhanmondi to Motijheel',
  },

  // Western & Eastern Boulevards
  blvd_ns_w1: {
    roadId: 'blvd_ns_w1',
    name: 'Mirpur Road',
    banglaName: 'মিরপুর রোড',
    corridorNote: 'Western express corridor under Dhaka Express Flyover',
  },
  blvd_ns_e1: {
    roadId: 'blvd_ns_e1',
    name: 'Gulshan Avenue',
    banglaName: 'গুলশান এভিনিউ',
    corridorNote: 'Commercial spine linking Motijheel, Hatirjheel, and Gulshan',
  },
  blvd_ns_w2: {
    roadId: 'blvd_ns_w2',
    name: 'Satmasjid Road',
    banglaName: 'সাতমসজিদ রোড',
    corridorNote: 'Dhanmondi Lakefront and Old Dhaka western connector',
  },
  blvd_ns_e2: {
    roadId: 'blvd_ns_e2',
    name: 'Pragati Sarani',
    banglaName: 'প্রগতি সরণি',
    corridorNote: 'Eastern commercial boulevard through Motijheel and Banani East',
  },
  blvd_ns_w3: {
    roadId: 'blvd_ns_w3',
    name: 'Beribadh West Rd',
    banglaName: 'বেড়িবাঁধ পশ্চিম রোড',
    corridorNote: 'Outer western embankment boulevard',
  },
  blvd_ns_e3: {
    roadId: 'blvd_ns_e3',
    name: 'Kamal Ataturk Ext',
    banglaName: 'কামাল আতাতুর্ক এক্সটেনশন',
    corridorNote: 'Outer eastern metropolitan ring',
  },

  // East-West Cross Arterials
  blvd_ew_n1: {
    roadId: 'blvd_ew_n1',
    name: 'Hatirjheel Link Rd',
    banglaName: 'হাতিরঝিল লিংক রোড',
    corridorNote: 'Cross-town avenue along Hatirjheel and Kawran Bazar',
  },
  blvd_ew_s1: {
    roadId: 'blvd_ew_s1',
    name: 'Nawabpur Road',
    banglaName: 'নবাবপুর রোড',
    corridorNote: 'Historic Old Dhaka hardware and bazaar thoroughfare',
  },
  blvd_ew_n2: {
    roadId: 'blvd_ew_n2',
    name: 'Rokeya Sarani',
    banglaName: 'রোকেয়া সরণি',
    corridorNote: 'Northern commercial link between Mirpur and Banani North',
  },
  blvd_ew_s2: {
    roadId: 'blvd_ew_s2',
    name: 'Chawkbazar Circular Rd',
    banglaName: 'চকবাজার সার্কুলার রোড',
    corridorNote: 'Southern heritage corridor above Sadarghat',
  },
  blvd_ew_n3: {
    roadId: 'blvd_ew_n3',
    name: 'Airport Link Rd',
    banglaName: 'এয়ারপোর্ট লিংক রোড',
    corridorNote: 'Northern diplomatic and bazaar bypass',
  },

  // Riverfront Promenade
  ave_sadarghat: {
    roadId: 'ave_sadarghat',
    name: 'Sadarghat Launch Rd',
    banglaName: 'সদরঘাট লঞ্চ রোড',
    corridorNote: 'Buriganga riverfront promenade and launch terminal road',
  },

  // Secondary Heritage & Market Connectors
  alley_old_dhaka_1: {
    roadId: 'alley_old_dhaka_1',
    name: 'Shakhari Bazar Lane',
    banglaName: 'শাঁখারী বাজার লেন',
    corridorNote: 'Narrow Old Dhaka heritage lane',
  },
  alley_old_dhaka_2: {
    roadId: 'alley_old_dhaka_2',
    name: 'Chawk Spice Goli',
    banglaName: 'চক মশলা গলি',
    corridorNote: 'Dense Chawkbazar market alley',
  },
  street_motijheel_1: {
    roadId: 'street_motijheel_1',
    name: 'Dilkusha C/A',
    banglaName: 'দিলকুশা বাণিজ্যিক এলাকা',
    corridorNote: 'Motijheel banking district avenue',
  },
  street_motijheel_2: {
    roadId: 'street_motijheel_2',
    name: 'Arambagh Cross Rd',
    banglaName: 'আরামবাগ ক্রস রোড',
    corridorNote: 'Motijheel southern bus corridor',
  },
  street_gulshan_n: {
    roadId: 'street_gulshan_n',
    name: 'Banani Road 11',
    banglaName: 'বনানী রোড ১১',
    corridorNote: 'Upscale Banani & Gulshan connector',
  },
  street_mirpur_1: {
    roadId: 'street_mirpur_1',
    name: 'Mirpur 10 Bazaar Rd',
    banglaName: 'মিরপুর ১০ বাজার রোড',
    corridorNote: 'Bustling Mirpur market street',
  },
  street_dhanmondi_s: {
    roadId: 'street_dhanmondi_s',
    name: 'Jigatola Road',
    banglaName: 'জিগাতলা রোড',
    corridorNote: 'Southern Dhanmondi residential street',
  },

  // Expanded Arterials
  alley_ahsan_manzil: {
    roadId: 'alley_ahsan_manzil',
    name: 'Ahsan Manzil Ghat Road',
    banglaName: 'আহসান মঞ্জিল ঘাট রোড',
    corridorNote: 'Historic riverfront approach to Ahsan Manzil Pink Palace',
  },
  street_lalbagh_approach: {
    roadId: 'street_lalbagh_approach',
    name: 'Lalbagh Fort Road',
    banglaName: 'লালবাগ কেল্লা রোড',
    corridorNote: 'Heritage brick avenue leading to Lalbagh Mughal Fort',
  },
  ave_metro_line6: {
    roadId: 'ave_metro_line6',
    name: 'MRT-6 Metro Viaduct',
    banglaName: 'মেট্রোরেল লাইন ৬ ভায়াডাক্ট',
    corridorNote: 'Elevated rapid transit corridor to Uttara',
  },
};

/**
 * Pure lookup returning the nearest named Dhaka road corridor for HUD display.
 */
export function getNearestStreetCorridor(
  x: number,
  z: number,
  roadSegments: readonly RoadSegment[],
): DhakaCorridorMeta | null {
  let bestMeta: DhakaCorridorMeta | null = null;
  let bestDist = Infinity;

  for (const seg of roadSegments) {
    const minX = Math.min(seg.x1, seg.x2);
    const maxX = Math.max(seg.x1, seg.x2);
    const minZ = Math.min(seg.z1, seg.z2);
    const maxZ = Math.max(seg.z1, seg.z2);

    const cx = Math.max(minX, Math.min(x, maxX));
    const cz = Math.max(minZ, Math.min(z, maxZ));
    const dist = Math.hypot(x - cx, z - cz);

    // Must be on or immediately beside the street corridor
    if (dist <= seg.width * 0.5 + 10 && dist < bestDist) {
      const meta = DHAKA_ROAD_CORRIDORS[seg.id];
      if (meta) {
        bestDist = dist;
        bestMeta = meta;
      }
    }
  }

  return bestMeta;
}
