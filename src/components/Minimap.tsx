import React, { useEffect, useRef } from 'react';
import { MinimapBlip } from '../game/types';
import { LAKE_ZONES, RIVER_Z_START, ROAD_SEGMENTS, WORLD_HALF_SIZE } from '../game/worldData';

interface MinimapProps {
  playerX: number;
  playerZ: number;
  playerHeading: number;
  blips: MinimapBlip[];
  wantedLevel: number;
  inVehicle?: boolean;
  policeSearchRadius?: { x: number; z: number; radius: number; active: boolean } | null;
  companion?: { x?: number; z?: number; name?: string; state?: string } | null;
}

export const Minimap: React.FC<MinimapProps> = ({
  playerX,
  playerZ,
  playerHeading,
  blips,
  wantedLevel,
  inVehicle = false,
  policeSearchRadius,
  companion,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const size = canvas.width;
    const half = size / 2;
    // Dynamic zoom tuned for 540m x 470m expanded Dhaka (wider sightline while driving)
    const scale = inVehicle ? 0.34 : 0.54;

    ctx.clearRect(0, 0, size, size);

    // Circular clip
    ctx.save();
    ctx.beginPath();
    ctx.arc(half, half, half - 3, 0, Math.PI * 2);
    ctx.clip();

    // Dark Dhaka urban map base
    ctx.fillStyle = '#141b24';
    ctx.fillRect(0, 0, size, size);

    const toMap = (wx: number, wz: number): [number, number] => [
      half + (wx - playerX) * scale,
      half + (wz - playerZ) * scale,
    ];

    // Playable city pad tint so bounds read clearly
    const [bx1, by1] = toMap(-WORLD_HALF_SIZE, -WORLD_HALF_SIZE);
    const [bx2, by2] = toMap(WORLD_HALF_SIZE, RIVER_Z_START);
    ctx.fillStyle = '#1b2430';
    ctx.fillRect(bx1, by1, bx2 - bx1, by2 - by1);

    // Inland lakes (Dhanmondi Lake)
    ctx.fillStyle = '#115e75';
    for (const lake of LAKE_ZONES) {
      const [lx1, ly1] = toMap(lake.x - lake.w * 0.5, lake.z - lake.d * 0.5);
      ctx.fillRect(lx1, ly1, lake.w * scale, lake.d * scale);
    }

    // Buriganga River band (south of RIVER_Z_START)
    const [, riverY] = toMap(0, RIVER_Z_START);
    if (riverY < size + 40) {
      ctx.fillStyle = '#0c4a63';
      ctx.fillRect(0, riverY, size, size - riverY + 60);
      // River embankment edge line
      ctx.strokeStyle = '#64748b';
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(0, riverY);
      ctx.lineTo(size, riverY);
      ctx.stroke();

      // Buriganga Bridge deck crossing at x = 0
      const [brX, brY1] = toMap(0, RIVER_Z_START - 4);
      const [, brY2] = toMap(0, RIVER_Z_START + 48);
      ctx.strokeStyle = '#64748b';
      ctx.lineWidth = Math.max(4, 14 * scale);
      ctx.beginPath();
      ctx.moveTo(brX, brY1);
      ctx.lineTo(brX, brY2);
      ctx.stroke();
    }

    // Hierarchical Dhaka road network (alleys/streets first, then boulevards, then grand avenues)
    ctx.lineCap = 'butt';
    const tierOrder = ['alley', 'street', 'boulevard', 'avenue'] as const;
    for (const tier of tierOrder) {
      ctx.strokeStyle =
        tier === 'avenue'
          ? '#526277'
          : tier === 'boulevard'
            ? '#3e4c5e'
            : '#2c3746';
      for (const seg of ROAD_SEGMENTS) {
        if (seg.tier !== tier) continue;
        const [x1, y1] = toMap(seg.x1, seg.z1);
        const [x2, y2] = toMap(seg.x2, seg.z2);
        const minW = tier === 'avenue' ? 4.5 : tier === 'boulevard' ? 3.2 : 2.1;
        ctx.lineWidth = Math.max(minW, seg.width * scale * 0.72);
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
      }
    }

    // Shapla Roundabout ring at (0,0)
    const [sx, sy] = toMap(0, 0);
    ctx.fillStyle = '#0f766e';
    ctx.beginPath();
    ctx.arc(sx, sy, Math.max(3.8, 7 * scale), 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#d4a017';
    ctx.lineWidth = 1.4;
    ctx.stroke();

    // City bounds outline
    ctx.strokeStyle = 'rgba(255,255,255,0.16)';
    ctx.lineWidth = 1;
    ctx.strokeRect(bx1, by1, bx2 - bx1, by2 - by1);

    // Blips (clamped to radar edge for mission & fare targets; nearest garages/shops clamped gently)
    for (const blip of blips) {
      let [mx, my] = toMap(blip.x, blip.z);
      const dx = mx - half;
      const dy = my - half;
      const dist = Math.hypot(dx, dy);
      const maxR = half - 10;

      if (dist > maxR) {
        if (blip.kind === 'mission' || blip.kind === 'fare') {
          mx = half + (dx / dist) * maxR;
          my = half + (dy / dist) * maxR;
        } else if ((blip.kind === 'garage' || blip.kind === 'shop') && dist < maxR * 2.2) {
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
        ctx.fillRect(mx - 3.5, my - 3.5, 7, 7);
      } else if (blip.kind === 'shop') {
        ctx.fillStyle = '#10b981';
        ctx.fillRect(mx - 3.5, my - 3.5, 7, 7);
      } else if (blip.kind === 'police') {
        ctx.fillStyle = '#3b82f6';
        ctx.beginPath();
        ctx.arc(mx, my, 3.8, 0, Math.PI * 2);
        ctx.fill();
      } else if (blip.kind === 'enemy') {
        ctx.fillStyle = '#ef4444';
        ctx.beginPath();
        ctx.arc(mx, my, 3.8, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    // GTA VI Flashing Police Search Radius Perimeter
    if (policeSearchRadius && policeSearchRadius.active) {
      const [px, py] = toMap(policeSearchRadius.x, policeSearchRadius.z);
      const pr = Math.max(10, policeSearchRadius.radius * scale);
      ctx.save();
      ctx.fillStyle = 'rgba(239, 68, 68, 0.22)';
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 2.0;
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.arc(px, py, pr, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.restore();
    }

    // GTA VI Tag-Team Companion Blip
    if (companion && companion.x !== undefined && companion.z !== undefined) {
      const [cx, cy] = toMap(companion.x, companion.z);
      ctx.save();
      ctx.fillStyle = '#38bdf8';
      ctx.strokeStyle = '#0284c7';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.arc(cx, cy, 4.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.restore();
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
  }, [playerX, playerZ, playerHeading, blips, wantedLevel, inVehicle, policeSearchRadius, companion]);

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
