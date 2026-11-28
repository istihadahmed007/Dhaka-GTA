using System;
using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Environment;
using DhakaGta.GIS;
using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Complete reusable open-world mission & hustle orchestrator supporting:
    /// - 6 Connected Tariq & Maya Story Missions (using real GIS coordinates + bilingual dialogue),
    /// - Side Quests, CNG/Taxi Fares, Timed Courier Deliveries, Vehicle Theft Contracts, and Street Races,
    /// - Objective tracking, NPC contact hooks, enemy spawning, wanted evasion, timers, failure states, and rewards.
    /// </summary>
    public sealed class DhakaMissionEngine : MonoBehaviour
    {
        [SerializeField] private MissionDefinitionSO[] missionCatalog = Array.Empty<MissionDefinitionSO>();
        [SerializeField] private ProtagonistController protagonistController;
        [SerializeField] private ProtagonistSwitcher protagonistSwitcher;
        [SerializeField] private ProtagonistInteractionScanner interactionScanner;
        [SerializeField] private HealthComponent playerHealth;
        [SerializeField] private PlayerEconomyAndInventory economy;
        [SerializeField] private DhakaGarageManager garageManager;
        [SerializeField] private WorldCoordinateManager coordinateManager;
        [SerializeField] private DhakaSaveSystem saveSystem;

        private readonly HashSet<string> _completedMissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MissionTargetActor> _spawnedTargets = new List<MissionTargetActor>();
        private readonly List<GameObject> _stepHelperObjects = new List<GameObject>();

        private MissionDefinitionSO _activeMission;
        private int _currentStepIndex;
        private int _stepEliminations;
        private float _stepTimerRemaining = -1f;
        private bool _evadeHadWanted;
        private StreetRaceController _raceController;
        private MissionContactNpc _activeStepNpc;

        public IReadOnlyList<MissionDefinitionSO> Catalog => missionCatalog;
        public IReadOnlyCollection<string> CompletedMissionIds => _completedMissions;
        public MissionDefinitionSO ActiveMission => _activeMission;
        public int CurrentStepIndex => _currentStepIndex;
        public bool IsMissionActive => _activeMission != null;
        public MissionProgressSnapshot CurrentSnapshot { get; private set; }

        public void BindSystems(
            MissionDefinitionSO[] missions,
            ProtagonistController protagonist,
            ProtagonistSwitcher switcher,
            ProtagonistInteractionScanner scanner,
            HealthComponent health,
            PlayerEconomyAndInventory econ,
            DhakaGarageManager garage,
            WorldCoordinateManager coordMgr,
            DhakaSaveSystem saver)
        {
            missionCatalog = missions ?? Array.Empty<MissionDefinitionSO>();
            protagonistController = protagonist;
            protagonistSwitcher = switcher;
            interactionScanner = scanner;
            playerHealth = health;
            economy = econ;
            garageManager = garage;
            coordinateManager = coordMgr;
            saveSystem = saver;
        }

        public bool IsMissionUnlocked(MissionDefinitionSO m)
        {
            if (m == null) return false;
            if (m.prerequisiteMissionIds == null || m.prerequisiteMissionIds.Length == 0) return true;
            for (int i = 0; i < m.prerequisiteMissionIds.Length; i++)
            {
                string req = m.prerequisiteMissionIds[i];
                if (!string.IsNullOrEmpty(req) && !_completedMissions.Contains(req)) return false;
            }
            return true;
        }

        public bool StartMission(string missionId)
        {
            MissionDefinitionSO found = null;
            for (int i = 0; i < missionCatalog.Length; i++)
            {
                if (missionCatalog[i] != null && missionCatalog[i].missionId == missionId)
                {
                    found = missionCatalog[i];
                    break;
                }
            }
            return found != null && StartMissionAsset(found);
        }

        public bool StartMissionAsset(MissionDefinitionSO mission)
        {
            if (mission == null) return false;
            CleanupStepEntities();

            _activeMission = mission;
            _currentStepIndex = 0;
            InitializeCurrentStep();
            return true;
        }

        // ------------------------ DYNAMIC SIDE JOBS (PLAYABLE HUSTLES) ------------------------

        public bool StartDynamicCngOrTaxiFare(Vector3 playerPos)
        {
            Vector3 dropoff = playerPos + new Vector3(48f, 0f, -64f);
            var job = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            job.missionId = $"fare_{Time.frameCount}";
            job.title = "Gulshan-Banani CNG Meter Fare";
            job.banglaTitle = "সিএনজি ভাড়া ট্রিপ";
            job.category = MissionCategory.TaxiCngFare;
            job.rewardBdt = 850;
            job.relationshipBondXp = 5;
            job.failOnTimerExpired = true;
            job.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "fare_pickup",
                    titleEnglish = "Board CNG/Sedan & Pick Up Commuter",
                    titleBangla = "যাত্রী তুলে নিন",
                    objectivePrompt = "Drive a CNG or Sedan to the passenger curb stop",
                    objectiveType = MissionObjectiveType.PassengerFareDropoff,
                    fallbackLocalPosition = playerPos + new Vector3(16f, 0f, 12f),
                    triggerRadiusMeters = 8.5f,
                    timeLimitSeconds = 90f,
                    dialogueSpeaker = "Dhaka Commuter",
                    dialogueBangla = "মামা, দ্রুত গুলশান সার্কেল ১-এ চলেন!",
                    dialogueEnglish = "Brother, hurry to Gulshan Circle 1!"
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "fare_dropoff",
                    titleEnglish = "Drop Passenger at Destination",
                    titleBangla = "গন্তব্যে নামিয়ে দিন",
                    objectivePrompt = "Deliver commuter smoothly to destination marker",
                    objectiveType = MissionObjectiveType.PassengerFareDropoff,
                    fallbackLocalPosition = dropoff,
                    triggerRadiusMeters = 9.0f,
                    timeLimitSeconds = 110f
                }
            };
            return StartMissionAsset(job);
        }

        public bool StartDynamicDeliveryJob(Vector3 originPos)
        {
            var job = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            job.missionId = $"delivery_{Time.frameCount}";
            job.title = "Dhaka Express Tiffin & Parcel Run";
            job.banglaTitle = "জরুরি পার্সেল ডেলিভারি";
            job.category = MissionCategory.CourierDelivery;
            job.rewardBdt = 2100;
            job.relationshipBondXp = 8;
            job.failOnTimerExpired = true;
            job.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "del_pickup",
                    titleEnglish = "Collect Sealed Package at Tea Stall",
                    titleBangla = "চা স্টল থেকে পার্সেল নিন",
                    objectivePrompt = "Grab the urgent parcel before courier timer runs out",
                    objectiveType = MissionObjectiveType.DeliverPackageTimed,
                    fallbackLocalPosition = originPos + new Vector3(-22f, 0f, 18f),
                    triggerRadiusMeters = 7.0f,
                    timeLimitSeconds = 60f,
                    dialogueSpeaker = "Maya (Radio)",
                    dialogueBangla = "তারেক, এই পার্সেলটা বনানী চেকপোস্টের আগে পৌঁছাতে হবে!",
                    dialogueEnglish = "Tariq, get that parcel dropped before Banani checkpoint locks down!"
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "del_drop",
                    titleEnglish = "Drop Off at Hatirjheel Promenade",
                    titleBangla = "হাতিরঝিলে ডেলিভারি দিন",
                    objectivePrompt = "Reach Hatirjheel delivery zone before timer expires",
                    objectiveType = MissionObjectiveType.DeliverPackageTimed,
                    fallbackLocalPosition = originPos + new Vector3(68f, 0f, -52f),
                    triggerRadiusMeters = 8.5f,
                    timeLimitSeconds = 75f
                }
            };
            return StartMissionAsset(job);
        }

        public bool StartDynamicVehicleContractJob(Vector3 garagePos)
        {
            var job = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            job.missionId = $"contract_{Time.frameCount}";
            job.title = "Ostagar Priority Sedan Boost";
            job.banglaTitle = "ওস্তাগার গাড়ির কন্ট্রাক্ট";
            job.category = MissionCategory.VehicleContract;
            job.rewardBdt = 5500;
            job.wantedRiskStars = 1;
            job.failOnTimerExpired = true;
            job.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "contract_steal_deliver",
                    titleEnglish = "Acquire Vehicle & Deliver to Garage",
                    titleBangla = "গাড়ি সংগ্রহ করে গ্যারেজে আনুন",
                    objectivePrompt = "Enter any Sedan/SUV [F] and drive it to the Gulshan Garage Bay",
                    objectiveType = MissionObjectiveType.StealAndDeliverVehicle,
                    requiredVehicleId = "sedan",
                    fallbackLocalPosition = garagePos + new Vector3(36f, 0f, -24f),
                    triggerRadiusMeters = 10f,
                    timeLimitSeconds = 120f,
                    dialogueSpeaker = "Garage Ostagar",
                    dialogueBangla = "বস, গুলশানে একটা ফ্রেশ গাড়ি আছে—অক্ষত অবস্থায় গ্যারেজে এনে দাও!",
                    dialogueEnglish = "Boss, fresh ride spotted in Gulshan—deliver it intact to our bay!"
                }
            };
            return StartMissionAsset(job);
        }

        public bool StartDynamicStreetRace(Vector3 startPos)
        {
            var job = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            job.missionId = $"race_{Time.frameCount}";
            job.title = "Hatirjheel Midnight Dash";
            job.banglaTitle = "হাতিরঝিল স্ট্রিট রেস";
            job.category = MissionCategory.StreetRace;
            job.rewardBdt = 6500;
            job.relationshipBondXp = 15;
            job.failOnTimerExpired = true;
            job.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "race_run",
                    titleEnglish = "Clear All Hatirjheel Checkpoints",
                    titleBangla = "সব চেকপয়েন্ট অতিক্রম করুন",
                    objectivePrompt = "Drive through all 4 midnight street race checkpoints cleanly",
                    objectiveType = MissionObjectiveType.CompleteRaceCheckpoints,
                    timeLimitSeconds = 140f,
                    raceCheckpointsLocal = new[]
                    {
                        startPos + new Vector3(0f, 0f, 32f),
                        startPos + new Vector3(48f, 0f, 52f),
                        startPos + new Vector3(72f, 0f, -12f),
                        startPos + new Vector3(24f, 0f, -40f)
                    },
                    dialogueSpeaker = "Maya Rahman",
                    dialogueBangla = "ইঞ্জিন গরম করো—সবার আগে শেষ চেকপয়েন্টে পৌঁছাতে হবে!",
                    dialogueEnglish = "Warm up that engine—hit every checkpoint before the Syndicate runners!"
                }
            };
            return StartMissionAsset(job);
        }

        // ------------------------ LIFECYCLE, OBJECTIVES & FAILURES ------------------------

        public void NotifyNpcInteracted(string npcId)
        {
            if (_activeMission == null) return;
            MissionObjectiveStepSpec step = GetCurrentStepSpec();
            if (step != null && step.objectiveType == MissionObjectiveType.InteractWithNpc)
            {
                AdvanceOrCompleteStep();
            }
        }

        public void RestoreProgressionState(List<string> completedIds, string savedActiveMissionId, int stepIndex)
        {
            _completedMissions.Clear();
            if (completedIds != null)
            {
                foreach (string id in completedIds) _completedMissions.Add(id);
            }

            if (!string.IsNullOrEmpty(savedActiveMissionId) && StartMission(savedActiveMissionId))
            {
                int total = _activeMission.structuredSteps != null ? _activeMission.structuredSteps.Length : 0;
                if (stepIndex > 0 && stepIndex < total)
                {
                    CleanupStepEntities();
                    _currentStepIndex = stepIndex;
                    InitializeCurrentStep();
                }
            }
        }

        private void Update()
        {
            if (_activeMission == null || GameBootstrap.CurrentState == GameState.Paused) return;
            StepActiveMission(Time.deltaTime);
        }

        public void StepActiveMission(float dt)
        {
            if (_activeMission == null) return;

            // Failure Condition 1: Protagonist incapacitated
            if (_activeMission.failOnPlayerDeath && playerHealth != null && !playerHealth.IsAlive)
            {
                FailActiveMission("Protagonist incapacitated");
                return;
            }

            MissionObjectiveStepSpec step = GetCurrentStepSpec();
            if (step == null)
            {
                CompleteActiveMission();
                return;
            }

            // Failure Condition 2: Countdown timer
            if (step.timeLimitSeconds > 0f && _stepTimerRemaining > 0f)
            {
                _stepTimerRemaining -= Mathf.Max(0f, dt);
                if (_stepTimerRemaining <= 0f && _activeMission.failOnTimerExpired)
                {
                    FailActiveMission("Mission timer expired (সময় শেষ)");
                    return;
                }
            }

            Vector3 activeSubjectPos = ResolvePlayerSubjectPosition();
            Vector3 targetWorldPos = ResolveStepWorldPosition(step);

            bool stepCompleted = false;
            int progCount = 0;
            int reqCount = 1;

            switch (step.objectiveType)
            {
                case MissionObjectiveType.GoToLocation:
                case MissionObjectiveType.PassengerFareDropoff:
                case MissionObjectiveType.DeliverPackageTimed:
                    if (Vector3.Distance(activeSubjectPos, targetWorldPos) <= Mathf.Max(3f, step.triggerRadiusMeters))
                    {
                        stepCompleted = true;
                    }
                    break;

                case MissionObjectiveType.StealAndDeliverVehicle:
                    bool inValidVeh = IsPlayerInTargetVehicle(step.requiredVehicleId);
                    if (inValidVeh && Vector3.Distance(activeSubjectPos, targetWorldPos) <= Mathf.Max(4f, step.triggerRadiusMeters))
                    {
                        stepCompleted = true;
                    }
                    break;

                case MissionObjectiveType.InteractWithNpc:
                    // Also allow proximity fallback if player runs right beside contact NPC after trigger
                    break;

                case MissionObjectiveType.EliminateTargets:
                    progCount = _stepEliminations;
                    reqCount = Mathf.Max(1, step.requiredEliminations);
                    if (_stepEliminations >= reqCount)
                    {
                        stepCompleted = true;
                    }
                    break;

                case MissionObjectiveType.CompleteRaceCheckpoints:
                    if (_raceController != null)
                    {
                        progCount = _raceController.CurrentCheckpointIndex;
                        reqCount = Mathf.Max(1, _raceController.TotalCheckpoints);
                        if (_raceController.EvaluateDriverPosition(activeSubjectPos))
                        {
                            stepCompleted = true;
                        }
                        targetWorldPos = _raceController.CurrentCheckpointPosition;
                    }
                    break;

                case MissionObjectiveType.EvadePoliceWanted:
                    if (CityDangerBus.CurrentWantedLevel > 0) _evadeHadWanted = true;
                    if (_evadeHadWanted && CityDangerBus.CurrentWantedLevel == 0)
                    {
                        stepCompleted = true;
                    }
                    break;

                case MissionObjectiveType.SwitchProtagonistStep:
                    string currentId = protagonistController != null && protagonistController.Profile != null
                        ? protagonistController.Profile.protagonistId
                        : "";
                    if (string.IsNullOrEmpty(step.requiredProtagonistId) ||
                        string.Equals(currentId, step.requiredProtagonistId, StringComparison.OrdinalIgnoreCase))
                    {
                        stepCompleted = true;
                    }
                    break;
            }

            PublishSnapshot(step, progCount, reqCount, targetWorldPos, isDone: false, isFailed: false);

            if (stepCompleted)
            {
                AdvanceOrCompleteStep();
            }
        }

        private void AdvanceOrCompleteStep()
        {
            CleanupStepEntities();
            _currentStepIndex++;
            int count = _activeMission.structuredSteps != null ? _activeMission.structuredSteps.Length : 0;
            if (_currentStepIndex >= count)
            {
                CompleteActiveMission();
            }
            else
            {
                InitializeCurrentStep();
            }
        }

        private void InitializeCurrentStep()
        {
            MissionObjectiveStepSpec step = GetCurrentStepSpec();
            if (step == null)
            {
                CompleteActiveMission();
                return;
            }

            _stepEliminations = 0;
            _evadeHadWanted = false;
            _stepTimerRemaining = step.timeLimitSeconds > 0f ? step.timeLimitSeconds : -1f;

            // Optional forced switch notification (or auto-prompt)
            if (!string.IsNullOrEmpty(step.dialogueEnglish) || !string.IsNullOrEmpty(step.dialogueBangla))
            {
                MissionObjectiveEvents.RaiseDialogue(new BilingualDialogueLine
                {
                    Speaker = string.IsNullOrEmpty(step.dialogueSpeaker) ? "Dhaka Comms" : step.dialogueSpeaker,
                    TextBangla = step.dialogueBangla,
                    TextEnglish = step.dialogueEnglish,
                    DisplaySeconds = 5.5f
                });
            }

            Vector3 targetPos = ResolveStepWorldPosition(step);

            if (step.objectiveType == MissionObjectiveType.EliminateTargets)
            {
                int count = Mathf.Max(1, step.requiredEliminations);
                Transform playerTf = protagonistController != null ? protagonistController.transform : transform;
                for (int i = 0; i < count; i++)
                {
                    float ang = (i / (float)count) * Mathf.PI * 2f;
                    Vector3 spawnPos = targetPos + new Vector3(Mathf.Cos(ang) * 4.5f, 0.1f, Mathf.Sin(ang) * 4.5f);
                    var host = new GameObject($"SyndicateTarget_{i + 1}");
                    host.transform.position = spawnPos;
                    var actor = host.AddComponent<MissionTargetActor>();
                    actor.InitializeTarget(playerTf, hp: 85f, seed: 800 + i);
                    actor.OnEliminated += HandleTargetEliminated;
                    _spawnedTargets.Add(actor);
                }
            }
            else if (step.objectiveType == MissionObjectiveType.InteractWithNpc)
            {
                var npcGo = new GameObject($"MissionNpc_{step.stepId}");
                npcGo.transform.position = targetPos;
                _activeStepNpc = npcGo.AddComponent<MissionContactNpc>();
                _activeStepNpc.ConfigureContact(
                    step.stepId,
                    string.IsNullOrEmpty(step.dialogueSpeaker) ? "Syndicate Informant" : step.dialogueSpeaker,
                    PedestrianWardrobeArchetype.PanjabiAndPajama,
                    new[] { new BilingualDialogueLine { Speaker = step.dialogueSpeaker, TextBangla = step.dialogueBangla, TextEnglish = step.dialogueEnglish, DisplaySeconds = 5f } },
                    this);
                _stepHelperObjects.Add(npcGo);
            }
            else if (step.objectiveType == MissionObjectiveType.CompleteRaceCheckpoints)
            {
                var raceGo = new GameObject("ActiveStreetRaceController");
                _raceController = raceGo.AddComponent<StreetRaceController>();
                Vector3[] cps = step.raceCheckpointsLocal != null && step.raceCheckpointsLocal.Length > 0
                    ? step.raceCheckpointsLocal
                    : new[] { targetPos };
                _raceController.BeginRace(cps, 9.0f);
                _stepHelperObjects.Add(raceGo);
            }
            else if (step.objectiveType == MissionObjectiveType.EvadePoliceWanted)
            {
                int stars = Mathf.Max(1, _activeMission.wantedRiskStars);
                if (CityDangerBus.CurrentWantedLevel < stars)
                {
                    CityDangerBus.AddWantedHeat(stars, targetPos);
                }
                _evadeHadWanted = true;
            }
            else
            {
                SpawnLocationGoalBeacon(targetPos, step.triggerRadiusMeters);
            }

            PublishSnapshot(step, 0, Mathf.Max(1, step.requiredEliminations), targetPos, isDone: false, isFailed: false);
        }

        private void HandleTargetEliminated(MissionTargetActor actor)
        {
            _stepEliminations++;
            _spawnedTargets.Remove(actor);
        }

        private void CompleteActiveMission()
        {
            MissionDefinitionSO finished = _activeMission;
            CleanupStepEntities();
            _activeMission = null;
            _currentStepIndex = 0;

            if (finished != null)
            {
                if (finished.category == MissionCategory.Story || finished.category == MissionCategory.SideQuest)
                {
                    _completedMissions.Add(finished.missionId);
                }

                if (economy != null)
                {
                    economy.AddBdt(finished.rewardBdt, finished.title);
                    economy.AddRelationshipBondXp(finished.relationshipBondXp);
                    economy.GrantWeaponUnlock(finished.unlockWeaponId);
                    economy.GrantSafehouseUnlock(finished.unlockSafehouseId);
                    economy.GrantOutfitUnlock(finished.unlockOutfitId);

                    switch (finished.category)
                    {
                        case MissionCategory.TaxiCngFare: economy.FaresCompletedCount++; break;
                        case MissionCategory.CourierDelivery: economy.DeliveriesCompletedCount++; break;
                        case MissionCategory.VehicleContract: economy.ContractsCompletedCount++; break;
                        case MissionCategory.StreetRace: economy.RacesWonCount++; break;
                    }
                }

                CurrentSnapshot = new MissionProgressSnapshot
                {
                    MissionId = finished.missionId,
                    TitleEnglish = finished.title,
                    TitleBangla = finished.banglaTitle,
                    Category = finished.category,
                    IsCompleted = true
                };

                MissionObjectiveEvents.RaiseMissionUpdated(CurrentSnapshot);
                MissionObjectiveEvents.RaiseMissionCompleted(finished);
                saveSystem?.SaveToDisk();
            }
        }

        public void FailActiveMission(string reason)
        {
            MissionDefinitionSO failed = _activeMission;
            CleanupStepEntities();
            _activeMission = null;
            _currentStepIndex = 0;

            CurrentSnapshot = new MissionProgressSnapshot
            {
                MissionId = failed != null ? failed.missionId : string.Empty,
                TitleEnglish = failed != null ? failed.title : "Mission",
                IsFailed = true,
                StepPrompt = reason
            };

            MissionObjectiveEvents.RaiseMissionUpdated(CurrentSnapshot);
            MissionObjectiveEvents.RaiseMissionFailed(failed, reason);
        }

        private void CleanupStepEntities()
        {
            for (int i = _spawnedTargets.Count - 1; i >= 0; i--)
            {
                if (_spawnedTargets[i] != null)
                {
                    _spawnedTargets[i].OnEliminated -= HandleTargetEliminated;
                    Destroy(_spawnedTargets[i].gameObject);
                }
            }
            _spawnedTargets.Clear();

            for (int i = _stepHelperObjects.Count - 1; i >= 0; i--)
            {
                if (_stepHelperObjects[i] != null) Destroy(_stepHelperObjects[i]);
            }
            _stepHelperObjects.Clear();
            _raceController = null;
            _activeStepNpc = null;
        }

        private MissionObjectiveStepSpec GetCurrentStepSpec()
        {
            if (_activeMission?.structuredSteps != null &&
                _currentStepIndex >= 0 &&
                _currentStepIndex < _activeMission.structuredSteps.Length)
            {
                return _activeMission.structuredSteps[_currentStepIndex];
            }
            return null;
        }

        private Vector3 ResolveStepWorldPosition(MissionObjectiveStepSpec step)
        {
            if (step == null) return Vector3.zero;
            if (Mathf.Abs((float)step.targetLatitude) > 0.01f && Mathf.Abs((float)step.targetLongitude) > 0.01f)
            {
                if (coordinateManager != null)
                {
                    UtmCoord u = GeographicCoordinateConverter.LatLonToUtm(step.targetLatitude, step.targetLongitude);
                    return coordinateManager.UtmToUnityPosition(u, 0.2f);
                }
                Vector2 flat = DhakaGeoProjection.LatLonToMetersXZ(step.targetLatitude, step.targetLongitude);
                return new Vector3(flat.x, 0.2f, flat.y);
            }
            return step.fallbackLocalPosition;
        }

        private Vector3 ResolvePlayerSubjectPosition()
        {
            if (interactionScanner != null) return interactionScanner.ActiveSubjectPosition;
            return protagonistController != null ? protagonistController.transform.position : transform.position;
        }

        private bool IsPlayerInTargetVehicle(string requiredVehicleId)
        {
            if (interactionScanner == null || interactionScanner.ActiveVehicleBehaviour == null) return false;
            if (string.IsNullOrWhiteSpace(requiredVehicleId)) return true;

            if (interactionScanner.ActiveVehicleBehaviour is DhakaVehicleController veh && veh.Spec != null)
            {
                return string.Equals(veh.Spec.vehicleId, requiredVehicleId, StringComparison.OrdinalIgnoreCase) ||
                       veh.Spec.vehicleClass.ToString().IndexOf(requiredVehicleId, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return true;
        }

        private void SpawnLocationGoalBeacon(Vector3 targetPos, float radiusMeters)
        {
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "MissionGoalBeacon";
            var col = beacon.GetComponent<Collider>();
            if (col != null) Destroy(col);
            beacon.transform.position = targetPos + Vector3.up * 0.12f;
            beacon.transform.localScale = new Vector3(radiusMeters * 0.65f, 0.14f, radiusMeters * 0.65f);

            var mr = beacon.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                Color gold = new Color(1.0f, 0.78f, 0.14f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", gold * 2.4f);
                mr.sharedMaterial = mat;
            }
            _stepHelperObjects.Add(beacon);
        }

        private void PublishSnapshot(
            MissionObjectiveStepSpec step,
            int progressCount,
            int requiredCount,
            Vector3 targetPos,
            bool isDone,
            bool isFailed)
        {
            CurrentSnapshot = new MissionProgressSnapshot
            {
                MissionId = _activeMission.missionId,
                TitleEnglish = _activeMission.title,
                TitleBangla = _activeMission.banglaTitle,
                Category = _activeMission.category,
                CurrentStepIndex = _currentStepIndex,
                TotalSteps = _activeMission.structuredSteps != null ? _activeMission.structuredSteps.Length : 1,
                StepPrompt = !string.IsNullOrEmpty(step.objectivePrompt) ? step.objectivePrompt : step.titleEnglish,
                ProgressCount = progressCount,
                RequiredCount = requiredCount,
                RemainingTimerSeconds = _stepTimerRemaining,
                HasTargetPoint = true,
                TargetWorldPosition = targetPos,
                IsCompleted = isDone,
                IsFailed = isFailed
            };
            MissionObjectiveEvents.RaiseMissionUpdated(CurrentSnapshot);
        }
    }
}
