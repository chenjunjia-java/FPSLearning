using System;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.Roguelike.Level;
using Unity.FPS.Roguelike.Stats;
using Unity.FPS.Roguelike.UI;
using UnityEngine;

namespace Unity.FPS.Roguelike.Cards
{
    [DisallowMultipleComponent]
    public sealed class RoguelikeCardManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RoguelikeAffixPoolSO m_AffixPool;
        [SerializeField] [Min(1)] private int m_RewardOptionCount = 3;

        [Header("References")]
        [SerializeField] private RoguelikeCardSelectionUI m_SelectionUI;
        [SerializeField] private RoguelikePlayerStats m_PlayerStats;
        [SerializeField] private PlayerWeaponsManager m_WeaponsManager;

        private readonly List<CandidateEntry> m_CandidateBuffer = new List<CandidateEntry>(64);
        private readonly List<CandidateEntry> m_DrawBuffer = new List<CandidateEntry>(64);
        private readonly List<RuntimeCardOption> m_RuntimeOptions = new List<RuntimeCardOption>(4);
        private readonly List<RoguelikeCardPresentationData> m_PresentationData = new List<RoguelikeCardPresentationData>(4);
        private readonly HashSet<string> m_PickedUniqueAffixIds = new HashSet<string>();
        private Action m_OnSelectionResolved;

        public bool IsSelectionInProgress { get; private set; }

        /// <summary>
        /// 清空本局已选“仅可抽一次”词条记录（新一局开始时由 Run 流程调用）。
        /// </summary>
        public void ClearPickedUniqueAffixIds()
        {
            m_PickedUniqueAffixIds.Clear();
        }

        private void Awake()
        {
            if (m_SelectionUI == null)
            {
                m_SelectionUI = FindObjectOfType<RoguelikeCardSelectionUI>();
            }

            if (m_WeaponsManager == null)
            {
                m_WeaponsManager = FindObjectOfType<PlayerWeaponsManager>();
            }

            if (m_PlayerStats == null)
            {
                m_PlayerStats = FindObjectOfType<RoguelikePlayerStats>();
            }
        }

        public bool RequestRewardForSegment(LevelSegment segment, Action onSelectionResolved)
        {
            if (IsSelectionInProgress)
            {
                return false;
            }

            if (m_SelectionUI == null)
            {
                Debug.LogError("RoguelikeCardManager requires RoguelikeCardSelectionUI.", this);
                return false;
            }

            m_OnSelectionResolved = onSelectionResolved;

            if (!BuildOptions())
            {
                ResolveSelection();
                return false;
            }

            IsSelectionInProgress = true;
            m_SelectionUI.ShowSelection(m_PresentationData, HandleCardSelected);
            return true;
        }

        private bool BuildOptions()
        {
            m_RuntimeOptions.Clear();
            m_PresentationData.Clear();
            BuildCandidates();
            if (m_CandidateBuffer.Count == 0)
            {
                return false;
            }

            m_DrawBuffer.Clear();
            for (int i = 0; i < m_CandidateBuffer.Count; i++)
            {
                m_DrawBuffer.Add(m_CandidateBuffer[i]);
            }

            int drawCount = Mathf.Max(1, m_RewardOptionCount);
            for (int i = 0; i < drawCount; i++)
            {
                if (m_DrawBuffer.Count == 0)
                {
                    for (int j = 0; j < m_CandidateBuffer.Count; j++)
                    {
                        m_DrawBuffer.Add(m_CandidateBuffer[j]);
                    }
                }

                int drawIndex = PickWeightedIndex(m_DrawBuffer);
                if (drawIndex < 0)
                {
                    drawIndex = 0;
                }

                CandidateEntry candidate = m_DrawBuffer[drawIndex];
                RemoveAtSwapBack(m_DrawBuffer, drawIndex);

                RuntimeCardOption option = BuildRuntimeOption(candidate);
                m_RuntimeOptions.Add(option);
                m_PresentationData.Add(BuildPresentation(option));
            }

            return m_RuntimeOptions.Count > 0;
        }

        private RuntimeCardOption BuildRuntimeOption(CandidateEntry candidate)
        {
            float rolledValue = candidate.Affix.RollValue();
            return new RuntimeCardOption
            {
                Affix = candidate.Affix,
                TargetWeapon = candidate.TargetWeapon,
                Value = rolledValue,
            };
        }

        private RoguelikeCardPresentationData BuildPresentation(RuntimeCardOption option)
        {
            string weaponName = option.TargetWeapon != null ? option.TargetWeapon.WeaponName : string.Empty;
            string valueString = option.Affix.FormatValue(option.Value);

            string description = option.Affix.DescriptionTemplate;
            if (string.IsNullOrEmpty(description))
            {
                description = "{value}";
            }

            description = description.Replace("{value}", valueString);
            description = description.Replace("{weapon}", weaponName);

            string title = option.Affix.DisplayName;
            if (option.Affix.Target == RoguelikeAffixTarget.Weapon && !string.IsNullOrEmpty(weaponName))
            {
                title = string.Format("{0} ({1})", title, weaponName);
            }

            return new RoguelikeCardPresentationData
            {
                Title = title,
                Description = description,
                Icon = option.Affix.Icon,
                Rarity = option.Affix.Rarity,
            };
        }

