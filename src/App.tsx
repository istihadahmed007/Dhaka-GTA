import { useCallback, useEffect, useRef, useState } from 'react';
import { DhakaGameEngine } from './game/engine';
import { GraphicsQualityPreset, HudSnapshot, OutfitId, TimePreset, WeaponId, WeatherMode } from './game/types';
import { STORY_MISSIONS } from './game/worldData';
import { FALLBACK_DHAKA_REGIONAL_PLACES } from './api/bdapisClient';
import { TitleScreen } from './components/TitleScreen';
import { GameHud } from './components/GameHud';
import { Overlays } from './components/Overlays';

const INITIAL_HUD: HudSnapshot = {
  phase: 'title',
  modal: 'none',
  districtName: 'Old Dhaka · Chawkbazar',
  districtBangla: 'পুরান ঢাকা · চকবাজার',
  streetName: 'Kazi Nazrul Islam Ave',
  streetBangla: 'কাজী নজরুল ইসলাম এভিনিউ',
  timeOfDay: 15.8,
  timeLabel: '3:48 PM',
  weather: 'clear',
  graphicsQuality: 'high',
  characterRigMode: 'procedural',
  liveWeather: {
    enabled: false,
    status: 'idle',
    conditionLabel: null,
    conditionBangla: null,
    temperatureC: null,
    humidityPct: null,
    windKmh: null,
    fromCache: false,
    error: null,
  },
  regionalPlaces: {
    enabled: false,
    status: 'idle',
    source: 'fallback',
    places: [...FALLBACK_DHAKA_REGIONAL_PLACES],
    error: null,
  },
  hp: 100,
  maxHp: 100,
  armor: 50,
  maxArmor: 100,
  stamina: 100,
  money: 800,
  wantedLevel: 0,
  wantedCooldownRatio: 0,
  inVehicle: false,
  vehicleName: '',
  vehicleBangla: '',
  vehicleKind: null,
  vehicleSpeedKmh: 0,
  vehicleHp: 0,
  vehicleMaxHp: 100,
  weapon: 'fists',
  unlockedWeapons: ['fists', 'pistol'],
  magAmmo: 0,
  reserveAmmo: 0,
  reloading: false,
  outfit: 'street',
  unlockedOutfits: ['street'],
  interactPrompt: null,
  interactKey: null,
  missionTitle: null,
  missionBangla: null,
  missionObjective: null,
  missionProgressText: null,
  fareActive: false,
  fareText: null,
  fareTimer: 0,
  dialogue: null,
  toast: null,
  muted: false,
  playerX: -2.2,
  playerZ: 35.2,
  playerHeading: Math.PI,
  blips: [],
  stats: {
    kills: 0,
    missionsCompleted: 0,
    totalMissions: STORY_MISSIONS.length,
    faresCompleted: 0,
    playTimeSec: 0,
    cause: '',
  },
};

export default function App() {
  const mountRef = useRef<HTMLDivElement | null>(null);
  const engineRef = useRef<DhakaGameEngine | null>(null);
  const [hud, setHud] = useState<HudSnapshot>(INITIAL_HUD);

  useEffect(() => {
    const el = mountRef.current;
    if (!el) return;
    const engine = new DhakaGameEngine(el, (snap) => {
      setHud(snap);
    });
    engineRef.current = engine;
    return () => {
      engine.dispose();
      engineRef.current = null;
    };
  }, []);

  const handleStart = useCallback(() => engineRef.current?.startGame(), []);
  const handleRestart = useCallback(() => engineRef.current?.restartGame(), []);
  const handlePause = useCallback(() => engineRef.current?.togglePause(), []);
  const handleTitle = useCallback(() => engineRef.current?.returnToTitle(), []);
  const handleCloseModal = useCallback(() => engineRef.current?.closeModal(), []);
  const handleMute = useCallback(() => engineRef.current?.toggleMute(), []);
  const handleWardrobe = useCallback(() => engineRef.current?.openWardrobe(), []);
  const handleSelectWeapon = useCallback((w: WeaponId) => engineRef.current?.selectWeapon(w), []);
  const handleToggleFare = useCallback(() => engineRef.current?.toggleSideFare(), []);
  const handleSetTime = useCallback((t: TimePreset) => engineRef.current?.setTimePreset(t), []);
  const handleSetWeather = useCallback((w: WeatherMode) => engineRef.current?.setWeather(w), []);
  const handleSetGraphicsQuality = useCallback(
    (q: GraphicsQualityPreset) => engineRef.current?.setGraphicsQuality(q),
    [],
  );
  const handleToggleLiveWeather = useCallback(
    (enabled: boolean) => engineRef.current?.setLiveWeatherEnabled(enabled),
    [],
  );
  const handleRefreshLiveWeather = useCallback(() => engineRef.current?.refreshLiveWeather(), []);
  const handleToggleRegionalPlaces = useCallback(
    (enabled: boolean) => engineRef.current?.setRegionalPlacesEnabled(enabled),
    [],
  );
  const handleShopBuy = useCallback((item: 'biriyani' | 'armor' | WeaponId) => engineRef.current?.handleShopPurchase(item), []);
  const handleGarage = useCallback(
    (svc: 'repair' | 'clear_heat' | 'paint', color?: number) => engineRef.current?.handleGarageAction(svc, color),
    [],
  );
  const handleOutfit = useCallback((id: OutfitId) => engineRef.current?.handleOutfitSelect(id), []);

  return (
    <div className="relative w-screen h-screen overflow-hidden bg-[#0b0e13]">
      {/* Isolated WebGL canvas mount sibling to React HUD */}
      <div ref={mountRef} data-testid="dhaka-canvas-root" className="fixed inset-0 w-full h-full" />

      {hud.phase === 'title' && (
        <TitleScreen onStart={handleStart} muted={hud.muted} onToggleMute={handleMute} />
      )}

      {hud.phase !== 'title' && (
        <GameHud
          hud={hud}
          onToggleMute={handleMute}
          onTogglePause={handlePause}
          onOpenWardrobe={handleWardrobe}
          onSelectWeapon={handleSelectWeapon}
          onToggleFare={handleToggleFare}
        />
      )}

      <Overlays
        hud={hud}
        onResume={handlePause}
        onRestart={handleRestart}
        onTitle={handleTitle}
        onCloseModal={handleCloseModal}
        onSetTime={handleSetTime}
        onSetWeather={handleSetWeather}
        onSetGraphicsQuality={handleSetGraphicsQuality}
        onToggleLiveWeather={handleToggleLiveWeather}
        onRefreshLiveWeather={handleRefreshLiveWeather}
        onToggleRegionalPlaces={handleToggleRegionalPlaces}
        onShopBuy={handleShopBuy}
        onGarageService={handleGarage}
        onOutfitSelect={handleOutfit}
      />
    </div>
  );
}
