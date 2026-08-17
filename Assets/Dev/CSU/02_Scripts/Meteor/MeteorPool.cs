using System.Collections.Generic;
using UnityEngine;

namespace Dev.CSU._02_Scripts.Meteor
{
    [DisallowMultipleComponent]
    public sealed class MeteorPool : MonoBehaviour, IMeteorPool
    {
        private sealed class VariantPool
        {
            public readonly GameObject Prefab;
            public readonly Vector3 DefaultScale;
            public readonly Stack<MeteorMover> Inactive = new Stack<MeteorMover>();
            public readonly HashSet<MeteorMover> Rented = new HashSet<MeteorMover>();
            public readonly HashSet<MeteorMover> All = new HashSet<MeteorMover>();

            public VariantPool(GameObject prefab)
            {
                Prefab = prefab;
                DefaultScale = prefab.transform.localScale;
            }
        }

        [Header("Variants")]
        [Tooltip("Meteor Prefab variants prepared during Awake. Null and duplicate entries are ignored.")]
        [SerializeField] private GameObject[] meteorVariants;

        [Header("Capacity")]
        [Tooltip("Number of inactive instances created for each Variant before spawning begins.")]
        [Min(0)]
        [SerializeField] private int prewarmCountPerVariant = 8;

        [Tooltip("Maximum number of instances retained for each Variant.")]
        [Min(1)]
        [SerializeField] private int maxCountPerVariant = 16;

        [Tooltip("Allows a Variant pool to grow up to its maximum when all prepared instances are active.")]
        [SerializeField] private bool allowExpansion = true;

        [Header("Hierarchy")]
        [Tooltip("Parent used for returned, inactive instances. This Transform is used when left empty.")]
        [SerializeField] private Transform inactiveRoot;

        private readonly Dictionary<GameObject, VariantPool> _variantPools =
            new Dictionary<GameObject, VariantPool>();

        private readonly Dictionary<MeteorMover, VariantPool> _instancePools =
            new Dictionary<MeteorMover, VariantPool>();

        private readonly List<MeteorMover> _destroyedInstanceBuffer =
            new List<MeteorMover>();

        private readonly List<MeteorMover> _rentedInstanceBuffer =
            new List<MeteorMover>();

        private readonly HashSet<GameObject> _warnedExhaustedVariants =
            new HashSet<GameObject>();

        private bool _warnedNullRentVariant;
        private bool _warnedForeignReturn;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (VariantPool pool in _variantPools.Values)
                {
                    count += CountValid(pool.Rented);
                }

