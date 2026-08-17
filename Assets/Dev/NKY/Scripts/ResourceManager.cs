using System;
using TMPro;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    public class ResourceManager : MonoBehaviour
    {
        private const string SavedResourceKey = "SpaceGame.Resource.Current";

        [field: SerializeField] public int CurrentResource { get; set; } = 1000; // 보유 자원 (기본 1000)
        
        [SerializeField] private TextMeshProUGUI resourceText;

        public event Action<int> OnResourceChanged;

        private void Awake()
        {
            CurrentResource = Mathf.Max(
                0,
                PlayerPrefs.GetInt(SavedResourceKey, CurrentResource));
        }

        private void OnEnable()
        {
            OnResourceChanged += Resource;
            OnResourceChanged?.Invoke(CurrentResource);
        }

        private void OnDisable()
        {
            OnResourceChanged -= Resource;
        }

        /// <summary>
        /// 보유 자원이 필요 수치 이상인지 확인합니다.
        /// </summary>
        public bool HasEnoughResource(int amount)
        {
            return CurrentResource >= amount;
        }

        /// <summary>
        /// 자원을 소모합니다. 자원이 부족하면 false를 반환합니다.
        /// </summary>
        public bool ConsumeResource(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Resource costs cannot be negative.");
            }

            if (!HasEnoughResource(amount)) return false;

            CurrentResource -= amount;
            SaveAndNotify();
            return true;
        }

        /// <summary>
        /// 자원을 획득합니다.
        /// </summary>
        public void AddResource(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Resource rewards cannot be negative.");
            }

            long updatedResource = (long)CurrentResource + amount;
            CurrentResource = updatedResource >= int.MaxValue
                ? int.MaxValue
                : updatedResource <= 0L
                    ? 0
                    : (int)updatedResource;
            SaveAndNotify();
        }

        private void SaveAndNotify()
        {
            PlayerPrefs.SetInt(SavedResourceKey, CurrentResource);
            PlayerPrefs.Save();
            OnResourceChanged?.Invoke(CurrentResource);
        }

        public void Resource(int amount)
        {
            if (resourceText != null)
            {
                resourceText.text = amount.ToString();
            }
        }
    }
}
