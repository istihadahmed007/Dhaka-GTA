import React, { useEffect, useState } from 'react';
import { HudSnapshot, WeaponId, WEAPON_SPECS } from '../game/types';
import { Minimap } from './Minimap';

interface GameHudProps {
  hud: HudSnapshot;
  onToggleMute: () => void;
  onTogglePause: () => void;
  onOpenWardrobe: () => void;
  onSelectWeapon: (w: WeaponId) => void;
  onToggleFare: () => void;
  onUseItem?: (itemId: string) => void;
  onOpenMultiplayerLobby?: () => void;
  onSendChatMessage?: (text: string) => void;
  onSwitchProtagonist?: () => void;
  onToggleAbility?: () => void;
  onCycleCamera?: () => void;
}

const WEAPON_ORDER: WeaponId[] = ['fists', 'pistol', 'smg', 'shotgun'];

export const GameHud: React.FC<GameHudProps> = ({
  hud,
  onToggleMute,
  onTogglePause,
  onOpenWardrobe,
  onSelectWeapon,
  onToggleFare,
  onUseItem,
  onOpenMultiplayerLobby,
  onSendChatMessage,
  onSwitchProtagonist,
  onToggleAbility,
  onCycleCamera,
}) => {
  // Delayed trailing damage ghost bar for player HP
  const [ghostHp, setGhostHp] = useState(hud.hp);
  const [chatOpen, setChatOpen] = useState(false);
  const [chatText, setChatText] = useState('');

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'KeyT' && !chatOpen && hud.multiplayerActive) {
        const activeTag = document.activeElement?.tagName.toLowerCase();
        if (activeTag !== 'input' && activeTag !== 'textarea') {
          e.preventDefault();
          setChatOpen(true);
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [chatOpen, hud.multiplayerActive]);

  useEffect(() => {
    if (hud.hp >= ghostHp) {
      setGhostHp(hud.hp);
      return;
    }
    const timer = window.setTimeout(() => setGhostHp(hud.hp), 360);
    return () => window.clearTimeout(timer);
  }, [hud.hp, ghostHp]);

  const hpPct = Math.max(0, Math.min(100, (hud.hp / hud.maxHp) * 100));
  const ghostPct = Math.max(hpPct, Math.min(100, (ghostHp / hud.maxHp) * 100));
  const armorPct = Math.max(0, Math.min(100, (hud.armor / hud.maxArmor) * 100));
  const staminaPct = Math.max(0, Math.min(100, hud.stamina));
  const activeWeaponSpec = WEAPON_SPECS[hud.weapon];

  return (
    <div className="fixed inset-0 z-10 pointer-events-none">
      {/* Subtle critical low-HP peripheral vignette */}
      {hud.hp > 0 && hud.hp <= 30 && (
        <div
          className="fixed inset-0 pointer-events-none"
          style={{
            boxShadow: 'inset 0 0 110px rgba(220, 38, 38, 0.55)',
          }}
        />
      )}

      {/* Center Reticle (only when holding a firearm on foot) */}
      {!hud.inVehicle && hud.weapon !== 'fists' && hud.modal === 'none' && (
        <div className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 pointer-events-none">
          <svg viewBox="0 0 32 32" className="w-7 h-7" aria-hidden="true">
            <circle cx="16" cy="16" r="2.2" fill="#f8fafc" stroke="#0b0e13" strokeWidth="1.2" />
            <path d="M16 4v5M16 23v5M4 16h5M23 16h5" stroke="#f8fafc" strokeWidth="1.6" strokeLinecap="round" />
          </svg>
        </div>
      )}

      {/* TOP-LEFT: Compact Active Mission Objective & Toast */}
      <div className="fixed top-4 left-4 max-w-[330px] flex flex-col gap-2">
        {hud.activeSideMission && (
          <div className="bg-[#181326]/95 border-l-4 border-[#a855f7] px-3.5 py-2 shadow-[0_6px_18px_rgba(0,0,0,0.6)]">
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-display text-sm font-bold uppercase tracking-wider text-[#d8b4fe]">
                {hud.activeSideMission.title}
              </span>
              <span className="font-bangla text-[11px] text-[#e9d5ff]/80">{hud.activeSideMission.banglaTitle}</span>
            </div>
            <p className="text-xs text-[#f5f2eb] mt-0.5 leading-snug">{hud.activeSideMission.objective}</p>
            <div className="mt-1 flex items-center justify-between text-[11px] font-bold text-[#c084fc] tabular-nums">
              <span>Reward: ৳{hud.activeSideMission.rewardMoney}</span>
              {hud.activeSideMission.timer && (
                <span className="text-[#f87171]">{Math.ceil(hud.activeSideMission.timer)}s</span>
              )}
            </div>
          </div>
        )}

        {hud.missionTitle && (
          <div className="bg-[#10151c]/90 border-l-4 border-[#e6a135] px-3.5 py-2 shadow-[0_6px_18px_rgba(0,0,0,0.6)]">
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-display text-sm font-bold uppercase tracking-wider text-[#f5d07a]">
                {hud.missionTitle}
              </span>
              {hud.missionBangla && (
                <span className="font-bangla text-[11px] text-[#e5e0d5]/80">{hud.missionBangla}</span>
              )}
            </div>
            <p className="text-xs text-[#f5f2eb] mt-0.5 leading-snug">{hud.missionObjective}</p>
            {hud.missionProgressText && (
              <div className="mt-1 text-[11px] font-bold uppercase tracking-wider text-[#f87171] tabular-nums">
                {hud.missionProgressText}
              </div>
            )}
          </div>
        )}

        {hud.fareActive && hud.fareText && (
          <div className="bg-[#0e1b24]/92 border-l-4 border-[#22d3ee] px-3.5 py-1.5 shadow-[0_4px_14px_rgba(0,0,0,0.55)] flex items-center justify-between gap-3">
            <span className="text-xs text-[#e0f2fe] font-medium">{hud.fareText}</span>
            <span className="font-display text-base font-bold text-[#22d3ee] tabular-nums">{hud.fareTimer}s</span>
          </div>
        )}

        {hud.toast && (
          <div className="bg-[#161c24]/95 border border-[#e6a135]/60 px-3 py-1.5 text-xs font-semibold text-[#fef3c7] shadow-lg">
            {hud.toast}
          </div>
        )}
      </div>

      {/* TOP-RIGHT: Taka Currency, Wanted Stars, Weapon Readout, District & Compact Utilities */}
      <div className="fixed top-4 right-4 flex flex-col items-end gap-2">
        {/* Utility Buttons Row */}
        <div className="flex items-center gap-1.5">
          <div className="bg-[#10151c]/90 border border-[#ffffff]/12 px-2.5 py-1 text-right mr-1">
            <div className="text-[11px] font-bold uppercase tracking-wider text-[#f5d07a] leading-none">
              {hud.districtName}
            </div>
            <div className="text-[10px] text-[#cbd5e1] mt-0.5 flex items-center justify-end gap-1.5 tabular-nums">
              <span className="font-bangla">{hud.districtBangla}</span>
              <span>·</span>
              <span>{hud.timeLabel}</span>
              <span>·</span>
              <span className="uppercase">
                {hud.liveWeather.enabled && hud.liveWeather.status === 'live' && hud.liveWeather.temperatureC !== null
                  ? `${hud.liveWeather.temperatureC}°C ${hud.weather}`
                  : hud.weather}
              </span>
            </div>
            {hud.streetName && (
              <div className="text-[9px] text-[#94a3b8] mt-0.5 tracking-wide leading-none">
                {hud.streetName}
              </div>
            )}
          </div>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onOpenMultiplayerLobby?.();
            }}
            aria-label="Open Multiplayer Lobby"
            title="Multiplayer Lobby [L]"
            className={`pointer-events-auto px-2 h-9 flex items-center gap-1.5 bg-[#10151c]/92 border transition-colors cursor-pointer ${
              hud.multiplayerActive
                ? 'border-[#34d399] text-[#34d399] hover:bg-[#0f7a53]/20'
                : 'border-[#e6a135]/40 hover:border-[#f5d07a] text-[#f5f2eb]'
            }`}
          >
            <span
              className={`w-2 h-2 rounded-full ${
                hud.multiplayerActive ? 'bg-[#34d399] animate-pulse' : 'bg-[#94a3b8]'
              }`}
            />
            <span className="font-display text-xs font-bold uppercase tracking-wider">
              {hud.multiplayerActive ? `${hud.multiplayerPing}ms (${hud.multiplayerPlayerCount}P)` : 'ONLINE'}
            </span>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onCycleCamera?.();
            }}
            aria-label="Cycle camera view"
            title="Camera View [V]"
            className="pointer-events-auto h-9 px-2 flex items-center gap-1.5 bg-[#10151c]/92 border border-[#e6a135]/40 hover:border-[#f5d07a] text-[#f5f2eb] cursor-pointer"
          >
            <svg viewBox="0 0 24 24" className="w-3.5 h-3.5 fill-current" aria-hidden="true">
              <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z" />
            </svg>
            <span className="font-display text-[10px] font-bold uppercase tracking-wider text-[#f5d07a]">
              {hud.cameraViewMode === 'first_person'
                ? '1ST'
                : hud.cameraViewMode === 'third_person_far'
                ? 'CHASE'
                : hud.cameraViewMode === 'cinematic'
                ? 'CINE'
                : '3RD'}
            </span>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onSwitchProtagonist?.();
            }}
            aria-label="Switch protagonist"
            title="Switch Character [C]"
            className={`pointer-events-auto h-9 px-2.5 flex items-center gap-2 border transition-colors cursor-pointer ${
              hud.activeProtagonist === 'tariq'
                ? 'bg-[#1e293b]/95 border-[#38bdf8] text-[#38bdf8]'
                : 'bg-[#2a131b]/95 border-[#fb7185] text-[#fb7185]'
            }`}
          >
            <span className="w-2 h-2 rounded-full bg-current animate-pulse" />
            <div className="flex flex-col text-left leading-none">
              <span className="font-display text-[10px] font-bold uppercase tracking-wider">
                {hud.protagonistName} [C]
              </span>
              <span className="font-bangla text-[9px] opacity-80 mt-0.5">
                {hud.protagonistBangla}
              </span>
            </div>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onToggleAbility?.();
            }}
            aria-label="Activate special ability"
            title={`${hud.protagonistAbilityName} [X / Q]`}
            className={`pointer-events-auto h-9 px-2 flex items-center gap-1.5 border transition-all cursor-pointer ${
              hud.protagonistAbilityActive
                ? 'bg-[#eab308]/30 border-[#eab308] text-[#fef08a] shadow-[0_0_15px_rgba(234,179,8,0.6)]'
                : hud.protagonistAbilityCooldownRatio > 0
                ? 'bg-[#10151c]/90 border-gray-600 text-gray-400'
                : 'bg-[#10151c]/92 border-[#e6a135]/60 hover:border-[#f5d07a] text-[#f5f2eb]'
            }`}
          >
            <div className="flex flex-col text-left leading-none">
              <span className="font-display text-[9px] font-bold uppercase tracking-wider">
                {hud.protagonistAbilityActive ? 'ACTIVE!' : `${hud.protagonistAbilityName} [X]`}
              </span>
              <div className="w-16 h-1 bg-black/40 mt-1 rounded overflow-hidden">
                <div
                  className={`h-full transition-all ${
                    hud.protagonistAbilityActive
                      ? 'bg-[#eab308]'
                      : hud.protagonistAbilityCooldownRatio > 0
                      ? 'bg-red-500'
                      : 'bg-[#34d399]'
                  }`}
                  style={{
                    width: hud.protagonistAbilityActive
                      ? `${Math.round(hud.protagonistAbilityTimerRatio * 100)}%`
                      : hud.protagonistAbilityCooldownRatio > 0
                      ? `${Math.round((1 - hud.protagonistAbilityCooldownRatio) * 100)}%`
                      : '100%',
                  }}
                />
              </div>
            </div>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onOpenWardrobe();
            }}
            aria-label="Open character wardrobe"
            title="Wardrobe [O]"
            className="pointer-events-auto w-9 h-9 flex items-center justify-center bg-[#10151c]/92 border border-[#e6a135]/40 hover:border-[#f5d07a] text-[#f5f2eb] cursor-pointer"
          >
            <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" aria-hidden="true">
              <path d="M16 3l4 3-2 4-2-1v12H8V9L6 10 4 6l4-3h8zm-4 1a2 2 0 01-2 2 2 2 0 01-2-2h4z" />
            </svg>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onToggleMute();
            }}
            aria-label={hud.muted ? 'Unmute audio' : 'Mute audio'}
            title="Mute [M]"
            className="pointer-events-auto w-9 h-9 flex items-center justify-center bg-[#10151c]/92 border border-[#e6a135]/40 hover:border-[#f5d07a] text-[#f5f2eb] cursor-pointer"
          >
            <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" aria-hidden="true">
              {hud.muted ? (
                <path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z" />
              ) : (
                <path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z" />
              )}
            </svg>
          </button>

          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.currentTarget.blur();
              onTogglePause();
            }}
            aria-label="Pause game"
            title="Pause [P / Esc]"
            className="pointer-events-auto w-9 h-9 flex items-center justify-center bg-[#10151c]/92 border border-[#e6a135]/40 hover:border-[#f5d07a] text-[#f5f2eb] cursor-pointer"
          >
            <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" aria-hidden="true">
              <path d="M6 5h4v14H6zm8 0h4v14h-4z" />
            </svg>
          </button>
        </div>

        {/* Taka Balance (Hero Tabular Readout) */}
        <div className="flex items-baseline gap-1.5 hud-text-stroke" data-testid="hud-money">
          <span className="font-display text-3xl sm:text-4xl font-extrabold text-[#34d399] tabular-nums tracking-tight">
            ৳{hud.money.toLocaleString()}
          </span>
        </div>

        {/* Wanted Stars (1–5) */}
        <div className="flex items-center gap-1" aria-label={`Wanted level ${hud.wantedLevel} of 5`}>
          {[1, 2, 3, 4, 5].map((star) => {
            const active = star <= hud.wantedLevel;
            return (
              <svg key={star} viewBox="0 0 24 24" className="w-5 h-5" aria-hidden="true">
                <path
                  d="M12 2.4l2.9 6 6.6.9-4.8 4.6 1.2 6.6L12 17.3l-5.9 3.2 1.2-6.6-4.8-4.6 6.6-.9z"
                  fill={active ? '#ef4444' : 'rgba(15,23,42,0.7)'}
                  stroke={active ? '#fef08a' : 'rgba(255,255,255,0.22)'}
                  strokeWidth="1.4"
                />
              </svg>
            );
          })}
        </div>

        {/* Equipped Weapon Selector & Ammo */}
        <div className="bg-[#10151c]/90 border-r-4 border-[#0f7a53] px-3 py-1.5 text-right shadow-[0_6px_16px_rgba(0,0,0,0.6)]">
          <div className="flex items-center justify-end gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wider text-[#94a3b8]">
              {activeWeaponSpec.role}
            </span>
            <span className="font-display text-base font-bold uppercase text-[#f8f5ee]">
              {activeWeaponSpec.name}
            </span>
          </div>
          {activeWeaponSpec.magSize > 0 ? (
            <div className="font-display text-lg font-bold text-[#f5d07a] tabular-nums leading-tight">
              {hud.reloading ? 'RELOADING...' : `${hud.magAmmo} / ${hud.reserveAmmo}`}
            </div>
          ) : (
            <div className="text-[11px] uppercase tracking-wider text-[#94a3b8]">Melee Ready</div>
          )}

          {/* Compact [1-4] Weapon Strip */}
          <div className="mt-1.5 flex items-center justify-end gap-1">
            {WEAPON_ORDER.map((wId) => {
              const unlocked = hud.unlockedWeapons.includes(wId);
              const selected = hud.weapon === wId;
              const spec = WEAPON_SPECS[wId];
              return (
                <button
                  key={wId}
                  type="button"
                  tabIndex={-1}
                  disabled={!unlocked}
                  onClick={(e) => {
                    e.currentTarget.blur();
                    onSelectWeapon(wId);
                  }}
                  aria-label={`Select ${spec.name}`}
                  className={`pointer-events-auto px-1.5 py-0.5 text-[10px] font-bold uppercase border transition-colors ${
                    selected
                      ? 'bg-[#0f7a53] border-[#f5d07a] text-white'
                      : unlocked
                        ? 'bg-[#1c2430] border-white/15 text-[#cbd5e1] hover:border-white/40 cursor-pointer'
                        : 'bg-[#0e1218] border-white/5 text-[#475569] cursor-not-allowed'
                  }`}
                >
                  [{spec.key}] {wId === 'fists' ? 'Fist' : wId === 'pistol' ? '9mm' : wId === 'smg' ? 'SMG' : '12G'}
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* BOTTOM-LEFT: Dhaka Minimap + Health / Armor / Stamina Bars */}
      <div className="fixed bottom-4 left-4 flex items-end gap-3">
        <Minimap
          playerX={hud.playerX}
          playerZ={hud.playerZ}
          playerHeading={hud.playerHeading}
          blips={hud.blips}
          wantedLevel={hud.wantedLevel}
          inVehicle={hud.inVehicle}
        />

        <div className="w-36 sm:w-44 bg-[#10151c]/90 border border-white/12 p-2.5 shadow-[0_6px_18px_rgba(0,0,0,0.65)] flex flex-col gap-1.5">
          {/* Health Bar with Delayed Ghost Drain */}
          <div>
            <div className="flex justify-between text-[10px] font-bold uppercase tracking-wider mb-0.5">
              <span className="text-[#4ade80]">HP</span>
              <span className="text-[#f5f2eb] tabular-nums" data-testid="hud-hp">
                {hud.hp}
              </span>
            </div>
            <div className="relative h-2.5 bg-[#090c10] overflow-hidden border border-white/10">
              <div
                className="absolute inset-y-0 left-0 bg-[#ef4444]/80 transition-all duration-300"
                style={{ width: `${ghostPct}%` }}
              />
              <div
                className="absolute inset-y-0 left-0 bg-[#16a34a] transition-all duration-100"
                style={{ width: `${hpPct}%` }}
              />
            </div>
          </div>

          {/* Armor Bar */}
          <div>
            <div className="flex justify-between text-[10px] font-bold uppercase tracking-wider mb-0.5">
              <span className="text-[#38bdf8]">ARMOR</span>
              <span className="text-[#cbd5e1] tabular-nums">{hud.armor}</span>
            </div>
            <div className="h-2 bg-[#090c10] overflow-hidden border border-white/10">
              <div className="h-full bg-[#0284c7] transition-all duration-150" style={{ width: `${armorPct}%` }} />
            </div>
          </div>

          {/* Stamina Bar */}
          <div>
            <div className="flex justify-between text-[10px] font-bold uppercase tracking-wider mb-0.5">
              <span className="text-[#f5d07a]">STAMINA</span>
              <span className="text-[#cbd5e1] tabular-nums">{hud.stamina}</span>
            </div>
            <div className="h-1.5 bg-[#090c10] overflow-hidden">
              <div className="h-full bg-[#e6a135] transition-all duration-100" style={{ width: `${staminaPct}%` }} />
            </div>
          </div>

          {/* Quick Inventory Tray */}
          {hud.inventory && hud.inventory.length > 0 && hud.modal === 'none' && (
            <div className="flex flex-wrap items-center gap-1 mt-1 pt-1 border-t border-white/10 pointer-events-auto">
              {hud.inventory.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  tabIndex={-1}
                  onClick={(e) => {
                    e.currentTarget.blur();
                    onUseItem?.(item.id);
                  }}
                  title={`${item.name} (${item.banglaName}) - ${item.description}`}
                  className="px-1.5 py-0.5 bg-[#10151c]/90 hover:bg-[#1e293b] border border-white/15 text-[9px] font-semibold text-[#f5f2eb] flex items-center gap-1 cursor-pointer transition-colors"
                >
                  <span>{item.kind === 'food' ? '☕' : item.kind === 'fuel' ? '⛽' : '🩹'}</span>
                  <span>{item.banglaName}</span>
                  <span className="px-1 bg-white/10 rounded text-[9px] text-[#34d399] font-bold">{item.count}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* BOTTOM-CENTER: Contextual Prompt & Bilingual Dialogue Subtitle */}
      <div className="fixed bottom-4 left-1/2 -translate-x-1/2 flex flex-col items-center gap-2 max-w-[92vw] sm:max-w-xl">
        {hud.interactPrompt && hud.interactKey && hud.modal === 'none' && (
          <div className="bg-[#10151c]/95 border border-[#e6a135] px-4 py-1.5 shadow-[0_6px_18px_rgba(0,0,0,0.75)] flex items-center gap-2.5">
            <span className="px-2 py-0.5 bg-[#e6a135] text-[#0b0e13] font-display text-sm font-extrabold">
              {hud.interactKey}
            </span>
            <span className="text-xs sm:text-sm font-semibold text-[#f8f5ee]">{hud.interactPrompt}</span>
          </div>
        )}

        {hud.dialogue && (
          <div className="bg-[#0c1016]/94 border-t-2 border-[#e6a135] px-5 py-2.5 text-center shadow-[0_8px_24px_rgba(0,0,0,0.8)]">
            <div className="text-[11px] font-bold uppercase tracking-widest text-[#f5d07a]">
              {hud.dialogue.speaker}
            </div>
            <div className="font-bangla text-sm text-[#fef3c7] mt-0.5">{hud.dialogue.bangla}</div>
            <div className="text-xs text-[#e2e8f0] mt-0.5">{hud.dialogue.english}</div>
          </div>
        )}
      </div>

      {/* TOP-RIGHT MULTIPLAYER KILLFEED */}
      {hud.multiplayerKillfeed && hud.multiplayerKillfeed.length > 0 && (
        <div className="fixed top-56 right-4 flex flex-col gap-1.5 pointer-events-none z-20 max-w-[280px]">
          {hud.multiplayerKillfeed.map((k) => (
            <div
              key={k.id}
              className="px-2.5 py-1 bg-[#10151c]/90 border-l-4 border-[#ef4444] text-[#f8fafc] text-xs font-bold shadow-[0_4px_12px_rgba(0,0,0,0.7)]"
            >
              ⚔ {k.text}
            </div>
          ))}
        </div>
      )}

      {/* MULTIPLAYER CHAT TRAY */}
      {hud.multiplayerActive && (
        <div className="fixed bottom-44 left-4 max-w-[340px] flex flex-col gap-1 z-20 pointer-events-auto">
          {hud.multiplayerChat && hud.multiplayerChat.length > 0 && (
            <div className="max-h-36 overflow-y-auto flex flex-col gap-1 bg-[#0b0e14]/85 border border-white/10 p-2 text-xs">
              {hud.multiplayerChat.slice(-6).map((msg) => (
                <div key={msg.id} className="leading-snug">
                  <span className={`font-bold ${msg.isSystem ? 'text-[#38bdf8]' : 'text-[#f5d07a]'}`}>
                    {msg.sender}:
                  </span>{' '}
                  <span className="text-[#f8fafc]">{msg.text}</span>
                </div>
              ))}
            </div>
          )}

          {chatOpen ? (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (chatText.trim() && onSendChatMessage) {
                  onSendChatMessage(chatText.trim());
                  setChatText('');
                }
                setChatOpen(false);
              }}
              className="flex gap-1"
            >
              <input
                type="text"
                value={chatText}
                onChange={(e) => setChatText(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') {
                    setChatOpen(false);
                  }
                }}
                autoFocus
                placeholder="Type in-game chat..."
                className="flex-1 bg-[#111720] border border-[#e6a135] px-2 py-1 text-xs text-[#f8f5ee] focus:outline-none"
              />
              <button
                type="submit"
                className="px-2 py-1 bg-[#0f7a53] text-white text-xs font-bold uppercase cursor-pointer"
              >
                Send
              </button>
            </form>
          ) : (
            <button
              type="button"
              onClick={() => setChatOpen(true)}
              className="self-start px-2 py-0.5 bg-[#10151c]/80 hover:bg-[#1b2432] border border-white/10 text-[10px] text-[#94a3b8] cursor-pointer"
            >
              Press [T] to Chat
            </button>
          )}
        </div>
      )}

      {/* BOTTOM-RIGHT: Vehicle Speedometer & Integrity (only when driving) */}
      {hud.inVehicle && (
        <div
          className="fixed bottom-4 right-4 bg-[#10151c]/92 border-r-4 border-[#e6a135] px-4 py-3 shadow-[0_8px_22px_rgba(0,0,0,0.75)] min-w-[185px]"
          data-testid="hud-vehicle"
        >
          <div className="flex items-baseline justify-between gap-3">
            <span className="font-display text-4xl font-extrabold text-[#f8f5ee] tabular-nums leading-none">
              {hud.vehicleSpeedKmh}
            </span>
            <span className="text-xs font-bold uppercase tracking-widest text-[#f5d07a]">KM/H</span>
          </div>
          <div className="mt-1 flex items-center justify-between gap-2">
            <span className="text-xs font-bold uppercase text-[#e2e8f0]">{hud.vehicleName}</span>
            <span className="font-bangla text-[11px] text-[#94a3b8]">{hud.vehicleBangla}</span>
          </div>
          <div className="mt-1.5 h-1.5 bg-[#090c10] overflow-hidden">
            <div
              className={`h-full ${hud.vehicleHp < hud.vehicleMaxHp * 0.35 ? 'bg-[#ef4444]' : 'bg-[#10b981]'}`}
              style={{ width: `${Math.max(0, Math.min(100, (hud.vehicleHp / hud.vehicleMaxHp) * 100))}%` }}
            />
          </div>

          {/* Fuel Gauge Bar */}
          <div className="mt-1.5">
            <div className="flex items-center justify-between text-[9px] font-bold uppercase tracking-wider mb-0.5">
              <span className={hud.isLowFuel ? 'text-[#ef4444] animate-pulse font-extrabold' : 'text-[#38bdf8]'}>
                {hud.isLowFuel ? '⚠️ LOW FUEL' : 'FUEL'}
              </span>
              <span className="text-[#cbd5e1] tabular-nums">{hud.fuel}%</span>
            </div>
            <div className="h-1.5 bg-[#090c10] overflow-hidden border border-white/10">
              <div
                className={`h-full transition-all duration-150 ${
                  hud.fuel < 20 ? 'bg-[#ef4444]' : hud.fuel < 45 ? 'bg-[#f59e0b]' : 'bg-[#38bdf8]'
                }`}
                style={{ width: `${Math.max(0, Math.min(100, hud.fuelRatio * 100))}%` }}
              />
            </div>
          </div>
          <div className="mt-2 flex items-center justify-between gap-2 text-[10px] uppercase tracking-wider text-[#94a3b8]">
            <span>[H] Horn · [F] Exit</span>
            <button
              type="button"
              tabIndex={-1}
              onClick={(e) => {
                e.currentTarget.blur();
                onToggleFare();
              }}
              className="pointer-events-auto px-1.5 py-0.5 bg-[#1e293b] hover:bg-[#0f7a53] text-[#f8f5ee] font-bold cursor-pointer"
            >
              [N] {hud.fareActive ? 'Cancel Fare' : 'Fare Job'}
            </button>
          </div>
        </div>
      )}

      {/* Fullscreen Cinematic Character Switch Sky-Cam Banner */}
      {hud.characterSwitching && (
        <div className="fixed inset-0 pointer-events-none flex flex-col items-center justify-center z-50 bg-black/30 backdrop-blur-[2px] transition-all">
          <div className="bg-[#0b0e14]/92 border-y-2 border-[#e6a135] px-12 py-5 text-center shadow-[0_12px_40px_rgba(0,0,0,0.85)] max-w-lg w-full">
            <div className="text-[11px] font-bold uppercase tracking-[0.35em] text-[#e6a135]">
              CHARACTER TRANSITION · চরিত্র পরিবর্তন
            </div>
            <div className="font-display text-2xl sm:text-3xl font-extrabold uppercase tracking-widest text-[#f8fafc] mt-1.5">
              {hud.characterSwitchBanner}
            </div>
            {hud.characterSwitchBangla && (
              <div className="font-bangla text-base text-[#f5d07a] mt-1 font-semibold">
                {hud.characterSwitchBangla}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
