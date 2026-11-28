import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { WebSocket } from 'ws';
import { AccountStore } from './accountStore';
import { DhakaAuthoritativeServer } from './authoritativeServer';

function waitMs(ms: number) {
  return new Promise((r) => setTimeout(r, ms));
}

async function connectAndAuth(
  port: number,
  opts: { name: string; token?: string; pin?: string; protagonistId?: 'tariq' | 'maya'; clientEngine?: 'unity' | 'web' },
) {
  const ws = new WebSocket(`ws://localhost:${port}`);
  const inbox: any[] = [];
  await new Promise<void>((resolve, reject) => {
    ws.once('open', () => resolve());
    ws.once('error', reject);
  });
  ws.on('message', (data) => {
    try {
      inbox.push(JSON.parse(data.toString()));
    } catch {}
  });
  ws.send(
    JSON.stringify({
      type: 'c_auth',
      name: opts.name,
      token: opts.token,
      pin: opts.pin,
      protagonistId: opts.protagonistId ?? 'tariq',
      clientEngine: opts.clientEngine ?? 'unity',
    }),
  );

  for (let i = 0; i < 30; i++) {
    const authOk = inbox.find((m) => m.type === 's_auth_ok');
    if (authOk) return { ws, inbox, authOk };
    await waitMs(25);
  }
  throw new Error(`Timed out waiting for s_auth_ok for ${opts.name}: ${JSON.stringify(inbox)}`);
}

test('unity-mp-server: 4 simultaneous Unity clients sync in 1:1 extent without fake players', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const c1 = await connectAndAuth(port, { name: 'Unity_Tariq_1', protagonistId: 'tariq', clientEngine: 'unity' });
  const c2 = await connectAndAuth(port, { name: 'Unity_Maya_2', protagonistId: 'maya', clientEngine: 'unity' });
  const c3 = await connectAndAuth(port, { name: 'Unity_Rafi_3', protagonistId: 'tariq', clientEngine: 'unity' });
  const c4 = await connectAndAuth(port, { name: 'Unity_Sadia_4', protagonistId: 'maya', clientEngine: 'unity' });

  try {
    // Move Client 1 beyond legacy 270m bounds inside 1:1 Gulshan (~600m NE)
    const s1 = server.getSession(c1.authOk.playerId)!;
    s1.state.transform.x = 420;
    s1.state.transform.z = -510;
    c1.ws.send(JSON.stringify({ type: 'c_input', seq: 1, dt: 0.05, moveX: 1, moveZ: 0, heading: 1.57, sprint: true, jump: false }));

    await waitMs(130);

    // Must NOT clamp Unity clients inside +/-270m
    assert.ok(s1.state.transform.x > 400, 'Unity 1:1 client coordinates past 270m must be preserved');

    // Broadcast snapshot must contain ONLY the 4 real clients (zero fake online players!)
    const snap = c2.inbox.filter((m) => m.type === 's_world_snapshot').pop();
    assert.ok(snap, 'Clients must receive 20Hz s_world_snapshot updates');
    assert.equal(snap.players.length, 4);
    assert.equal(snap.world.connectedCount, 4);

    // Verify /health monitoring telemetry
    const healthRaw = await new Promise<string>((resolve, reject) => {
      http.get(`http://localhost:${port}/health`, (res) => {
        let body = '';
        res.on('data', (chunk) => (body += chunk));
        res.on('end', () => resolve(body));
      }).on('error', reject);
    });
    const health = JSON.parse(healthRaw);
    assert.equal(health.status, 'ok');
    assert.equal(health.players, 4);
    assert.equal(health.maxPlayers, 16);
    assert.equal(health.realPlayersOnly, true);
    assert.ok(health.tickRateHz === 20);
  } finally {
    c1.ws.close();
    c2.ws.close();
    c3.ws.close();
    c4.ws.close();
    await server.stop();
  }
});

