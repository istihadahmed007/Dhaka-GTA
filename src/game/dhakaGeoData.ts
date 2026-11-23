import { RoadSegment } from './worldData';

/**
 * Stylized Dhaka geographic reference and offline OSM-extract adapter.
 *
 * IMPORTANT:
 * - Zero live OpenStreetMap calls happen at runtime.
 * - The playable world is a stylized 540m x 470m representation anchored near
 *   Shapla Crossing (23.7330 N, 90.4170 E), not a 1:1 GIS projection.
 * - Road names and waterway references below are authored locally for Dhaka
 *   authenticity; an offline OSM extract (e.g. Geofabrik bangladesh-latest.osm.pbf
 *   converted to GeoJSON under ODbL) can be converted via projectOsmLatLonToWorld().
 */

export const DHAKA_GEO_ORIGIN = {
  lat: 23.733, // Shapla Crossing / Motijheel reference anchor
  lon: 90.417,
  metersPerDegLat: 110750,
  metersPerDegLon: 101850, // cos(23.73 deg) * 111320
  stylizedScale: 0.085, // compresses ~6km metropolitan span into ~510m playable city
} as const;

export const OSM_ATTRIBUTION_NOTE =
  'Stylized Dhaka layout. Offline OSM extracts require © OpenStreetMap contributors (ODbL).';

export interface DhakaCorridorMeta {
  roadId: string;
  name: string;
  banglaName: string;
  corridorNote: string;
}

export interface DhakaOsmExtractFeature {
  osmId: string;
  kind: 'road' | 'waterway' | 'landmark' | 'neighbourhood';
  name: string;
  nameBn?: string;
  lat: number;
  lon: number;
}

export interface ProjectedWorldPoint {
  x: number;
  z: number;
  inPlayableBounds: boolean;
}

/**
 * Deterministic offline converter from WGS84 lat/lon (e.g., from a local
 * Geofabrik / HOTOSM GeoJSON extract) into stylized Dhaka GTA world meters.
 * +X = East, -Z = North, +Z = South (toward Buriganga River).
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
 * Authentic Dhaka corridor names mapped deterministically to our connected
 * road segments in worldData.ts.
 */
export const DHAKA_ROAD_CORRIDORS: Record<string, DhakaCorridorMeta> = {
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
  ave_sadarghat: {
    roadId: 'ave_sadarghat',
    name: 'Sadarghat Launch Rd',
    banglaName: 'সদরঘাট লঞ্চ রোড',
    corridorNote: 'Buriganga riverfront promenade and launch terminal road',
  },
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
