import test from 'node:test';
import assert from 'node:assert/strict';
import {
  TRAFFIC_INTERSECTIONS,
  getIntersectionPhase,
  isVehicleStoppedByTrafficLight,
  DHAKA_BUS_STOPS,
  isNearBusStop,
  detectForwardObstacles,
  updateLaneAwareTrafficVehicle,
  spawnTrafficVehicle,
  createTrafficLightVisuals,
} from './trafficSystem';
import { VehicleState, VEHICLE_SPECS } from './types';
import { TRAFFIC_ROUTES } from './worldData';

test('trafficSystem: 12 intersections have valid cycle timings and phased offsets', () => {
  assert.strictEqual(TRAFFIC_INTERSECTIONS.length, 12);
  for (const inter of TRAFFIC_INTERSECTIONS) {
    assert.ok(inter.id.length > 0);
    assert.ok(inter.banglaName.length > 0);
    assert.ok(inter.radius >= 12);
    assert.strictEqual(
      inter.nsGreenDuration + inter.nsYellowDuration + inter.ewGreenDuration + inter.ewYellowDuration,
      inter.cycleTime,
    );
  }
});

test('trafficSystem: getIntersectionPhase cycles smoothly through 4 phases', () => {
  const inter = TRAFFIC_INTERSECTIONS[0]; // Shapla: 11s NS_GREEN, 3s NS_YELLOW, 11s EW_GREEN, 3s EW_YELLOW
  assert.strictEqual(getIntersectionPhase(inter, 0), 'NS_GREEN');
  assert.strictEqual(getIntersectionPhase(inter, 5), 'NS_GREEN');
  assert.strictEqual(getIntersectionPhase(inter, 11.5), 'NS_YELLOW');
  assert.strictEqual(getIntersectionPhase(inter, 14.5), 'EW_GREEN');
  assert.strictEqual(getIntersectionPhase(inter, 25.5), 'EW_YELLOW');
  assert.strictEqual(getIntersectionPhase(inter, 28), 'NS_GREEN'); // wrapped around
});

test('trafficSystem: isVehicleStoppedByTrafficLight stops approaching vehicles on red and lets through on green', () => {
  const inter = TRAFFIC_INTERSECTIONS[0]; // Center at (0, 0)
  // Vehicle traveling Northbound towards center: starts at z = 18, heading = Math.PI (towards -Z)
  const vehNorthbound: VehicleState = {
    id: 'test_veh_1',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 16,
    heading: Math.PI,
    speed: 8,
    steerAngle: 0,
    hp: 100,
    maxHp: 100,
    color: 0xffffff,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  // At time 0: phase is NS_GREEN -> vehicle is NOT stopped
  assert.strictEqual(isVehicleStoppedByTrafficLight(vehNorthbound, inter, 0), false);

  // At time 15: phase is EW_GREEN (NS is RED) -> Northbound vehicle MUST stop
  assert.strictEqual(isVehicleStoppedByTrafficLight(vehNorthbound, inter, 15), true);

  // Vehicle already past intersection (z = -16, heading = Math.PI) is NOT stopped
  const vehDeparting: VehicleState = {
    ...vehNorthbound,
    z: -16,
  };
  assert.strictEqual(isVehicleStoppedByTrafficLight(vehDeparting, inter, 15), false);
});

test('trafficSystem: bus stops detect city buses within boarding range', () => {
  assert.ok(DHAKA_BUS_STOPS.length >= 6);
  const stop = DHAKA_BUS_STOPS[0];

  const nearBus: VehicleState = {
    id: 'test_bus_1',
    kind: 'bus',
    x: stop.x + 2,
    y: 0,
    z: stop.z + 1,
    heading: 0,
    speed: 0,
    steerAngle: 0,
    hp: 300,
    maxHp: 300,
    color: 0xb81d24,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };
  assert.strictEqual(isNearBusStop(nearBus), true);

  const farBus: VehicleState = {
    ...nearBus,
    x: 350,
    z: 350,
  };
  assert.strictEqual(isNearBusStop(farBus), false);
});

test('trafficSystem: detectForwardObstacles detects lead vehicles and crossing pedestrians', () => {
  const host: VehicleState = {
    id: 'host',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 0,
    heading: 0, // facing +Z
    speed: 10,
    steerAngle: 0,
    hp: 100,
    maxHp: 100,
    color: 0xffffff,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  const leadAhead: VehicleState = {
    id: 'lead',
    kind: 'rickshaw',
    x: 0,
    y: 0,
    z: 6,
    heading: 0,
    speed: 3,
    steerAngle: 0,
    hp: 60,
    maxHp: 60,
    color: 0xc62828,
    driver: 'traffic',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  const detection = detectForwardObstacles(host, [host, leadAhead], 999, 999, null);
  assert.strictEqual(detection.blocked, true);
  assert.ok(detection.leadDistance < 7);
  assert.strictEqual(detection.isLeadSlow, true);

  // Crossing pedestrian directly ahead yields blocked
  const pedCrossing = [{ x: 0.5, z: 5.0, isCrossing: true }];
  const detectionPed = detectForwardObstacles(host, [host], 999, 999, null, pedCrossing);
  assert.strictEqual(detectionPed.pedestrianAhead, true);
  assert.strictEqual(detectionPed.blocked, true);
});

test('trafficSystem: lane-aware traffic drives on left and slow rickshaws hug the shoulder', () => {
  const rickshaw = spawnTrafficVehicle('test_rick', 'rickshaw', 0, 0, 'traffic');
  assert.strictEqual(rickshaw.kind, 'rickshaw');
  assert.strictEqual(rickshaw.laneOffset, -1.0); // Far left shoulder

  const sedan = spawnTrafficVehicle('test_sedan', 'sedan', 0, 0, 'traffic');
  assert.strictEqual(sedan.laneOffset, 0.0); // Center travel lane

  // Simulate one step of lane-aware traffic
  const prevSpeed = sedan.speed;
  updateLaneAwareTrafficVehicle(sedan, 0.05, [sedan], 999, 999, null, 0, []);
  assert.ok(sedan.speed > 0);
  assert.ok(Math.abs(sedan.x) < 500);
});

test('trafficSystem: createTrafficLightVisuals generates 3D visual groups and updates without throwing', () => {
  const handles = createTrafficLightVisuals();
  assert.ok(handles.group.children.length > 0);
  assert.doesNotThrow(() => handles.update(10));
  assert.doesNotThrow(() => handles.dispose());
});