test('unity-mp-server: Vehicle purchase, ownership lock validation, persistent inventory & reconnect work cleanly', async () => {
  const tmpFile = path.join(os.tmpdir(), `dhaka_mp_test_${Date.now()}.json`);
  const store = new AccountStore(tmpFile);
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  try {
    const owner = await connectAndAuth(port, { name: 'Owner_Tariq', pin: '4321', clientEngine: 'unity' });
    const thief = await connectAndAuth(port, { name: 'Rival_Jack', clientEngine: 'unity' });

    // Owner has ৳1,200 starter + grant enough for CNG purchase (৳1,200) and cha_flask (৳60)
    const ownerSession = server.getSession(owner.authOk.playerId)!;
    ownerSession.state.money = 2000;

    // 1. Buy CNG via authoritative transaction + lock it
    owner.ws.send(JSON.stringify({ type: 'c_buy_vehicle', vehicleKind: 'cng' }));
    await waitMs(80);

    const ownedVehId = ownerSession.account.ownedVehicleIds[0];
    assert.ok(ownedVehId, 'Owned CNG must be registered to AccountStore');
    assert.equal(ownerSession.state.money, 800);

    owner.ws.send(JSON.stringify({ type: 'c_vehicle_lock', vehicleId: ownedVehId, locked: true }));
    owner.ws.send(JSON.stringify({ type: 'c_buy_item', itemId: 'cha_flask' }));
    owner.ws.send(JSON.stringify({ type: 'c_switch_protagonist', protagonistId: 'maya' }));
    owner.ws.send(JSON.stringify({ type: 'c_progression_sync', completedMissionId: 'story_01_tong' }));
    await waitMs(80);

    // 2. Rival attempts to enter Owner's locked CNG within range -> must reject
    const vehObj = server.getVehicles().find((v) => v.id === ownedVehId)!;
    const thiefSession = server.getSession(thief.authOk.playerId)!;
    thiefSession.state.transform.x = vehObj.x + 1.0;
    thiefSession.state.transform.z = vehObj.z + 1.0;
    thief.ws.send(JSON.stringify({ type: 'c_vehicle_action', action: 'enter', vehicleId: ownedVehId }));
    await waitMs(80);

    assert.equal(thiefSession.state.vehicleId, null, 'Thief must NOT enter locked owner vehicle');
    assert.ok(thief.inbox.some((m) => m.type === 's_error' && m.code === 'VEHICLE_LOCKED'));

    // 3. Disconnect owner and flush to disk, then reload fresh server instance & reconnect via token
    const savedToken = owner.authOk.token;
    owner.ws.close();
    thief.ws.close();
    await server.stop();

    const reloadedStore = new AccountStore(tmpFile);
    const server2 = new DhakaAuthoritativeServer(reloadedStore);
    await server2.listen(0);
    const port2 = server2.getPort();

    const reconnected = await connectAndAuth(port2, { name: 'IgnoredCallSign', token: savedToken, clientEngine: 'unity' });
    assert.equal(reconnected.authOk.isNewAccount, false);
    assert.equal(reconnected.authOk.player.protagonistId, 'maya');
    assert.equal(reconnected.authOk.player.money, 740); // 800 - 60 cha_flask
    assert.equal(reconnected.authOk.player.inventory.cha_flask, 3); // 2 starter + 1 purchased
    assert.ok(reconnected.authOk.player.ownedVehicleIds.includes(ownedVehId));
    assert.ok(reconnected.authOk.player.completedMissionIds.includes('story_01_tong'));

    reconnected.ws.close();
    await server2.stop();
  } finally {
    try {
      if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile);
    } catch {}
  }
});

test('unity-mp-server: Anti-cheat flags dt speed-hack floods, unowned weapons, and invalid aim angles', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const cheater = await connectAndAuth(port, { name: 'CheatBot_01', clientEngine: 'unity' });
  const target = await connectAndAuth(port, { name: 'CleanPlayer', clientEngine: 'unity' });

  try {
    // 1. dt budget speedhack flood: 15 rapid c_input packets each claiming 0.1s dt (1.5s inside 50ms -> 2 strikes)
    for (let i = 1; i <= 15; i++) {
      cheater.ws.send(JSON.stringify({
        type: 'c_input',
        seq: i,
        dt: 0.1,
        moveX: 1,
        moveZ: 0,
        heading: 0,
        sprint: true,
        jump: false,
      }));
    }

    // 2. Unowned weapon strike check ('shotgun' not unlocked yet -> Strike 3)
    cheater.ws.send(JSON.stringify({
      type: 'c_attack',
      seq: 1,
      weapon: 'shotgun',
      aimHeading: 0,
      targetId: target.authOk.playerId,
      targetKind: 'player',
    }));

    // 3. Off-cone aimbot angle attempt (>120 deg away -> Strike 4)
    const cSess = server.getSession(cheater.authOk.playerId)!;
    const tSess = server.getSession(target.authOk.playerId)!;
    cSess.state.transform.x = 0;
    cSess.state.transform.z = 0;
    tSess.state.transform.x = 0;
    tSess.state.transform.z = 12;
    cheater.ws.send(JSON.stringify({
      type: 'c_attack',
      seq: 2,
      weapon: 'pistol',
      aimHeading: Math.PI, // facing South while victim is directly North!
      targetId: target.authOk.playerId,
      targetKind: 'player',
    }));

    await waitMs(140);

    const alerts = cheater.inbox.filter((m) => m.type === 's_anticheat_alert');
    assert.ok(alerts.some((a) => a.code === 'DT_BUDGET_EXCEEDED'), 'Speed-hack dt flood must raise anti-cheat strike');
    assert.ok(alerts.some((a) => a.code === 'UNOWNED_WEAPON'), 'Locked weapon must raise anti-cheat strike');
    assert.ok(alerts.some((a) => a.code === 'AIM_MISMATCH'), 'Invalid aim cone must raise anti-cheat strike');
    assert.equal(tSess.state.hp, 100, 'Cheat attacks must deal zero damage');
    assert.ok(server.getHealthMetrics().antiCheatStrikesTotal >= 3);

    // 4. Push past 8 strikes to verify automatic disconnect enforcement
    for (let k = 0; k < 6; k++) {
      cheater.ws.send(JSON.stringify({
        type: 'c_attack',
        seq: 10 + k,
        weapon: 'shotgun',
        aimHeading: 0,
      }));
    }
    await waitMs(120);
    assert.equal(server.getSession(cheater.authOk.playerId), undefined, 'Repeated cheater must be disconnected cleanly at strike limit');
  } finally {
    cheater.ws.close();
    target.ws.close();
    await server.stop();
  }
});
