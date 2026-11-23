import test from 'node:test';
import assert from 'node:assert/strict';
import {
  buildOpenMeteoUrl,
  clearOpenMeteoCache,
  fetchDhakaLiveWeather,
  mapWmoCodeToGameWeather,
  OPEN_METEO_CACHE_TTL_MS,
  parseOpenMeteoResponse,
} from './openMeteoClient';
import {
  clearBdapisCache,
  FALLBACK_DHAKA_REGIONAL_PLACES,
  fetchDhakaRegionalPlaces,
  parseBdapisDivisionResponse,
} from './bdapisClient';
import {
  DHAKA_GEO_ORIGIN,
  DHAKA_ROAD_CORRIDORS,
  getNearestStreetCorridor,
  projectOsmLatLonToWorld,
} from '../game/dhakaGeoData';
import { LAKE_ZONES, ROAD_SEGMENTS } from '../game/worldData';

test('mapWmoCodeToGameWeather maps WMO codes accurately onto game weather modes', () => {
  assert.equal(mapWmoCodeToGameWeather(0, 0).mode, 'clear');
  assert.equal(mapWmoCodeToGameWeather(2, 0).mode, 'clear');
  assert.equal(mapWmoCodeToGameWeather(45, 0).mode, 'clear');

  assert.equal(mapWmoCodeToGameWeather(51, 0.2).mode, 'monsoon');
  assert.equal(mapWmoCodeToGameWeather(63, 2.5).mode, 'monsoon');
  assert.equal(mapWmoCodeToGameWeather(80, 1.0).mode, 'monsoon');
  // Measurable rainfall overrides clear code
  assert.equal(mapWmoCodeToGameWeather(3, 1.2).mode, 'monsoon');

  assert.equal(mapWmoCodeToGameWeather(82, 6.0).mode, 'storm');
  assert.equal(mapWmoCodeToGameWeather(95, 4.0).mode, 'storm');
  assert.equal(mapWmoCodeToGameWeather(99, 12.0).mode, 'storm');
  // Heavy precipitation overrides moderate code into storm
  assert.equal(mapWmoCodeToGameWeather(63, 9.5).mode, 'storm');
});

test('parseOpenMeteoResponse validates payload strictly and rejects malformed inputs', () => {
  const valid = parseOpenMeteoResponse(
    {
      current: {
        time: '2026-11-22T20:00',
        temperature_2m: 29.4,
        relative_humidity_2m: 74,
        precipitation: 1.8,
        weather_code: 61,
        wind_speed_10m: 14.2,
        is_day: 1,
      },
    },
    1000,
  );
  assert.ok(valid);
  assert.equal(valid.mappedMode, 'monsoon');
  assert.equal(valid.temperatureC, 29.4);
  assert.equal(valid.humidityPct, 74);
  assert.equal(valid.windKmh, 14.2);

  assert.equal(parseOpenMeteoResponse(null), null);
  assert.equal(parseOpenMeteoResponse({}), null);
  assert.equal(parseOpenMeteoResponse({ current: { temperature_2m: 'hot' } }), null);
  assert.equal(
    parseOpenMeteoResponse({
      current: { time: '', temperature_2m: 25, precipitation: 0, weather_code: 0 },
    }),
    null,
  );
});

test('fetchDhakaLiveWeather caches for 30 minutes and handles 429 & network failures gracefully', async () => {
  clearOpenMeteoCache();
  assert.ok(buildOpenMeteoUrl().includes('latitude=23.8103'));
  assert.ok(buildOpenMeteoUrl().includes('longitude=90.4125'));

  let callCount = 0;
  const mockOkFetch = (async () => {
    callCount++;
    return new Response(
      JSON.stringify({
        current: {
          time: '2026-11-22T20:00',
          temperature_2m: 27.6,
          relative_humidity_2m: 68,
          precipitation: 0,
          weather_code: 1,
          wind_speed_10m: 9.4,
          is_day: 1,
        },
      }),
      { status: 200, headers: { 'Content-Type': 'application/json' } },
    );
  }) as typeof fetch;

  const t0 = 1_700_000_000_000;
  const first = await fetchDhakaLiveWeather({ fetchImpl: mockOkFetch, nowMs: t0 });
  assert.equal(first.ok, true);
  assert.equal(first.reading?.mappedMode, 'clear');
  assert.equal(first.reading?.fromCache, false);
  assert.equal(callCount, 1);

  // Second call within 30 minutes returns cache without hitting network
  const second = await fetchDhakaLiveWeather({ fetchImpl: mockOkFetch, nowMs: t0 + 10 * 60 * 1000 });
  assert.equal(second.ok, true);
  assert.equal(second.reading?.fromCache, true);
  assert.equal(callCount, 1);

  // Call after TTL expires refetches
  const third = await fetchDhakaLiveWeather({
    fetchImpl: mockOkFetch,
    nowMs: t0 + OPEN_METEO_CACHE_TTL_MS + 1000,
  });
  assert.equal(third.ok, true);
  assert.equal(third.reading?.fromCache, false);
  assert.equal(callCount, 2);

  // Rate-limit (429) degrades gracefully without throwing
  const rateLimited = await fetchDhakaLiveWeather({
    forceRefresh: true,
    fetchImpl: (async () => new Response('', { status: 429 })) as typeof fetch,
  });
  assert.equal(rateLimited.ok, false);
  assert.ok(rateLimited.error?.includes('429') || rateLimited.error?.toLowerCase().includes('rate limit'));

  // Network throw degrades gracefully
  const offline = await fetchDhakaLiveWeather({
    forceRefresh: true,
    fetchImpl: (async () => {
      throw new Error('ECONNREFUSED');
    }) as typeof fetch,
  });
  assert.equal(offline.ok, false);
  assert.ok(offline.error);
});

