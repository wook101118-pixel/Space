using System.Collections.Generic;
using Dev.CSU._02_Scripts.Stage;
using UnityEngine;

namespace Dev.CSU._02_Scripts.Meteor
{
    public enum MeteorRotationDirection
    {
        Clockwise,
        CounterClockwise,
        Random
    }

    [DisallowMultipleComponent]
    public sealed class MeteorSpawner : MonoBehaviour
    {
        [System.Serializable]
        private sealed class StageMeteorVariantSet
        {
            [SerializeField, Min(1)] private int stageNumber = 1;
            [SerializeField] private GameObject[] variants;

            public int StageNumber => stageNumber;
            public GameObject[] Variants => variants;

            public void Validate()
            {
                stageNumber = Mathf.Max(1, stageNumber);
            }
        }

        [System.Serializable]
        private sealed class StageDifficultyProfile
        {
            [SerializeField, Min(1)] private int stageNumber = 1;
            [SerializeField, Min(0.1f)] private float scaleMultiplier = 1f;
            [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;
            [SerializeField, Min(0.1f)] private float intervalMultiplier = 1f;
            [SerializeField, Min(0f)] private float oneMeteorWeight = 50f;
            [SerializeField, Min(0f)] private float twoMeteorsWeight = 30f;
            [SerializeField, Min(0f)] private float threeMeteorsWeight = 20f;

            public int StageNumber => stageNumber;
            public float ScaleMultiplier => scaleMultiplier;
            public float SpeedMultiplier => speedMultiplier;
            public float IntervalMultiplier => intervalMultiplier;
            public float OneMeteorWeight => oneMeteorWeight;
            public float TwoMeteorsWeight => twoMeteorsWeight;
            public float ThreeMeteorsWeight => threeMeteorsWeight;

            public StageDifficultyProfile(
                int stage,
                float scale,
                float speed,
                float interval,
                float oneWeight,
                float twoWeight,
                float threeWeight)
            {
                stageNumber = stage;
                scaleMultiplier = scale;
                speedMultiplier = speed;
                intervalMultiplier = interval;
                oneMeteorWeight = oneWeight;
                twoMeteorsWeight = twoWeight;
                threeMeteorsWeight = threeWeight;
            }

            public void Validate()
            {
                stageNumber = Mathf.Max(1, stageNumber);
                scaleMultiplier = Mathf.Max(0.1f, scaleMultiplier);
                speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
                intervalMultiplier = Mathf.Max(0.1f, intervalMultiplier);
                oneMeteorWeight = Mathf.Max(0f, oneMeteorWeight);
                twoMeteorsWeight = Mathf.Max(0f, twoMeteorsWeight);
                threeMeteorsWeight = Mathf.Max(0f, threeMeteorsWeight);
            }
        }

        [Header("Meteor")]
        [Tooltip("운석으로 무작위 선택해 생성할 Prefab 목록입니다.")]
        [SerializeField] private GameObject[] meteorVariants;

        [Header("Stage Themes")]
        [Tooltip("Current stage provider. Each stage uses only its configured obstacle variants.")]
        [SerializeField] private StageThemeController stageThemeController;

        [Tooltip("Obstacle Prefab variants selected for each stage theme.")]
        [SerializeField]
        private StageMeteorVariantSet[] stageMeteorVariants;

        [Header("Stage Difficulty")]
        [Tooltip("Per-stage multipliers and wave weights. Missing stages use the base values below.")]
        [SerializeField]
        private StageDifficultyProfile[] stageDifficultyProfiles =
        {
            new StageDifficultyProfile(1, 0.9f, 0.85f, 1.15f, 70f, 25f, 5f),
            new StageDifficultyProfile(2, 1f, 1f, 1f, 55f, 30f, 15f),
            new StageDifficultyProfile(3, 1.05f, 1.2f, 0.85f, 40f, 40f, 20f)
        };

        [Tooltip("사용할 Spawn Point 목록입니다. 비어 있으면 이 오브젝트의 직접적인 자식을 Hierarchy 순서대로 사용합니다.")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("운석에 적용할 최소 균일 크기입니다.")]
        [Min(0f)]
        [SerializeField] private float minimumScale = 0.7f;

        [Tooltip("운석에 적용할 최대 균일 크기입니다.")]
        [Min(0f)]
        [SerializeField] private float maximumScale = 1.5f;

        [Tooltip("운석의 최소 이동 속도입니다.")]
        [Min(0f)]
        [SerializeField] private float minimumSpeed = 3f;

