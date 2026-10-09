// Browser glue for the WebGL build.
mergeInto(LibraryManager.library, {
  // The engine's WebGL glue (Emscripten's GL.getNewId) takes every GL object id from one counter that only goes up, and
  // pads each object table with empty slots up to it. Unity makes a GL fence every frame, so the counter climbs about 60 a
  // second and every table grows with it: the page's JS heap gained about 15 MB an hour while the tab was visible. From
  // here on, the object types the engine deletes and recreates get the lowest free id in their own table instead, as
  // native GL drivers do. Programs and shaders keep the engine's allocator (the glue checks program ids against the
  // counter). Test tools: ?glids=engine keeps the engine's allocator; window.agentClickerGL() reports the tables.
  AgentClicker_ReuseGLIds__deps: ['$GL'],
  AgentClicker_ReuseGLIds: function () {
    var names = ['buffers', 'textures', 'framebuffers', 'renderbuffers', 'syncs', 'vaos', 'queries', 'samplers',
                 'transformFeedbacks'];
    var reuse = names.map(function (n) { return GL[n]; }).filter(function (t) { return Array.isArray(t); });
    var engine = GL.getNewId;
    var on = new URLSearchParams(location.search).get('glids') !== 'engine';
    if (on && !GL.agentClickerReuse) {
      GL.agentClickerReuse = true;
      GL.getNewId = function (table) {
        if (reuse.indexOf(table) < 0) return engine(table);
        var id = 1; // 0 means "no object" in GL
        while (id < table.length && table[id]) id++;
        if (id === table.length) table.push(null);
        return id;
      };
    }
    window.agentClickerGL = function () {
      var r = { reuse: !!GL.agentClickerReuse, counter: GL.counter, largest: 0, live: 0 };
      names.concat(['programs', 'shaders']).forEach(function (n) {
        var t = GL[n];
        if (!Array.isArray(t)) return;
        var live = 0;
        for (var i = 0; i < t.length; i++) if (t[i]) live++;
        r[n] = t.length + '/' + live;
        r.largest = Math.max(r.largest, t.length);
        r.live += live;
      });
      return r;
    };
    console.log('[GL] ' + (GL.agentClickerReuse ? 'reusing freed GL object ids' : "engine's GL id allocator (?glids=engine)"));
  },

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

  // The tab's title: the credits, and an alert while something needs the player (Core/TabTitle.cs).
  AgentClicker_SetTitle: function (title) {
    document.title = UTF8ToString(title);
  },

  // The page's own Fullscreen button (index.html) shows on the title screen only: in the game it sat on CorpOS's store.
  AgentClicker_ShowFullscreenButton: function (show) {
    if (window.agentClickerFullscreenButton) window.agentClickerFullscreenButton(!!show);
  },

  // Whether this browser can make the page fullscreen (iPhones can't).
  AgentClicker_CanFullscreen: function () {
    return document.fullscreenEnabled || document.webkitFullscreenEnabled ? 1 : 0;
  },

  // Fullscreen on or off right away. Browsers allow it for a few seconds after a tap, click or key press (transient user
  // activation), so a button that acts a frame after the press still qualifies. This is the same call as the page's own
  // Fullscreen button (unityInstance.SetFullscreen); Unity's Screen.fullScreen would wait for the next input event.
  AgentClicker_RequestFullscreen: function (on) {
    if (Module.SetFullscreen) Module.SetFullscreen(on ? 1 : 0);
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

  // A phone or tablet: the main pointer is a finger and there's no mouse or trackpad (Platform.TouchFirst). A laptop with
  // a touch screen has a fine pointer too, so it isn't one.
  AgentClicker_TouchFirst: function () {
    try { return matchMedia('(pointer: coarse)').matches && !matchMedia('(any-pointer: fine)').matches ? 1 : 0; }
    catch (e) { return 0; }
  },

  // What the page's on-screen touch controls should offer now (UI/TouchControls.cs): a small JSON object the page reads.
  AgentClicker_TouchState: function (state) {
    if (window.agentClickerTouchState) window.agentClickerTouchState(UTF8ToString(state));
  },
});
