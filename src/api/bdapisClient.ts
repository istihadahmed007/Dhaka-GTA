/**
 * Isolated optional BDAPIs client for Dhaka Division regional place names
 * in Bangla and English.
 *
 * Design rules:
 * - NEVER overwrites the game's 9 authored Dhaka City districts (Old Dhaka,
 *   Shapla Crossing, Gulshan, Motijheel, Dhanmondi, Sadarghat, etc.).
 * - Fetches ONLY when the player opts in (e.g. Regional Directory / Transit
 *   destination labels), cached for 24 hours in memory + localStorage.
 * - Validates external JSON strictly and always keeps authored local fallback
 *   names so gameplay works 100% offline.
 */

export const BDAPIS_DHAKA_URL = 'https://bdapis.com/api/v1.2/division/dhaka';
export const BDAPIS_CACHE_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
const BDAPIS_STORAGE_KEY = 'dhaka_gta_bdapis_dhaka_v1';
const REQUEST_TIMEOUT_MS = 6000;

export const BDAPIS_ATTRIBUTION = 'Regional Bangla/English names via BDAPIs (bdapis.com)';

export interface BangladeshRegionalPlace {
  name: string;
  banglaName: string;
  upazilas: string[];
}

export interface FetchRegionalPlacesResult {
  ok: boolean;
  source: 'bdapis' | 'fallback';
  places: BangladeshRegionalPlace[];
  fetchedAtMs: number;
  error: string | null;
}

/**
 * Authored local fallback for Dhaka Division regional hubs (used whenever
 * BDAPIs is disabled, offline, rate-limited, or returns invalid data).
 */
export const FALLBACK_DHAKA_REGIONAL_PLACES: readonly BangladeshRegionalPlace[] = [
  {
    name: 'Dhaka',
    banglaName: 'ঢাকা',
    upazilas: ['Keraniganj', 'Savar', 'Dhamrai', 'Dohar', 'Nawabganj'],
  },
  {
    name: 'Narayanganj',
    banglaName: 'নারায়ণগঞ্জ',
    upazilas: ['Narayanganj Sadar', 'Bandar', 'Sonargaon', 'Rupganj'],
  },
  {
    name: 'Gazipur',
    banglaName: 'গাজীপুর',
    upazilas: ['Gazipur Sadar', 'Kaliakair', 'Tongi', 'Sreepur'],
  },
  {
    name: 'Munshiganj',
    banglaName: 'মুন্সিগঞ্জ',
    upazilas: ['Munshiganj Sadar', 'Lohajang', 'Sreenagar'],
  },
  {
    name: 'Manikganj',
    banglaName: 'মানিকগঞ্জ',
    upazilas: ['Manikganj Sadar', 'Singair', 'Saturia'],
  },
  {
    name: 'Narsingdi',
    banglaName: 'নরসিংদী',
    upazilas: ['Narsingdi Sadar', 'Palash', 'Shibpur'],
  },
];

/**
 * Strictly validates BDAPIs v1.2 division response payload.
 * Expected shape: { status: { code: 200 }, data: [{ district, districtbn, upazilla: string[] }] }
 */
export function parseBdapisDivisionResponse(raw: unknown): BangladeshRegionalPlace[] | null {
  if (!raw || typeof raw !== 'object') return null;
  const obj = raw as Record<string, unknown>;
  const data = obj.data;
  if (!Array.isArray(data) || data.length === 0) return null;

  const parsed: BangladeshRegionalPlace[] = [];
  for (const item of data) {
    if (!item || typeof item !== 'object') continue;
    const rec = item as Record<string, unknown>;
    const name = typeof rec.district === 'string' ? rec.district.trim() : '';
    const banglaName = typeof rec.districtbn === 'string' ? rec.districtbn.trim() : '';
    if (!name || !banglaName) continue;

    const rawUpazillas = Array.isArray(rec.upazilla) ? rec.upazilla : [];
    const upazilas = rawUpazillas
      .filter((u): u is string => typeof u === 'string' && u.trim().length > 0)
      .map((u) => u.trim())
      .slice(0, 8);

    parsed.push({ name, banglaName, upazilas });
  }

  return parsed.length > 0 ? parsed : null;
}

