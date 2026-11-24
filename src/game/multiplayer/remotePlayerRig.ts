import * as THREE from 'three';
import {
  createHumanoidRig,
  HumanoidRig,
  setHumanoidOutfit,
  setHumanoidWeaponMesh,
  upgradeHumanoidRigToGlb,
} from '../models';
import { DhakaTextureSet } from '../textures';
import { DhakaCharacterGlbTemplate } from '../assetLoader';
import { OutfitId, OUTFIT_SPECS, WeaponId } from '../types';

export class RemotePlayerRig {
  public id: string;
  public name: string;
  public rig: HumanoidRig;
  public group: THREE.Group;
  private nameSprite: THREE.Sprite;
  private nameCanvas: HTMLCanvasElement;
  private nameCtx: CanvasRenderingContext2D;
  private nameTexture: THREE.CanvasTexture;
  private lastHp = 100;
  private currentWeapon: WeaponId = 'fists';

  constructor(
    id: string,
    name: string,
    outfit: OutfitId,
    color: number,
    textures: DhakaTextureSet,
    characterTemplate: DhakaCharacterGlbTemplate | null,
    castShadow = true,
  ) {
    this.id = id;
    this.name = name;
    this.group = new THREE.Group();
    this.group.name = `RemotePlayer_${id}`;

    const outfitSpec = OUTFIT_SPECS[outfit] || OUTFIT_SPECS['street'];

    // Base humanoid rig
    this.rig = createHumanoidRig(
      outfitSpec.torsoColor,
      outfitSpec.pantsColor,
      outfitSpec.accentColor,
      textures,
      { isPlayer: false, castShadow },
    );
    if (characterTemplate) {
      upgradeHumanoidRigToGlb(this.rig, characterTemplate, textures, castShadow);
    }
    setHumanoidOutfit(this.rig, outfit);
    setHumanoidWeaponMesh(this.rig, 'fists');
    this.currentWeapon = 'fists';

    this.group.add(this.rig.group);

    // Overhead nametag and health bar billboard sprite
    this.nameCanvas = document.createElement('canvas');
    this.nameCanvas.width = 256;
    this.nameCanvas.height = 64;
    this.nameCtx = this.nameCanvas.getContext('2d')!;

    this.nameTexture = new THREE.CanvasTexture(this.nameCanvas);
    this.nameTexture.colorSpace = THREE.SRGBColorSpace;

    const spriteMat = new THREE.SpriteMaterial({
      map: this.nameTexture,
      transparent: true,
      depthTest: false,
    });

    this.nameSprite = new THREE.Sprite(spriteMat);
    this.nameSprite.position.y = 2.25;
    this.nameSprite.scale.set(2.4, 0.6, 1.0);
    this.group.add(this.nameSprite);

    this.updateNametag(100, 50);
  }

  public updateNametag(hp: number, armor: number): void {
    const ctx = this.nameCtx;
    const w = 256;
    const h = 64;

    ctx.clearRect(0, 0, w, h);

    // Background pill
    ctx.fillStyle = 'rgba(11, 14, 19, 0.85)';
    ctx.beginPath();
    ctx.roundRect(16, 4, 224, 56, 12);
    ctx.fill();

    ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Player Callsign
    ctx.fillStyle = '#f8fafc';
    ctx.font = 'bold 22px "Barlow Condensed", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(this.name.toUpperCase(), 128, 28);

    // Health bar container
    const barX = 36;
    const barY = 38;
    const barW = 184;
    const barH = 10;

    ctx.fillStyle = '#1e293b';
    ctx.fillRect(barX, barY, barW, barH);

    // Health fill
    const hpRatio = Math.max(0, Math.min(1, hp / 100));
    ctx.fillStyle = hpRatio > 0.4 ? '#22c55e' : hpRatio > 0.2 ? '#eab308' : '#ef4444';
    ctx.fillRect(barX, barY, barW * hpRatio, barH);

    // Armor fill top border
    if (armor > 0) {
      const armRatio = Math.max(0, Math.min(1, armor / 100));
      ctx.fillStyle = '#38bdf8';
      ctx.fillRect(barX, barY, barW * armRatio, 3);
    }

    this.nameTexture.needsUpdate = true;
  }

  public updateInterpolated(
    x: number,
    y: number,
    z: number,
    heading: number,
    speed: number,
    weapon: WeaponId,
    hp: number,
    armor: number,
    state: string,
    dt: number,
  ): void {
    this.group.position.set(x, y, z);
    this.group.rotation.y = heading;

    if (Math.abs(hp - this.lastHp) > 1) {
      this.lastHp = hp;
      this.updateNametag(hp, armor);
    }

    // Update weapon mesh if changed
    if (this.currentWeapon !== weapon) {
      this.currentWeapon = weapon;
      setHumanoidWeaponMesh(this.rig, weapon);
    }

    // Drive animation mixer
    this.rig.animator.update(dt, {
      speed,
      isGrounded: state !== 'jump',
      weapon,
      maxWalkSpeed: 3.8,
      maxRunSpeed: 7.2,
    });
  }

  public dispose(): void {
    this.rig.animator.disposeMixer();
    this.nameTexture.dispose();
    if (this.nameSprite.material) {
      this.nameSprite.material.dispose();
    }
  }
}
