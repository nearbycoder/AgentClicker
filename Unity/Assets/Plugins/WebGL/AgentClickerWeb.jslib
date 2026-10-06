// Browser glue for the WebGL build.
mergeInto(LibraryManager.library, {
  // persistentDataPath lives in an in-memory file system backed by IndexedDB: push a finished save to IndexedDB now,
  // so it survives closing or reloading the tab. One sync at a time; a save during a sync gets one more sync after it.
  AgentClicker_SyncFileSystem: function () {
    if (Module.agentClickerSyncing) { Module.agentClickerSyncAgain = true; return; }
    Module.agentClickerSyncing = true;
    var done = function (err) {
      if (err) console.warn('[SaveSystem] IndexedDB sync failed: ' + err);
      if (Module.agentClickerSyncAgain) { Module.agentClickerSyncAgain = false; FS.syncfs(false, done); }
      else Module.agentClickerSyncing = false;
    };
    FS.syncfs(false, done);
  },

  // Save the moment the tab is hidden or closed: browsers stop running hidden tabs, and the autosave is every 15 s.
  AgentClicker_SaveWhenHidden: function (objectName, methodName) {
    var target = UTF8ToString(objectName), method = UTF8ToString(methodName);
    var save = function () { SendMessage(target, method); };
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') save(); });
    window.addEventListener('pagehide', save);
  },
});