        [Tooltip("운석의 최대 이동 속도입니다.")]
        [Min(0f)]
        [SerializeField] private float maximumSpeed = 8f;

        [Header("Meteor Rotation")]
        [Tooltip("생성된 각 운석이 회전할 확률입니다.")]
        [Range(0f, 100f)]
        [SerializeField] private float rotationChance = 50f;

        [Tooltip("회전하는 운석의 최소 회전 속도입니다. 단위는 초당 각도입니다.")]
        [Min(0f)]
        [SerializeField] private float minimumRotationSpeed = 30f;

        [Tooltip("회전하는 운석의 최대 회전 속도입니다. 단위는 초당 각도입니다.")]
        [Min(0f)]
        [SerializeField] private float maximumRotationSpeed = 120f;

        [Tooltip("회전하는 운석에 적용할 회전 방향입니다.")]
        [SerializeField] private MeteorRotationDirection rotationDirection =
            MeteorRotationDirection.Random;

        [Tooltip("생성될 때마다 무작위 초기 Z축 회전값을 사용할지 결정합니다.")]
        [SerializeField] private bool randomizeSpawnRotation = true;

        [Tooltip("무작위 초기 회전을 사용하지 않을 때 적용할 Z축 각도입니다.")]
        [SerializeField] private float fixedSpawnRotation;

        [Tooltip("무작위 초기 Z축 회전값의 최솟값입니다.")]
        [SerializeField] private float minimumSpawnRotation;

        [Tooltip("무작위 초기 Z축 회전값의 최댓값입니다.")]
        [SerializeField] private float maximumSpawnRotation = 360f;

        [Header("Wave Probability Weights")]
        [Tooltip("한 Wave에 운석 1개가 선택될 가중치입니다.")]
        [Min(0f)]
        [SerializeField] private float oneMeteorProbability = 50f;

        [Tooltip("한 Wave에 운석 2개가 선택될 가중치입니다.")]
        [Min(0f)]
        [SerializeField] private float twoMeteorsProbability = 30f;

        [Tooltip("한 Wave에 운석 3개가 선택될 가중치입니다.")]
        [Min(0f)]
        [SerializeField] private float threeMeteorsProbability = 20f;

