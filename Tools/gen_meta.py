"""给新素材补 .meta（Unity 需要每个资源有唯一 guid 的 importer 文件）。"""
import os
import uuid

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")

TEMPLATE = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def ensure_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(TEMPLATE.format(guid=uuid.uuid4().hex))
    return True


targets = []
for base in ("char", "anim"):
    d = os.path.join(ROOT, base)
    for root, _dirs, files in os.walk(d):
        for fn in files:
            if fn.endswith(".jpg"):
                targets.append(os.path.join(root, fn))

made = 0
for t in sorted(targets):
    if ensure_meta(t):
        made += 1
        print("meta:", os.path.relpath(t, ROOT))
print(f"共新建 {made} 个 .meta")
