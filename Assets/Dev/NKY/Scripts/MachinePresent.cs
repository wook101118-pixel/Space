using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dev.NKY.Scripts
{
    public class MachinePresent : MonoBehaviour
    {
        private Sprite _icon;
        private string _name;
        private string _description;
        private List<StatModifier> _statModifier;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statText;
        [SerializeField] private SoundDataSO popUpOnSound;
        [SerializeField] private SoundDataSO popUpOffSound;
        [SerializeField, Min(0.1f)]
        [Tooltip("툴팁 열기/닫기 사운드의 전역 재생 간격입니다. 권장 범위: 0.1~1초")]
        private float soundCooldown = 0.25f;

        private const float MinimumSoundCooldown = 0.1f;
        private static float s_NextSoundTime = float.NegativeInfinity;
        private bool _isVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSoundCooldown()
        {
            s_NextSoundTime = float.NegativeInfinity;
        }

        public void Initialize(MachinePartsDataSo data)
        {
            _icon = data.icon;
            _name = data.partName;
            _description = data.partDescription;
            _statModifier = data.statData;
            
            Apply();
        }

        public void NothingPart()
        {
            canvasGroup.alpha = 0;
        }

        private void Apply()
        {
            canvasGroup.alpha = 1;
            
            icon.sprite = _icon;
            nameText.text = _name;
            descriptionText.text = _description;

            statText.text = "";
            foreach (var stat in _statModifier)
            {
                string pn = stat.value >= 0 ? "+" : "";
                string displayName =
                    stat.type.ToKoreanDescription();
                if (stat.modifierType == ModifierType.Flat)
                {
                    statText.text +=
                        $"{displayName}\n" +
                        $"증가량: {pn}{(int)stat.value}\n";
                }
                else
                {
                    double percent = Math.Round(stat.value * 100, 1);
                    statText.text +=
                        $"{displayName}\n" +
                        $"증가량: {pn}{percent}%\n";
                }

                statText.text += "\n";
            }
        }
        
        public void Show(Vector3 position)
        {
            transform.position = position;

            bool stateChanged = !_isVisible;
            _isVisible = true;
            gameObject.SetActive(true);

            if (stateChanged)
            {
                TryPlaySound(popUpOnSound);
            }
        }

        public void Hide()
        {
            SetHidden(true);
        }

        public void HideSilently()
        {
            SetHidden(false);
        }

        private void SetHidden(bool playSound)
        {
            bool stateChanged = _isVisible;
            _isVisible = false;

            if (stateChanged && playSound)
            {
                TryPlaySound(popUpOffSound);
            }

            gameObject.SetActive(false);
        }

        private void TryPlaySound(SoundDataSO soundData)
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null || soundData == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < s_NextSoundTime)
            {
                return;
            }

            float cooldown = Mathf.Max(MinimumSoundCooldown, soundCooldown);
            s_NextSoundTime = now + cooldown;
            soundManager.PlaySFX(soundData);
        }
    }
}