        [Header("Automatic Spawning")]
        [Tooltip("다음 Spawn Wave까지의 최소 대기 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float minimumSpawnInterval = 1.5f;

        [Tooltip("다음 Spawn Wave까지의 최대 대기 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float maximumSpawnInterval = 3f;

        [Tooltip("생성된 운석이 이동할 월드 방향입니다.")]
        [SerializeField] private Vector2 movementDirection = Vector2.left;

        [Tooltip("생성된 운석을 자동 제거하기까지의 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float meteorLifetime = 15f;

        [Header("Pooling")]
        [Tooltip("MonoBehaviour that implements IMeteorPool and owns meteor instances.")]
        [SerializeField] private MonoBehaviour meteorPoolSource;

        private readonly List<Transform> _resolvedSpawnPoints = new List<Transform>();
        private readonly List<int[]> _validCombinations = new List<int[]>();
        private readonly List<int> _combinationBuffer = new List<int>(3);

        private float _nextSpawnTime;
        private int _lastSpawnFrame = -1;
        private bool _spawnScheduled;
        private bool _warnedZeroWeights;
        private bool _warnedMissingVariants;
        private bool _warnedMissingSpawnPoints;
        private bool _warnedMissingPool;
        private IMeteorPool _meteorPool;
        private GameObject[] _activeMeteorVariants;
        private StageDifficultyProfile _activeDifficultyProfile;

        public int ActiveMeteorCount => _meteorPool != null ? _meteorPool.ActiveCount : 0;

        public int InactiveMeteorCount => _meteorPool != null ? _meteorPool.InactiveCount : 0;

        public int TotalMeteorCount => _meteorPool != null ? _meteorPool.TotalCount : 0;

        public int ActiveStageNumber { get; private set; } = 1;

        public int ActiveVariantCount
        {
            get
            {
                GameObject[] variants = GetActiveMeteorVariants();
                int count = 0;
                for (int index = 0;
                     index < variants.Length;
                     index++)
                {
                    if (variants[index] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            ResolveStageThemeController();
            ApplyStageTheme(GetCurrentStageNumber(), false);
            ResolveSpawnPoints();
            ResolveAndPreparePool();
        }

        private void OnEnable()
        {
            SubscribeStageTheme();
            ApplyStageTheme(GetCurrentStageNumber(), false);
            ResolveSpawnPoints();
            ResolveAndPreparePool();
            ScheduleNextWave();
        }

        private void OnDisable()
        {
            UnsubscribeStageTheme();
            _spawnScheduled = false;
        }

        private void Update()
        {
            if (!_spawnScheduled || Time.time < _nextSpawnTime)
            {
                return;
            }

            SpawnWave();
            ScheduleNextWave();
        }

        public void SpawnWave()
        {
            if (_lastSpawnFrame == Time.frameCount)
            {
                return;
            }

            _lastSpawnFrame = Time.frameCount;

            if (!TryChooseMeteorCount(out int requestedCount))
            {
                return;
            }

            if (_resolvedSpawnPoints.Count == 0)
            {
                ResolveSpawnPoints();
            }

            if (_resolvedSpawnPoints.Count == 0)
            {
                WarnMissingSpawnPointsOnce();
                return;
            }

            if (!HasUsableMeteorVariant())
            {
                WarnMissingVariantsOnce();
                return;
            }

            if (!ResolveAndPreparePool())
            {
                WarnMissingPoolOnce();
                return;
            }

            int[] selectedIndices = ChooseNonAdjacentCombination(requestedCount);
            if (selectedIndices == null)
            {
                return;
            }

            for (int i = 0; i < selectedIndices.Length; i++)
            {
                SpawnMeteor(_resolvedSpawnPoints[selectedIndices[i]]);
            }
        }

        private void ResolveSpawnPoints()
        {
            _resolvedSpawnPoints.Clear();

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] != null)
                    {
                        _resolvedSpawnPoints.Add(spawnPoints[i]);
                    }
                }

                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                _resolvedSpawnPoints.Add(transform.GetChild(i));
            }
        }

        private bool TryChooseMeteorCount(out int count)
        {
            float oneWeight = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.OneMeteorWeight
                : oneMeteorProbability;
            float twoWeight = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.TwoMeteorsWeight
                : twoMeteorsProbability;
            float threeWeight = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.ThreeMeteorsWeight
                : threeMeteorsProbability;
            float totalWeight = oneWeight + twoWeight + threeWeight;

            if (totalWeight <= 0f)
            {
                if (!_warnedZeroWeights)
                {
                    Debug.LogWarning(
                        $"{nameof(MeteorSpawner)} on '{name}' has no positive spawn probability weights.",
                        this);
                    _warnedZeroWeights = true;
                }

                count = 0;
                return false;
            }

            _warnedZeroWeights = false;
            float roll = Random.value * totalWeight;

            if (roll < oneWeight)
            {
                count = 1;
            }
            else if (roll < oneWeight + twoWeight)
            {
                count = 2;
            }
            else
            {
                count = 3;
            }

            return true;
        }

        private int[] ChooseNonAdjacentCombination(int requestedCount)
        {
            int maximumRequested = Mathf.Clamp(requestedCount, 1, 3);

            for (int count = maximumRequested; count >= 1; count--)
            {
                _validCombinations.Clear();
                _combinationBuffer.Clear();
                BuildNonAdjacentCombinations(0, count);

                if (_validCombinations.Count > 0)
                {
                    return _validCombinations[Random.Range(0, _validCombinations.Count)];
                }
            }

            return null;
        }

        private void BuildNonAdjacentCombinations(int startIndex, int remaining)
        {
            if (remaining == 0)
            {
                _validCombinations.Add(_combinationBuffer.ToArray());
                return;
            }

            int lastStartIndex = _resolvedSpawnPoints.Count - remaining;
            for (int index = startIndex; index <= lastStartIndex; index++)
            {
                if (_combinationBuffer.Count > 0
                    && index - _combinationBuffer[_combinationBuffer.Count - 1] <= 1)
                {
                    continue;
                }

                _combinationBuffer.Add(index);
                BuildNonAdjacentCombinations(index + 1, remaining - 1);
                _combinationBuffer.RemoveAt(_combinationBuffer.Count - 1);
            }
        }

        private void SpawnMeteor(Transform spawnPoint)
        {
            GameObject selectedPrefab = ChooseMeteorVariant();
            if (selectedPrefab == null)
            {
                return;
            }

            if (!_meteorPool.TryRent(
                    selectedPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    out MeteorMover mover))
            {
                return;
            }

            float scaleMultiplier = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.ScaleMultiplier
                : 1f;
            float scale = Random.Range(
                minimumScale * scaleMultiplier,
                maximumScale * scaleMultiplier);
            mover.transform.localScale = Vector3.one * scale;

            float speedMultiplier = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.SpeedMultiplier
                : 1f;
            float speed = Random.Range(
                minimumSpeed * speedMultiplier,
                maximumSpeed * speedMultiplier);
            float spawnRotation = ChooseSpawnRotation();
            float rotationSpeed = ChooseRotationSpeed();

            mover.Initialize(
                movementDirection,
                speed,
                meteorLifetime,
                rotationSpeed,
                spawnRotation);
        }

        private float ChooseRotationSpeed()
        {
            if (!ShouldRotateMeteor())
            {
                return 0f;
            }

            float speed = Random.Range(minimumRotationSpeed, maximumRotationSpeed);
            return speed * ChooseRotationDirectionSign();
        }

        private bool ShouldRotateMeteor()
        {
            if (rotationChance <= 0f)
            {
                return false;
            }

            if (rotationChance >= 100f)
            {
                return true;
            }

            return Random.value < rotationChance / 100f;
        }

        private float ChooseRotationDirectionSign()
        {
            switch (rotationDirection)
            {
                case MeteorRotationDirection.Clockwise:
                    return -1f;

                case MeteorRotationDirection.CounterClockwise:
                    return 1f;

                default:
                    return Random.value < 0.5f ? -1f : 1f;
            }
        }

        private float ChooseSpawnRotation()
        {
            return randomizeSpawnRotation
                ? Random.Range(minimumSpawnRotation, maximumSpawnRotation)
                : fixedSpawnRotation;
        }

        private GameObject ChooseMeteorVariant()
        {
            GameObject[] variants = GetActiveMeteorVariants();
            int usableCount = 0;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null)
                {
                    usableCount++;
                }
            }

            int selectedUsableIndex = Random.Range(0, usableCount);
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] == null)
                {
                    continue;
                }

                if (selectedUsableIndex == 0)
                {
                    return variants[i];
                }

                selectedUsableIndex--;
            }

            return null;
        }

