import { WeatherMode } from '../game/types';

/**
 * Isolated Open-Meteo client for optional real-world Dhaka weather.
 *
 * Usage & Licensing:
 * - Open-Meteo Forecast API (https://open-meteo.com/) requires NO API key for
 *   non-commercial use and redistributes weather data under CC BY 4.0.
 * - Only fetched when the player explicitly opts in.
 * - Cached for 30 minutes (memory + localStorage) to stay respectful and fast.
 * - Strict response validation, timeout protection, and graceful fallback
 *   ensure network errors never interrupt or freeze gameplay.
 */

export const DHAKA_COORDS = {
  latitude: 23.8103,
  longitude: 90.4125,
} as const;

export const OPEN_METEO_CACHE_TTL_MS = 30 * 60 * 1000; // 30 minutes
const OPEN_METEO_STORAGE_KEY = 'dhaka_gta_open_meteo_cache_v1';
const REQUEST_TIMEOUT_MS = 6000;

export const OPEN_METEO_ATTRIBUTION =
  'Weather data by Open-Meteo.com (CC BY 4.0 · non-commercial use)';

export interface OpenMeteoCurrentPayload {
  time: string;
  temperature_2m: number;
  relative_humidity_2m?: number;
  precipitation: number;
  weather_code: number;
  wind_speed_10m?: number;
  is_day?: number;
}

export interface DhakaWeatherReading {
  mappedMode: WeatherMode;
  weatherCode: number;
  conditionLabel: string;
  conditionBangla: string;
  temperatureC: number;
  humidityPct: number | null;
  precipitationMm: number;
  windKmh: number | null;
  isDay: boolean;
  observedTimeIso: string;
  fetchedAtMs: number;
  fromCache: boolean;
}

export interface FetchWeatherResult {
  ok: boolean;
  reading: DhakaWeatherReading | null;
  error: string | null;
}

export interface MappedWeatherCondition {
  mode: WeatherMode;
  label: string;
  banglaLabel: string;
}

/**
 * Pure WMO weather interpretation code mapper onto the game's existing
 * WeatherMode ('clear' | 'monsoon' | 'storm').
 */
export function mapWmoCodeToGameWeather(
  weatherCode: number,
  precipitationMm = 0,
): MappedWeatherCondition {
  // 95, 96, 99: Thunderstorm (slight/moderate/hail)
  if (weatherCode >= 95 && weatherCode <= 99) {
    return {
      mode: 'storm',
      label: 'Dhaka Thunderstorm',
      banglaLabel: 'বজ্রবৃষ্টি',
    };
  }

  // 82: Violent rain showers, or very heavy precipitation (>= 8mm/hr) -> storm
  if (weatherCode === 82 || precipitationMm >= 8) {
    return {
      mode: 'storm',
      label: 'Heavy Monsoon Squall',
      banglaLabel: 'প্রবল বর্ষণ',
    };
  }

  // 51..67 (drizzle / rain), 80..81 (rain showers), or measurable rain >= 0.4mm -> monsoon
  if (
    (weatherCode >= 51 && weatherCode <= 67) ||
    (weatherCode >= 80 && weatherCode <= 86) ||
    precipitationMm >= 0.4
  ) {
    return {
      mode: 'monsoon',
      label: weatherCode >= 51 && weatherCode <= 57 ? 'Dhaka Drizzle' : 'Monsoon Rain',
      banglaLabel: 'বর্ষার বৃষ্টি',
    };
  }

  // 45, 48: Fog / river mist
  if (weatherCode === 45 || weatherCode === 48) {
    return {
      mode: 'clear',
      label: 'River Mist & Fog',
      banglaLabel: 'কুয়াশা',
    };
  }

  // 1, 2, 3: Partly cloudy / overcast
  if (weatherCode >= 1 && weatherCode <= 3) {
    return {
      mode: 'clear',
      label: weatherCode === 3 ? 'Overcast Dhaka Sky' : 'Partly Cloudy',
      banglaLabel: weatherCode === 3 ? 'মেঘলা আকাশ' : 'আংশিক মেঘলা',
    };
  }

  // 0: Clear sky (and safe default for any unexpected non-rain code)
  return {
    mode: 'clear',
    label: 'Clear Tropical Sky',
    banglaLabel: 'পরিষ্কার আকাশ',
  };
}

/**
 * Strictly validates untrusted external JSON from Open-Meteo.
 * Returns null if the payload is malformed or missing required fields.
 */
export function parseOpenMeteoResponse(
  raw: unknown,
  nowMs = Date.now(),
): DhakaWeatherReading | null {
  if (!raw || typeof raw !== 'object') return null;
  const obj = raw as Record<string, unknown>;
  const current = obj.current;
  if (!current || typeof current !== 'object') return null;

  const cur = current as Record<string, unknown>;
  const weatherCode = cur.weather_code;
  const temp = cur.temperature_2m;
  const precip = cur.precipitation;
  const time = cur.time;

  if (
    typeof weatherCode !== 'number' ||
    !Number.isFinite(weatherCode) ||
    typeof temp !== 'number' ||
    !Number.isFinite(temp) ||
    typeof precip !== 'number' ||
    !Number.isFinite(precip) ||
    typeof time !== 'string' ||
    time.length === 0
  ) {
    return null;
  }

  const humidity =
    typeof cur.relative_humidity_2m === 'number' && Number.isFinite(cur.relative_humidity_2m)
      ? Math.round(cur.relative_humidity_2m)
      : null;
  const wind =
    typeof cur.wind_speed_10m === 'number' && Number.isFinite(cur.wind_speed_10m)
      ? Math.round(cur.wind_speed_10m * 10) / 10
      : null;
  const isDay = typeof cur.is_day === 'number' ? cur.is_day === 1 : true;

  const mapped = mapWmoCodeToGameWeather(weatherCode, precip);

  return {
    mappedMode: mapped.mode,
    weatherCode,
    conditionLabel: mapped.label,
    conditionBangla: mapped.banglaLabel,
    temperatureC: Math.round(temp * 10) / 10,
    humidityPct: humidity,
    precipitationMm: Math.round(precip * 100) / 100,
    windKmh: wind,
    isDay,
    observedTimeIso: time,
    fetchedAtMs: nowMs,
    fromCache: false,
  };
}

