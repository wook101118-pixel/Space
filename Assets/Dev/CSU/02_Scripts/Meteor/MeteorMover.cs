using System;
using Dev.NKY.Scripts.Health;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dev.CSU._02_Scripts.Meteor
{
    [DisallowMultipleComponent]
    public sealed class MeteorMover : MonoBehaviour
    {
        [Header("Collision Damage")]
        [SerializeField, Min(0f)] private float meteorDamage = 10f;
        private Rigidbody2D _rigidbody2D;
        private Vector2 _direction = Vector2.left;
        private float _speed;
        private float _rotationSpeed;
        private float _remainingLifetime;
        private bool _initialized;
        private bool _isRented;
        private bool _warnedMissingPool;
        private IMeteorPool _ownerPool;
        private GameObject _variant;

        public bool IsRented => _isRented;

        public GameObject Variant => _variant;

        private void Awake()
        {
            CacheRigidbody();
        }

        internal void BindPool(IMeteorPool owner, GameObject variant)
        {
            _ownerPool = owner;
            _variant = variant;
            _isRented = false;
            _warnedMissingPool = false;
        }

        public void Initialize(
            Vector2 direction,
            float speed,
            float lifetime,
            float rotationSpeed,
            float spawnRotation)
        {
            CacheRigidbody();
            ResetRigidbody();

            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            _speed = Mathf.Max(0f, speed);
            _rotationSpeed = rotationSpeed;
            _remainingLifetime = Mathf.Max(0f, lifetime);
            SetZRotation(spawnRotation);
            _initialized = true;
            _isRented = true;

            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = _direction * _speed;
                _rigidbody2D.angularVelocity = _rotationSpeed;
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (_rigidbody2D == null)
            {
                transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));
                transform.Rotate(
                    0f,
                    0f,
                    _rotationSpeed * Time.deltaTime,
                    Space.Self);
            }

            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                ReturnToPool();
            }
        }

        public void ReturnToPool()
        {
            if (_ownerPool is Object ownerObject && ownerObject == null)
            {
                _ownerPool = null;
            }

            if (_ownerPool != null)
            {
                _isRented = false;
                _initialized = false;
                _ownerPool.Return(this);
                return;
            }

            if (!_isRented)
            {
                return;
            }

            _isRented = false;
            _initialized = false;

            if (!_warnedMissingPool)
            {
                Debug.LogWarning(
                    $"{nameof(MeteorMover)} on '{name}' cannot return because it has no pool owner.",
                    this);
                _warnedMissingPool = true;
            }

            ResetRuntimeState();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ResetRuntimeState();
        }

        private void ResetRuntimeState()
        {
            _direction = Vector2.left;
            _speed = 0f;
            _rotationSpeed = 0f;
            _remainingLifetime = 0f;
            _initialized = false;
            _isRented = false;
            SetZRotation(0f);
            ResetRigidbody();
        }

        private void CacheRigidbody()
        {
            if (_rigidbody2D != null)
            {
                return;
            }

            if (TryGetComponent(out _rigidbody2D))
            {
                return;
            }

            _rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void ResetRigidbody()
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
            }
        }

        private void SetZRotation(float zRotation)
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.rotation = zRotation;
                return;
            }

            Vector3 eulerAngles = transform.eulerAngles;
            eulerAngles.z = zRotation;
            transform.rotation = Quaternion.Euler(eulerAngles);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(meteorDamage);
                ReturnToPool();
            }
        }

        private void OnValidate()
        {
            meteorDamage = Mathf.Max(0f, meteorDamage);
        }
    }
}