let memoryCache: { places: BangladeshRegionalPlace[]; fetchedAtMs: number } | null = null;

function readStorageCache(): { places: BangladeshRegionalPlace[]; fetchedAtMs: number } | null {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return null;
    const raw = window.localStorage.getItem(BDAPIS_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as { places?: unknown; fetchedAtMs?: unknown };
    if (typeof parsed.fetchedAtMs !== 'number' || !Array.isArray(parsed.places)) return null;
    const valid = parsed.places.filter(
      (p): p is BangladeshRegionalPlace =>
        Boolean(p) &&
        typeof p === 'object' &&
        typeof (p as BangladeshRegionalPlace).name === 'string' &&
        typeof (p as BangladeshRegionalPlace).banglaName === 'string' &&
        Array.isArray((p as BangladeshRegionalPlace).upazilas),
    );
    if (valid.length === 0) return null;
    return { places: valid, fetchedAtMs: parsed.fetchedAtMs };
  } catch {
    return null;
  }
}

function writeStorageCache(places: BangladeshRegionalPlace[], fetchedAtMs: number): void {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return;
    window.localStorage.setItem(BDAPIS_STORAGE_KEY, JSON.stringify({ places, fetchedAtMs }));
  } catch {
    // Ignore storage errors
  }
}

export function clearBdapisCache(): void {
  memoryCache = null;
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(BDAPIS_STORAGE_KEY);
    }
  } catch {
    // Ignore
  }
}

export interface FetchBdapisOptions {
  forceRefresh?: boolean;
  nowMs?: number;
  fetchImpl?: typeof fetch;
  timeoutMs?: number;
}

/**
 * Fetches Dhaka Division regional districts & upazilas in English and Bangla
 * from BDAPIs with 24-hour caching and guaranteed authored local fallback.
 */
export async function fetchDhakaRegionalPlaces(
  options: FetchBdapisOptions = {},
): Promise<FetchRegionalPlacesResult> {
  const nowMs = options.nowMs ?? Date.now();
  const forceRefresh = options.forceRefresh ?? false;
  const fetchFn = options.fetchImpl ?? globalThis.fetch;
  const timeoutMs = options.timeoutMs ?? REQUEST_TIMEOUT_MS;

  if (!forceRefresh) {
    const cached = memoryCache ?? readStorageCache();
    if (cached && nowMs - cached.fetchedAtMs < BDAPIS_CACHE_TTL_MS) {
      memoryCache = cached;
      return {
        ok: true,
        source: 'bdapis',
        places: cached.places,
        fetchedAtMs: cached.fetchedAtMs,
        error: null,
      };
    }
  }

  if (typeof fetchFn !== 'function') {
    return {
      ok: false,
      source: 'fallback',
      places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
      fetchedAtMs: nowMs,
      error: 'Network unavailable — showing local Dhaka Division directory',
    };
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const resp = await fetchFn(BDAPIS_DHAKA_URL, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });

    if (!resp.ok) {
      return {
        ok: false,
        source: 'fallback',
        places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
        fetchedAtMs: nowMs,
        error: `BDAPIs returned HTTP ${resp.status} — using local fallback`,
      };
    }

    const json: unknown = await resp.json();
    const places = parseBdapisDivisionResponse(json);
    if (!places) {
      return {
        ok: false,
        source: 'fallback',
        places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
        fetchedAtMs: nowMs,
        error: 'Invalid BDAPIs payload — using local fallback',
      };
    }

    memoryCache = { places, fetchedAtMs: nowMs };
    writeStorageCache(places, nowMs);
    return {
      ok: true,
      source: 'bdapis',
      places,
      fetchedAtMs: nowMs,
      error: null,
    };
  } catch (err) {
    const isAbort = err instanceof Error && err.name === 'AbortError';
    return {
      ok: false,
      source: 'fallback',
      places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
      fetchedAtMs: nowMs,
      error: isAbort
        ? 'BDAPIs request timed out — using local fallback'
        : 'BDAPIs unavailable — using local fallback',
    };
  } finally {
    clearTimeout(timer);
  }
}
