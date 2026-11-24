import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { OutfitId, WeaponId } from '../game/types';

/**
 * Generates a salted SHA-256 hash for secure account PIN storage.
 */
export function hashPin(pin: string, salt = crypto.randomBytes(16).toString('hex')): string {
  const hash = crypto.createHash('sha256').update(`${salt}:${pin}`).digest('hex');
  return `${salt}:${hash}`;
}

/**
 * Constant-time verification of user PIN against stored salted hash or legacy plaintext.
 */
export function verifyPin(pin: string, storedHashOrPlain: string): boolean {
  if (storedHashOrPlain.includes(':')) {
    const parts = storedHashOrPlain.split(':');
    if (parts.length === 2) {
      const [salt, expectedHash] = parts;
      const actualHash = crypto.createHash('sha256').update(`${salt}:${pin}`).digest('hex');
      try {
        return crypto.timingSafeEqual(Buffer.from(actualHash), Buffer.from(expectedHash));
      } catch {
        return false;
      }
    }
  }
  return storedHashOrPlain === pin;
}

export interface PersistentAccount {
  id: string;
  name: string;
  token: string;
  pin?: string;
  outfit: OutfitId;
  color: number;
  money: number;
  kills: number;
  deaths: number;
  weapon: WeaponId;
  unlockedWeapons: WeaponId[];
  ammo: Record<WeaponId, number>;
  lastX: number;
  lastY: number;
  lastZ: number;
  lastHeading: number;
  createdAt: number;
  lastSeenAt: number;
}

export type AuthResult =
  | { ok: true; account: PersistentAccount; isNew: boolean }
  | { ok: false; error: string; code: 'AUTH_REQUIRED' | 'INVALID_TOKEN' | 'NAME_TAKEN' | 'INVALID_INPUT' };

export class AccountStore {
  private accounts = new Map<string, PersistentAccount>();
  private nameToId = new Map<string, string>();
  private tokenToId = new Map<string, string>();
  private filePath: string | null = null;
  private dirty = false;
  private saveTimer: NodeJS.Timeout | null = null;

  constructor(filePath?: string) {
    if (filePath) {
      this.filePath = path.resolve(filePath);
      this.loadFromDisk();
    }
  }

  public authenticate(
    name: string,
    token?: string,
    pin?: string,
    preferredOutfit: OutfitId = 'street',
    preferredColor = 0x26466d,
  ): AuthResult {
    const cleanName = (name || '').trim().slice(0, 16);
    if (!cleanName || !/^[a-zA-Z0-9_\-\u0980-\u09FF ]{2,16}$/.test(cleanName)) {
      return {
        ok: false,
        error: 'CallSign must be between 2 and 16 letters, numbers, or Bangla characters.',
        code: 'INVALID_INPUT',
      };
    }

    // 1. Reconnection via secret session token
    if (token) {
      const cleanToken = token.trim();
      const id = this.tokenToId.get(cleanToken);
      if (id) {
        const existing = this.accounts.get(id);
        if (existing && existing.token === cleanToken) {
          existing.lastSeenAt = Date.now();
          this.scheduleSave();
          return { ok: true, account: existing, isNew: false };
        }
      }
      return {
        ok: false,
        error: 'Session token invalid or expired. Enter your CallSign and PIN to sign in.',
        code: 'INVALID_TOKEN',
      };
    }

    const lowerName = cleanName.toLowerCase();
    const existingId = this.nameToId.get(lowerName);

    // 2. Existing account with this name: require verified ownership (PIN or valid token)
    if (existingId) {
      const existing = this.accounts.get(existingId);
      if (existing) {
        if (pin && existing.pin && verifyPin(pin.trim(), existing.pin)) {
          // If legacy plain, automatically migrate to salted hash
          if (!existing.pin.includes(':')) {
            existing.pin = hashPin(pin.trim());
          }
          existing.lastSeenAt = Date.now();
          this.scheduleSave();
          return { ok: true, account: existing, isNew: false };
        }
        return {
          ok: false,
          error: `CallSign "${cleanName}" is already registered. Enter your account PIN or reconnect using your session token.`,
          code: 'AUTH_REQUIRED',
        };
      }
    }

    // 3. Create fresh account
    const id = `usr_${crypto.randomBytes(8).toString('hex')}`;
    const newToken = `tok_${crypto.randomBytes(24).toString('hex')}`;
    const account: PersistentAccount = {
      id,
      name: cleanName,
      token: newToken,
      pin: pin ? hashPin(pin.trim().slice(0, 8)) : undefined,
      outfit: preferredOutfit,
      color: preferredColor,
      money: 1200, // Starter Taka
      kills: 0,
      deaths: 0,
      weapon: 'fists',
      unlockedWeapons: ['fists', 'pistol'],
      ammo: {
        fists: 0,
        pistol: 48,
        smg: 0,
        shotgun: 0,
      },
      lastX: -2.2,
      lastY: 0,
      lastZ: 35.2,
      lastHeading: 0,
      createdAt: Date.now(),
      lastSeenAt: Date.now(),
    };

    this.accounts.set(id, account);
    this.nameToId.set(lowerName, id);
    this.tokenToId.set(newToken, id);
    this.scheduleSave();

    return { ok: true, account, isNew: true };
  }

