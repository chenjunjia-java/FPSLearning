using DG.Tweening;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class CrosshairManager : MonoBehaviour
    {
        public Image CrosshairImage;
        public Sprite NullCrosshairSprite;
        public float CrosshairUpdateshrpness = 5f;

        [Header("Shoot Punch (Game Feel)")]
        [SerializeField] [Tooltip("开火时准星放大的比例，例如 0.5 表示放大 50%")]
        float m_ShootPunchAmount = 0.5f;
        [SerializeField] [Tooltip("放大后恢复的时长（秒），越小回弹越迅速")]
        float m_ShootPunchDuration = 0.08f;
        [SerializeField] [Tooltip("恢复曲线，OutCubic 回弹更干脆")]
        Ease m_ShootPunchEase = Ease.OutCubic;

        PlayerWeaponsManager m_WeaponsManager;
        WeaponController m_CurrentWeapon;
        bool m_WasPointingAtEnemy;
        RectTransform m_CrosshairRectTransform;
        CrosshairData m_CrosshairDataDefault;
        CrosshairData m_CrosshairDataTarget;
        CrosshairData m_CurrentCrosshair;
        float m_ShootPunchSize;
        Tween m_ShootPunchTween;

        void Start()
        {
            m_WeaponsManager = GameObject.FindObjectOfType<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, CrosshairManager>(m_WeaponsManager, this);

            OnWeaponChanged(m_WeaponsManager.GetActiveWeapon());

            m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;
        }

        void OnDisable()
        {
            m_ShootPunchTween?.Kill();
            m_ShootPunchTween = null;
            if (m_WeaponsManager != null)
                m_WeaponsManager.OnSwitchedToWeapon -= OnWeaponChanged;
            if (m_CurrentWeapon != null)
            {
                m_CurrentWeapon.OnShoot -= OnWeaponFired;
                m_CurrentWeapon = null;
            }
        }

        void Update()
        {
            UpdateCrosshairPointingAtEnemy(false);
            m_WasPointingAtEnemy = m_WeaponsManager.IsPointingAtEnemy;
        }

        void OnWeaponFired()
        {
            m_ShootPunchTween?.Kill();
            m_ShootPunchSize = m_ShootPunchAmount;
            m_ShootPunchTween = DOTween
                .To(() => m_ShootPunchSize, x => m_ShootPunchSize = x, 0f, m_ShootPunchDuration)
                .SetEase(m_ShootPunchEase)
                .SetTarget(this);
        }

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            if (m_CrosshairDataDefault.CrosshairSprite == null)
                return;

            if ((force || !m_WasPointingAtEnemy) && m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataTarget;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }
            else if ((force || m_WasPointingAtEnemy) && !m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataDefault;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }

            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateshrpness);

            float targetSize = m_CurrentCrosshair.CrosshairSize * (1f + m_ShootPunchSize);
            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                targetSize,
                Time.deltaTime * CrosshairUpdateshrpness) * Vector2.one;
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            if (m_CurrentWeapon != null)
            {
                m_CurrentWeapon.OnShoot -= OnWeaponFired;
                m_CurrentWeapon = null;
            }

            if (newWeapon)
            {
                m_CurrentWeapon = newWeapon;
                m_CurrentWeapon.OnShoot += OnWeaponFired;
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;
                m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
                DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                    this, CrosshairImage.gameObject);
            }
            else
            {
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            UpdateCrosshairPointingAtEnemy(true);
        }
    }
}