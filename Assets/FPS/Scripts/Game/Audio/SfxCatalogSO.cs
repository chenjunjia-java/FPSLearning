using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    [CreateAssetMenu(fileName = "SfxCatalog", menuName = "FPS/Audio/SFX Catalog")]
    public sealed class SfxCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public AudioClip Clip;
            public AudioUtility.AudioGroups Group;

            [Range(0f, 1f)] public float Volume;
            [Tooltip("Pitch 随机范围（1=原音高）。X=Min, Y=Max")]
            public Vector2 PitchRange;

            [Range(0f, 1f)] public float SpatialBlend;
            [Min(0.01f)] public float MinDistance;

            [Min(0f)] public float CooldownSeconds;
        }

        [SerializeField] private Entry[] m_Entries;

        private readonly Dictionary<int, int> m_IndexByKeyHash = new Dictionary<int, int>(128);

        public bool TryGet(string key, out Entry entry)
        {
            if (string.IsNullOrEmpty(key))
            {
                entry = default;
                return false;
            }

            return TryGet(SfxKey.Hash(key), out entry);
        }

        public bool TryGet(int keyHash, out Entry entry)
        {
            BuildIndexIfNeeded();
            if (m_Entries != null && m_IndexByKeyHash.TryGetValue(keyHash, out int idx) && idx >= 0 &&
                idx < m_Entries.Length)
            {
                entry = m_Entries[idx];
                return entry.Clip != null;
            }

            entry = default;
            return false;
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            RebuildIndex();
            ApplyDefaults();
        }

        private void ApplyDefaults()
        {
            if (m_Entries == null)
            {
                return;
            }

            for (int i = 0; i < m_Entries.Length; i++)
            {
                Entry e = m_Entries[i];
                if (e.Volume <= 0f)
                {
                    e.Volume = 1f;
                }

                if (e.PitchRange == Vector2.zero)
                {
                    e.PitchRange = Vector2.one;
                }

                if (e.MinDistance <= 0f)
                {
                    e.MinDistance = 1f;
                }

                m_Entries[i] = e;
            }
        }

        private void BuildIndexIfNeeded()
        {
            if (m_IndexByKeyHash.Count == 0 && m_Entries != null && m_Entries.Length > 0)
            {
                RebuildIndex();
            }
        }

        private void RebuildIndex()
        {
            m_IndexByKeyHash.Clear();
            if (m_Entries == null)
            {
                return;
            }

            for (int i = 0; i < m_Entries.Length; i++)
            {
                string key = m_Entries[i].Key;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                int hash = SfxKey.Hash(key);
                // Later entries override earlier ones.
                m_IndexByKeyHash[hash] = i;
            }
        }
    }
}

