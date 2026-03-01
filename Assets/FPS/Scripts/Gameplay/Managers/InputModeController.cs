using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class InputModeController : MonoBehaviour
    {
        static InputModeController s_Instance;

        [SerializeField] bool m_EnableEscapeToggle = false;

        readonly Dictionary<int, bool> m_TokenPauseMap = new Dictionary<int, bool>(8);
        int m_NextTokenId = 1;
        int m_ActiveBlockCount;
        int m_PauseRequestCount;
        int m_EscapeTokenId = -1;

        public static InputModeController GetOrCreate()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            s_Instance = FindObjectOfType<InputModeController>();
            if (s_Instance != null)
            {
                return s_Instance;
            }

            GameObject go = new GameObject(nameof(InputModeController));
            s_Instance = go.AddComponent<InputModeController>();
            return s_Instance;
        }

        public bool IsUiInputBlocked => m_ActiveBlockCount > 0;

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            ApplyCurrentMode();
        }

        void Update()
        {
            if (!m_EnableEscapeToggle || !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (m_EscapeTokenId < 0)
            {
                m_EscapeTokenId = AcquireUiInputBlock(true);
            }
            else
            {
                ReleaseUiInputBlock(m_EscapeTokenId);
                m_EscapeTokenId = -1;
            }
        }

        public int AcquireUiInputBlock(bool pauseGame)
        {
            int token = m_NextTokenId++;
            m_TokenPauseMap[token] = pauseGame;

            m_ActiveBlockCount++;
            if (pauseGame)
            {
                m_PauseRequestCount++;
            }

            ApplyCurrentMode();
            return token;
        }

        public void ReleaseUiInputBlock(int token)
        {
            if (token < 0)
            {
                return;
            }

            if (!m_TokenPauseMap.TryGetValue(token, out bool pauseGame))
            {
                return;
            }

            m_TokenPauseMap.Remove(token);
            if (m_ActiveBlockCount > 0)
            {
                m_ActiveBlockCount--;
            }

            if (pauseGame && m_PauseRequestCount > 0)
            {
                m_PauseRequestCount--;
            }

            if (m_EscapeTokenId == token)
            {
                m_EscapeTokenId = -1;
            }

            ApplyCurrentMode();
        }

        public void ApplyCurrentMode()
        {
            if (IsUiInputBlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (m_PauseRequestCount > 0)
                {
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = 1f;
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Time.timeScale = 1f;
            }
        }
    }
}
