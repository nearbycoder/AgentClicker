// Browser glue for the WebGL build.
mergeInto(LibraryManager.library, {
  // persistentDataPath lives in an in-memory file system backed by IndexedDB: push writes to IndexedDB now,
  // so a save survives closing or reloading the tab.
  AgentClicker_SyncFileSystem: function () {
    FS.syncfs(false, function (err) {
      if (err) console.warn('[SaveSystem] IndexedDB sync failed: ' + err);
    });
  },
});
