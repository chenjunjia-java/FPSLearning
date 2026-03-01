namespace Unity.FPS.GameFramework
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
