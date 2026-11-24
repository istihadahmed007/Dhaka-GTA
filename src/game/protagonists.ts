import { WeaponId, OutfitId } from './types';

export type ProtagonistId = 'tariq' | 'maya';

export type SpecialAbilityKind = 'street_overdrive' | 'focus_reflex';

export interface ProtagonistProfile {
  id: ProtagonistId;
  name: string;
  banglaName: string;
  title: string;
  banglaTitle: string;
  bio: string;
  banglaBio: string;
  district: string;
  banglaDistrict: string;
  specialAbility: SpecialAbilityKind;
  abilityName: string;
  abilityBangla: string;
  abilityDescription: string;
  abilityDuration: number; // Duration in seconds
  abilityCooldown: number; // Cooldown in seconds
  defaultColors: {
    torso: number;
    pants: number;
    accent: number;
  };
  preferredWeapon: WeaponId;
  defaultOutfit: OutfitId;
  attributes: {
    maxHp: number;
    maxArmor: number;
    maxStamina: number;
    walkSpeed: number;
    runSpeed: number;
    sprintSpeed: number;
    driftHandlingBonus: number; // Higher drift stability and torque for Tariq
    aimPrecisionMultiplier: number; // Tighter bullet spread & zoom for Maya
    meleeDamageMultiplier: number;
    collisionResistance: number;
  };
}

export const PROTAGONIST_PROFILES: Record<ProtagonistId, ProtagonistProfile> = {
  tariq: {
    id: 'tariq',
    name: 'Tariq',
    banglaName: 'তারেক',
    title: 'The Master Wheelman',
    banglaTitle: 'পুরান ঢাকার ওস্তাদ ড্রাইভার',
    bio: 'Born in Chawkbazar, Tariq learned mechanics in Dholaikhal and street racing through narrow Old Dhaka alleys. Known for daring escapes and unmatched vehicle handling.',
    banglaBio: 'চকবাজারে জন্ম, ধোলাইখালের গ্যারেজে বড় হওয়া তারেক। পুরান ঢাকার অলিগলিতে গাড়ি চালিয়ে সিন্ডিকেটের হাত থেকে বাঁচার ওস্তাদ।',
    district: 'Old Dhaka',
    banglaDistrict: 'পুরান ঢাকা',
    specialAbility: 'street_overdrive',
    abilityName: 'Street Overdrive',
    abilityBangla: 'স্ট্রিট ওভারড্রাইভ',
    abilityDescription: 'Unleashes maximum engine torque, instant handbrake drift recovery, and doubled melee shockwave damage.',
    abilityDuration: 12.0,
    abilityCooldown: 30.0,
    defaultColors: {
      torso: 0x1e3a5f, // Deep indigo henley / jacket
      pants: 0x1f242d, // Dark denim jeans
      accent: 0xd4a017, // Brass belt & leather wristband
    },
    preferredWeapon: 'shotgun',
    defaultOutfit: 'street',
    attributes: {
      maxHp: 125,
      maxArmor: 100,
      maxStamina: 100,
      walkSpeed: 4.8,
      runSpeed: 8.4,
      sprintSpeed: 10.8,
      driftHandlingBonus: 0.45,
      aimPrecisionMultiplier: 1.0,
      meleeDamageMultiplier: 1.45,
      collisionResistance: 1.35,
    },
  },
  maya: {
    id: 'maya',
    name: 'Maya',
    banglaName: 'মায়া',
    title: 'Undercover Operative',
    banglaTitle: 'গুলশানের আন্ডারকভার অপারেটিভ',
    bio: 'Raised between Dhanmondi and Gulshan, Maya is a razor-sharp tech operative and investigative journalist uncovering the syndicate ties behind Dhaka megaprojects.',
    banglaBio: 'ধানমন্ডি ও গুলশানে বেড়ে ওঠা মায়া একাধারে প্রযুক্তি বিশেষজ্ঞ ও নির্ভীক অনুসন্ধানী সাংবাদিক। সিন্ডিকেটের গোপন নেটওয়ার্ক উন্মোচনে তার জুড়ি নেই।',
    district: 'Gulshan',
    banglaDistrict: 'গুলশান',
    specialAbility: 'focus_reflex',
    abilityName: 'Focus Reflex',
    abilityBangla: 'ফোকাস রিফ্লেক্স',
    abilityDescription: 'Slows down perception of time (bullet-time), eliminates weapon recoil, and grants silent high-speed sprint.',
    abilityDuration: 9.0,
    abilityCooldown: 25.0,
    defaultColors: {
      torso: 0x8a1c27, // Crimson/graphite sleek tactical jacket
      pants: 0x14181f, // Tactical carbon pants
      accent: 0x00d2d3, // Cyber-cyan smartwatch & collar trim
    },
    preferredWeapon: 'pistol',
    defaultOutfit: 'gulshan',
    attributes: {
      maxHp: 100,
      maxArmor: 120,
      maxStamina: 130,
      walkSpeed: 5.2,
      runSpeed: 9.2,
      sprintSpeed: 11.8,
      driftHandlingBonus: 0.1,
      aimPrecisionMultiplier: 1.65,
      meleeDamageMultiplier: 1.0,
      collisionResistance: 1.0,
    },
  },
};

export interface ProtagonistInstanceState {
  id: ProtagonistId;
  x: number;
  y: number;
  z: number;
  heading: number;
  hp: number;
  armor: number;
  stamina: number;
  money: number;
  weapon: WeaponId;
  unlockedWeapons: WeaponId[];
  ammo: Record<WeaponId, number>;
  magAmmo: Record<WeaponId, number>;
  outfit: OutfitId;
  abilityTimer: number;
  abilityCooldownTimer: number;
  abilityActive: boolean;
}

export function createInitialProtagonistState(id: ProtagonistId, startX = 0, startZ = 0): ProtagonistInstanceState {
  const profile = PROTAGONIST_PROFILES[id];
  return {
    id,
    x: startX,
    y: 0,
    z: startZ,
    heading: 0,
    hp: profile.attributes.maxHp,
    armor: 50,
    stamina: profile.attributes.maxStamina,
    money: id === 'tariq' ? 1200 : 2500,
    weapon: profile.preferredWeapon,
    unlockedWeapons: ['fists', profile.preferredWeapon],
    ammo: { fists: 0, pistol: 48, smg: 90, shotgun: 24 },
    magAmmo: { fists: 0, pistol: 12, smg: 30, shotgun: 6 },
    outfit: profile.defaultOutfit,
    abilityTimer: 0,
    abilityCooldownTimer: 0,
    abilityActive: false,
  };
}

export type CharacterSwitchPhase = 'none' | 'ascend' | 'pan' | 'descend';

export interface CharacterSwitchState {
  active: boolean;
  phase: CharacterSwitchPhase;
  timer: number;
  duration: number;
  fromId: ProtagonistId;
  toId: ProtagonistId;
  fromPos: { x: number; y: number; z: number };
  toPos: { x: number; y: number; z: number };
  skyAltitude: number;
  bannerText: string;
  bannerBangla: string;
}

export function createInitialCharacterSwitchState(): CharacterSwitchState {
  return {
    active: false,
    phase: 'none',
    timer: 0,
    duration: 2.4,
    fromId: 'tariq',
    toId: 'maya',
    fromPos: { x: 0, y: 0, z: 0 },
    toPos: { x: 0, y: 0, z: 0 },
    skyAltitude: 55,
    bannerText: '',
    bannerBangla: '',
  };
}
