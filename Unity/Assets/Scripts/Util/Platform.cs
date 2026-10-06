using System.Runtime.InteropServices;

namespace AgentClicker.Util
{
    /// <summary>What the game is running on, for the few things the browser build does differently.</summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void AgentClicker_SyncFileSystem();

        /// <summary>Flushes persistentDataPath to IndexedDB so saves survive a reload.</summary>
        public static void SyncFileSystem() => AgentClicker_SyncFileSystem();
#else
        public static readonly bool IsWeb = false;

        public static void SyncFileSystem() { }
#endif
    }
}
