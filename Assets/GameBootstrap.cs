public static class GameBootstrap
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializePersistentManagers()
    {
        CashManager.EnsureExists();
        PlayerNeedsManager.EnsureExists();
        LaptopBusinessManager.EnsureExists();
        TimeOfDayManager.EnsureExists();
    }
}
