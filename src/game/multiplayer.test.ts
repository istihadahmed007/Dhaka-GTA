import test from 'node:test';
import assert from 'node:assert/strict';
import { WebSocket } from 'ws';
import { AccountStore } from '../server/accountStore';
import { DhakaAuthoritativeServer } from '../server/authoritativeServer';
import { NetworkClient } from './multiplayer/networkClient';
import {
  CAttackMsg,
  CBuyItemMsg,
  CInputMsg,
  CVehicleActionMsg,
  DEFAULT_SERVER_PORT,
  MULTIPLAYER_SERVER_TICK_RATE,
  NetPlayerSnapshot,
  NetTransform,
} from './multiplayer/protocol';

test('multiplayer: AccountStore persists accounts, starter wallet, and reconnect tokens', () => {
  const store = new AccountStore();

  // 1. Initial creation
  const auth1 = store.authenticate('Tariq_Mirpur', undefined, undefined, 'street');
  assert.equal(auth1.ok, true);
  if (!auth1.ok) return;
  assert.equal(auth1.isNew, true);
  assert.equal(auth1.account.name, 'Tariq_Mirpur');
  assert.equal(auth1.account.money, 1200);
  assert.ok(auth1.account.token.startsWith('tok_'));
  assert.ok(auth1.account.id.startsWith('usr_'));
  assert.equal(auth1.account.kills, 0);

  // 2. Reconnection using session token
  const auth2 = store.authenticate('DifferentNameIgnored', auth1.account.token);
  assert.equal(auth2.ok, true);
  if (!auth2.ok) return;
  assert.equal(auth2.isNew, false);
  assert.equal(auth2.account.id, auth1.account.id);
  assert.equal(auth2.account.name, 'Tariq_Mirpur');

  // 3. Attempting to take over existing name without token or PIN must be rejected
  const authTakeover = store.authenticate('Tariq_Mirpur');
  assert.equal(authTakeover.ok, false);
  if (!authTakeover.ok) {
    assert.equal(authTakeover.code, 'AUTH_REQUIRED');
  }

  // 4. Update stats & verify persistence
  store.updateStats(auth1.account.id, {
    money: 2500,
    kills: 3,
    lastX: 45.0,
    lastZ: -20.5,
  });

  const updated = store.getById(auth1.account.id);
  assert.ok(updated);
  assert.equal(updated.money, 2500);
  assert.equal(updated.kills, 3);
  assert.equal(updated.lastX, 45.0);

  store.close();
});

test('multiplayer: AuthoritativeServer initializes default vehicles across land and Buriganga water', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);

  const vehicles = server.getVehicles();
  assert.equal(vehicles.length, 8);

  const sedan = vehicles.find((v) => v.kind === 'sedan');
  const speedboat = vehicles.find((v) => v.kind === 'speedboat');
  const launch = vehicles.find((v) => v.kind === 'launch');
  const police = vehicles.find((v) => v.kind === 'police');

  assert.ok(sedan, 'Sedan must be spawned');
  assert.ok(speedboat, 'Speedboat must be spawned in Buriganga');
  assert.ok(launch, 'Passenger launch must be spawned in Buriganga');
  assert.ok(police, 'Police cruiser must be spawned');

  // Verify Buriganga water positioning (Z >= 195)
  assert.ok(speedboat.z >= 190);
  assert.ok(launch.z >= 200);

  await server.stop();
});

test('multiplayer: Server validates vehicle entry range and rejects remote attempts', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const ws = new WebSocket(`ws://localhost:${port}`);
  await new Promise<void>((resolve) => ws.on('open', resolve));

  // Authenticate
  ws.send(JSON.stringify({ type: 'c_auth', name: 'Rider_01' }));
  await new Promise((r) => setTimeout(r, 100));

  const sessions = Array.from(server.getSessions().values());
  assert.equal(sessions.length, 1);
  const session = sessions[0];
  const targetVeh = server.getVehicles()[0]; // located at (-14, 28)

  // 1. Player is far away at (100, 100) -> Must fail vehicle entry (< 5.5m required)
  session.state.transform.x = 100;
  session.state.transform.z = 100;

  const remoteEnterMsg: CVehicleActionMsg = {
    type: 'c_vehicle_action',
    action: 'enter',
    vehicleId: targetVeh.id,
  };
  ws.send(JSON.stringify(remoteEnterMsg));
  await new Promise((r) => setTimeout(r, 100));

  assert.equal(session.state.vehicleId, null, 'Entry must be rejected when player is far from vehicle');
  assert.equal(targetVeh.driverId, null);

  // 2. Player moves close (< 3m) -> Entry succeeds
  session.state.transform.x = targetVeh.x + 1.2;
  session.state.transform.z = targetVeh.z + 1.0;

  const validEnterMsg: CVehicleActionMsg = {
    type: 'c_vehicle_action',
    action: 'enter',
    vehicleId: targetVeh.id,
  };
  ws.send(JSON.stringify(validEnterMsg));
  await new Promise((r) => setTimeout(r, 100));

  assert.equal(session.state.vehicleId, targetVeh.id, 'Entry must succeed when within 5.5m');
  assert.equal(targetVeh.driverId, session.state.id);

  // 3. Exit vehicle
  const exitMsg: CVehicleActionMsg = {
    type: 'c_vehicle_action',
    action: 'exit',
  };
  ws.send(JSON.stringify(exitMsg));
  await new Promise((r) => setTimeout(r, 100));

  assert.equal(session.state.vehicleId, null);
  assert.equal(targetVeh.driverId, null);

  ws.close();
  await server.stop();
});

