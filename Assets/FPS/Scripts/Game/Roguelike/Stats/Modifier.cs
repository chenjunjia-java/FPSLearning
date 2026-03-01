using System;

namespace Unity.FPS.Roguelike.Stats
{
    public enum ModifierKind
    {
        Add = 0,
        Mul = 1,
    }

    [Serializable]
    public struct Modifier
    {
        public StatId StatId;
        public ModifierKind Kind;
        public float Value;
        public string SourceId;

        public Modifier(StatId statId, ModifierKind kind, float value, string sourceId)
        {
            StatId = statId;
            Kind = kind;
            Value = value;
            SourceId = sourceId;
        }
    }
}
