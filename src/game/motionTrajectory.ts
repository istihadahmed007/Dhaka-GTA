import * as THREE from 'three';

/**
 * Fluid Motion Trajectory Visualizer (inspired by UE5 Motion Matching / Pose Search).
 * Dynamically predicts the character's locomotion path over the next 1.8 seconds,
 * rendering illuminated neon-green chevrons / directional arrows along the pavement.
 */
export class MotionTrajectoryVisualizer {
  public group = new THREE.Group();
  public enabled = true;

  private arrowCount = 12;
  private arrowMeshes: THREE.Mesh[] = [];
  private pulseTime = 0;
  private arrowMat: THREE.MeshBasicMaterial;

  constructor() {
    this.group.name = 'MotionTrajectoryVisualizer';

    // Glowing electric green material with soft transparency
    this.arrowMat = new THREE.MeshBasicMaterial({
      color: 0x22c55e,
      transparent: true,
      opacity: 0.85,
      depthWrite: false,
      side: THREE.DoubleSide,
    });

    // Create chevron geometry: V-shaped chevron arrow pointing forward (+Z)
    const shape = new THREE.Shape();
    // Chevron outline
    shape.moveTo(0, 0.45);      // Tip
    shape.lineTo(0.36, -0.28);  // Right outer wing
    shape.lineTo(0.24, -0.38);  // Right inner wing
    shape.lineTo(0, -0.05);     // Inner crotch
    shape.lineTo(-0.24, -0.38); // Left inner wing
    shape.lineTo(-0.36, -0.28); // Left outer wing
    shape.closePath();

    const chevronGeo = new THREE.ShapeGeometry(shape);
    chevronGeo.rotateX(-Math.PI / 2); // Flat on ground XZ plane

    for (let i = 0; i < this.arrowCount; i++) {
      const mesh = new THREE.Mesh(chevronGeo, this.arrowMat.clone());
      mesh.visible = false;
      this.arrowMeshes.push(mesh);
      this.group.add(mesh);
    }
  }

  public setEnabled(val: boolean): void {
    this.enabled = val;
    if (!val) {
      this.hide();
    }
  }

  public toggle(): boolean {
    this.setEnabled(!this.enabled);
    return this.enabled;
  }

  public hide(): void {
    for (let i = 0; i < this.arrowMeshes.length; i++) {
      this.arrowMeshes[i].visible = false;
    }
  }

  /**
   * Updates trajectory prediction given player position, velocity, heading, and ground height.
   */
  public update(
    dt: number,
    playerX: number,
    playerY: number,
    playerZ: number,
    playerVx: number,
    playerVz: number,
    playerHeading: number,
    inVehicle: boolean,
  ): void {
    if (!this.enabled || inVehicle) {
      this.hide();
      return;
    }

    const speed = Math.hypot(playerVx, playerVz);

    // Only project trajectory when moving with discernible speed
    if (speed < 0.35) {
      this.hide();
      return;
    }

    this.pulseTime += dt * 3.5;

    // Current movement angle
    let moveAngle = Math.atan2(playerVx, playerVz);
    // Blend with player heading for natural curvature
    const angleDiff = Math.atan2(Math.sin(playerHeading - moveAngle), Math.cos(playerHeading - moveAngle));
    const turnRate = angleDiff * 1.8;

    let curX = playerX;
    let curZ = playerZ;
    let curAngle = moveAngle;
    const timeStep = 0.12;

    for (let i = 0; i < this.arrowCount; i++) {
      const stepDist = (speed * 0.95 + 1.2) * timeStep;
      curAngle += turnRate * timeStep * 0.55;

      curX += Math.sin(curAngle) * stepDist;
      curZ += Math.cos(curAngle) * stepDist;

      const mesh = this.arrowMeshes[i];
      mesh.visible = true;
      mesh.position.set(curX, playerY + 0.04, curZ);
      mesh.rotation.y = curAngle;

      // Scale: start small near feet, expand, taper off at distance horizon
      const progress = i / (this.arrowCount - 1);
      const scale = THREE.MathUtils.lerp(0.85, 1.45, Math.sin(progress * Math.PI));
      mesh.scale.set(scale, 1, scale);

      // Animate flowing pulse along the chevrons
      const pulsePhase = (i - this.pulseTime) % this.arrowCount;
      const normalizedPhase = ((pulsePhase % this.arrowCount) + this.arrowCount) % this.arrowCount;
      const alphaPulse = 0.45 + 0.5 * Math.sin((normalizedPhase / this.arrowCount) * Math.PI * 2);
      const distanceFade = 1.0 - progress * 0.65;

      const mat = mesh.material as THREE.MeshBasicMaterial;
      mat.opacity = Math.max(0.15, alphaPulse * distanceFade * 0.95);
    }
  }

  public dispose(): void {
    this.arrowMeshes.forEach((mesh) => {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    });
    this.arrowMat.dispose();
    this.group.clear();
  }
}
