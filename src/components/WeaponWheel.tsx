import React, { useEffect, useState, useMemo } from 'react';
import { WeaponId, WEAPON_SPECS, WeaponSpec, InventoryItem } from '../game/types';

interface WeaponWheelProps {
  isOpen: boolean;
  activeWeapon: WeaponId;
  unlockedWeapons: WeaponId[];
  magAmmo: number;
  reserveAmmo: number;
  ammoMap?: Record<WeaponId, number>;
  magAmmoMap?: Record<WeaponId, number>;
  inventory?: InventoryItem[];
  onSelectWeapon: (weapon: WeaponId) => void;
  onUseItem?: (itemId: string) => void;
  onClose: () => void;
}

interface WheelSlice {
  id: WeaponId;
  spec: WeaponSpec;
  angle: number; // Center angle in degrees (0 = East, 90 = South, 180 = West, 270 = North)
  label: string;
  bangla: string;
  icon: string;
}

const WHEEL_SLICES: WheelSlice[] = [
  {
    id: 'fists',
    spec: WEAPON_SPECS.fists,
    angle: 270, // Top / North
    label: 'Melee Brawler',
    bangla: 'হাতাহাতি',
    icon: '👊',
  },
  {
    id: 'pistol',
    spec: WEAPON_SPECS.pistol,
    angle: 0, // Right / East
    label: '9mm Service',
    bangla: 'সার্ভিস পিস্তল',
    icon: '🔫',
  },
  {
    id: 'smg',
    spec: WEAPON_SPECS.smg,
    angle: 90, // Bottom / South
    label: 'Dhaka SMG',
    bangla: 'এসএমজি',
    icon: '💥',
  },
  {
    id: 'shotgun',
    spec: WEAPON_SPECS.shotgun,
    angle: 180, // Left / West
    label: '12G Shotgun',
    bangla: '১২-গেজ শটগান',
    icon: '🛑',
  },
];

