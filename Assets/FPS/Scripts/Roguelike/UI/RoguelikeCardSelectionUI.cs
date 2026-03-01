using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.FPS.Roguelike.Cards;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.Roguelike.UI
{
    [Serializable]
    public struct RoguelikeCardPresentationData
    {
        public string Title;
        public string Description;
        public Sprite Icon;
        public CardRarity Rarity;
    }

    [DisallowMultipleComponent]
    public sealed class RoguelikeCardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject m_MenuRoot;
        [SerializeField] private Transform m_CardContainer;
        [SerializeField] private RoguelikeCardView m_CardPrefab;
        [SerializeField] [Min(1)] private int m_DefaultCardCount = 3;
        [SerializeField] private bool m_PauseGameWhenOpen = true;

        [Header("Tween - Menu")]
        [SerializeField] [Min(0f)] private float m_MenuOpenDuration = 0.25f;
        [SerializeField] private Ease m_MenuOpenEase = Ease.OutBack;
        [SerializeField] [Min(0f)] private float m_MenuCloseDuration = 0.18f;
        [SerializeField] private Ease m_MenuCloseEase = Ease.InBack;
        [Tooltip("当外部把 Time.timeScale 设为 0 时，是否仍然播放动效（建议开启）")]
        [SerializeField] private bool m_IgnoreTimeScale = true;

        [Header("Tween - Cards")]
        [SerializeField] [Min(0f)] private float m_CardSpawnDuration = 0.22f;
        [SerializeField] private Ease m_CardSpawnEase = Ease.OutBack;
        [SerializeField] [Min(0f)] private float m_CardSpawnStagger = 0.06f;

        [Header("Tween - Select")]
        [SerializeField] [Min(0f)] private float m_SelectedScale = 1.18f;
        [SerializeField] [Min(0f)] private float m_SelectedScaleDuration = 0.14f;
        [SerializeField] private Ease m_SelectedScaleEase = Ease.OutBack;
        [SerializeField] [Min(0f)] private float m_OtherDisappearDuration = 0.12f;
        [SerializeField] private Ease m_OtherDisappearEase = Ease.InBack;
        [SerializeField] [Min(0f)] private float m_SelectedHoldDuration = 0.55f;

        private readonly List<RoguelikeCardView> m_RuntimeCards = new List<RoguelikeCardView>(4);
        private readonly List<RoguelikeCardPresentationData> m_PendingOptions = new List<RoguelikeCardPresentationData>(4);
        private Action<int> m_OnCardSelected;
        private bool m_IsOpen;
        private InputModeController m_InputModeController;
        private int m_InputBlockToken = -1;

        private Sequence m_MenuSequence;
        private Sequence m_CardSpawnSequence;
        private Sequence m_SelectionSequence;
        private bool m_IsResolvingSelection;
        private Vector3 m_MenuRootOriginalScale = Vector3.one;

        private void Awake()
        {
            if (m_MenuRoot == null)
            {
                m_MenuRoot = gameObject;
            }

            m_MenuRoot.SetActive(false);
            m_MenuRootOriginalScale = m_MenuRoot.transform.localScale;
            m_InputModeController = InputModeController.GetOrCreate();
        }

        private void OnDisable()
        {
            if (m_IsOpen)
            {
                CloseSelectionImmediate();
            }
        }

        public void ShowSelection(IReadOnlyList<RoguelikeCardPresentationData> options, Action<int> onCardSelected)
        {
            if (options == null || options.Count == 0)
            {
                return;
            }

            if (!EnsureOpenState())
            {
                return;
            }

            ClearRuntimeCards();

            m_IsResolvingSelection = false;
            m_OnCardSelected = onCardSelected;

            m_PendingOptions.Clear();
            int count = Mathf.Min(options.Count, Mathf.Max(1, m_DefaultCardCount));
            for (int i = 0; i < count; i++)
            {
                m_PendingOptions.Add(options[i]);
            }

            PlayMenuOpenThenSpawnCards();
        }

        public void CloseSelection()
        {
            if (!m_IsOpen)
            {
                return;
            }

            m_IsResolvingSelection = true;
            SetAllCardsInteractable(false);
            KillTweens();
            PlayMenuClose(() =>
            {
                m_IsOpen = false;
                m_OnCardSelected = null;
                ClearRuntimeCards();
                SetMenuRootActive(false);
                ReleaseInputBlock();
                m_IsResolvingSelection = false;
            });
        }

        private void HandleCardClicked(int index)
        {
            if (!m_IsOpen)
            {
                return;
            }

            if (m_IsResolvingSelection)
            {
                return;
            }

            if (index < 0 || index >= m_RuntimeCards.Count)
            {
                CloseSelection();
                return;
            }

            m_IsResolvingSelection = true;
            SetAllCardsInteractable(false);

            RoguelikeCardView selectedCard = m_RuntimeCards[index];
            if (selectedCard != null)
            {
                selectedCard.transform.SetAsLastSibling();
            }

            KillTweens();
            m_SelectionSequence = DOTween.Sequence()
                .SetUpdate(m_IgnoreTimeScale)
                .SetTarget(this);

            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                RoguelikeCardView card = m_RuntimeCards[i];
                if (card == null)
                {
                    continue;
                }

                if (i == index)
                {
                    m_SelectionSequence.Join(card.transform.DOScale(m_SelectedScale, m_SelectedScaleDuration).SetEase(m_SelectedScaleEase));
                }
                else
                {
                    m_SelectionSequence.Join(card.transform.DOScale(0f, m_OtherDisappearDuration).SetEase(m_OtherDisappearEase));
                }
            }

            m_SelectionSequence.AppendInterval(m_SelectedHoldDuration);
            m_SelectionSequence.AppendCallback(() =>
            {
                for (int i = 0; i < m_RuntimeCards.Count; i++)
                {
                    if (i == index)
                    {
                        continue;
                    }

                    RoguelikeCardView card = m_RuntimeCards[i];
                    if (card != null)
                    {
                        card.gameObject.SetActive(false);
                    }
                }
            });

            m_SelectionSequence.Append(m_MenuRoot.transform.DOScale(Vector3.zero, m_MenuCloseDuration).SetEase(m_MenuCloseEase));
            m_SelectionSequence.OnComplete(() =>
            {
                int pickedIndex = index;
                Action<int> callback = m_OnCardSelected;

                m_IsOpen = false;
                m_OnCardSelected = null;
                ClearRuntimeCards();
                SetMenuRootActive(false);
                ReleaseInputBlock();
                m_IsResolvingSelection = false;

                callback?.Invoke(pickedIndex);
            });
        }

        private bool EnsureOpenState()
        {
            if (m_MenuRoot == null || m_CardContainer == null || m_CardPrefab == null)
            {
                Debug.LogError("RoguelikeCardSelectionUI references are incomplete.", this);
                return false;
            }

            m_IsOpen = true;
            SetMenuRootActive(true);
            if (m_InputBlockToken < 0)
            {
                m_InputBlockToken = m_InputModeController.AcquireUiInputBlock(m_PauseGameWhenOpen);
            }

            return true;
        }

        private void ClearRuntimeCards()
        {
            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                if (m_RuntimeCards[i] != null)
                {
                    Destroy(m_RuntimeCards[i].gameObject);
                }
            }

            m_RuntimeCards.Clear();
        }

        private void PlayMenuOpenThenSpawnCards()
        {
            KillTweens();

            if (m_MenuRoot == null)
            {
                return;
            }

            m_MenuRoot.transform.localScale = Vector3.zero;
            m_MenuSequence = DOTween.Sequence()
                .SetUpdate(m_IgnoreTimeScale)
                .SetTarget(this);

            m_MenuSequence.Append(m_MenuRoot.transform.DOScale(m_MenuRootOriginalScale, m_MenuOpenDuration).SetEase(m_MenuOpenEase));
            m_MenuSequence.OnComplete(SpawnCardsAndPlaySpawnTween);
        }

        private void SpawnCardsAndPlaySpawnTween()
        {
            if (!m_IsOpen)
            {
                return;
            }

            ClearRuntimeCards();

            for (int i = 0; i < m_PendingOptions.Count; i++)
            {
                int optionIndex = i;
                RoguelikeCardView card = Instantiate(m_CardPrefab, m_CardContainer);
                RoguelikeCardPresentationData option = m_PendingOptions[i];
                card.Bind(option.Title, option.Description, option.Icon, () => HandleCardClicked(optionIndex), option.Rarity);
                card.SetInteractable(false);
                card.transform.localScale = Vector3.zero;
                m_RuntimeCards.Add(card);
            }

            m_CardSpawnSequence = DOTween.Sequence()
                .SetUpdate(m_IgnoreTimeScale)
                .SetTarget(this);

            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                RoguelikeCardView card = m_RuntimeCards[i];
                if (card == null)
                {
                    continue;
                }

                float delay = i * m_CardSpawnStagger;
                m_CardSpawnSequence.Insert(delay, card.transform.DOScale(1f, m_CardSpawnDuration).SetEase(m_CardSpawnEase));
            }

            m_CardSpawnSequence.OnComplete(() =>
            {
                if (!m_IsOpen || m_IsResolvingSelection)
                {
                    return;
                }
                SetAllCardsInteractable(true);
            });
        }

        private void PlayMenuClose(Action onClosed)
        {
            if (m_MenuRoot == null)
            {
                onClosed?.Invoke();
                return;
            }

            m_MenuSequence = DOTween.Sequence()
                .SetUpdate(m_IgnoreTimeScale)
                .SetTarget(this);

            m_MenuSequence.Append(m_MenuRoot.transform.DOScale(Vector3.zero, m_MenuCloseDuration).SetEase(m_MenuCloseEase));
            m_MenuSequence.OnComplete(() => onClosed?.Invoke());
        }

        private void CloseSelectionImmediate()
        {
            KillTweens();

            m_IsOpen = false;
            m_IsResolvingSelection = false;
            m_OnCardSelected = null;
            m_PendingOptions.Clear();

            ClearRuntimeCards();
            ReleaseInputBlock();
            SetMenuRootActive(false);
        }

        private void SetMenuRootActive(bool active)
        {
            if (m_MenuRoot == null)
            {
                return;
            }

            if (m_MenuRoot.activeSelf != active)
            {
                m_MenuRoot.SetActive(active);
            }
        }

        private void SetAllCardsInteractable(bool interactable)
        {
            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                RoguelikeCardView card = m_RuntimeCards[i];
                if (card != null)
                {
                    card.SetInteractable(interactable);
                }
            }
        }

        private void KillTweens()
        {
            if (m_MenuSequence != null && m_MenuSequence.IsActive())
            {
                m_MenuSequence.Kill(false);
            }
            m_MenuSequence = null;

            if (m_CardSpawnSequence != null && m_CardSpawnSequence.IsActive())
            {
                m_CardSpawnSequence.Kill(false);
            }
            m_CardSpawnSequence = null;

            if (m_SelectionSequence != null && m_SelectionSequence.IsActive())
            {
                m_SelectionSequence.Kill(false);
            }
            m_SelectionSequence = null;
        }

        private void ReleaseInputBlock()
        {
            if (m_InputBlockToken < 0 || m_InputModeController == null)
            {
                return;
            }

            m_InputModeController.ReleaseUiInputBlock(m_InputBlockToken);
            m_InputBlockToken = -1;
        }
    }
}
