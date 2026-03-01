using System;
using Unity.FPS.Roguelike.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.FPS.Roguelike.UI
{
    [DisallowMultipleComponent]
    public sealed class RoguelikeCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_IconImage;
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_DescriptionText;

        [Header("Animation")]
        [SerializeField] [Min(0f)] private float m_HoverScale = 1.06f;
        [SerializeField] [Min(0f)] private float m_HoverDuration = 0.12f;
        [SerializeField] private Ease m_HoverEase = Ease.OutQuad;
        [Tooltip("当外部把 Time.timeScale 设为 0 时，是否仍然播放动效（建议开启）")]
        [SerializeField] private bool m_IgnoreTimeScale = true;

        [Header("Rarity")]
        [Tooltip("可选：按稀有度着色边框/背景")]
        [SerializeField] private Image m_RarityBorderOrBackground;
        [SerializeField] private Color m_CommonColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color m_RareColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color m_EpicColor = new Color(0.6f, 0.2f, 1f);

        private Action m_OnClick;
        private bool m_Interactable = true;
        private Tween m_ScaleTween;

        private void Awake()
        {
            CacheReferencesIfNeeded();
            EnsureButton();
        }

        private void OnDestroy()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveListener(HandleButtonClicked);
            }

            KillScaleTween();
        }

        public void Bind(string title, string description, Sprite iconSprite, Action onClick, CardRarity rarity = CardRarity.Common)
        {
            CacheReferencesIfNeeded();
            EnsureButton();

            if (m_TitleText != null)
            {
                m_TitleText.text = title ?? string.Empty;
            }

            if (m_DescriptionText != null)
            {
                m_DescriptionText.text = description ?? string.Empty;
            }

            SetIcon(iconSprite);
            ApplyRarityColor(rarity);
            m_OnClick = onClick;
        }

        private void ApplyRarityColor(CardRarity rarity)
        {
            if (m_RarityBorderOrBackground == null)
            {
                return;
            }

            Color c = rarity switch
            {
                CardRarity.Rare => m_RareColor,
                CardRarity.Epic => m_EpicColor,
                _ => m_CommonColor,
            };
            m_RarityBorderOrBackground.color = c;
        }

        private void HandleButtonClicked()
        {
            if (!m_Interactable)
            {
                return;
            }

            m_OnClick?.Invoke();
        }

        public void SetInteractable(bool interactable)
        {
            m_Interactable = interactable;
            if (m_Button != null)
            {
                m_Button.interactable = interactable;
            }

            if (!interactable)
            {
                KillScaleTween();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!m_Interactable)
            {
                return;
            }

            AnimateScale(m_HoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!m_Interactable)
            {
                return;
            }

            AnimateScale(1f);
        }

        private void CacheReferencesIfNeeded()
        {
            if (m_IconImage == null)
            {
                Transform icon = transform.Find("Icon");
                if (icon != null)
                {
                    m_IconImage = icon.GetComponent<Image>();
                }
            }

            if (m_TitleText == null)
            {
                Transform title = transform.Find("Title");
                if (title != null)
                {
                    m_TitleText = title.GetComponent<TMP_Text>();
                }
            }

            if (m_DescriptionText == null)
            {
                Transform description = transform.Find("Description");
                if (description != null)
                {
                    m_DescriptionText = description.GetComponent<TMP_Text>();
                }
            }

            if (m_Button == null)
            {
                m_Button = GetComponent<Button>();
            }
        }

        private void EnsureButton()
        {
            if (m_Button == null)
            {
                m_Button = gameObject.AddComponent<Button>();
                m_Button.transition = Selectable.Transition.ColorTint;
            }

            m_Button.onClick.RemoveListener(HandleButtonClicked);
            m_Button.onClick.AddListener(HandleButtonClicked);
        }

        private void SetIcon(Sprite iconSprite)
        {
            if (m_IconImage == null)
            {
                return;
            }

            m_IconImage.sprite = iconSprite;
            m_IconImage.enabled = iconSprite != null;
        }

        private void AnimateScale(float scale)
        {
            KillScaleTween();
            m_ScaleTween = transform.DOScale(scale, m_HoverDuration)
                .SetEase(m_HoverEase)
                .SetUpdate(m_IgnoreTimeScale)
                .SetTarget(this);
        }

        private void KillScaleTween()
        {
            if (m_ScaleTween != null && m_ScaleTween.IsActive())
            {
                m_ScaleTween.Kill(false);
            }
            m_ScaleTween = null;
        }
    }
}
