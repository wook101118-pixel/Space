using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Dev.CSU._02_Scripts.Distance
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class DistanceDisplay : MonoBehaviour
    {
        private const int DecimalMultiplier = 10;
        private const float MetersPerKilometer = 1000f;
        private const int RuntimeFontSize = 90;
        private const int AtlasSize = 1024;
        private const int AtlasPadding = 9;

        [Header("References")]
        [SerializeField]
        [Tooltip("Tracker that supplies the accumulated rightward distance.")]
        private HorizontalDistanceTracker tracker;

        [SerializeField]
        [Tooltip("TextMeshPro text used to display the distance.")]
        private TMP_Text distanceText;

        [SerializeField]
        [Tooltip("Project font used to render the Korean distance label.")]
        private Font koreanSourceFont;

        private TMP_FontAsset _runtimeFontAsset;
        private int _displayedTenths = int.MinValue;

        private void Awake()
        {
            if (distanceText == null)
            {
                distanceText = GetComponent<TMP_Text>();
            }

            EnsureReadableFont();
        }

        private void OnEnable()
        {
            if (tracker == null || distanceText == null)
            {
                Debug.LogError(
                    "DistanceDisplay requires both a tracker and a TextMeshPro reference.",
                    this);
                enabled = false;
                return;
            }

            tracker.DistanceChanged += HandleDistanceChanged;
            RefreshText(tracker.DistanceMeters, true);
        }

        private void OnDisable()
        {
            if (tracker != null)
            {
                tracker.DistanceChanged -= HandleDistanceChanged;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeFontAsset != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeFontAsset);
                }
                else
                {
                    DestroyImmediate(_runtimeFontAsset);
                }
            }
        }

        private void OnValidate()
        {
            if (distanceText == null)
            {
                distanceText = GetComponent<TMP_Text>();
            }

            EnsureReadableFont();
        }

        private void HandleDistanceChanged(float distanceMeters)
        {
            RefreshText(distanceMeters, false);
        }

        private void RefreshText(float distanceMeters, bool force)
        {
            float distanceKilometers = distanceMeters / MetersPerKilometer;
            int roundedTenths = Mathf.RoundToInt(distanceKilometers * DecimalMultiplier);
            if (!force && roundedTenths == _displayedTenths)
            {
                return;
            }

            _displayedTenths = roundedTenths;
            float displayedKilometers = roundedTenths / (float)DecimalMultiplier;
            distanceText.text = string.Format(
                CultureInfo.InvariantCulture,
                "\uC774\uB3D9\uAC70\uB9AC: {0:F1} KM",
                displayedKilometers);
        }

        private void EnsureReadableFont()
        {
            if (distanceText == null)
            {
                return;
            }

            if (_runtimeFontAsset != null)
            {
                distanceText.font = _runtimeFontAsset;
                return;
            }

            if (distanceText.font != null)
            {
                return;
            }

            if (koreanSourceFont == null)
            {
                Debug.LogWarning(
                    "DistanceDisplay requires a Korean source font for its TMP text.",
                    this);
                return;
            }

            _runtimeFontAsset = TMP_FontAsset.CreateFontAsset(
                koreanSourceFont,
                RuntimeFontSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                true);

            if (_runtimeFontAsset != null)
            {
                _runtimeFontAsset.name = "Runtime Korean Distance Font";
                _runtimeFontAsset.hideFlags = HideFlags.HideAndDontSave;
                distanceText.font = _runtimeFontAsset;
            }
            else
            {
                Debug.LogWarning("DistanceDisplay could not create a TMP font asset.", this);
            }
        }

    }
}