let memoryCache: DhakaWeatherReading | null = null;

function readStorageCache(): DhakaWeatherReading | null {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return null;
    const raw = window.localStorage.getItem(OPEN_METEO_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<DhakaWeatherReading>;
    if (
      parsed &&
      typeof parsed.fetchedAtMs === 'number' &&
      typeof parsed.temperatureC === 'number' &&
      typeof parsed.weatherCode === 'number' &&
      (parsed.mappedMode === 'clear' || parsed.mappedMode === 'monsoon' || parsed.mappedMode === 'storm')
    ) {
      return {
        mappedMode: parsed.mappedMode,
        weatherCode: parsed.weatherCode,
        conditionLabel: typeof parsed.conditionLabel === 'string' ? parsed.conditionLabel : 'Dhaka Weather',
        conditionBangla: typeof parsed.conditionBangla === 'string' ? parsed.conditionBangla : 'ঢাকার আবহাওয়া',
        temperatureC: parsed.temperatureC,
        humidityPct: typeof parsed.humidityPct === 'number' ? parsed.humidityPct : null,
        precipitationMm: typeof parsed.precipitationMm === 'number' ? parsed.precipitationMm : 0,
        windKmh: typeof parsed.windKmh === 'number' ? parsed.windKmh : null,
        isDay: typeof parsed.isDay === 'boolean' ? parsed.isDay : true,
        observedTimeIso: typeof parsed.observedTimeIso === 'string' ? parsed.observedTimeIso : '',
        fetchedAtMs: parsed.fetchedAtMs,
        fromCache: true,
      };
    }
  } catch {
    // Ignore storage access/parse failures gracefully
  }
  return null;
}

function writeStorageCache(reading: DhakaWeatherReading): void {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return;
    window.localStorage.setItem(OPEN_METEO_STORAGE_KEY, JSON.stringify(reading));
  } catch {
    // Ignore quota/privacy-mode errors
  }
}

export function clearOpenMeteoCache(): void {
  memoryCache = null;
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(OPEN_METEO_STORAGE_KEY);
    }
  } catch {
    // Ignore
  }
}

export function buildOpenMeteoUrl(): string {
  const params = new URLSearchParams({
    latitude: String(DHAKA_COORDS.latitude),
    longitude: String(DHAKA_COORDS.longitude),
    current: 'temperature_2m,relative_humidity_2m,precipitation,weather_code,wind_speed_10m,is_day',
  });
  return `https://api.open-meteo.com/v1/forecast?${params.toString()}`;
}

export interface FetchWeatherOptions {
  forceRefresh?: boolean;
  nowMs?: number;
  fetchImpl?: typeof fetch;
  timeoutMs?: number;
}

/**
 * Fetches current Dhaka weather from Open-Meteo with 30-minute caching,
 * AbortController timeout, rate-limit handling, and zero uncaught throws.
 */
export async function fetchDhakaLiveWeather(
  options: FetchWeatherOptions = {},
): Promise<FetchWeatherResult> {
  const nowMs = options.nowMs ?? Date.now();
  const forceRefresh = options.forceRefresh ?? false;
  const fetchFn = options.fetchImpl ?? globalThis.fetch;
  const timeoutMs = options.timeoutMs ?? REQUEST_TIMEOUT_MS;

  if (!forceRefresh) {
    const cached = memoryCache ?? readStorageCache();
    if (cached && nowMs - cached.fetchedAtMs < OPEN_METEO_CACHE_TTL_MS) {
      memoryCache = cached;
      return { ok: true, reading: { ...cached, fromCache: true }, error: null };
    }
  }

  if (typeof fetchFn !== 'function') {
    return { ok: false, reading: memoryCache, error: 'Weather fetch unavailable in this environment' };
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const resp = await fetchFn(buildOpenMeteoUrl(), {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });

    if (resp.status === 429) {
      return {
        ok: false,
        reading: memoryCache,
        error: 'Open-Meteo rate limit reached — keeping current weather',
      };
    }

    if (!resp.ok) {
      return {
        ok: false,
        reading: memoryCache,
        error: `Weather service returned HTTP ${resp.status}`,
      };
    }

    const json: unknown = await resp.json();
    const parsed = parseOpenMeteoResponse(json, nowMs);
    if (!parsed) {
      return {
        ok: false,
        reading: memoryCache,
        error: 'Received invalid weather data from Open-Meteo',
      };
    }

    memoryCache = parsed;
    writeStorageCache(parsed);
    return { ok: true, reading: parsed, error: null };
  } catch (err) {
    const isAbort = err instanceof Error && err.name === 'AbortError';
    return {
      ok: false,
      reading: memoryCache,
      error: isAbort ? 'Weather request timed out — manual weather preserved' : 'Offline or network error — manual weather preserved',
    };
  } finally {
    clearTimeout(timer);
  }
}
