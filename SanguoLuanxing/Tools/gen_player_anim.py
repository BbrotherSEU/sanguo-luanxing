"""生成玩家（新版立绘）的攻击帧条素材。

背景
----
玩家换成了新版立绘（char/player.jpg：青衫、长马尾、弯刀），anim/player/ 下只补了
idle / walk 两个 PNG 帧条。旧的 attack*.jpg 是另一个人物（老画风），接进游戏会在
出招瞬间换脸，所以历史上被排除 —— 结果玩家出招完全没有动作。

本脚本按现有资产管线补齐：
  1) 用 MiniMax image-01 逐姿势生成绿幕全身像；
  2) 用 Tools/align_strip.py 按脚底对齐拼成等宽帧条（Unity 端 CutoutFrames 等分切格的前提）。

身份一致性
----------
同一组内用「上一帧作为下一帧的参考图」链式生成（与 .tmp_gen/chain.py 同一手法）：
image-01 的 subject_reference 只认单张参考，链式生成能把脸/服装/体型锁在上一帧上，
四帧之间才不会各生成各的人。

用法
----
    python gen_player_anim.py attack1      # 生成一组
    python gen_player_anim.py all
"""
import base64
import io
import json
import os
import sys
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
TMP = os.path.join(PROJECT, ".tmp_gen", "player_anim")
API = "https://api.minimaxi.com/v1/image_generation"
KEY = os.environ["MINIMAX_API_KEY"]

# 新版立绘画风：偏写实的国风手游插画（不是老素材的水墨漫画风），绿幕抠像
STYLE = ("full body Chinese wuxia game character illustration, polished modern Chinese game art style, "
         "clean crisp line art with soft painterly cel shading, "
         "flat uniform pure green screen background #00FF00 filling the whole image, "
         "no cast shadow on the background, no ground plane, no scenery, no text, no watermark, "
         "no border, no frame lines, no extra figures, no extra limbs")

CHAR = ("a single young Chinese swordsman with long black hair tied back in a high ponytail with a teal "
        "ribbon, teal-blue hanfu robe with dark brown leather shoulder guard and bracers, dark trousers, "
        "brown leather boots, holding a curved dao sabre in his right hand")

PROFILE = ("seen in strict SIDE PROFILE facing LEFT, the left edge of the image is the direction he "
           "faces, his nose pointing left, never turned toward the viewer")

# 起始参考图：侧身待机（和游戏内待机/走路帧同一画风与角度）
REF_IDLE = os.path.join(ROOT, ".cursor", "projects", "c-Users-FanBin-Documents-trae-projects-r",
                        "assets", "hero_idle.jpg")

# 每组帧条：名称 → 姿势列表
GROUPS = {
    # 轻击一段：撩刀起手 → 斜劈（命中）→ 前刺 → 收势
    "attack1": [
        "standing with the dao held back low behind his right hip, the body coiled and the front knee bent",
        "the dao sweeping down and forward in a fast diagonal slash, the torso twisted hard to the left, the blade crossing his front at chest height",
        "the dao fully extended forward at waist height in a thrust, the body lunging forward with the front leg straight and the rear leg trailing",
        "the dao finished low and behind him in the follow-through, the body leaning forward and the hair and sleeves whipping forward",
    ],
    # 轻击二段：反向回旋斩
    "attack2": [
        "the dao swung back behind his right shoulder for a backhand cut, the body coiled the other way with the blade raised",
        "the dao cutting across his front in a fast rising backhand slash, the torso twisting to the right and the blade passing at chest height",
        "the dao extended forward again at shoulder height, the body lunging through the slash with the rear leg lifted",
        "the dao trailing behind him low as the body settles out of the turn, sleeves and hair settling",
    ],
    # 轻击三段：旋风终斩
    "attack3": [
        "the dao raised high above his head with both arms, the body stretched upward ready to spin",
        "the dao swinging down across his front in a wide descending arc, the body spinning and the robes flaring out",
        "the dao fully extended forward at waist height, the body spun around and lunging, the blade leading the spin",
        "the dao sweeping out to his left side at shoulder height, the body still turning with the hair whipping around",
        "the dao finished low and behind after the full turn, the body leaning forward in the follow-through",
    ],
    # 重击：跃起劈砍
    "heavy": [
        "the dao held low in both hands at his right side, crouched deeply with the knees bent and the body coiled to strike",
        "the dao hauled up high above his head with both arms fully extended, the body rising up out of the crouch",
        "the dao plunging down in front of him at waist height in a heavy overhead chop, the body slamming downward with the front knee deeply bent",
        "the dao driven straight down into the ground at his feet, the body compressed low over the blade with the hair hanging forward",
        "the dao still planted low as the body starts to rise out of the chop, sleeves settling around him",
    ],
}


