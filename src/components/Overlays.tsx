import React, { useState } from 'react';
import {
  GraphicsQualityPreset,
  HudSnapshot,
  OutfitId,
  OUTFIT_SPECS,
  SafehouseId,
  TimePreset,
  WeaponId,
  WEAPON_SPECS,
  WeatherMode,
} from '../game/types';
import { SAFEHOUSE_SPECS } from '../game/worldData';
import { resolveServerUrl } from '../game/multiplayer/protocol';
import { OPEN_METEO_ATTRIBUTION } from '../api/openMeteoClient';
import { BDAPIS_ATTRIBUTION } from '../api/bdapisClient';
import { OSM_ATTRIBUTION_NOTE } from '../game/dhakaGeoData';

interface OverlaysProps {
  hud: HudSnapshot;
  onResume: () => void;
  onRestart: () => void;
  onTitle: () => void;
  onCloseModal: () => void;
  onSetTime: (t: TimePreset) => void;
  onSetWeather: (w: WeatherMode) => void;
  onSetGraphicsQuality: (q: GraphicsQualityPreset) => void;
  onToggleLiveWeather: (enabled: boolean) => void;
  onRefreshLiveWeather: () => void;
  onToggleRegionalPlaces: (enabled: boolean) => void;
  onShopBuy: (item: 'biriyani' | 'armor' | WeaponId) => void;
  onGarageService: (svc: 'repair' | 'clear_heat' | 'paint', color?: number) => void;
  onOutfitSelect: (id: OutfitId) => void;
  onSaveGame?: () => void;
  onLoadGame?: () => void;
  onSafehouseAction?: (action: 'rest' | 'save' | 'purchase' | 'armory' | 'wardrobe', safehouseId?: SafehouseId) => void;
  onRefuelAction?: (action: 'refuel' | 'jerrycan', stationId?: string) => void;
  onVehicleUpgrade?: (option: 'engine' | 'brakes' | 'turbo' | 'spoiler' | 'tint' | 'neon' | 'jammer', val?: any) => void;
  onUseItem?: (itemId: string) => void;
  onDialogueOption?: (optionId: string) => void;
  onConnectMultiplayer?: (serverUrl: string, callsign: string, pin?: string) => void;
  onDisconnectMultiplayer?: () => void;
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
  onSetGraphicsQuality,
  onToggleLiveWeather,
  onRefreshLiveWeather,
  onToggleRegionalPlaces,
  onShopBuy,
  onGarageService,
  onOutfitSelect,
  onSaveGame,
  onLoadGame,
  onSafehouseAction,
  onRefuelAction,
  onVehicleUpgrade,
  onUseItem,
  onDialogueOption,
  onConnectMultiplayer,
  onDisconnectMultiplayer,
}) => {
  const [garageTab, setGarageTab] = useState<'service' | 'performance' | 'styling'>('service');
  const [serverUrl, setServerUrl] = useState(() => resolveServerUrl());
  const [callsign, setCallsign] = useState('DhakaRider');
  const [pin, setPin] = useState('');
  // 1. PAUSE MODAL
  if (hud.phase === 'paused') {
    const lw = hud.liveWeather;
    const rp = hud.regionalPlaces;
    return (
      <div className="fixed inset-0 z-30 flex items-center justify-center bg-[#070a0f]/82 p-4 overflow-y-auto">
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Game Paused"
          className="w-full max-w-xl bg-[#111720] border-t-4 border-[#e6a135] border-x border-b border-white/15 p-5 sm:p-6 shadow-[0_24px_60px_rgba(0,0,0,0.85)] max-h-[92vh] overflow-y-auto"
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
                      hud.weather === w && !lw.enabled
                        ? 'bg-[#e6a135] text-[#0b0e13]'
                        : hud.weather === w
                          ? 'bg-[#e6a135]/75 text-[#0b0e13]'
                          : 'bg-[#1b2432] hover:bg-[#0f7a53] text-[#f5f2eb]'
                    }`}
                  >
                    {w === 'clear' ? 'Clear Tropical' : w === 'monsoon' ? 'Monsoon Rain' : 'Thunderstorm'}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Scalable Graphics Quality Preset (Low / Medium / High) */}
          <div className="mt-3 bg-[#0b0f16] p-3 border border-white/10">
            <div className="flex flex-wrap items-center justify-between gap-2 mb-2">
              <div className="text-[11px] font-bold uppercase tracking-wider text-[#94a3b8]">
                Graphics Quality
              </div>
              <span className="text-[10px] uppercase tracking-wider text-[#64748b]">
                {hud.characterRigMode === 'glb' ? 'Rigged GLB Characters Active' : 'Sculpted Procedural Fallback'}
              </span>
            </div>
            <div className="grid grid-cols-3 gap-1.5">
              {(['low', 'medium', 'high'] as GraphicsQualityPreset[]).map((q) => (
                <button
                  key={q}
                  type="button"
                  data-testid={`quality-${q}`}
                  onClick={() => onSetGraphicsQuality(q)}
                  className={`px-2 py-1.5 text-xs font-semibold uppercase transition-colors cursor-pointer ${
                    hud.graphicsQuality === q
                      ? 'bg-[#e6a135] text-[#0b0e13]'
                      : 'bg-[#1b2432] hover:bg-[#0f7a53] text-[#f5f2eb]'
                  }`}
                >
                  {q}
                </button>
              ))}
            </div>
          </div>

          {/* Optional Open-Meteo Real-World Dhaka Weather */}
          <div className="mt-3 bg-[#0b0f16] p-3 border border-white/10">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <div className="text-[11px] font-bold uppercase tracking-wider text-[#f5d07a]">
                  Use Dhaka Real-World Weather
                </div>
                <div className="text-[10px] text-[#94a3b8]">
                  Optional Open-Meteo sync (cached 30 min · manual [V] always overrides)
                </div>
              </div>
              <div className="flex items-center gap-1.5">
                {lw.enabled && (
                  <button
                    type="button"
                    onClick={onRefreshLiveWeather}
                    disabled={lw.status === 'loading'}
                    className="px-2.5 py-1 text-[11px] font-bold uppercase bg-[#1b2432] hover:bg-[#334155] disabled:opacity-50 text-[#e2e8f0] cursor-pointer"
                  >
                    Refresh
                  </button>
                )}
                <button
                  type="button"
                  data-testid="toggle-live-weather"
                  onClick={() => onToggleLiveWeather(!lw.enabled)}
                  className={`px-3 py-1 text-[11px] font-bold uppercase cursor-pointer transition-colors ${
                    lw.enabled
                      ? 'bg-[#0f7a53] text-[#f8f5ee] border border-[#34d399]'
                      : 'bg-[#1b2432] hover:bg-[#0f7a53] text-[#cbd5e1] border border-white/15'
                  }`}
                >
                  {lw.enabled ? 'Enabled' : 'Off'}
                </button>
              </div>
            </div>

            {lw.enabled && (
              <div className="mt-2 pt-2 border-t border-white/10 text-xs">
                {lw.status === 'loading' && (
                  <span className="text-[#94a3b8]">Fetching Dhaka current conditions...</span>
                )}
                {lw.status === 'live' && lw.temperatureC !== null && (
                  <div className="flex flex-wrap items-center justify-between gap-2 text-[#e2e8f0]">
                    <span>
                      <strong className="text-[#34d399] tabular-nums">{lw.temperatureC}°C</strong>
                      {' · '}
                      {lw.conditionLabel}
                      {lw.conditionBangla ? (
                        <span className="font-bangla text-[#f5d07a] ml-1.5">{lw.conditionBangla}</span>
                      ) : null}
                    </span>
                    <span className="text-[10px] text-[#94a3b8] tabular-nums">
                      {lw.humidityPct !== null ? `RH ${lw.humidityPct}%` : ''}
                      {lw.windKmh !== null ? ` · Wind ${lw.windKmh} km/h` : ''}
                      {lw.fromCache ? ' · Cached' : ''}
                    </span>
                  </div>
                )}
                {lw.status === 'error' && (
                  <span className="text-[#fca5a5]">{lw.error ?? 'Weather unavailable — manual controls remain active.'}</span>
                )}
              </div>
            )}

            <div className="mt-1.5 text-[10px] text-[#64748b]">{OPEN_METEO_ATTRIBUTION}</div>
          </div>

          {/* Optional BDAPIs Dhaka Division Regional Directory */}
          <div className="mt-3 bg-[#0b0f16] p-3 border border-white/10">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <div className="text-[11px] font-bold uppercase tracking-wider text-[#cbd5e1]">
                  Dhaka Division Regional Directory (Optional)
                </div>
                <div className="text-[10px] text-[#94a3b8]">
                  Bangla &amp; English inter-district destinations · Core Dhaka City districts unchanged
                </div>
              </div>
              <button
                type="button"
                data-testid="toggle-regional-places"
                onClick={() => onToggleRegionalPlaces(!rp.enabled)}
                className={`px-3 py-1 text-[11px] font-bold uppercase cursor-pointer transition-colors ${
                  rp.enabled
                    ? 'bg-[#0f7a53] text-[#f8f5ee] border border-[#34d399]'
                    : 'bg-[#1b2432] hover:bg-[#334155] text-[#cbd5e1] border border-white/15'
                }`}
              >
                {rp.enabled ? 'Hide' : 'Load BDAPIs'}
              </button>
            </div>

            {rp.enabled && (
              <div className="mt-2 pt-2 border-t border-white/10">
                {rp.status === 'loading' ? (
                  <div className="text-xs text-[#94a3b8]">Loading Dhaka Division place names...</div>
                ) : (
                  <>
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
                      {rp.places.slice(0, 6).map((place) => (
                        <div key={place.name} className="bg-[#111720] border border-white/10 px-2 py-1">
                          <div className="text-[11px] font-semibold text-[#f5f2eb] leading-tight">{place.name}</div>
                          <div className="font-bangla text-[10px] text-[#f5d07a] leading-tight">{place.banglaName}</div>
                        </div>
                      ))}
                    </div>
                    <div className="mt-1.5 flex flex-wrap items-center justify-between gap-1 text-[10px] text-[#64748b]">
                      <span>
                        {rp.source === 'bdapis' ? BDAPIS_ATTRIBUTION : 'Using authored local Dhaka Division fallback'}
                      </span>
                      {rp.error && <span className="text-[#fca5a5]">{rp.error}</span>}
                    </div>
                  </>
                )}
              </div>
            )}
          </div>

          <div className="mt-3 bg-[#0b0f16] p-3 border border-white/10 text-xs text-[#cbd5e1] leading-relaxed">
            <strong className="text-[#f5d07a] uppercase">Controls:</strong> WASD Move/Drive · Space Jump/Handbrake ·
            Q/E or Right-Drag Camera · F Enter/Exit Vehicle · Left Click / J Attack · 1–4 Weapons · R Reload · H Horn ·
            G Shop/Garage · C Wardrobe · N CNG Fare Job
            <div className="mt-1 text-[10px] text-[#64748b]">{OSM_ATTRIBUTION_NOTE}</div>
          </div>

          {/* Persistence Quick Actions */}
          <div className="mt-3 grid grid-cols-2 gap-2">
            <button
              type="button"
              data-testid="pause-save-btn"
              onClick={onSaveGame}
              className="px-3 py-2 bg-[#1b2432] hover:bg-[#0f7a53] border border-[#34d399]/40 text-[#34d399] text-xs font-bold uppercase tracking-wider flex items-center justify-center gap-1.5 cursor-pointer transition-colors"
            >
              <span>💾</span>
              <span>Save Progress</span>
            </button>
            <button
              type="button"
              data-testid="pause-load-btn"
              onClick={onLoadGame}
              className="px-3 py-2 bg-[#1b2432] hover:bg-[#334155] border border-[#f5d07a]/40 text-[#f5d07a] text-xs font-bold uppercase tracking-wider flex items-center justify-center gap-1.5 cursor-pointer transition-colors"
            >
              <span>📂</span>
              <span>Load Saved Game</span>
            </button>
          </div>

          {hud.multiplayerActive && onDisconnectMultiplayer && (
            <div className="mt-3">
              <button
                type="button"
                data-testid="pause-disconnect-mp"
                onClick={() => {
                  onDisconnectMultiplayer();
                  onResume();
                }}
                className="w-full px-4 py-2.5 bg-[#7f1d1d] hover:bg-[#991b1b] border border-[#ef4444]/50 text-white font-display text-sm font-bold uppercase tracking-wider cursor-pointer"
              >
                Disconnect Multiplayer (Switch to Offline)
              </button>
            </div>
          )}

          <div className="mt-5 flex flex-wrap gap-3">
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
              : hud.modal === 'safehouse'
                ? 'Dhaka Safehouse'
                : hud.modal === 'fuel_station'
                  ? 'Dhaka Fuel Station'
                  : hud.modal === 'dialogue'
                    ? 'Conversation'
                    : 'Character Wardrobe'
        }
        className="w-full max-w-xl bg-[#111720] border-t-4 border-[#e6a135] border-x border-b border-white/15 p-6 shadow-[0_24px_60px_rgba(0,0,0,0.88)] max-h-[92vh] overflow-y-auto"
      >
        <div className="flex items-center justify-between border-b border-white/10 pb-3">
          <div>
            <h2 className="font-display text-2xl sm:text-3xl font-extrabold uppercase tracking-wider text-[#f8f5ee]">
              {hud.modal === 'garage'
                ? 'DHAKA CUSTOMS GARAGE'
                : hud.modal === 'shop'
                  ? "BABU'S HARDWARE & GEAR"
                  : hud.modal === 'safehouse'
                    ? hud.safehouseName?.toUpperCase() ?? 'DHAKA SAFEHOUSE'
                    : hud.modal === 'fuel_station'
                      ? 'PADMA & MEGHNA FUEL TERMINAL'
                      : hud.modal === 'dialogue'
                        ? hud.activeConversation?.npcName.toUpperCase() ?? 'CONVERSATION'
                        : hud.modal === 'multiplayer_lobby'
                          ? 'DHAKA MULTIPLAYER LOBBY'
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

        {/* GARAGE MODAL WITH TUNING TABS */}
        {hud.modal === 'garage' && (
          <div className="mt-4 flex flex-col gap-4">
            {/* Tabs */}
            <div className="flex border-b border-white/10 gap-2">
              <button
                type="button"
                onClick={() => setGarageTab('service')}
                className={`px-3 py-1.5 text-xs font-bold uppercase tracking-wider transition-colors cursor-pointer ${
                  garageTab === 'service'
                    ? 'border-b-2 border-[#e6a135] text-[#f5d07a]'
                    : 'text-[#94a3b8] hover:text-[#f8f5ee]'
                }`}
              >
                Service & Repairs
              </button>
              <button
                type="button"
                onClick={() => setGarageTab('performance')}
                className={`px-3 py-1.5 text-xs font-bold uppercase tracking-wider transition-colors cursor-pointer ${
                  garageTab === 'performance'
                    ? 'border-b-2 border-[#e6a135] text-[#f5d07a]'
                    : 'text-[#94a3b8] hover:text-[#f8f5ee]'
                }`}
              >
                Engine & Tuning
              </button>
              <button
                type="button"
                onClick={() => setGarageTab('styling')}
                className={`px-3 py-1.5 text-xs font-bold uppercase tracking-wider transition-colors cursor-pointer ${
                  garageTab === 'styling'
                    ? 'border-b-2 border-[#e6a135] text-[#f5d07a]'
                    : 'text-[#94a3b8] hover:text-[#f8f5ee]'
                }`}
              >
                Custom Styling
              </button>
            </div>

            {garageTab === 'service' && (
              <div className="flex flex-col gap-4">
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
              </div>
            )}

            {garageTab === 'performance' && (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => onVehicleUpgrade?.('engine')}
                  className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] transition-colors cursor-pointer"
                >
                  <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Stage Engine ECU Tuning</div>
                  <div className="text-xs text-[#94a3b8] mt-0.5">+12% Torque & +5% Top Speed per stage (Stages 1-3)</div>
                  <div className="mt-2 font-display text-base font-bold text-[#34d399] tabular-nums">৳800 - ৳2,500</div>
                </button>

                <button
                  type="button"
                  onClick={() => onVehicleUpgrade?.('brakes')}
                  className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] transition-colors cursor-pointer"
                >
                  <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Ceramic Ventilated Brakes</div>
                  <div className="text-xs text-[#94a3b8] mt-0.5">+25% Stopping power & reduced drift slip</div>
                  <div className="mt-2 font-display text-base font-bold text-[#34d399] tabular-nums">৳450 - ৳900</div>
                </button>

                <button
                  type="button"
                  onClick={() => onVehicleUpgrade?.('turbo')}
                  className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] transition-colors cursor-pointer"
                >
                  <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Garrett Twin-Scroll Turbo</div>
                  <div className="text-xs text-[#94a3b8] mt-0.5">+15% Turbo spool power boost</div>
                  <div className="mt-2 font-display text-base font-bold text-[#38bdf8] tabular-nums">৳1,800</div>
                </button>

                <button
                  type="button"
                  onClick={() => onVehicleUpgrade?.('jammer')}
                  className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] transition-colors cursor-pointer"
                >
                  <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">DMP Digital Plate Jammer</div>
                  <div className="text-xs text-[#94a3b8] mt-0.5">Scrambles automatic police radar detection</div>
                  <div className="mt-2 font-display text-base font-bold text-[#f5d07a] tabular-nums">৳1,200</div>
                </button>
              </div>
            )}

            {garageTab === 'styling' && (
              <div className="flex flex-col gap-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => onVehicleUpgrade?.('spoiler')}
                    className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] transition-colors cursor-pointer"
                  >
                    <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Carbon Aero Spoiler</div>
                    <div className="text-xs text-[#94a3b8] mt-0.5">+25% High-speed downforce & rear tire stability</div>
                    <div className="mt-2 font-display text-base font-bold text-[#38bdf8] tabular-nums">৳700</div>
                  </button>

                  <button
                    type="button"
                    onClick={() => onVehicleUpgrade?.('tint')}
                    className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] transition-colors cursor-pointer"
                  >
                    <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Ceramic Privacy Window Tint</div>
                    <div className="text-xs text-[#94a3b8] mt-0.5">5% Limo darkness shade</div>
                    <div className="mt-2 font-display text-base font-bold text-[#34d399] tabular-nums">৳350</div>
                  </button>

                  <button
                    type="button"
                    onClick={() => onVehicleUpgrade?.('neon', 0x00f3ff)}
                    className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] transition-colors cursor-pointer"
                  >
                    <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Cyber Neon Underglow</div>
                    <div className="text-xs text-[#94a3b8] mt-0.5">Electric cyan under-chassis illumination</div>
                    <div className="mt-2 font-display text-base font-bold text-[#38bdf8] tabular-nums">৳500</div>
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
          </div>
        )}

        {/* SAFEHOUSE MODAL */}
        {hud.modal === 'safehouse' && (
          <div className="mt-4 flex flex-col gap-4">
            {(() => {
              const safehouseId = (hud.unlockedSafehouses[0] ?? 'old_dhaka_rooftop') as SafehouseId;
              const spec = SAFEHOUSE_SPECS.find((s) => s.id === safehouseId);
              const isOwned = hud.unlockedSafehouses.includes(safehouseId);
              return (
                <div>
                  <div className="bg-[#0b0f16] p-4 border border-white/15">
                    <div className="flex items-baseline justify-between gap-2">
                      <div className="font-display text-xl font-bold uppercase text-[#f5d07a]">{spec?.name}</div>
                      <div className="font-bangla text-sm text-[#f5f2eb]">{spec?.banglaName}</div>
                    </div>
                    <p className="text-xs text-[#cbd5e1] mt-1">{spec?.description}</p>
                    <div className="mt-2 text-xs font-bold uppercase text-[#34d399]">
                      Status: {isOwned ? 'Deed Owned · Full Access' : `Unclaimed Deed (৳${spec?.price})`}
                    </div>
                  </div>

                  <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
                    {!isOwned ? (
                      <button
                        type="button"
                        onClick={() => onSafehouseAction?.('purchase', safehouseId)}
                        className="p-3.5 text-left bg-[#0f7a53] hover:bg-[#149465] text-[#f8f5ee] border border-[#34d399] transition-colors cursor-pointer"
                      >
                        <div className="font-display text-lg font-bold uppercase">Purchase Property Deed</div>
                        <div className="text-xs text-[#d1fae5] mt-0.5">Unlock resting, armory and garage bay</div>
                        <div className="mt-2 font-display text-base font-bold text-[#fef08a] tabular-nums">
                          ৳{spec?.price}
                        </div>
                      </button>
                    ) : (
                      <>
                        <button
                          type="button"
                          onClick={() => onSafehouseAction?.('rest', safehouseId)}
                          className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#34d399] transition-colors cursor-pointer"
                        >
                          <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Rest & Sleep (6h)</div>
                          <div className="text-xs text-[#94a3b8] mt-0.5">Skips 6 hours, restores 100% HP & Stamina</div>
                          <div className="mt-2 text-xs font-bold text-[#34d399]">Free · Safehouse Perk</div>
                        </button>

                        <button
                          type="button"
                          onClick={() => onSafehouseAction?.('armory', safehouseId)}
                          className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] transition-colors cursor-pointer"
                        >
                          <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Safehouse Armory</div>
                          <div className="text-xs text-[#94a3b8] mt-0.5">Fully restock reserve ammunition</div>
                          <div className="mt-2 text-xs font-bold text-[#38bdf8]">Restock Free</div>
                        </button>

                        <button
                          type="button"
                          onClick={() => onSafehouseAction?.('save', safehouseId)}
                          className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] transition-colors cursor-pointer"
                        >
                          <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Save Progress</div>
                          <div className="text-xs text-[#94a3b8] mt-0.5">Store mission, money & gear to browser save</div>
                          <div className="mt-2 text-xs font-bold text-[#f5d07a]">Cloud Sync</div>
                        </button>

                        <button
                          type="button"
                          onClick={() => onSafehouseAction?.('wardrobe', safehouseId)}
                          className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-white/50 transition-colors cursor-pointer"
                        >
                          <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Wardrobe Closet</div>
                          <div className="text-xs text-[#94a3b8] mt-0.5">Change outfit style</div>
                          <div className="mt-2 text-xs font-bold text-[#cbd5e1]">Change Clothes</div>
                        </button>
                      </>
                    )}
                  </div>
                </div>
              );
            })()}
          </div>
        )}

        {/* FUEL STATION MODAL */}
        {hud.modal === 'fuel_station' && (
          <div className="mt-4 flex flex-col gap-4">
            <div className="bg-[#0b0f16] p-4 border border-white/15">
              <div className="flex items-baseline justify-between gap-2">
                <div className="font-display text-xl font-bold uppercase text-[#38bdf8]">Dhaka Fuel Terminal</div>
                <div className="font-bangla text-sm text-[#f5d07a]">জ্বালানি স্টেশন</div>
              </div>
              <p className="text-xs text-[#cbd5e1] mt-1">High-octane gasoline, diesel and CNG refueling service.</p>
              <div className="mt-2 text-xs font-bold uppercase text-[#38bdf8]">
                Current Tank: <span className="tabular-nums">{hud.fuel}%</span>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() => onRefuelAction?.('refuel')}
                className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#38bdf8] transition-colors cursor-pointer"
              >
                <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Fill Tank to 100%</div>
                <div className="text-xs text-[#94a3b8] mt-0.5">Refills full tank for active vehicle or boat</div>
                <div className="mt-2 font-display text-base font-bold text-[#38bdf8] tabular-nums">৳1.5 / Unit</div>
              </button>

              <button
                type="button"
                onClick={() => onRefuelAction?.('jerrycan')}
                className="p-3.5 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] transition-colors cursor-pointer"
              >
                <div className="font-display text-lg font-bold uppercase text-[#f8f5ee]">Emergency Jerrycan (10L)</div>
                <div className="text-xs text-[#94a3b8] mt-0.5">Portable fuel canister (+35% tank anywhere)</div>
                <div className="mt-2 font-display text-base font-bold text-[#f5d07a] tabular-nums">৳250</div>
              </button>
            </div>
          </div>
        )}

        {/* INTERACTIVE NPC CONVERSATION MODAL */}
        {hud.modal === 'dialogue' && hud.activeConversation && (
          <div className="mt-4 flex flex-col gap-4">
            <div className="bg-[#0b0f16] p-4 border border-white/15">
              <div className="flex items-baseline justify-between gap-2">
                <div className="font-display text-xl font-bold uppercase text-[#f5d07a]">
                  {hud.activeConversation.npcName}
                </div>
                <div className="font-bangla text-sm text-[#f5f2eb]">{hud.activeConversation.npcBanglaName}</div>
              </div>
              <div className="text-xs font-bold text-[#94a3b8] uppercase">{hud.activeConversation.role}</div>
              <div className="mt-3 bg-[#111720] p-3 border-l-2 border-[#e6a135]">
                <div className="font-bangla text-base text-[#fef08a] leading-relaxed">
                  "{hud.activeConversation.greetingBangla}"
                </div>
                <div className="text-xs text-[#cbd5e1] mt-1 italic">
                  "{hud.activeConversation.greetingEnglish}"
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <div className="text-xs font-bold uppercase tracking-wider text-[#94a3b8]">Select Response:</div>
              {hud.activeConversation.options.map((opt) => (
                <button
                  key={opt.id}
                  type="button"
                  onClick={() => onDialogueOption?.(opt.id)}
                  className="p-3 text-left bg-[#0b0f16] border border-white/15 hover:border-[#f5d07a] hover:bg-[#1b2432] transition-colors cursor-pointer"
                >
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="font-bangla text-sm font-semibold text-[#f5d07a]">{opt.banglaLabel}</span>
                  </div>
                  <div className="text-xs text-[#e2e8f0] mt-0.5">{opt.label}</div>
                </button>
              ))}
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

        {hud.modal === 'multiplayer_lobby' && (
          <div className="mt-4 flex flex-col gap-4">
            <div className="bg-[#0b0f16] p-4 border border-white/15">
              <div className="flex items-center justify-between border-b border-white/10 pb-2 mb-3">
                <span className="text-xs font-bold uppercase tracking-wider text-[#94a3b8]">Connection Status</span>
                <span
                  data-testid="mp-status-pill"
                  className={`px-2 py-0.5 text-xs font-extrabold uppercase ${
                    hud.multiplayerActive ? 'bg-[#0f7a53] text-[#34d399]' : 'bg-[#ef4444]/20 text-[#f87171]'
                  }`}
                >
                  {hud.multiplayerActive ? `● ONLINE (${hud.multiplayerPing}ms)` : '○ OFFLINE (Single Player)'}
                </span>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-3">
                <div>
                  <label className="block text-[11px] font-bold uppercase text-[#94a3b8] mb-1">Server WebSocket URL</label>
                  <input
                    type="text"
                    data-testid="mp-url-input"
                    value={serverUrl}
                    onChange={(e) => setServerUrl(e.target.value)}
                    disabled={hud.multiplayerActive}
                    className="w-full bg-[#111720] border border-white/20 px-3 py-1.5 text-sm text-[#f8f5ee] focus:border-[#e6a135] focus:outline-none"
                    placeholder="wss://... or ws://localhost:8080"
                  />
                </div>
                <div>
                  <label className="block text-[11px] font-bold uppercase text-[#94a3b8] mb-1">Callsign / Player Name</label>
                  <input
                    type="text"
                    data-testid="mp-name-input"
                    value={callsign}
                    onChange={(e) => setCallsign(e.target.value)}
                    disabled={hud.multiplayerActive}
                    maxLength={16}
                    className="w-full bg-[#111720] border border-white/20 px-3 py-1.5 text-sm text-[#f8f5ee] focus:border-[#e6a135] focus:outline-none"
                    placeholder="DhakaRider"
                  />
                </div>
                <div>
                  <label className="block text-[11px] font-bold uppercase text-[#94a3b8] mb-1">Account PIN (Protect CallSign)</label>
                  <input
                    type="password"
                    data-testid="mp-pin-input"
                    value={pin}
                    onChange={(e) => setPin(e.target.value)}
                    disabled={hud.multiplayerActive}
                    maxLength={8}
                    className="w-full bg-[#111720] border border-white/20 px-3 py-1.5 text-sm text-[#f8f5ee] focus:border-[#e6a135] focus:outline-none"
                    placeholder="4-8 digit PIN"
                  />
                </div>
              </div>

              <div className="flex items-center gap-3">
                {hud.multiplayerActive ? (
                  <button
                    type="button"
                    data-testid="mp-disconnect-btn"
                    onClick={() => onDisconnectMultiplayer?.()}
                    className="px-5 py-2 bg-[#b91c1c] hover:bg-[#dc2626] text-white font-display text-sm font-bold uppercase tracking-wider cursor-pointer"
                  >
                    Disconnect from Server
                  </button>
                ) : (
                  <button
                    type="button"
                    data-testid="mp-connect-btn"
                    onClick={() => onConnectMultiplayer?.(serverUrl, callsign, pin)}
                    className="px-6 py-2 bg-[#0f7a53] hover:bg-[#149465] text-[#f8fafc] font-display text-sm font-bold uppercase tracking-wider cursor-pointer"
                  >
                    Connect to Server
                  </button>
                )}
              </div>
            </div>

            <div className="bg-[#0b0f16] p-4 border border-white/15">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold uppercase tracking-wider text-[#94a3b8]">Live City Roster</span>
                <span className="text-xs font-bold text-[#f5d07a] tabular-nums">{hud.multiplayerPlayerCount} / 16 Players</span>
              </div>
              <p className="text-xs text-[#94a3b8] leading-relaxed mb-3">
                Server-authoritative 20 Hz simulation with client-side prediction and hermite interpolation. Money, vehicle ownership, and combat damage are verified server-side.
              </p>
              {hud.multiplayerKillfeed.length > 0 && (
                <div className="mt-2 flex flex-col gap-1 border-t border-white/10 pt-2">
                  <div className="text-[10px] font-bold uppercase text-[#94a3b8]">Recent Combat Events:</div>
                  {hud.multiplayerKillfeed.map((k) => (
                    <div key={k.id} className="text-xs text-[#ef4444] font-medium font-mono">
                      ⚔ {k.text}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