test('BDAPIs client validates payload, caches result, and falls back to local Dhaka Division data on error', async () => {
  clearBdapisCache();

  const parsed = parseBdapisDivisionResponse({
    status: { code: 200 },
    data: [
      { district: 'Dhaka', districtbn: 'ঢাকা', upazilla: ['Keraniganj', 'Savar'] },
      { district: 'Gazipur', districtbn: 'গাজীপুর', upazilla: ['Tongi'] },
    ],
  });
  assert.ok(parsed);
  assert.equal(parsed.length, 2);
  assert.equal(parsed[0].banglaName, 'ঢাকা');
  assert.equal(parseBdapisDivisionResponse({ data: [] }), null);

  // Offline fetch returns local fallback cleanly
  const fallbackRes = await fetchDhakaRegionalPlaces({
    forceRefresh: true,
    fetchImpl: (async () => {
      throw new Error('offline');
    }) as typeof fetch,
  });
  assert.equal(fallbackRes.ok, false);
  assert.equal(fallbackRes.source, 'fallback');
  assert.equal(fallbackRes.places.length, FALLBACK_DHAKA_REGIONAL_PLACES.length);

  // Valid fetch succeeds and caches
  let bdCalls = 0;
  const okFetch = (async () => {
    bdCalls++;
    return new Response(
      JSON.stringify({
        status: { code: 200 },
        data: [
          { district: 'Dhaka', districtbn: 'ঢাকা', upazilla: ['Keraniganj', 'Savar'] },
          { district: 'Narayanganj', districtbn: 'নারায়ণগঞ্জ', upazilla: ['Sonargaon'] },
        ],
      }),
      { status: 200 },
    );
  }) as typeof fetch;

  const liveRes = await fetchDhakaRegionalPlaces({ fetchImpl: okFetch, nowMs: 5000 });
  assert.equal(liveRes.ok, true);
  assert.equal(liveRes.source, 'bdapis');
  assert.equal(bdCalls, 1);

  const cachedRes = await fetchDhakaRegionalPlaces({ fetchImpl: okFetch, nowMs: 6000 });
  assert.equal(cachedRes.ok, true);
  assert.equal(bdCalls, 1);
});

test('dhakaGeoData maps all road corridors, projects OSM coordinates, and includes Hatirjheel Lake', () => {
  for (const seg of ROAD_SEGMENTS) {
    const meta = DHAKA_ROAD_CORRIDORS[seg.id];
    assert.ok(meta, `Missing Dhaka corridor metadata for road segment ${seg.id}`);
    assert.ok(meta.name.length > 0 && meta.banglaName.length > 0);
  }

  const centralStreet = getNearestStreetCorridor(0, 34, ROAD_SEGMENTS);
  assert.equal(centralStreet?.name, 'Kazi Nazrul Islam Ave');

  const gulshanAve = getNearestStreetCorridor(64, -30, ROAD_SEGMENTS);
  assert.equal(gulshanAve?.name, 'Gulshan Avenue');

  const originProj = projectOsmLatLonToWorld(DHAKA_GEO_ORIGIN.lat, DHAKA_GEO_ORIGIN.lon);
  assert.equal(originProj.x, 0);
  assert.equal(originProj.z, 0);
  assert.equal(originProj.inPlayableBounds, true);

  // North of Shapla Crossing (Gulshan ~23.79N) projects to negative Z
  const gulshanProj = projectOsmLatLonToWorld(23.792, 90.416);
  assert.ok(gulshanProj.z < 0);

  // Hatirjheel Lake + Dhanmondi Lake both present
  assert.ok(LAKE_ZONES.some((l) => l.id === 'hatirjheel_lake'));
  assert.ok(LAKE_ZONES.some((l) => l.id === 'dhanmondi_lake'));
});