        /// <summary>
        /// 按卡池稀有度权重从候选中随机一个索引。
        /// </summary>
        private int PickWeightedIndex(List<CandidateEntry> candidates)
        {
            if (candidates == null || candidates.Count == 0 || m_AffixPool == null)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += m_AffixPool.GetWeightForRarity(candidates[i].Affix.Rarity);
            }

            if (totalWeight <= 0f)
            {
                return UnityEngine.Random.Range(0, candidates.Count);
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = m_AffixPool.GetWeightForRarity(candidates[i].Affix.Rarity);
                if (roll < w)
                {
                    return i;
                }
                roll -= w;
            }

            return candidates.Count - 1;
        }

        private void BuildCandidates()
        {
            m_CandidateBuffer.Clear();

            if (m_AffixPool == null || m_AffixPool.AllAffixes == null || m_AffixPool.AllAffixes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < m_AffixPool.AllAffixes.Count; i++)
            {
                RoguelikeAffixDefinitionSO affix = m_AffixPool.AllAffixes[i];
                if (affix == null)
                {
                    continue;
                }

                if (!affix.AllowDuplicateInRun)
                {
                    string affixId = string.IsNullOrEmpty(affix.Id) ? affix.name : affix.Id;
                    if (m_PickedUniqueAffixIds.Contains(affixId))
                    {
                        continue;
                    }
                }

                if (affix.Target == RoguelikeAffixTarget.Player)
                {
                    m_CandidateBuffer.Add(new CandidateEntry
                    {
                        Affix = affix,
                        TargetWeapon = null,
                    });
                    continue;
                }

                if (m_WeaponsManager == null)
                {
                    continue;
                }

                foreach (WeaponController weapon in m_WeaponsManager.GetOwnedWeapons())
                {
                    if (weapon == null || !affix.IsWeaponCompatible(weapon))
                    {
                        continue;
                    }

                    m_CandidateBuffer.Add(new CandidateEntry
                    {
                        Affix = affix,
                        TargetWeapon = weapon,
                    });
                }
            }
        }

        private void HandleCardSelected(int index)
        {
            if (index < 0 || index >= m_RuntimeOptions.Count)
            {
                ResolveSelection();
                return;
            }

            RuntimeCardOption option = m_RuntimeOptions[index];
            ApplyOption(option);
            ResolveSelection();
        }

        private void ApplyOption(RuntimeCardOption option)
        {
            if (option.Affix == null)
            {
                return;
            }

            string sourceId = string.IsNullOrEmpty(option.Affix.Id) ? option.Affix.name : option.Affix.Id;
            if (!option.Affix.AllowDuplicateInRun)
            {
                m_PickedUniqueAffixIds.Add(sourceId);
            }

            if (TryApplyInstantEffect(option))
            {
                return;
            }

            Modifier modifier = new Modifier(option.Affix.StatId, option.Affix.ModifierKind, option.Value, sourceId);

            if (option.Affix.Target == RoguelikeAffixTarget.Player)
            {
                if (m_PlayerStats != null)
                {
                    m_PlayerStats.AddModifier(modifier);
                }

                return;
            }

            if (m_WeaponsManager != null && option.TargetWeapon != null)
            {
                m_WeaponsManager.ApplyModifierToWeapon(option.TargetWeapon, modifier);
            }
        }

        private bool TryApplyInstantEffect(RuntimeCardOption option)
        {
            if (option.Affix == null)
            {
                return false;
            }

            switch (option.Affix.EffectType)
            {
                case RoguelikeAffixEffectType.InstantHeal:
                    if (m_PlayerStats != null && m_PlayerStats.TryGetComponent(out Health health))
                    {
                        float healRatio = Mathf.Max(0f, option.Value);
                        float healAmount = healRatio * health.MaxHealth;
                        health.Heal(healAmount);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private void ResolveSelection()
        {
            IsSelectionInProgress = false;
            Action callback = m_OnSelectionResolved;
            m_OnSelectionResolved = null;
            callback?.Invoke();
        }

        private static void RemoveAtSwapBack(List<CandidateEntry> list, int index)
        {
            int lastIndex = list.Count - 1;
            if (index < 0 || index > lastIndex)
            {
                return;
            }

            list[index] = list[lastIndex];
            list.RemoveAt(lastIndex);
        }

        private struct CandidateEntry
        {
            public RoguelikeAffixDefinitionSO Affix;
            public WeaponController TargetWeapon;
        }

        private struct RuntimeCardOption
        {
            public RoguelikeAffixDefinitionSO Affix;
            public WeaponController TargetWeapon;
            public float Value;
        }
    }
}
