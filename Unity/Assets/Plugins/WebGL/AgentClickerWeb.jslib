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

  // Hands the player a file (the save, to keep or move to another browser or the desktop game).
  AgentClicker_DownloadText: function (fileName, text) {
    var a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([UTF8ToString(text)], { type: 'application/json' }));
    a.download = UTF8ToString(fileName);
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 10000);
  },

  // Lets the player pick a text file and sends its contents (or '' if it's too big) to the named object's method.
  AgentClicker_PickTextFile: function (objectName, methodName) {
    var target = UTF8ToString(objectName), method = UTF8ToString(methodName);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json,text/plain';
    input.onchange = function () {
      var file = input.files && input.files[0];
      if (!file) return;
      if (file.size > 5000000) { SendMessage(target, method, ''); return; }
      file.text().then(function (text) { SendMessage(target, method, text); });
    };
    input.click();
  },
});
