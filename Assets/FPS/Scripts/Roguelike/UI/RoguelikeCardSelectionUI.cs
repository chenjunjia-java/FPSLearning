using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.FPS.Game;
using Unity.FPS.Roguelike.Cards;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] [Min(1f)] private float m_MenuClosePopScale = 1.08f;
        [SerializeField] [Min(0f)] private float m_MenuClosePopDuration = 0.08f;
        [SerializeField] private Ease m_MenuClosePopEase = Ease.OutQuad;
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
        [SerializeField] private SfxKey m_UiClickSfxKey = SfxKey.UIClick;

        private readonly List<RoguelikeCardView> m_RuntimeCards = new List<RoguelikeCardView>(4);
        private readonly List<RoguelikeCardPresentationData> m_PendingOptions = new List<RoguelikeCardPresentationData>(4);
        private readonly List<Behaviour> m_DisabledCardContainerLayouts = new List<Behaviour>(4);
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

            RestoreCardContainerLayout();
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
                CollapseMenuRootToZero();
                SetMenuRootActive(false);
                ReleaseInputBlock();
                RestoreCardContainerLayout();
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

            if (m_UiClickSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_UiClickSfxKey, transform.position);
            }

            if (index < 0 || index >= m_RuntimeCards.Count)
            {
                CloseSelection();
                return;
            }

            m_IsResolvingSelection = true;
            SetAllCardsInteractable(false);

            RoguelikeCardView selectedCard = m_RuntimeCards[index];
            PrepareCardContainerForSelectionAnimation();
            RectTransform selectedRect = selectedCard != null ? selectedCard.transform as RectTransform : null;
            if (selectedRect != null)
            {
                // LayoutGroup 会把子物体 anchors 强制设为左/上；这里把选中卡改回“中心锚点”，
                // 并保持当前屏幕位置不跳，确保 DOAnchorPos(Vector2.zero) 能移动到容器中心。
                ReanchorToCenterKeepingWorldPosition(selectedRect);

                // 布局冻结后置顶，保证层级正确且不会触发布局位移。
                selectedRect.SetAsLastSibling();
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
                    RectTransform cardRect = card.transform as RectTransform;
                    if (cardRect != null)
                    {
                        m_SelectionSequence.Join(cardRect.DOAnchorPos(Vector2.zero, m_SelectedScaleDuration).SetEase(m_SelectedScaleEase));
                    }
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

            AppendMenuJellyCloseTween(m_SelectionSequence);
            m_SelectionSequence.OnComplete(() =>
            {
                int pickedIndex = index;
                Action<int> callback = m_OnCardSelected;

                m_IsOpen = false;
                m_OnCardSelected = null;
                ClearRuntimeCards();
                CollapseMenuRootToZero();
                SetMenuRootActive(false);
                ReleaseInputBlock();
                RestoreCardContainerLayout();
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
            RestoreCardContainerLayout();

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

            AppendMenuJellyCloseTween(m_MenuSequence);
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
            CollapseMenuRootToZero();
            SetMenuRootActive(false);
            RestoreCardContainerLayout();
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

        private void CollapseMenuRootToZero()
        {
            if (m_MenuRoot == null)
            {
                return;
            }

            m_MenuRoot.transform.localScale = Vector3.zero;
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

        private void PrepareCardContainerForSelectionAnimation()
        {
            if (m_CardContainer == null)
            {
                return;
            }

            RestoreCardContainerLayout();
            Canvas.ForceUpdateCanvases();

            LayoutGroup[] layoutGroups = m_CardContainer.GetComponents<LayoutGroup>();
            for (int i = 0; i < layoutGroups.Length; i++)
            {
                LayoutGroup group = layoutGroups[i];
                if (group == null || !group.enabled)
                {
                    continue;
                }

                m_DisabledCardContainerLayouts.Add(group);
                group.enabled = false;
            }

            ContentSizeFitter[] fitters = m_CardContainer.GetComponents<ContentSizeFitter>();
            for (int i = 0; i < fitters.Length; i++)
            {
                ContentSizeFitter fitter = fitters[i];
                if (fitter == null || !fitter.enabled)
                {
                    continue;
                }

                m_DisabledCardContainerLayouts.Add(fitter);
                fitter.enabled = false;
            }
        }

        private void RestoreCardContainerLayout()
        {
            for (int i = 0; i < m_DisabledCardContainerLayouts.Count; i++)
            {
                Behaviour behaviour = m_DisabledCardContainerLayouts[i];
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            m_DisabledCardContainerLayouts.Clear();
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

        private static void ReanchorToCenterKeepingWorldPosition(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Vector3 worldPos = rect.position;
            Quaternion worldRot = rect.rotation;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            rect.position = worldPos;
            rect.rotation = worldRot;
        }

        private void AppendMenuJellyCloseTween(Sequence sequence)
        {
            if (sequence == null || m_MenuRoot == null)
            {
                return;
            }

            Transform menuTransform = m_MenuRoot.transform;
            sequence.Append(menuTransform.DOScale(m_MenuRootOriginalScale * m_MenuClosePopScale, m_MenuClosePopDuration).SetEase(m_MenuClosePopEase));
            sequence.Append(menuTransform.DOScale(Vector3.zero, m_MenuCloseDuration).SetEase(m_MenuCloseEase));
        }
    }
}