        private bool HasUsableMeteorVariant()
        {
            GameObject[] variants = GetActiveMeteorVariants();
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ResolveAndPreparePool()
        {
            if (_meteorPool is Object poolObject && poolObject == null)
            {
                _meteorPool = null;
            }

            if (_meteorPool == null)
            {
                _meteorPool = meteorPoolSource as IMeteorPool;
            }

            if (_meteorPool == null)
            {
                return false;
            }

            _meteorPool.Prepare(GetActiveMeteorVariants());
            _warnedMissingPool = false;
            return true;
        }

        private void ResolveStageThemeController()
        {
            if (stageThemeController == null)
            {
                stageThemeController =
                    FindFirstObjectByType<StageThemeController>(
                        FindObjectsInactive.Include);
            }
        }

        private void SubscribeStageTheme()
        {
            ResolveStageThemeController();
            if (stageThemeController == null)
            {
                return;
            }

            stageThemeController.StageBackgroundReady -=
                HandleStageBackgroundReady;
            stageThemeController.StageBackgroundReady +=
                HandleStageBackgroundReady;
        }

        private void UnsubscribeStageTheme()
        {
            if (stageThemeController != null)
            {
                stageThemeController.StageBackgroundReady -=
                    HandleStageBackgroundReady;
            }
        }

        private void HandleStageBackgroundReady(int stageNumber)
        {
            ApplyStageTheme(stageNumber, true);
        }

        private int GetCurrentStageNumber()
        {
            return stageThemeController != null
                   && stageThemeController.CurrentStageNumber > 0
                ? stageThemeController.CurrentStageNumber
                : 1;
        }

        private void ApplyStageTheme(
            int stageNumber,
            bool preparePool)
        {
            int nextStageNumber = Mathf.Max(1, stageNumber);
            bool stageChanged =
                ActiveStageNumber != nextStageNumber;
            if (preparePool
                && stageChanged
                && meteorPoolSource is MeteorPool meteorPool)
            {
                meteorPool.ReturnAllActive();
            }

            ActiveStageNumber = nextStageNumber;
            _activeMeteorVariants = null;
            _activeDifficultyProfile = FindDifficultyProfile(
                ActiveStageNumber);

            if (stageMeteorVariants != null)
            {
                for (int index = 0;
                     index < stageMeteorVariants.Length;
                     index++)
                {
                    StageMeteorVariantSet variantSet =
                        stageMeteorVariants[index];
                    if (variantSet == null
                        || variantSet.StageNumber
                            != ActiveStageNumber
                        || !HasUsableVariant(
                            variantSet.Variants))
                    {
                        continue;
                    }

                    _activeMeteorVariants =
                        variantSet.Variants;
                    break;
                }
            }

            _warnedMissingVariants = false;
            if (preparePool)
            {
                ResolveAndPreparePool();

                if (isActiveAndEnabled)
                {
                    ScheduleNextWave();
                }
            }
        }

        private StageDifficultyProfile FindDifficultyProfile(int stageNumber)
        {
            if (stageDifficultyProfiles == null)
            {
                return null;
            }

            for (int index = 0;
                 index < stageDifficultyProfiles.Length;
                 index++)
            {
                StageDifficultyProfile profile =
                    stageDifficultyProfiles[index];
                if (profile != null && profile.StageNumber == stageNumber)
                {
                    return profile;
                }
            }

            return null;
        }

        private GameObject[] GetActiveMeteorVariants()
        {
            if (_activeMeteorVariants != null
                && _activeMeteorVariants.Length > 0)
            {
                return _activeMeteorVariants;
            }

            return meteorVariants
                ?? System.Array.Empty<GameObject>();
        }

        private static bool HasUsableVariant(
            GameObject[] variants)
        {
            if (variants == null)
            {
                return false;
            }

            for (int index = 0;
                 index < variants.Length;
                 index++)
            {
                if (variants[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleNextWave()
        {
            float intervalMultiplier = _activeDifficultyProfile != null
                ? _activeDifficultyProfile.IntervalMultiplier
                : 1f;
            _nextSpawnTime = Time.time + Random.Range(
                minimumSpawnInterval * intervalMultiplier,
                maximumSpawnInterval * intervalMultiplier);
            _spawnScheduled = true;
        }

        private void WarnMissingVariantsOnce()
        {
            if (_warnedMissingVariants)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MeteorSpawner)} on '{name}' has no usable meteor Prefab variants.",
                this);
            _warnedMissingVariants = true;
        }

        private void WarnMissingSpawnPointsOnce()
        {
            if (_warnedMissingSpawnPoints)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MeteorSpawner)} on '{name}' has no usable spawn points.",
                this);
            _warnedMissingSpawnPoints = true;
        }

