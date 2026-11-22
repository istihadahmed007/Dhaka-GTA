import React from 'react';
import {
  HudSnapshot,
  OutfitId,
  OUTFIT_SPECS,
  TimePreset,
  WeaponId,
  WEAPON_SPECS,
  WeatherMode,
} from '../game/types';

interface OverlaysProps {
  hud: HudSnapshot;
  onResume: () => void;
  onRestart: () => void;
  onTitle: () => void;
  onCloseModal: () => void;
  onSetTime: (t: TimePreset) => void;
  onSetWeather: (w: WeatherMode) => void;
  onShopBuy: (item: 'biriyani' | 'armor' | WeaponId) => void;
  onGarageService: (svc: 'repair' | 'clear_heat' | 'paint', color?: number) => void;
  onOutfitSelect: (id: OutfitId) => void;
}

const PAINT_SWATCHES: Array<{ label: string; hex: number; css: string }> = [
  { label: 'Dhaka CNG Emerald', hex: 0x0f6a44, css: '#0f6a44' },
  { label: 'Bengal Crimson', hex: 0xb91c1c, css: '#b91c1c' },
  { label: 'Gulshan Pearl', hex: 0xe2e8f0, css: '#e2e8f0' },
  { label: 'Saffron Marigold', hex: 0xd97706, css: '#d97706' },
  { label: 'Padma Midnight', hex: 0x1d4ed8, css: '#1d4ed8' },
  { label: 'Matte Obsidian', hex: 0x181c22, css: '#181c22' },
];

