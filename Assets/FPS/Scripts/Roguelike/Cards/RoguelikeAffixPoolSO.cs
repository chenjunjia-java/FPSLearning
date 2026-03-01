using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Roguelike.Cards
{
    [System.Serializable]
    public struct RarityWeightEntry
    {
        public CardRarity Rarity;
        [Min(0f)] public float Weight;
    }

    [CreateAssetMenu(fileName = "RoguelikeAffixPool", menuName = "FPS/Roguelike/Card Affix Pool")]
    public sealed class RoguelikeAffixPoolSO : ScriptableObject
    {
        [SerializeField] private List<RoguelikeAffixDefinitionSO> m_AllAffixes = new List<RoguelikeAffixDefinitionSO>(32);

        [Header("Rarity Weights")]
        [Tooltip("抽卡时按稀有度权重随机。未配置的稀有度视为权重 1。")]
        [SerializeField] private List<RarityWeightEntry> m_RarityWeights = new List<RarityWeightEntry>
        {
            new RarityWeightEntry { Rarity = CardRarity.Common, Weight = 10f },
            new RarityWeightEntry { Rarity = CardRarity.Rare, Weight = 4f },
            new RarityWeightEntry { Rarity = CardRarity.Epic, Weight = 1.5f },
        };

        public IReadOnlyList<RoguelikeAffixDefinitionSO> AllAffixes => m_AllAffixes;

        /// <summary>
        /// 获取指定稀有度的抽卡权重；未配置时返回 1。
        /// </summary>
        public float GetWeightForRarity(CardRarity rarity)
        {
            if (m_RarityWeights == null)
            {
                return 1f;
            }

            for (int i = 0; i < m_RarityWeights.Count; i++)
            {
                if (m_RarityWeights[i].Rarity == rarity)
                {
                    return Mathf.Max(0f, m_RarityWeights[i].Weight);
                }
            }

            return 1f;
        }
    }
}
