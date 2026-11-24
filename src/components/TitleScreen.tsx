import React from 'react';

interface TitleScreenProps {
  onStart: () => void;
  muted: boolean;
  onToggleMute: () => void;
  hasSaveGame?: boolean;
  onContinue?: () => void;
  onStartMultiplayer?: () => void;
}

export const TitleScreen: React.FC<TitleScreenProps> = ({
  onStart,
  muted,
  onToggleMute,
  hasSaveGame,
  onContinue,
  onStartMultiplayer,
}) => {
  return (
    <div className="fixed inset-0 z-20 pointer-events-none flex flex-col justify-between p-6 sm:p-10 md:p-14">
      {/* Subtle left-side dark scrim for guaranteed typography contrast over live 3D Dhaka flyover */}
      <div
        className="fixed inset-y-0 left-0 w-full md:w-[52%] pointer-events-none"
        style={{
          background:
            'linear-gradient(90deg, rgba(10,14,19,0.88) 0%, rgba(10,14,19,0.68) 62%, rgba(10,14,19,0.0) 100%)',
        }}
      />

      {/* Top safe bar: minimal audio toggle */}
      <div className="relative z-10 flex items-center justify-end">
        <button
          type="button"
          onClick={(e) => {
            e.currentTarget.blur();
            onToggleMute();
          }}
          aria-label={muted ? 'Unmute audio' : 'Mute audio'}
          className="pointer-events-auto flex items-center gap-2 px-3 py-2 text-xs font-semibold uppercase tracking-wider text-[#f5f2eb] bg-[#12171f]/90 border border-[#d4a017]/40 hover:border-[#f5b82e] transition-colors cursor-pointer"
        >
          <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" aria-hidden="true">
            {muted ? (
              <path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z" />
            ) : (
              <path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z" />
            )}
          </svg>
          <span>{muted ? 'Muted' : 'Audio On'}</span>
        </button>
      </div>

      {/* Left-third hero title lockup over calm negative space */}
      <div className="relative z-10 max-w-xl my-auto">
        <svg
          viewBox="0 0 680 214"
          className="w-full max-w-[580px] h-auto drop-shadow-[0_6px_18px_rgba(0,0,0,0.85)]"
          role="img"
          aria-label="PROJECT DHAKA — GTA Bangladesh"
        >
          {/* Rickshaw-art inspired crimson & emerald geometric accent bar */}
          <rect x="4" y="16" width="12" height="164" fill="#0f7a53" />
          <rect x="20" y="16" width="5" height="164" fill="#e6a135" />

          {/* Stylized Shapla (Water Lily) Crest */}
          <g transform="translate(52, 34)">
            <path d="M26 0 L34 20 L26 26 L18 20 Z" fill="#f8fafc" />
            <path d="M10 8 L23 22 L16 27 L4 22 Z" fill="#e6a135" />
            <path d="M42 8 L48 22 L36 27 L29 22 Z" fill="#e6a135" />
            <rect x="6" y="29" width="40" height="3.5" fill="#dc2626" />
          </g>

          {/* Small Bangla Identity Line */}
          <text
            x="116"
            y="56"
            fill="#f5d07a"
            fontSize="24"
            fontWeight="700"
            fontFamily="'Noto Sans Bengali', sans-serif"
            letterSpacing="1"
          >
            প্রজেক্ট ঢাকা
          </text>

          {/* Main Display Title */}
          <text
            x="40"
            y="136"
            fill="#f8f5ee"
            stroke="#0b0e13"
            strokeWidth="7"
            paintOrder="stroke fill"
            fontSize="82"
            fontWeight="800"
            fontFamily="'Barlow Condensed', sans-serif"
            letterSpacing="2.5"
          >
            PROJECT DHAKA
          </text>

          {/* Subtitle Strip */}
          <rect x="42" y="152" width="286" height="28" fill="#b91c1c" />
          <text
            x="56"
            y="171"
            fill="#fef3c7"
            fontSize="16"
            fontWeight="700"
            fontFamily="'Barlow Condensed', sans-serif"
            letterSpacing="4"
          >
            OPEN-WORLD BANGLADESH
          </text>
        </svg>

        <div className="mt-8 flex flex-wrap items-center gap-4">
          {hasSaveGame && onContinue && (
            <button
              type="button"
              data-testid="continue-game"
              onClick={(e) => {
                e.currentTarget.blur();
                onContinue();
              }}
              className="pointer-events-auto group flex items-center gap-3 px-8 py-4 bg-[#1e293b] hover:bg-[#334155] text-[#34d399] font-display text-2xl font-extrabold uppercase tracking-[0.14em] border-b-4 border-[#34d399] shadow-[0_10px_28px_rgba(0,0,0,0.65)] transition-transform active:translate-y-0.5 cursor-pointer"
            >
              <span>CONTINUE SAVED GAME</span>
              <svg viewBox="0 0 24 24" className="w-6 h-6 fill-current transition-transform group-hover:translate-x-1" aria-hidden="true">
                <path d="M8 5v14l11-7z" />
              </svg>
            </button>
          )}

          <button
            type="button"
            data-start="true"
            onClick={(e) => {
              e.currentTarget.blur();
              onStart();
            }}
            className="pointer-events-auto group flex items-center gap-3 px-8 py-4 bg-[#0f7a53] hover:bg-[#149465] active:bg-[#0c6242] text-[#faf7f0] font-display text-2xl font-extrabold uppercase tracking-[0.14em] border-b-4 border-[#e6a135] shadow-[0_10px_28px_rgba(0,0,0,0.65)] transition-transform active:translate-y-0.5 cursor-pointer"
          >
            <span>{hasSaveGame ? 'NEW GAME' : 'ENTER DHAKA'}</span>
            <svg viewBox="0 0 24 24" className="w-6 h-6 fill-current transition-transform group-hover:translate-x-1" aria-hidden="true">
              <path d="M8 5v14l11-7z" />
            </svg>
          </button>

          {onStartMultiplayer && (
            <button
              type="button"
              data-testid="start-multiplayer"
              onClick={(e) => {
                e.currentTarget.blur();
                onStartMultiplayer();
              }}
              className="pointer-events-auto group flex items-center gap-3 px-8 py-4 bg-[#1e1b4b] hover:bg-[#312e81] active:bg-[#1e1b4b] text-[#c084fc] font-display text-2xl font-extrabold uppercase tracking-[0.14em] border-b-4 border-[#a855f7] shadow-[0_10px_28px_rgba(0,0,0,0.65)] transition-transform active:translate-y-0.5 cursor-pointer"
            >
              <span>MULTIPLAYER (ONLINE)</span>
              <svg viewBox="0 0 24 24" className="w-6 h-6 fill-current transition-transform group-hover:translate-x-1" aria-hidden="true">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z" />
              </svg>
            </button>
          )}
        </div>
      </div>

      {/* Bottom safe margin: single flat borderless control-hint line */}
      <div className="relative z-10 text-xs sm:text-sm text-[#e5e0d5]/90 tracking-wide hud-text-stroke">
        WASD Move · Space Jump · F Drive Vehicle · [Tab] Weapon Wheel · [P] Smartphone · [C] Switch Hero · [Y] Tag-Team Partner · [H] Rob Shop · Esc Pause
      </div>
    </div>
  );
};