export const Overlays: React.FC<OverlaysProps> = ({
  hud,
  onResume,
  onRestart,
  onTitle,
  onCloseModal,
  onSetTime,
  onSetWeather,
  onShopBuy,
  onGarageService,
  onOutfitSelect,
}) => {
  // 1. PAUSE MODAL
  if (hud.phase === 'paused') {
    return (
      <div className="fixed inset-0 z-30 flex items-center justify-center bg-[#070a0f]/82 p-4">
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Game Paused"
          className="w-full max-w-lg bg-[#111720] border-t-4 border-[#e6a135] border-x border-b border-white/15 p-6 shadow-[0_24px_60px_rgba(0,0,0,0.85)]"
        >
          <div className="flex items-baseline justify-between border-b border-white/10 pb-3">
            <h2 className="font-display text-3xl font-extrabold uppercase tracking-wider text-[#f8f5ee]">
              GAME PAUSED
            </h2>
            <span className="font-bangla text-sm text-[#f5d07a]">বিরতি · PROJECT DHAKA</span>
          </div>

          {/* Environment quick controls */}
          <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="bg-[#0b0f16] p-3 border border-white/10">
              <div className="text-[11px] font-bold uppercase tracking-wider text-[#94a3b8] mb-2">
                Dhaka Time of Day [T]
              </div>
              <div className="grid grid-cols-2 gap-1.5">
                {(['dawn', 'day', 'sunset', 'night'] as TimePreset[]).map((t) => (
                  <button
                    key={t}
                    type="button"
                    onClick={() => onSetTime(t)}
                    className="px-2 py-1.5 text-xs font-semibold uppercase bg-[#1b2432] hover:bg-[#0f7a53] text-[#f5f2eb] transition-colors cursor-pointer"
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>

            <div className="bg-[#0b0f16] p-3 border border-white/10">
              <div className="text-[11px] font-bold uppercase tracking-wider text-[#94a3b8] mb-2">
                Monsoon Weather [V]
              </div>
              <div className="grid grid-cols-1 gap-1.5">
                {(['clear', 'monsoon', 'storm'] as WeatherMode[]).map((w) => (
                  <button
                    key={w}
                    type="button"
                    onClick={() => onSetWeather(w)}
                    className={`px-2 py-1 text-xs font-semibold uppercase transition-colors cursor-pointer ${
                      hud.weather === w
                        ? 'bg-[#e6a135] text-[#0b0e13]'
                        : 'bg-[#1b2432] hover:bg-[#0f7a53] text-[#f5f2eb]'
                    }`}
                  >
                    {w === 'clear' ? 'Clear Tropical' : w === 'monsoon' ? 'Monsoon Rain' : 'Thunderstorm'}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="mt-4 bg-[#0b0f16] p-3 border border-white/10 text-xs text-[#cbd5e1] leading-relaxed">
            <strong className="text-[#f5d07a] uppercase">Controls:</strong> WASD Move/Drive · Space Jump/Handbrake ·
            Q/E or Right-Drag Camera · F Enter/Exit Vehicle · Left Click / J Attack · 1–4 Weapons · R Reload · H Horn ·
            G Shop/Garage · C Wardrobe · N CNG Fare Job
          </div>

          <div className="mt-6 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={onResume}
              className="flex-1 px-5 py-3 bg-[#0f7a53] hover:bg-[#149465] text-[#f8f5ee] font-display text-lg font-extrabold uppercase tracking-wider cursor-pointer"
            >
              Resume Dhaka
            </button>
            <button
              type="button"
              onClick={onRestart}
              className="px-5 py-3 bg-[#1e293b] hover:bg-[#334155] text-[#f8f5ee] font-display text-lg font-bold uppercase tracking-wider cursor-pointer"
            >
              Restart
            </button>
            <button
              type="button"
              onClick={onTitle}
              className="px-4 py-3 bg-[#171c24] hover:bg-[#991b1b] text-[#e2e8f0] font-display text-lg font-bold uppercase tracking-wider cursor-pointer"
            >
              Title
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 2. GAME OVER / VICTORY MODAL
  if (hud.phase === 'gameover' || hud.phase === 'victory') {
    const isVictory = hud.phase === 'victory';
    return (
      <div className="fixed inset-0 z-30 flex items-center justify-center bg-[#070a0f]/86 p-4">
        <div
          role="dialog"
          aria-modal="true"
          aria-label={isVictory ? 'Victory' : 'Game Over'}
          className={`w-full max-w-md bg-[#111720] border-t-4 ${
            isVictory ? 'border-[#e6a135]' : 'border-[#dc2626]'
          } border-x border-b border-white/15 p-7 shadow-[0_24px_60px_rgba(0,0,0,0.9)] text-center`}
        >
          <div className="font-bangla text-sm text-[#f5d07a]">
            {isVictory ? 'ঢাকার রাজা · অভিযান সম্পন্ন' : 'অভিযান ব্যর্থ'}
          </div>
          <h2
            className={`font-display text-5xl font-extrabold uppercase tracking-wider mt-1 ${
              isVictory ? 'text-[#fbbf24]' : 'text-[#ef4444]'
            }`}
          >
            {isVictory ? 'KING OF DHAKA' : hud.stats.cause || 'WASTED'}
          </h2>
          <p className="text-sm text-[#cbd5e1] mt-2">
            {isVictory
              ? 'You dismantled the Padma Syndicate from Old Dhaka to Gulshan Plaza.'
              : 'The streets of Dhaka never forgive a mistake. Regroup and hit the avenue again.'}
          </p>

          <div className="mt-6 grid grid-cols-3 gap-2.5">
            <div className="bg-[#0b0f16] border border-white/10 p-3">
              <div className="text-[10px] uppercase tracking-wider text-[#94a3b8]">Missions</div>
              <div className="font-display text-2xl font-extrabold text-[#f5d07a] tabular-nums">
                {hud.stats.missionsCompleted}/{hud.stats.totalMissions}
              </div>
            </div>
            <div className="bg-[#0b0f16] border border-white/10 p-3">
              <div className="text-[10px] uppercase tracking-wider text-[#94a3b8]">Takedowns</div>
              <div className="font-display text-2xl font-extrabold text-[#f8f5ee] tabular-nums">
                {hud.stats.kills}
              </div>
            </div>
            <div className="bg-[#0b0f16] border border-white/10 p-3">
              <div className="text-[10px] uppercase tracking-wider text-[#94a3b8]">Taka Earned</div>
              <div className="font-display text-2xl font-extrabold text-[#34d399] tabular-nums">
                ৳{hud.money}
              </div>
            </div>
          </div>

          <div className="mt-6 flex gap-3">
            <button
              type="button"
              data-start="true"
              onClick={onRestart}
              className="flex-1 px-6 py-3.5 bg-[#0f7a53] hover:bg-[#149465] text-[#f8f5ee] font-display text-xl font-extrabold uppercase tracking-wider cursor-pointer"
            >
              {isVictory ? 'Play Again' : 'Try Again'}
            </button>
            <button
              type="button"
              onClick={onTitle}
              className="px-5 py-3.5 bg-[#1e293b] hover:bg-[#334155] text-[#e2e8f0] font-display text-lg font-bold uppercase tracking-wider cursor-pointer"
            >
              Title
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 3. INTERACTIVE CITY MODALS (Dhaka Customs Garage, Babu's Shop, Character Wardrobe)
  if (hud.modal === 'none') return null;

  return (
    <div className="fixed inset-0 z-30 flex items-center justify-center bg-[#070a0f]/80 p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-label={
          hud.modal === 'garage'
            ? 'Dhaka Customs Garage'
            : hud.modal === 'shop'
              ? "Babu's Hardware and Gear"
              : 'Character Wardrobe'
        }
        className="w-full max-w-xl bg-[#111720] border-t-4 border-[#e6a135] border-x border-b border-white/15 p-6 shadow-[0_24px_60px_rgba(0,0,0,0.88)]"
      >
        <div className="flex items-center justify-between border-b border-white/10 pb-3">
          <div>
            <h2 className="font-display text-2xl sm:text-3xl font-extrabold uppercase tracking-wider text-[#f8f5ee]">
              {hud.modal === 'garage'
                ? 'DHAKA CUSTOMS GARAGE'
                : hud.modal === 'shop'
                  ? "BABU'S HARDWARE & GEAR"
                  : "TARIQ'S WARDROBE"}
            </h2>
            <p className="text-xs text-[#94a3b8]">
              Balance: <span className="font-bold text-[#34d399] tabular-nums">৳{hud.money.toLocaleString()}</span>
            </p>
          </div>
          <button
            type="button"
            onClick={onCloseModal}
            aria-label="Close menu"
            className="px-3 py-1.5 bg-[#1e293b] hover:bg-[#991b1b] text-xs font-bold uppercase tracking-wider text-[#f8f5ee] cursor-pointer"
          >
            Close [Esc]
          </button>
        </div>

        {hud.modal === 'garage' && (
          <div className="mt-4 flex flex-col gap-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() => onGarageService('repair')}
                className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] transition-colors cursor-pointer"
              >
                <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Full Engine & Body Repair</div>
                <div className="text-xs text-[#94a3b8] mt-0.5">Restores active or nearby vehicle to 100% HP</div>
                <div className="mt-2 font-display text-base font-bold text-[#34d399] tabular-nums">৳200</div>
              </button>

              <button
                type="button"
                onClick={() => onGarageService('clear_heat')}
                className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] transition-colors cursor-pointer"
              >
                <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Swap Plates & Clear Heat</div>
                <div className="text-xs text-[#94a3b8] mt-0.5">Instantly wipes DMP Wanted Stars</div>
                <div className="mt-2 font-display text-base font-bold text-[#f5d07a] tabular-nums">৳300</div>
              </button>
            </div>

            <div>
              <div className="text-xs font-bold uppercase tracking-wider text-[#94a3b8] mb-2">
                Custom Dhaka Respray (৳150)
              </div>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                {PAINT_SWATCHES.map((sw) => (
                  <button
                    key={sw.label}
                    type="button"
                    onClick={() => onGarageService('paint', sw.hex)}
                    className="flex items-center gap-2.5 p-2 bg-[#0b0f16] border border-white/12 hover:border-white/50 text-left cursor-pointer"
                  >
                    <span className="w-5 h-5 shrink-0 border border-white/30" style={{ backgroundColor: sw.css }} />
                    <span className="text-xs font-semibold text-[#e2e8f0] leading-tight">{sw.label}</span>
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {hud.modal === 'shop' && (
          <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
            <button
              type="button"
              onClick={() => onShopBuy('biriyani')}
              className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] cursor-pointer"
            >
              <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Old Dhaka Kacchi Biriyani</div>
              <div className="text-xs text-[#94a3b8]">Restores Health to 100 HP</div>
              <div className="mt-2 font-display text-base font-bold text-[#34d399] tabular-nums">৳150</div>
            </button>

            <button
              type="button"
              onClick={() => onShopBuy('armor')}
              className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] cursor-pointer"
            >
              <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Tactical Street Vest</div>
              <div className="text-xs text-[#94a3b8]">Refills Armor to 100</div>
              <div className="mt-2 font-display text-base font-bold text-[#38bdf8] tabular-nums">৳400</div>
            </button>

            {(['pistol', 'smg', 'shotgun'] as WeaponId[]).map((wId) => {
              const spec = WEAPON_SPECS[wId];
              const owned = hud.unlockedWeapons.includes(wId);
              return (
                <button
                  key={wId}
                  type="button"
                  onClick={() => onShopBuy(wId)}
                  className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] cursor-pointer"
                >
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="font-display text-lg font-bold uppercase text-[#f8f5ee]">{spec.name}</span>
                    <span className="text-[10px] uppercase text-[#94a3b8]">[{spec.key}]</span>
                  </div>
                  <div className="text-xs text-[#94a3b8]">{spec.role}</div>
                  <div className="mt-2 font-display text-base font-bold text-[#f5d07a] tabular-nums">
                    {owned ? `Buy Ammo · ৳${spec.ammoPrice}` : `Unlock · ৳${spec.price}`}
                  </div>
                </button>
              );
            })}
          </div>
        )}

        {hud.modal === 'wardrobe' && (
          <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
            {(Object.keys(OUTFIT_SPECS) as OutfitId[]).map((oId) => {
              const spec = OUTFIT_SPECS[oId];
              const owned = hud.unlockedOutfits.includes(oId);
              const active = hud.outfit === oId;
              return (
                <button
                  key={oId}
                  type="button"
                  onClick={() => onOutfitSelect(oId)}
                  className={`p-3.5 text-left border transition-colors cursor-pointer ${
                    active
                      ? 'bg-[#0f7a53]/25 border-[#34d399]'
                      : 'bg-[#0b0f16] border-white/15 hover:border-[#f5d07a]'
                  }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-display text-lg font-bold uppercase text-[#f8f5ee]">{spec.name}</span>
                    <span
                      className="w-4 h-4 rounded-full border border-white/40"
                      style={{ backgroundColor: `#${spec.torsoColor.toString(16).padStart(6, '0')}` }}
                    />
                  </div>
                  <div className="font-bangla text-xs text-[#f5d07a]">{spec.banglaName}</div>
                  <div className="text-xs text-[#94a3b8] mt-1">{spec.description}</div>
                  <div className="mt-2 text-xs font-bold uppercase tracking-wider text-[#34d399] tabular-nums">
                    {active ? 'Equipped' : owned ? 'Equip Outfit' : `Unlock · ৳${spec.price}`}
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};
