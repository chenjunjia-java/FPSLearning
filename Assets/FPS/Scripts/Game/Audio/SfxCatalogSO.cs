using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.FPS.Game
{
    [CreateAssetMenu(fileName = "SfxCatalog", menuName = "FPS/Audio/SFX Catalog")]
    public sealed class SfxCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SfxKey Key;
            [HideInInspector] [FormerlySerializedAs("Key")] public string LegacyKey;
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

        private readonly Dictionary<SfxKey, int> m_IndexByKey = new Dictionary<SfxKey, int>(128);

        public bool TryGet(SfxKey key, out Entry entry)
        {
            if (key == SfxKey.None)
            {
                entry = default;
                return false;
            }

            BuildIndexIfNeeded();
            if (m_Entries != null && m_IndexByKey.TryGetValue(key, out int idx) && idx >= 0 &&
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
                MigrateLegacyKey(ref e);
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
            if (m_IndexByKey.Count == 0 && m_Entries != null && m_Entries.Length > 0)
            {
                RebuildIndex();
            }
        }

        private void RebuildIndex()
        {
            m_IndexByKey.Clear();
            if (m_Entries == null)
            {
                return;
            }

            for (int i = 0; i < m_Entries.Length; i++)
            {
                Entry entry = m_Entries[i];
                MigrateLegacyKey(ref entry);
                m_Entries[i] = entry;

                SfxKey key = entry.Key;
                if (key == SfxKey.None)
                {
                    continue;
                }

                // Later entries override earlier ones.
                m_IndexByKey[key] = i;
            }
        }

        private static void MigrateLegacyKey(ref Entry entry)
        {
            if (entry.Key != SfxKey.None || string.IsNullOrEmpty(entry.LegacyKey))
            {
                return;
            }

            if (SfxKeys.TryParse(entry.LegacyKey, out SfxKey parsedKey))
            {
                entry.Key = parsedKey;
            }
        }
    }
}

