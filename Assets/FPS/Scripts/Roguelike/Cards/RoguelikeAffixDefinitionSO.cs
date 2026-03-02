using System;
using Unity.FPS.Game;
using Unity.FPS.Roguelike.Stats;
using UnityEngine;

namespace Unity.FPS.Roguelike.Cards
{
    public enum RoguelikeAffixEffectType
    {
        StatModifier = 0,
        InstantHeal = 1,
    }

    public enum RoguelikeAffixTarget
    {
        Player = 0,
        Weapon = 1,
    }

    public enum RoguelikeValueDisplayMode
    {
        RawFloat = 0,
        RoundedInteger = 1,
        Percentage = 2,
    }

    /// <summary>
    /// 词条稀有度，用于卡池权重抽卡与 UI 展示（边框/颜色）。
    /// </summary>
    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
    }

    [Flags]
    public enum WeaponShootTypeMask
    {
        None = 0,
        Manual = 1 << 0,
        Automatic = 1 << 1,
        Charge = 1 << 2,
        All = Manual | Automatic | Charge,
    }

    [Serializable]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public float GetRandomValue()
        {
            float min = Mathf.Min(Min, Max);
            float max = Mathf.Max(Min, Max);
            return UnityEngine.Random.Range(min, max);
        }
    }

    [CreateAssetMenu(fileName = "RoguelikeAffixDefinition", menuName = "FPS/Roguelike/Card Affix Definition")]
    public sealed class RoguelikeAffixDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_Id = "affix.id";
        [SerializeField] private string m_DisplayName = "词条名称";
        [TextArea]
        [SerializeField] private string m_DescriptionTemplate = "{value}";
        [SerializeField] private Sprite m_Icon;
        [Tooltip("稀有度：影响抽卡权重与 UI 展示")]
        [SerializeField] private CardRarity m_Rarity = CardRarity.Common;

        [Header("Effect")]
        [Tooltip("StatModifier：常规属性词条；InstantHeal：选中时立刻回血（一次性效果）。")]
        [SerializeField] private RoguelikeAffixEffectType m_EffectType = RoguelikeAffixEffectType.StatModifier;
        [SerializeField] private RoguelikeAffixTarget m_Target = RoguelikeAffixTarget.Player;
        [SerializeField] private StatId m_StatId = StatId.Player_Attack;
        [SerializeField] private ModifierKind m_ModifierKind = ModifierKind.Add;
        [SerializeField] private FloatRange m_ValueRange = new FloatRange { Min = 0.1f, Max = 0.2f };
        [SerializeField] private RoguelikeValueDisplayMode m_ValueDisplayMode = RoguelikeValueDisplayMode.Percentage;

        [Header("Weapon Filter")]
        [SerializeField] private WeaponShootTypeMask m_AllowedShootTypes = WeaponShootTypeMask.All;

        [Header("Run")]
        [Tooltip("勾选后本局内可被多次抽到；不勾则整局只能选一次（如弹道+1）")]
        [SerializeField] private bool m_AllowDuplicateInRun = true;

        public string Id => m_Id;
        public string DisplayName => m_DisplayName;
        public string DescriptionTemplate => m_DescriptionTemplate;
        public Sprite Icon => m_Icon;
        public CardRarity Rarity => m_Rarity;
        public RoguelikeAffixEffectType EffectType => m_EffectType;
        public RoguelikeAffixTarget Target => m_Target;
        public StatId StatId => m_StatId;
        public ModifierKind ModifierKind => m_ModifierKind;
        public WeaponShootTypeMask AllowedShootTypes => m_AllowedShootTypes;
        public bool AllowDuplicateInRun => m_AllowDuplicateInRun;

        public float RollValue()
        {
            float value = m_ValueRange.GetRandomValue();
            if (m_ValueDisplayMode == RoguelikeValueDisplayMode.RoundedInteger)
            {
                value = Mathf.Round(value);
            }

            return value;
        }

        public bool IsWeaponCompatible(WeaponController weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            if (m_Target != RoguelikeAffixTarget.Weapon)
            {
                return true;
            }

            WeaponShootTypeMask typeMask = ToMask(weapon.ShootType);
            return (m_AllowedShootTypes & typeMask) != 0;
        }

        public string FormatValue(float value)
        {
            switch (m_ValueDisplayMode)
            {
                case RoguelikeValueDisplayMode.RoundedInteger:
                    return Mathf.RoundToInt(value).ToString();
                case RoguelikeValueDisplayMode.Percentage:
                    return string.Format("{0:P0}", value);
                default:
                    return value.ToString("0.##");
            }
        }

        private static WeaponShootTypeMask ToMask(WeaponShootType shootType)
        {
            switch (shootType)
            {
                case WeaponShootType.Manual:
                    return WeaponShootTypeMask.Manual;
                case WeaponShootType.Automatic:
                    return WeaponShootTypeMask.Automatic;
                case WeaponShootType.Charge:
                    return WeaponShootTypeMask.Charge;
                default:
                    return WeaponShootTypeMask.None;
            }
        }
    }
}