export const WeaponWheel: React.FC<WeaponWheelProps> = ({
  isOpen,
  activeWeapon,
  unlockedWeapons,
  magAmmo,
  reserveAmmo,
  ammoMap = { fists: 0, pistol: 36, smg: 90, shotgun: 24 },
  magAmmoMap = { fists: 0, pistol: 12, smg: 30, shotgun: 6 },
  inventory = [],
  onSelectWeapon,
  onUseItem,
  onClose,
}) => {
  const [highlightedWeapon, setHighlightedWeapon] = useState<WeaponId>(activeWeapon);

  // Sync highlighted weapon when opening
  useEffect(() => {
    if (isOpen) {
      setHighlightedWeapon(activeWeapon);
    }
  }, [isOpen, activeWeapon]);

  // Track mouse coordinates to calculate angular wedge selection
  useEffect(() => {
    if (!isOpen) return;

    const handleMouseMove = (e: MouseEvent) => {
      const centerX = window.innerWidth / 2;
      const centerY = window.innerHeight / 2;
      const dx = e.clientX - centerX;
      const dy = e.clientY - centerY;
      const dist = Math.hypot(dx, dy);

      // Deadzone inside center info card
      if (dist < 40) return;

      // Calculate angle in degrees [0, 360) where 0 is East
      let deg = (Math.atan2(dy, dx) * 180) / Math.PI;
      if (deg < 0) deg += 360;

      // Find closest slice
      // Slices: 270 (North: fists: 225-315), 0 (East: pistol: 315-45), 90 (South: smg: 45-135), 180 (West: shotgun: 135-225)
      let selected: WeaponId = 'fists';
      if (deg >= 315 || deg < 45) {
        selected = 'pistol';
      } else if (deg >= 45 && deg < 135) {
        selected = 'smg';
      } else if (deg >= 135 && deg < 225) {
        selected = 'shotgun';
      } else {
        selected = 'fists';
      }

      if (unlockedWeapons.includes(selected)) {
        setHighlightedWeapon(selected);
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'Digit1' && unlockedWeapons.includes('fists')) setHighlightedWeapon('fists');
      if (e.code === 'Digit2' && unlockedWeapons.includes('pistol')) setHighlightedWeapon('pistol');
      if (e.code === 'Digit3' && unlockedWeapons.includes('smg')) setHighlightedWeapon('smg');
      if (e.code === 'Digit4' && unlockedWeapons.includes('shotgun')) setHighlightedWeapon('shotgun');
      if (e.code === 'Escape') onClose();
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, unlockedWeapons, onClose]);

  const activeSpec = WEAPON_SPECS[highlightedWeapon] || WEAPON_SPECS.fists;
  const isUnlocked = unlockedWeapons.includes(highlightedWeapon);

  // Quick consumable item (e.g. Haji Biriyani or First Aid)
  const quickFood = useMemo(
    () => inventory.find((i) => i.kind === 'food' || i.kind === 'medical'),
    [inventory],
  );

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/65 backdrop-blur-md select-none transition-all duration-200 pointer-events-auto"
      onClick={() => {
        onSelectWeapon(highlightedWeapon);
        onClose();
      }}
    >
      {/* Background vignette & GTA VI slow-mo chromatic pulse */}
      <div className="absolute inset-0 pointer-events-none bg-[radial-gradient(circle_at_center,transparent_20%,rgba(0,0,0,0.85)_100%)]" />

      {/* Header GTA VI Banner */}
      <div className="absolute top-8 left-1/2 -translate-x-1/2 text-center pointer-events-none">
        <div className="text-[11px] font-bold tracking-[0.35em] text-[#f5d07a] uppercase font-display drop-shadow-[0_2px_8px_rgba(245,208,122,0.4)]">
          TACTICAL WEAPON SELECTION · অস্ত্র নির্বাচন
        </div>
        <div className="text-xs text-white/50 tracking-wider mt-0.5">
          Release <kbd className="px-1.5 py-0.5 bg-white/10 rounded text-white font-mono font-bold">[TAB]</kbd> or click to equip
        </div>
      </div>

      {/* Circular Radial Selector */}
      <div
        className="relative w-80 h-80 sm:w-96 sm:h-96 flex items-center justify-center"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Outer Circular Ring Background */}
        <svg viewBox="0 0 200 200" className="absolute inset-0 w-full h-full filter drop-shadow-[0_0_24px_rgba(0,0,0,0.9)]">
          {/* Subtle Outer Track */}
          <circle cx="100" cy="100" r="92" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="2" />
          <circle cx="100" cy="100" r="46" fill="none" stroke="rgba(255,255,255,0.12)" strokeWidth="1.5" />
        </svg>

        {/* 4 Quadrant Slices */}
        {WHEEL_SLICES.map((slice) => {
          const isSelected = highlightedWeapon === slice.id;
          const unlocked = unlockedWeapons.includes(slice.id);
          const currentMag = isSelected && slice.id === activeWeapon ? magAmmo : (magAmmoMap[slice.id] ?? slice.spec.magSize);
          const currentReserve = isSelected && slice.id === activeWeapon ? reserveAmmo : (ammoMap[slice.id] ?? 0);

          // Positioning offsets based on angle
          let posClass = '';
          if (slice.angle === 270) posClass = 'top-2 sm:top-4 left-1/2 -translate-x-1/2'; // North
          if (slice.angle === 0) posClass = 'right-2 sm:right-4 top-1/2 -translate-y-1/2'; // East
          if (slice.angle === 90) posClass = 'bottom-2 sm:bottom-4 left-1/2 -translate-x-1/2'; // South
          if (slice.angle === 180) posClass = 'left-2 sm:left-4 top-1/2 -translate-y-1/2'; // West

          return (
            <button
              key={slice.id}
              type="button"
              disabled={!unlocked}
              onClick={() => {
                if (unlocked) {
                  setHighlightedWeapon(slice.id);
                  onSelectWeapon(slice.id);
                  onClose();
                }
              }}
              onMouseEnter={() => {
                if (unlocked) setHighlightedWeapon(slice.id);
              }}
              className={`absolute ${posClass} w-28 sm:w-32 py-2 px-2.5 rounded-lg border transition-all duration-150 flex flex-col items-center justify-center cursor-pointer ${
                isSelected
                  ? 'bg-[#15342a]/95 border-[#22c55e] shadow-[0_0_20px_rgba(34,197,94,0.45)] scale-105 z-20'
                  : unlocked
                    ? 'bg-[#111720]/85 border-white/20 hover:border-white/50 hover:bg-[#1a2330]/90 text-white/90 z-10'
                    : 'bg-[#0b0e13]/60 border-white/5 text-white/30 cursor-not-allowed z-0'
              }`}
            >
              <span className="text-xl sm:text-2xl mb-1 filter drop-shadow">{slice.icon}</span>
              <span
                className={`text-[11px] font-bold tracking-wider uppercase font-display text-center truncate w-full ${
                  isSelected ? 'text-[#4ade80]' : unlocked ? 'text-white' : 'text-white/30'
                }`}
              >
                {slice.label}
              </span>
              <span className="text-[10px] text-white/60 font-bangla -mt-0.5">{slice.bangla}</span>

              {/* Ammo Pill */}
              {slice.id !== 'fists' ? (
                <div className="mt-1 flex items-center gap-1 text-[10px] font-mono font-bold tabular-nums text-white/80">
                  <span className={isSelected ? 'text-[#4ade80]' : 'text-white'}>{currentMag}</span>
                  <span className="text-white/40">/</span>
                  <span className="text-white/50">{currentReserve}</span>
                </div>
              ) : (
                <div className="mt-1 text-[10px] font-mono font-bold text-white/40 uppercase">Melee</div>
              )}
            </button>
          );
        })}

        {/* Center Card: High-Tech Weapon Specs & Weapon Data */}
        <div className="w-36 h-36 sm:w-40 sm:h-40 rounded-full bg-[#0d1219]/95 border border-white/25 shadow-[0_0_30px_rgba(0,0,0,0.95)] flex flex-col items-center justify-center p-3 text-center pointer-events-none z-30">
          <div className="text-[11px] font-bold text-[#f5d07a] uppercase font-display leading-tight truncate w-full">
            {activeSpec.name}
          </div>
          <div className="text-[9px] text-white/60 font-medium truncate w-full mb-1.5">
            {activeSpec.role}
          </div>

          {/* Stat Bars */}
          <div className="w-full flex flex-col gap-1 text-[9px] font-mono text-white/70">
            {/* Damage */}
            <div className="flex items-center justify-between gap-1">
              <span className="text-[8px] uppercase tracking-wider text-white/50">DMG</span>
              <div className="w-16 h-1.5 bg-white/10 rounded-full overflow-hidden">
                <div
                  className="h-full bg-[#f87171] rounded-full transition-all duration-200"
                  style={{ width: `${Math.min(100, (activeSpec.damage / 60) * 100)}%` }}
                />
              </div>
            </div>
            {/* Range */}
            <div className="flex items-center justify-between gap-1">
              <span className="text-[8px] uppercase tracking-wider text-white/50">RNG</span>
              <div className="w-16 h-1.5 bg-white/10 rounded-full overflow-hidden">
                <div
                  className="h-full bg-[#38bdf8] rounded-full transition-all duration-200"
                  style={{ width: `${Math.min(100, (activeSpec.range / 30) * 100)}%` }}
                />
              </div>
            </div>
            {/* Fire Rate */}
            <div className="flex items-center justify-between gap-1">
              <span className="text-[8px] uppercase tracking-wider text-white/50">ROF</span>
              <div className="w-16 h-1.5 bg-white/10 rounded-full overflow-hidden">
                <div
                  className="h-full bg-[#fbbf24] rounded-full transition-all duration-200"
                  style={{ width: `${Math.min(100, Math.max(10, (1 / activeSpec.cooldown) * 15))}%` }}
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Tactical Consumable Quick Slot */}
      {quickFood && onUseItem && (
        <div
          className="absolute bottom-8 left-1/2 -translate-x-1/2 flex items-center gap-3 bg-[#111720]/90 border border-white/20 rounded-full px-4 py-2 shadow-lg cursor-pointer hover:border-[#f5d07a] transition-all"
          onClick={(e) => {
            e.stopPropagation();
            onUseItem(quickFood.id);
          }}
        >
          <span className="text-xl">🍲</span>
          <div className="text-left">
            <div className="text-xs font-bold text-[#f5d07a] leading-none font-display">
              {quickFood.name} ({quickFood.count}x)
            </div>
            <div className="text-[10px] text-white/60 font-bangla">{quickFood.banglaName} · Click to Consume</div>
          </div>
        </div>
      )}
    </div>
  );
};