def b64_file(path):
    with open(path, "rb") as f:
        return "data:image/jpeg;base64," + base64.b64encode(f.read()).decode()


def b64_img(im):
    buf = io.BytesIO()
    im.convert("RGB").save(buf, format="JPEG", quality=92)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


def gen(prompt, refdata=None, width=1024, height=1024, tries=3):
    body = {"model": "image-01", "prompt": prompt + " " + STYLE, "width": width,
            "height": height, "response_format": "url", "n": 1, "prompt_optimizer": False}
    if refdata:
        body["subject_reference"] = [{"type": "character", "image_file": refdata}]
    last = ""
    for _ in range(tries):
        try:
            req = urllib.request.Request(
                API, data=json.dumps(body).encode(),
                headers={"Authorization": "Bearer " + KEY, "Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=300) as r:
                j = json.loads(r.read().decode())
            if j.get("base_resp", {}).get("status_code") != 0:
                last = "API %s %s" % (j["base_resp"]["status_code"], j["base_resp"]["status_msg"])
                continue
            with urllib.request.urlopen(j["data"]["image_urls"][0], timeout=300) as r:
                return Image.open(io.BytesIO(r.read())).convert("RGB")
        except Exception as e:
            last = repr(e)
    raise RuntimeError("gen failed: " + last)


def build_prompt(pose):
    return ("A single full body drawing of %s, %s, %s. "
            "Show the whole body from the top of the head to the soles of the boots with the feet at the "
            "bottom of the image." % (CHAR, PROFILE, pose))


def make_group(name, anchored=False):
    """anchored=True 时每一帧都用同一张标准参考图，不链式。

    链式生成在姿势跨度大的组（重击：蹲身→举刀过顶→砸地）会逐帧漂移，
    后面的帧回到站姿、脸也变了。跨度大时改为每帧都锚定标准参考图更稳。
    """
    poses = GROUPS[name]
    d = os.path.join(TMP, name)
    os.makedirs(d, exist_ok=True)

    seed = None
    if os.path.exists(REF_IDLE):
        seed = b64_file(REF_IDLE)

    frames = []
    for i, pose in enumerate(poses):
        p = os.path.join(d, "f%d.png" % i)
        if os.path.exists(p) and os.path.getsize(p) > 10000:
            im = Image.open(p).convert("RGB")
        else:
            # 链式：上一帧当下一帧的参考，锁住脸/服装/体型
            ref = seed if (i == 0 or anchored) else b64_img(frames[-1])
            print("  %s/%d gen..." % (name, i), flush=True)
            im = gen(build_prompt(pose), ref)
            im.save(p)
        frames.append(im)
    print("  %s: %d frames" % (name, len(frames)))

    # 预览：横向拼一遍，便于肉眼核对动作连续性
    prev = os.path.join(TMP, name + "_preview.png")
    W = sum(f.width for f in frames) + 8 * (len(frames) + 1)
    H = max(f.height for f in frames)
    sheet = Image.new("RGB", (W, H), (0, 255, 0))
    x = 8
    for f in frames:
        sheet.paste(f, (x, 0))
        x += f.width + 8
    sheet.save(prev)
    print("  preview ->", prev)

    # 拼帧条
    sys.path.insert(0, HERE)
    import align_strip
    out = os.path.join(TMP, name + ".png")
    align_strip.build([os.path.join(d, "f%d.png" % i) for i in range(len(frames))], out)
    return True


if __name__ == "__main__":
    which = sys.argv[1:] or ["all"]
    if which == ["all"]:
        which = list(GROUPS.keys())
    # 姿势跨度大的组锚定标准参考图，不走链式
    ANCHORED = {"heavy"}
    for w in which:
        print("=== %s ===" % w, flush=True)
        make_group(w, anchored=(w in ANCHORED))