        private void WarnMissingPoolOnce()
        {
            if (_warnedMissingPool)
            {
                return;
            }

            string sourceDescription = meteorPoolSource == null
                ? "no pool source is assigned"
                : $"'{meteorPoolSource.GetType().Name}' does not implement {nameof(IMeteorPool)}";

            Debug.LogWarning(
                $"{nameof(MeteorSpawner)} on '{name}' cannot spawn because {sourceDescription}.",
                this);
            _warnedMissingPool = true;
        }

        private void OnValidate()
        {
            minimumScale = Mathf.Max(0f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);
            minimumSpeed = Mathf.Max(0f, minimumSpeed);
            maximumSpeed = Mathf.Max(minimumSpeed, maximumSpeed);
            rotationChance = Mathf.Clamp(rotationChance, 0f, 100f);
            minimumRotationSpeed = Mathf.Max(0f, minimumRotationSpeed);
            maximumRotationSpeed = Mathf.Max(minimumRotationSpeed, maximumRotationSpeed);
            maximumSpawnRotation = Mathf.Max(minimumSpawnRotation, maximumSpawnRotation);
            oneMeteorProbability = Mathf.Max(0f, oneMeteorProbability);
            twoMeteorsProbability = Mathf.Max(0f, twoMeteorsProbability);
            threeMeteorsProbability = Mathf.Max(0f, threeMeteorsProbability);
            minimumSpawnInterval = Mathf.Max(0f, minimumSpawnInterval);
            maximumSpawnInterval = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
            meteorLifetime = Mathf.Max(0f, meteorLifetime);

            if (stageDifficultyProfiles != null)
            {
                for (int index = 0;
                     index < stageDifficultyProfiles.Length;
                     index++)
                {
                    stageDifficultyProfiles[index]?.Validate();
                }
            }

            if (stageMeteorVariants == null)
            {
                return;
            }

            for (int index = 0;
                 index < stageMeteorVariants.Length;
                 index++)
            {
                stageMeteorVariants[index]?.Validate();
            }
        }
    }
}
