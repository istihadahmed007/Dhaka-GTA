import { DhakaSaveGameData, OutfitId, SafehouseId, WeaponId } from './types';

export const DHAKA_SAVE_STORAGE_KEY = 'dhaka_gta_save_v1';
export const CURRENT_SAVE_VERSION = 1;

/**
 * Returns clean initial starter save game data.
 */
export function createDefaultSaveGameData(): DhakaSaveGameData {
  return {
    version: CURRENT_SAVE_VERSION,
    timestamp: Date.now(),
    player: {
      x: -2.2,
      y: 0,
      z: 35.2,
      heading: Math.PI,
      hp: 100,
      armor: 50,
      stamina: 100,
      money: 800,
      wantedLevel: 0,
      weapon: 'fists',
      unlockedWeapons: ['fists', 'pistol'],
      ammo: {
        fists: 0,
        pistol: 60,
        smg: 0,
        shotgun: 0,
      },
      outfit: 'street',
      unlockedOutfits: ['street'],
      unlockedSafehouses: ['old_dhaka_rooftop'],
      inventory: [
        {
          id: 'item_cha_flask',
          name: 'Bengal Thermal Cha Flask',
          banglaName: 'চায়ের ফ্লাস্ক',
          kind: 'food',
          count: 2,
          value: 40,
          description: 'Piping hot Old Dhaka cardamom tea. Restores 30 HP & 40 Stamina.',
        },
      ],
    },
    missions: {
      missionIdx: 0,
      stepIdx: 0,
      missionsCompleted: 0,
      faresCompleted: 0,
      bountiesCompleted: 0,
      carTheftsCompleted: 0,
    },
    world: {
      timeOfDay: 15.8,
      weather: 'clear',
      playTimeSec: 0,
    },
    ownedVehicles: [],
  };
}

/**
 * Validates and sanitizes a raw parsed save object to guarantee runtime safety.
 */