                return count;
            }
        }

        public int InactiveCount
        {
            get
            {
                int count = 0;
                foreach (VariantPool pool in _variantPools.Values)
                {
                    count += CountValid(pool.Inactive);
                }

                return count;
            }
        }

        public int TotalCount
        {
            get
            {
                int count = 0;
                foreach (VariantPool pool in _variantPools.Values)
                {
                    count += CountValid(pool.All);
                }

                return count;
            }
        }

        private Transform InactiveRoot => inactiveRoot != null ? inactiveRoot : transform;

        private void Awake()
        {
            Prepare(meteorVariants);
        }

        public void Prepare(IReadOnlyList<GameObject> variants)
        {
            if (variants == null)
            {
                return;
            }

            var uniqueVariants = new HashSet<GameObject>();
            for (int i = 0; i < variants.Count; i++)
            {
                GameObject variant = variants[i];
                if (variant == null || !uniqueVariants.Add(variant))
                {
                    continue;
                }

                VariantPool pool = GetOrCreateVariantPool(variant);
                Prewarm(pool);
            }
        }

        public bool TryRent(
            GameObject variant,
            Vector3 position,
            Quaternion rotation,
            out MeteorMover meteor)
        {
            meteor = null;

            if (variant == null)
            {
                WarnNullVariantOnce();
                return false;
            }

            VariantPool pool = GetOrCreateVariantPool(variant);
            Prewarm(pool);
            DiscardDestroyedInstances(pool);

            meteor = PopInactive(pool);
            if (meteor == null)
            {
                if (!allowExpansion || pool.All.Count >= maxCountPerVariant)
                {
                    WarnExhaustedOnce(pool);
                    return false;
                }

                meteor = CreateInstance(pool);
                if (meteor == null)
                {
                    WarnExhaustedOnce(pool);
                    return false;
                }

                PopSpecificInactive(pool, meteor);
            }

            pool.Rented.Add(meteor);
            PrepareForRent(meteor, pool, position, rotation);
            return true;
        }

        public void Return(MeteorMover meteor)
        {
            if (meteor == null)
            {
                return;
            }

            if (!_instancePools.TryGetValue(meteor, out VariantPool pool))
            {
                WarnForeignReturnOnce(meteor);
                return;
            }

            // Removing before SetActive protects against recursive returns from OnDisable.
            if (!pool.Rented.Remove(meteor))
            {
                return;
            }

            ResetRigidbody(meteor);

            Transform meteorTransform = meteor.transform;
            meteorTransform.SetParent(InactiveRoot, false);
            meteorTransform.localPosition = Vector3.zero;
            meteorTransform.localRotation = Quaternion.identity;
            meteorTransform.localScale = pool.DefaultScale;

            if (meteor.gameObject.activeSelf)
            {
                meteor.gameObject.SetActive(false);
            }

            pool.Inactive.Push(meteor);
        }

        public void ReturnAllActive()
        {
            _rentedInstanceBuffer.Clear();
            foreach (VariantPool pool in _variantPools.Values)
            {
                foreach (MeteorMover meteor in pool.Rented)
                {
                    if (meteor != null)
                    {
                        _rentedInstanceBuffer.Add(meteor);
                    }
                }
            }

            for (int index = 0;
                 index < _rentedInstanceBuffer.Count;
                 index++)
            {
                Return(_rentedInstanceBuffer[index]);
            }

            _rentedInstanceBuffer.Clear();
        }

        public bool TryGetCounts(
            GameObject variant,
            out int active,
            out int inactive,
            out int total)
        {
            active = 0;
            inactive = 0;
            total = 0;

            if (variant == null || !_variantPools.TryGetValue(variant, out VariantPool pool))
            {
                return false;
            }

            active = CountValid(pool.Rented);
            inactive = CountValid(pool.Inactive);
            total = CountValid(pool.All);
            return true;
        }

        private VariantPool GetOrCreateVariantPool(GameObject variant)
        {
            if (_variantPools.TryGetValue(variant, out VariantPool pool))
            {
                return pool;
            }

            pool = new VariantPool(variant);
            _variantPools.Add(variant, pool);
            return pool;
        }

        private void Prewarm(VariantPool pool)
        {
            DiscardDestroyedInstances(pool);

            int targetCount = Mathf.Min(prewarmCountPerVariant, maxCountPerVariant);
            while (pool.All.Count < targetCount)
            {
                if (CreateInstance(pool) == null)
                {
                    break;
                }
            }
        }

        private MeteorMover CreateInstance(VariantPool pool)
        {
            if (pool == null || pool.Prefab == null || pool.All.Count >= maxCountPerVariant)
            {
                return null;
            }

            GameObject instance = Instantiate(pool.Prefab, InactiveRoot);
            instance.name = $"{pool.Prefab.name}_Pooled_{pool.All.Count + 1:00}";

            if (!instance.TryGetComponent(out MeteorMover mover))
            {
                mover = instance.AddComponent<MeteorMover>();
            }

            mover.BindPool(this, pool.Prefab);
            ResetRigidbody(mover);

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = pool.DefaultScale;

            if (instance.activeSelf)
            {
                instance.SetActive(false);
            }

            pool.All.Add(mover);
            pool.Inactive.Push(mover);
            _instancePools[mover] = pool;
            return mover;
        }

        private static MeteorMover PopInactive(VariantPool pool)
        {
            while (pool.Inactive.Count > 0)
            {
                MeteorMover candidate = pool.Inactive.Pop();
                if (candidate != null && !pool.Rented.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void PopSpecificInactive(VariantPool pool, MeteorMover target)
        {
            if (pool.Inactive.Count == 0)
            {
                return;
            }

            // A newly created instance is always the most recent entry.
            if (ReferenceEquals(pool.Inactive.Peek(), target))
            {
                pool.Inactive.Pop();
            }
        }

        private void PrepareForRent(
            MeteorMover meteor,
            VariantPool pool,
            Vector3 position,
            Quaternion rotation)
        {
            ResetRigidbody(meteor);

            Transform meteorTransform = meteor.transform;
            meteorTransform.SetParent(null, false);
            meteorTransform.SetPositionAndRotation(position, rotation);
            meteorTransform.localScale = pool.DefaultScale;

            if (!meteor.gameObject.activeSelf)
            {
                meteor.gameObject.SetActive(true);
            }
        }

        private static void ResetRigidbody(MeteorMover meteor)
        {
            if (meteor != null && meteor.TryGetComponent(out Rigidbody2D rigidbody2D))
            {
                rigidbody2D.linearVelocity = Vector2.zero;
                rigidbody2D.angularVelocity = 0f;
            }
        }

        private void DiscardDestroyedInstances(VariantPool pool)
        {
            _destroyedInstanceBuffer.Clear();
            foreach (KeyValuePair<MeteorMover, VariantPool> pair in _instancePools)
            {
                if (pair.Key == null)
                {
                    _destroyedInstanceBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _destroyedInstanceBuffer.Count; i++)
            {
                _instancePools.Remove(_destroyedInstanceBuffer[i]);
            }

            pool.All.RemoveWhere(candidate => candidate == null);
            pool.Rented.RemoveWhere(candidate => candidate == null);

            if (pool.Inactive.Count == 0)
            {
                return;
            }

            var validInactive = new Stack<MeteorMover>(pool.Inactive.Count);
            while (pool.Inactive.Count > 0)
            {
                MeteorMover candidate = pool.Inactive.Pop();
                if (candidate != null && !pool.Rented.Contains(candidate))
                {
                    validInactive.Push(candidate);
                }
            }

            while (validInactive.Count > 0)
            {
                pool.Inactive.Push(validInactive.Pop());
            }
        }

        private static int CountValid(IEnumerable<MeteorMover> meteors)
        {
            int count = 0;
            foreach (MeteorMover meteor in meteors)
            {
                if (meteor != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void WarnNullVariantOnce()
        {
            if (_warnedNullRentVariant)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MeteorPool)} on '{name}' cannot rent a null meteor Variant.",
                this);
            _warnedNullRentVariant = true;
        }

        private void WarnExhaustedOnce(VariantPool pool)
        {
            if (pool.Prefab == null || !_warnedExhaustedVariants.Add(pool.Prefab))
            {
                return;
            }

            string capacityDescription = allowExpansion
                ? $"its limit of {maxCountPerVariant}"
                : $"its prepared capacity of {pool.All.Count} with expansion disabled";

            Debug.LogWarning(
                $"{nameof(MeteorPool)} on '{name}' exhausted Variant "
                + $"'{pool.Prefab.name}' at {capacityDescription}. "
                + "This spawn was skipped.",
                this);
        }

        private void WarnForeignReturnOnce(MeteorMover meteor)
        {
            if (_warnedForeignReturn)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(MeteorPool)} on '{name}' ignored a meteor that belongs to another pool.",
                meteor);
            _warnedForeignReturn = true;
        }

        private void OnValidate()
        {
            prewarmCountPerVariant = Mathf.Max(0, prewarmCountPerVariant);
            maxCountPerVariant = Mathf.Max(1, maxCountPerVariant);
            prewarmCountPerVariant = Mathf.Min(
                prewarmCountPerVariant,
                maxCountPerVariant);
        }
    }
}
