using System.Collections.Generic;

namespace Unity.FPS.Roguelike.Stats
{
    public sealed class StatCache
    {
        private readonly float[] m_BaseValues;
        private readonly float[] m_AddValues;
        private readonly float[] m_MulValues;
        private readonly float[] m_FinalValues;

        public StatCache()
        {
            int count = System.Enum.GetValues(typeof(StatId)).Length;
            m_BaseValues = new float[count];
            m_AddValues = new float[count];
            m_MulValues = new float[count];
            m_FinalValues = new float[count];
            ResetWorkingBuffers();
        }

        public void SetBaseValue(StatId statId, float value)
        {
            m_BaseValues[(int)statId] = value;
        }

        public float GetFinalValue(StatId statId)
        {
            return m_FinalValues[(int)statId];
        }

        public void Rebuild(IReadOnlyList<Modifier> modifiers)
        {
            ResetWorkingBuffers();

            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    Modifier modifier = modifiers[i];
                    int index = (int)modifier.StatId;
                    if (modifier.Kind == ModifierKind.Add)
                    {
                        m_AddValues[index] += modifier.Value;
                    }
                    else
                    {
                        m_MulValues[index] *= modifier.Value;
                    }
                }
            }

            for (int i = 0; i < m_FinalValues.Length; i++)
            {
                m_FinalValues[i] = (m_BaseValues[i] + m_AddValues[i]) * m_MulValues[i];
            }
        }

        private void ResetWorkingBuffers()
        {
            for (int i = 0; i < m_AddValues.Length; i++)
            {
                m_AddValues[i] = 0f;
                m_MulValues[i] = 1f;
                m_FinalValues[i] = m_BaseValues[i];
            }
        }
    }
}
