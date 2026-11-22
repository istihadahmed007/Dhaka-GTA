import React, { useEffect, useRef } from 'react';
import { MinimapBlip } from '../game/types';
import { RIVER_Z_START, ROAD_SEGMENTS, WORLD_HALF_SIZE } from '../game/worldData';

interface MinimapProps {
  playerX: number;
  playerZ: number;
  playerHeading: number;
  blips: MinimapBlip[];
  wantedLevel: number;
}

export const Minimap: React.FC<MinimapProps> = ({
  playerX,
  playerZ,
  playerHeading,
  blips,
  wantedLevel,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const size = canvas.width;
    const half = size / 2;
    const scale = 0.85; // pixels per world meter

    ctx.clearRect(0, 0, size, size);

    // Circular clip
    ctx.save();
    ctx.beginPath();
    ctx.arc(half, half, half - 3, 0, Math.PI * 2);
    ctx.clip();

    // Dark Dhaka urban map base
    ctx.fillStyle = '#181f28';
    ctx.fillRect(0, 0, size, size);

    const toMap = (wx: number, wz: number): [number, number] => [
      half + (wx - playerX) * scale,
      half + (wz - playerZ) * scale,
    ];

    // Buriganga River band (south of z = 80)
    const [, riverY] = toMap(0, RIVER_Z_START);
    if (riverY < size + 40) {
      ctx.fillStyle = '#0e4a63';
      ctx.fillRect(0, riverY, size, size - riverY + 60);
    }

    // City road network
    ctx.strokeStyle = '#3b4656';
    ctx.lineCap = 'butt';
    for (const seg of ROAD_SEGMENTS) {
      const [x1, y1] = toMap(seg.x1, seg.z1);
      const [x2, y2] = toMap(seg.x2, seg.z2);
      ctx.lineWidth = Math.max(4, seg.width * scale * 0.72);
      ctx.beginPath();
      ctx.moveTo(x1, y1);
      ctx.lineTo(x2, y2);
      ctx.stroke();
    }

    // Shapla Roundabout ring at (0,0)
    const [sx, sy] = toMap(0, 0);
    ctx.fillStyle = '#0f766e';
    ctx.beginPath();
    ctx.arc(sx, sy, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#d4a017';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // City bounds outline
    const [bx1, by1] = toMap(-WORLD_HALF_SIZE, -WORLD_HALF_SIZE);
    const [bx2, by2] = toMap(WORLD_HALF_SIZE, RIVER_Z_START);
    ctx.strokeStyle = 'rgba(255,255,255,0.12)';
    ctx.lineWidth = 1;
    ctx.strokeRect(bx1, by1, bx2 - bx1, by2 - by1);

    // Blips (clamped to radar edge for mission & fare targets so player always knows direction)
    for (const blip of blips) {
      let [mx, my] = toMap(blip.x, blip.z);
      const dx = mx - half;
      const dy = my - half;
      const dist = Math.hypot(dx, dy);
      const maxR = half - 10;

      if (dist > maxR) {
        if (blip.kind === 'mission' || blip.kind === 'fare' || blip.kind === 'garage' || blip.kind === 'shop') {
          mx = half + (dx / dist) * maxR;
          my = half + (dy / dist) * maxR;
        } else {
          continue;
        }
      }

      if (blip.kind === 'mission') {
        ctx.fillStyle = '#fbbf24';
        ctx.strokeStyle = '#111827';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(mx, my, 5.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
      } else if (blip.kind === 'fare') {
        ctx.fillStyle = '#22d3ee';
        ctx.strokeStyle = '#0f172a';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.arc(mx, my, 4.8, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
      } else if (blip.kind === 'garage') {
        ctx.fillStyle = '#f59e0b';
        ctx.fillRect(mx - 4, my - 4, 8, 8);
      } else if (blip.kind === 'shop') {
        ctx.fillStyle = '#10b981';
        ctx.fillRect(mx - 4, my - 4, 8, 8);
      } else if (blip.kind === 'police') {
        ctx.fillStyle = '#3b82f6';
        ctx.beginPath();
        ctx.arc(mx, my, 4, 0, Math.PI * 2);
        ctx.fill();
      } else if (blip.kind === 'enemy') {
        ctx.fillStyle = '#ef4444';
        ctx.beginPath();
        ctx.arc(mx, my, 4, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    // Player directional triangle at center (heading PI = North = up on canvas)
    ctx.save();
    ctx.translate(half, half);
    ctx.rotate(Math.PI - playerHeading);
    ctx.fillStyle = '#f8fafc';
    ctx.strokeStyle = '#0b0e13';
    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.moveTo(0, -7);
    ctx.lineTo(5.5, 6);
    ctx.lineTo(0, 3);
    ctx.lineTo(-5.5, 6);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.restore();

    ctx.restore();

    // Outer Dhaka Brass/Crimson Ring
    ctx.strokeStyle = wantedLevel > 0 ? '#ef4444' : '#d4a017';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(half, half, half - 2, 0, Math.PI * 2);
    ctx.stroke();

    // North Compass Pip
    ctx.fillStyle = '#0b0e13';
    ctx.beginPath();
    ctx.arc(half, 10, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#f5d07a';
    ctx.font = 'bold 10px Inter, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('N', half, 10.5);
  }, [playerX, playerZ, playerHeading, blips, wantedLevel]);

  return (
    <canvas
      ref={canvasRef}
      width={156}
      height={156}
      aria-label="Dhaka city minimap"
      className="w-[140px] h-[140px] sm:w-[156px] sm:h-[156px] rounded-full shadow-[0_8px_22px_rgba(0,0,0,0.75)]"
    />
  );
};