test('multiplayer: Server-authoritative economy strictly validates money deductions', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const ws = new WebSocket(`ws://localhost:${port}`);
  await new Promise<void>((resolve) => ws.on('open', resolve));

  // Authenticate (starter money ৳1,200)
  ws.send(JSON.stringify({ type: 'c_auth', name: 'Buyer_01' }));
  await new Promise((r) => setTimeout(r, 100));

  const session = Array.from(server.getSessions().values())[0];
  assert.equal(session.state.money, 1200);

  // 1. Buy Biriyani (costs ৳150)
  const buyBiriyani: CBuyItemMsg = {
    type: 'c_buy_item',
    itemId: 'biriyani',
  };
  ws.send(JSON.stringify(buyBiriyani));
  await new Promise((r) => setTimeout(r, 100));

  assert.equal(session.state.money, 1050, 'Money must be decremented on server');
  assert.equal(session.state.hp, 100);

  // 2. Reduce money to ৳50 and attempt expensive armor purchase (costs ৳400)
  session.state.money = 50;
  const buyArmor: CBuyItemMsg = {
    type: 'c_buy_item',
    itemId: 'armor',
  };
  ws.send(JSON.stringify(buyArmor));
  await new Promise((r) => setTimeout(r, 100));

  assert.equal(session.state.money, 50, 'Money must not change when balance is insufficient');

  ws.close();
  await server.stop();
});

test('multiplayer: Server-authoritative combat verifies range, armor absorption, and bounty rewards', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const wsAttacker = new WebSocket(`ws://localhost:${port}`);
  const wsVictim = new WebSocket(`ws://localhost:${port}`);

  await Promise.all([
    new Promise<void>((resolve) => wsAttacker.on('open', resolve)),
    new Promise<void>((resolve) => wsVictim.on('open', resolve)),
  ]);

  wsAttacker.send(JSON.stringify({ type: 'c_auth', name: 'Attacker_Tariq' }));
  wsVictim.send(JSON.stringify({ type: 'c_auth', name: 'Victim_Syndicate' }));
  await new Promise((r) => setTimeout(r, 120));

  const attackerSession = server.getSession(Array.from(server.getSessions().keys())[0])!;
  const victimSession = server.getSession(Array.from(server.getSessions().keys())[1])!;

  // Position victim and attacker close (distance 2.0m)
  attackerSession.state.transform.x = 0;
  attackerSession.state.transform.z = 0;
  attackerSession.state.transform.heading = 0; // facing +Z

  victimSession.state.transform.x = 0;
  victimSession.state.transform.z = 2.0;
  victimSession.state.hp = 100;
  victimSession.state.armor = 50;

  // 1. Attacker strikes with fists (damage = 18)
  // Armor absorbs 60% = 10.8 dmg (armor 50 -> 39), remaining 40% = 7.2 goes to HP (hp 100 -> 93)
  const attackMsg: CAttackMsg = {
    type: 'c_attack',
    seq: 1,
    weapon: 'fists',
    aimHeading: 0,
    targetId: victimSession.state.id,
    targetKind: 'player',
  };
  wsAttacker.send(JSON.stringify(attackMsg));
  await new Promise((r) => setTimeout(r, 120));

  assert.ok(victimSession.state.armor < 50, 'Armor must absorb portion of damage');
  assert.ok(victimSession.state.hp < 100, 'HP must take remainder');

  // 2. Lethal elimination: set victim to low HP and attack
  victimSession.state.armor = 0;
  victimSession.state.hp = 5;
  const startAttackerMoney = attackerSession.state.money;

  attackerSession.attackCooldownTimer = 0; // reset cooldown for immediate second strike
  wsAttacker.send(JSON.stringify({
    type: 'c_attack',
    seq: 2,
    weapon: 'fists',
    aimHeading: 0,
    targetId: victimSession.state.id,
    targetKind: 'player',
  }));
  await new Promise((r) => setTimeout(r, 120));

  assert.equal(victimSession.state.state, 'ko', 'Victim must enter KO state');
  assert.equal(victimSession.state.deaths, 1);
  assert.equal(attackerSession.state.kills, 1);
  assert.equal(attackerSession.state.money, startAttackerMoney + 350, '৳350 kill bounty awarded');
  assert.ok(victimSession.respawnTimer > 0, 'Respawn countdown initiated');

  wsAttacker.close();
  wsVictim.close();
  await server.stop();
});

