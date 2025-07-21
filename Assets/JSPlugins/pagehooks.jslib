mergeInto(LibraryManager.library, {

    SetUrlArg: function (key, val) {
      const url = new URL(window.location.href);
      url.searchParams.set(UTF8ToString(key), UTF8ToString(val));
      window.history.replaceState({}, '', url); // Update URL without reloading
    },
	
});