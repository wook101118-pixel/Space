using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dev.NKY.Scripts.Health
{
    public abstract class DamageTask : MonoBehaviour, IDamageable
    {
        public static bool DamageSystemEnabled { get; private set; } = true;

        [field:SerializeField] public HealthDataSo Data { get; private set; }
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        
        
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider bgHealthSlider;
        [SerializeField] private TextMeshProUGUI healthText;

        private Sequence healthUiSequence;
        
        public event Action DeadEvent;
        public event Action<float> DamageTaken;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDamageSystemState()
        {
            DamageSystemEnabled = true;
        }

        public static void SetDamageSystemEnabled(bool enabled)
        {
            DamageSystemEnabled = enabled;
        }

        public void HealthInit()
        {
            MaxHealth = Data.maxHealth;
            CurrentHealth = Data.currentHealth;
            IsDead = false;
        }

        public void SetHealth(float health)
        {
            MaxHealth = Mathf.Max(1f, health);
            CurrentHealth = MaxHealth;
            IsDead = false;
            OnHealthReset();
            SetUi(MaxHealth, CurrentHealth);
        }

        public virtual void Awake()
        {
            HealthInit();
        }

        protected virtual void OnDisable()
        {
            KillHealthUiSequence();
        }

        protected virtual void OnDestroy()
        {
            KillHealthUiSequence();
        }

        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            IsDead = false;
            OnHealthReset();
            SetUi(MaxHealth, CurrentHealth);
        }

        protected virtual void OnHealthReset()
        {
        }

        public void TakeDamage(float damage)
        {
            if (!DamageSystemEnabled
                || IsDead
                || damage <= 0f
                || float.IsNaN(damage)
                || float.IsInfinity(damage))
            {
                return;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            float appliedDamage = previousHealth - CurrentHealth;
            if (appliedDamage <= 0f)
            {
                return;
            }

            SetUi(MaxHealth, CurrentHealth);
            DamageTaken?.Invoke(appliedDamage);

            if (CurrentHealth <= 0f)
            {
                Dead();
            }
        }

        public void SetUi(float maxHealth, float currentHealth)
        {
            if (healthText != null)
            {
                healthText.text =
                    $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
            }

            KillHealthUiSequence();

            if (healthSlider == null && bgHealthSlider == null)
            {
                return;
            }

            healthUiSequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            float normalizedHealth = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;

            if (healthSlider != null)
            {
                healthUiSequence.Append(
                    healthSlider.DOValue(normalizedHealth, 0.1f)
                        .SetEase(Ease.OutCubic));
            }

            if (bgHealthSlider != null)
            {
                healthUiSequence.AppendInterval(0.15f);
                healthUiSequence.Append(
                    bgHealthSlider.DOValue(normalizedHealth, 0.1f)
                        .SetEase(Ease.OutCubic));
            }
        }

        private void KillHealthUiSequence()
        {
            if (healthUiSequence != null && healthUiSequence.IsActive())
            {
                healthUiSequence.Kill(false);
            }

            healthUiSequence = null;
        }

        public virtual void Dead()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            DeadEvent?.Invoke();
        }
    }
}