export function validateAndSanitizeSaveData(raw: unknown): DhakaSaveGameData | null {
  if (!raw || typeof raw !== 'object') return null;

  const data = raw as Partial<DhakaSaveGameData>;
  if (typeof data.version !== 'number' || data.version > CURRENT_SAVE_VERSION) {
    return null;
  }

  const defaultData = createDefaultSaveGameData();
  const rawP = (data.player || {}) as Record<string, any>;
  const rawM = (data.missions || {}) as Record<string, any>;
  const rawW = (data.world || {}) as Record<string, any>;

  // Sanitize player coordinates and stats
  const x = Number.isFinite(rawP.x) ? (rawP.x as number) : defaultData.player.x;
  const y = Number.isFinite(rawP.y) ? (rawP.y as number) : defaultData.player.y;
  const z = Number.isFinite(rawP.z) ? (rawP.z as number) : defaultData.player.z;
  const heading = Number.isFinite(rawP.heading) ? (rawP.heading as number) : defaultData.player.heading;
  const hp = Number.isFinite(rawP.hp) ? Math.max(1, Math.min(100, rawP.hp as number)) : 100;
  const armor = Number.isFinite(rawP.armor) ? Math.max(0, Math.min(100, rawP.armor as number)) : 50;
  const stamina = Number.isFinite(rawP.stamina) ? Math.max(0, Math.min(100, rawP.stamina as number)) : 100;
  const money = Number.isFinite(rawP.money) ? Math.max(0, rawP.money as number) : defaultData.player.money;
  const wantedLevel = Number.isFinite(rawP.wantedLevel) ? Math.max(0, Math.min(5, Math.floor(rawP.wantedLevel as number))) : 0;

  const validWeapons: WeaponId[] = ['fists', 'pistol', 'smg', 'shotgun'];
  const weapon = validWeapons.includes(rawP.weapon as WeaponId) ? (rawP.weapon as WeaponId) : 'fists';
  const unlockedWeapons = Array.isArray(rawP.unlockedWeapons)
    ? (rawP.unlockedWeapons.filter((w) => validWeapons.includes(w as WeaponId)) as WeaponId[])
    : defaultData.player.unlockedWeapons;
  if (!unlockedWeapons.includes('fists')) unlockedWeapons.unshift('fists');

  const validOutfits: OutfitId[] = ['street', 'panjabi', 'gulshan', 'monsoon'];
  const outfit = validOutfits.includes(rawP.outfit as OutfitId) ? (rawP.outfit as OutfitId) : 'street';
  const unlockedOutfits = Array.isArray(rawP.unlockedOutfits)
    ? (rawP.unlockedOutfits.filter((o) => validOutfits.includes(o as OutfitId)) as OutfitId[])
    : defaultData.player.unlockedOutfits;
  if (!unlockedOutfits.includes('street')) unlockedOutfits.unshift('street');

  const unlockedSafehouses = Array.isArray(rawP.unlockedSafehouses)
    ? (rawP.unlockedSafehouses as SafehouseId[])
    : defaultData.player.unlockedSafehouses;
  if (!unlockedSafehouses.includes('old_dhaka_rooftop')) unlockedSafehouses.unshift('old_dhaka_rooftop');

  const ammo = {
    fists: 0,
    pistol: Number.isFinite(rawP.ammo?.pistol) ? Math.max(0, rawP.ammo!.pistol) : 60,
    smg: Number.isFinite(rawP.ammo?.smg) ? Math.max(0, rawP.ammo!.smg) : 0,
    shotgun: Number.isFinite(rawP.ammo?.shotgun) ? Math.max(0, rawP.ammo!.shotgun) : 0,
  };

  const inventory = Array.isArray(rawP.inventory) ? rawP.inventory : defaultData.player.inventory;

  const missions = {
    missionIdx: Number.isFinite(rawM.missionIdx) ? Math.max(0, Math.floor(rawM.missionIdx as number)) : 0,
    stepIdx: Number.isFinite(rawM.stepIdx) ? Math.max(0, Math.floor(rawM.stepIdx as number)) : 0,
    missionsCompleted: Number.isFinite(rawM.missionsCompleted) ? Math.max(0, Math.floor(rawM.missionsCompleted as number)) : 0,
    faresCompleted: Number.isFinite(rawM.faresCompleted) ? Math.max(0, Math.floor(rawM.faresCompleted as number)) : 0,
    bountiesCompleted: Number.isFinite(rawM.bountiesCompleted) ? Math.max(0, Math.floor(rawM.bountiesCompleted as number)) : 0,
    carTheftsCompleted: Number.isFinite(rawM.carTheftsCompleted) ? Math.max(0, Math.floor(rawM.carTheftsCompleted as number)) : 0,
  };

  const world = {
    timeOfDay: Number.isFinite(rawW.timeOfDay) ? ((rawW.timeOfDay as number) % 24 + 24) % 24 : defaultData.world.timeOfDay,
    weather: rawW.weather === 'monsoon' || rawW.weather === 'storm' ? rawW.weather : 'clear',
    playTimeSec: Number.isFinite(rawW.playTimeSec) ? Math.max(0, rawW.playTimeSec as number) : 0,
  };

  const ownedVehicles = Array.isArray(data.ownedVehicles) ? data.ownedVehicles : [];

  return {
    version: CURRENT_SAVE_VERSION,
    timestamp: typeof data.timestamp === 'number' ? data.timestamp : Date.now(),
    player: {
      x,
      y,
      z,
      heading,
      hp,
      armor,
      stamina,
      money,
      wantedLevel,
      weapon,
      unlockedWeapons,
      ammo,
      outfit,
      unlockedOutfits,
      unlockedSafehouses,
      inventory,
    },
    missions,
    world,
    ownedVehicles,
  };
}

/**
 * Saves current game state to browser localStorage.
 */
export function saveDhakaGame(data: DhakaSaveGameData, storageKey = DHAKA_SAVE_STORAGE_KEY): boolean {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return false;
    data.timestamp = Date.now();
    const serialized = JSON.stringify(data);
    window.localStorage.setItem(storageKey, serialized);
    return true;
  } catch (err) {
    console.warn('[SaveSystem] Unable to write save game to localStorage:', err);
    return false;
  }
}

/**
 * Loads and validates saved game data from browser localStorage.
 * Returns null if no valid save exists.
 */
export function loadDhakaGame(storageKey = DHAKA_SAVE_STORAGE_KEY): DhakaSaveGameData | null {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return null;
    const rawStr = window.localStorage.getItem(storageKey);
    if (!rawStr) return null;
    const parsed = JSON.parse(rawStr);
    return validateAndSanitizeSaveData(parsed);
  } catch (err) {
    console.warn('[SaveSystem] Unable to load save game from localStorage:', err);
    return null;
  }
}

/**
 * Checks if a valid saved game currently exists.
 */
export function hasDhakaSaveGame(storageKey = DHAKA_SAVE_STORAGE_KEY): boolean {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return false;
    return Boolean(window.localStorage.getItem(storageKey));
  } catch {
    return false;
  }
}

/**
 * Clears saved game from localStorage.
 */
export function clearDhakaSaveGame(storageKey = DHAKA_SAVE_STORAGE_KEY): boolean {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return false;
    window.localStorage.removeItem(storageKey);
    return true;
  } catch {
    return false;
  }
}
