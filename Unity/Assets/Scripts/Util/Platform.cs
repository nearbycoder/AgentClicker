using System.Runtime.InteropServices;

namespace AgentClicker.Util
{
    /// <summary>What the game is running on, for the few things the browser build does differently.</summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void AgentClicker_SyncFileSystem();
        [DllImport("__Internal")] static extern void AgentClicker_SaveWhenHidden(string objectName, string methodName);
        [DllImport("__Internal")] static extern void AgentClicker_DownloadText(string fileName, string text);
        [DllImport("__Internal")] static extern void AgentClicker_PickTextFile(string objectName, string methodName);
        [DllImport("__Internal")] static extern void AgentClicker_ReuseGLIds();
        [DllImport("__Internal")] static extern void AgentClicker_ShowFullscreenButton(bool show);
        [DllImport("__Internal")] static extern int AgentClicker_CanFullscreen();

        /// <summary>Shows or hides the page's own Fullscreen button.</summary>
        public static void ShowFullscreenButton(bool show) => AgentClicker_ShowFullscreenButton(show);

        /// <summary>The browser can make the page fullscreen (iPhones can't).</summary>
        public static bool CanFullscreen => AgentClicker_CanFullscreen() != 0;

        /// <summary>WebGL objects get the lowest free id in their own table, so the engine's id tables stop growing.</summary>
        public static void ReuseGLIds() => AgentClicker_ReuseGLIds();

        /// <summary>The browser downloads <paramref name="text"/> as a file.</summary>
        public static void DownloadText(string fileName, string text) => AgentClicker_DownloadText(fileName, text);

        /// <summary>Opens the browser's file picker; the chosen file's text goes to <paramref name="methodName"/> on the named object.</summary>
        public static void PickTextFile(string objectName, string methodName) => AgentClicker_PickTextFile(objectName, methodName);

        /// <summary>Flushes persistentDataPath to IndexedDB so saves survive a reload.</summary>
        public static void SyncFileSystem() => AgentClicker_SyncFileSystem();

        /// <summary>Calls <paramref name="methodName"/> on the named object when the tab is hidden or closed.</summary>
        public static void SaveWhenHidden(string objectName, string methodName) => AgentClicker_SaveWhenHidden(objectName, methodName);
#else
        public static readonly bool IsWeb = false;

        public static void SyncFileSystem() { }

        public static void ReuseGLIds() { }

        public static void ShowFullscreenButton(bool show) { }

        public static bool CanFullscreen => false;

        public static void SaveWhenHidden(string objectName, string methodName) { }

        public static void DownloadText(string fileName, string text) { }

        public static void PickTextFile(string objectName, string methodName) { }
#endif
    }
}
