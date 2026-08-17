using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    public class PlayerStatChack : MonoBehaviour
    {
        [SerializeField] private PlayerStats stat;
        
        [SerializeField] private TextMeshProUGUI statText;
        
        private readonly StringBuilder _textBuilder = new StringBuilder(128);

        private void OnEnable()
        {
            if (stat != null)
            {
                stat.OnAllStatsUpdated += RenderStats;
            }
        }

        private void Start()
        {
            if (stat != null)
            {
                RenderStats(stat.GetAllFinalStats());
            }
        }

        private void OnDisable()
        {
            if (stat != null)
            {
                stat.OnAllStatsUpdated -= RenderStats;
            }
        }

        private void RenderStats(Dictionary<StatType, float> finalStats)
        {
            if (statText == null || finalStats == null)
            {
                return;
            }

            _textBuilder.Clear();
            foreach (KeyValuePair<StatType, float> finalStat in finalStats)
            {
                _textBuilder
                    .Append(finalStat.Key.ToKoreanDescription())
                    .Append(": ")
                    .Append(Mathf.CeilToInt(finalStat.Value))
                    .AppendLine();
            }

            statText.text = _textBuilder.ToString();
        }
    }
}
