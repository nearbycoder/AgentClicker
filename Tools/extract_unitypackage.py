#!/usr/bin/env python3
"""Extract a .unitypackage (tar.gz of GUID folders) into a Unity project without the editor."""
import os, sys, tarfile

def main(pkg, project):
    entries = {}
    with tarfile.open(pkg, "r:gz") as tar:
        for m in tar.getmembers():
            parts = m.name.strip("./").split("/")
            if len(parts) != 2 or not m.isfile():
                continue
            guid, kind = parts
            entries.setdefault(guid, {})[kind] = tar.extractfile(m).read()
    n = 0
    for guid, e in entries.items():
        if "pathname" not in e:
            continue
        rel = e["pathname"].decode().splitlines()[0].strip()
        dest = os.path.join(project, rel)
        if "asset" in e:
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "wb") as f: f.write(e["asset"])
            n += 1
        else:
            os.makedirs(dest, exist_ok=True)
        if "asset.meta" in e:
            with open(dest + ".meta", "wb") as f: f.write(e["asset.meta"])
    print(f"extracted {n} files from {os.path.basename(pkg)}")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