  public getById(id: string): PersistentAccount | null {
    return this.accounts.get(id) ?? null;
  }

  public getByToken(token: string): PersistentAccount | null {
    const id = this.tokenToId.get(token);
    return id ? this.accounts.get(id) ?? null : null;
  }

  public updateStats(
    id: string,
    updates: Partial<Pick<PersistentAccount, 'money' | 'kills' | 'deaths' | 'weapon' | 'lastX' | 'lastY' | 'lastZ' | 'lastHeading'>>,
  ): void {
    const acc = this.accounts.get(id);
    if (!acc) return;
    if (updates.money !== undefined) acc.money = Math.max(0, updates.money);
    if (updates.kills !== undefined) acc.kills = updates.kills;
    if (updates.deaths !== undefined) acc.deaths = updates.deaths;
    if (updates.weapon !== undefined) acc.weapon = updates.weapon;
    if (updates.lastX !== undefined) acc.lastX = updates.lastX;
    if (updates.lastY !== undefined) acc.lastY = updates.lastY;
    if (updates.lastZ !== undefined) acc.lastZ = updates.lastZ;
    if (updates.lastHeading !== undefined) acc.lastHeading = updates.lastHeading;
    acc.lastSeenAt = Date.now();
    this.scheduleSave();
  }

  public recordKill(killerId: string, victimId: string, reward = 350): void {
    const killer = this.accounts.get(killerId);
    if (killer) {
      killer.kills += 1;
      killer.money += reward;
    }
    const victim = this.accounts.get(victimId);
    if (victim) {
      victim.deaths += 1;
    }
    this.scheduleSave();
  }

  private scheduleSave(): void {
    this.dirty = true;
    if (this.saveTimer || !this.filePath) return;
    this.saveTimer = setTimeout(() => {
      this.saveTimer = null;
      this.saveToDisk();
    }, 1500);
  }

  public saveToDisk(): void {
    if (!this.filePath || !this.dirty) return;
    try {
      const dir = path.dirname(this.filePath);
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
      const records = Array.from(this.accounts.values());
      const tempPath = `${this.filePath}.${Date.now()}.tmp`;
      fs.writeFileSync(tempPath, JSON.stringify(records, null, 2), 'utf8');
      fs.renameSync(tempPath, this.filePath);
      this.dirty = false;
    } catch (err) {
      console.error('[AccountStore] Failed to save accounts to disk:', err);
    }
  }

  private loadFromDisk(): void {
    if (!this.filePath || !fs.existsSync(this.filePath)) return;
    try {
      const content = fs.readFileSync(this.filePath, 'utf8');
      const records = JSON.parse(content) as PersistentAccount[];
      if (Array.isArray(records)) {
        for (const r of records) {
          if (r.id && r.name && r.token) {
            this.accounts.set(r.id, r);
            this.nameToId.set(r.name.toLowerCase(), r.id);
            this.tokenToId.set(r.token, r.id);
          }
        }
      }
    } catch (err) {
      console.error('[AccountStore] Failed to load accounts from disk:', err);
    }
  }

  public close(): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
      this.saveTimer = null;
    }
    this.saveToDisk();
  }
}
