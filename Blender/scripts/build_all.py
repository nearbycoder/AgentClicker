"""
Build every Agent Clicker model and export it to Unity.

    blender -b --factory-startup -P Blender/scripts/build_all.py               # all assets
    blender -b --factory-startup -P Blender/scripts/build_all.py -- desk mug   # selected assets
    blender -b --factory-startup -P Blender/scripts/build_all.py -- --preview  # also render previews

Each module in assets/ exposes ASSETS = {"asset_name": build_fn}. A build function
creates objects in a fresh scene and returns optional animation metadata.
"""
import importlib
import os
import pkgutil
import sys
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import aclib  # noqa: E402
import assets  # noqa: E402


def discover():
    registry = {}
    for info in pkgutil.iter_modules(assets.__path__):
        mod = importlib.import_module(f"assets.{info.name}")
        importlib.reload(mod)
        for name, fn in getattr(mod, "ASSETS", {}).items():
            registry[name] = fn
    return registry


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    want_preview = "--preview" in argv
    argv = [a for a in argv if not a.startswith("--")]
    registry = discover()
    names = argv or sorted(registry)
    failed = []
    for name in names:
        if name not in registry:
            print(f"[build_all] unknown asset '{name}'. Known: {', '.join(sorted(registry))}")
            failed.append(name)
            continue
        print(f"[build_all] building {name}")
        try:
            aclib.reset()
            meta = registry[name]()
            aclib.export(name, meta)
            if want_preview:
                import preview
                preview.render(name)
        except Exception:
            traceback.print_exc()
            failed.append(name)
    print(f"[build_all] done: {len(names) - len(failed)} ok, {len(failed)} failed {failed if failed else ''}")
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