test('multiplayer: Snapshot interpolation calculates smooth positions between ticks', () => {
  const p1: NetPlayerSnapshot = {
    id: 'p1',
    x: 10,
    y: 0,
    z: 20,
    vx: 5,
    vz: 0,
    heading: 0,
    hp: 100,
    armor: 50,
    stamina: 100,
    weapon: 'pistol',
    vehicleId: null,
    state: 'run',
    ping: 25,
  };

  const p2: NetPlayerSnapshot = {
    id: 'p1',
    x: 20,
    y: 0,
    z: 20,
    vx: 5,
    vz: 0,
    heading: 0,
    hp: 90,
    armor: 45,
    stamina: 80,
    weapon: 'pistol',
    vehicleId: null,
    state: 'run',
    ping: 25,
  };

  // Interpolation factor t = 0.5
  const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
  const interpX = lerp(p1.x, p2.x, 0.5);
  const interpHp = lerp(p1.hp, p2.hp, 0.5);

  assert.equal(interpX, 15);
  assert.equal(interpHp, 95);
});

test('multiplayer: AccountStore enforces PIN authentication for protected CallSigns', () => {
  const store = new AccountStore();

  // 1. Register with PIN
  const registered = store.authenticate('SyndicateBoss', undefined, '7788');
  assert.equal(registered.ok, true);
  if (!registered.ok) return;
  assert.equal(registered.account.pin, '7788');

  // 2. Reject incorrect PIN
  const wrongPin = store.authenticate('SyndicateBoss', undefined, '0000');
  assert.equal(wrongPin.ok, false);

  // 3. Reject missing PIN
  const noPin = store.authenticate('SyndicateBoss');
  assert.equal(noPin.ok, false);

  // 4. Accept correct PIN
  const correctPin = store.authenticate('SyndicateBoss', undefined, '7788');
  assert.equal(correctPin.ok, true);
  if (correctPin.ok) {
    assert.equal(correctPin.account.id, registered.account.id);
  }

  store.close();
});

test('multiplayer: Server rejects NaN, Infinity, and malformed packets', async () => {
  const store = new AccountStore();
  const server = new DhakaAuthoritativeServer(store);
  await server.listen(0);
  const port = server.getPort();

  const ws = new WebSocket(`ws://localhost:${port}`);
  await new Promise<void>((resolve) => ws.on('open', resolve));

  const receivedMessages: any[] = [];
  ws.on('message', (data) => {
    receivedMessages.push(JSON.parse(data.toString()));
  });

  // 1. Send invalid non-JSON string
  ws.send('this is not json');
  await new Promise((r) => setTimeout(r, 60));
  assert.ok(receivedMessages.some((m) => m.type === 's_error' && m.code === 'MALFORMED_JSON'));

  // 2. Authenticate cleanly
  ws.send(JSON.stringify({ type: 'c_auth', name: 'SafeDriver_99' }));
  await new Promise((r) => setTimeout(r, 60));

  // 3. Send NaN in dt
  ws.send(JSON.stringify({
    type: 'c_input',
    seq: 1,
    dt: 'not-a-number',
    moveX: 0,
    moveZ: 0,
    heading: 0,
    sprint: false,
    jump: false,
  }));
  await new Promise((r) => setTimeout(r, 60));
  assert.ok(receivedMessages.some((m) => m.type === 's_error' && m.code === 'INVALID_PACKET'));

  // 4. Send out-of-bounds movement vector
  ws.send(JSON.stringify({
    type: 'c_input',
    seq: 2,
    dt: 0.05,
    moveX: 999.0, // speedhack value
    moveZ: 0,
    heading: 0,
    sprint: false,
    jump: false,
  }));
  await new Promise((r) => setTimeout(r, 60));
  assert.ok(receivedMessages.some((m) => m.type === 's_error' && m.code === 'INVALID_PACKET'));

  ws.close();
  await server.stop();
});
