using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Place this in a scene (e.g. GameManager) to wire the SfxCatalogSO into SfxService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxCatalogBootstrap : MonoBehaviour
    {
        [SerializeField] private SfxCatalogSO m_Catalog;

        private void Awake()
        {
            if (m_Catalog != null)
            {
                SfxService.SetCatalog(m_Catalog);
            }
        }
    }
}

