"""크라켄 FBX 에 뼈를 넣어 다시 내보낸다. (블렌더 헤드리스)

무쌍(Warriors) 3라운드 크라켄은 Meshy 로 만든 **정지 메시**라 뼈도 애니메이션도 없었다.
이 스크립트가 같은 메시에 팔마다 뼈 사슬을 넣어 `KrakenFinal_Rigged.fbx` 를 만든다.
유니티 쪽은 `WarriorsKrakenRigSwap` 도구가 프리팹 겉모습을 갈아 끼우고,
`WarriorsKrakenLegs` 가 뼈를 돌린다. (WARRIORS.md 9장)

    blender.exe --background --factory-startup --python art/tools/warriors_kraken_rig.py -- ^
      <원본.fbx> <내보낼.fbx> <확인렌더 폴더> [뼈개수=5] [팔수제한=0(제한없음)] [확인자세각도=14]
      [끝후보비율=0.75] [후보최소간격=0.15] [같은팔로볼_표면거리=0.80]

무쌍 최종 크라켄에 쓴 값:

    ... KrakenFinal_Rigged.fbx <렌더폴더> 5 0 9 0.70 0.15 0.80
    → 후보 36개를 표면 거리로 합쳐 **팔 6개**, 팔마다 뼈 5개 + Root = 31개.
      팔마다 정점 474~591개로 고르게 나뉜다.

⚠ **웨이트도 표면으로 정한다.** 뼈 사슬까지의 직선 거리로 정하면 팔 옆에 튀어나온 **가시**가
  사슬에서 멀다는 이유로 몸통(Root)에 붙어 버린다. 그러면 팔은 움직이는데 가시 끝만 제자리에
  못 박혀 **살이 늘어나 보인다** — 화면에서 실제로 그랬다. 표면으로 재면 가시는 자기 팔에 붙는다.
  실측: 팔마다 정점 644~816개 · Root 757개(머리만).

⚠ **끝점은 표면 거리로 합친다.** 직선 거리로 가르면 안 된다 — 팔이 서로 스쳐 지나가 옆 팔 끝이
  더 가까운 일이 흔하다. 실제로 그렇게 해서 위쪽 팔 하나에 끝점이 둘 찍혔고, 한 팔이 뼈사슬
  두 개로 쪼개져 **움직일 때 살이 늘어나 보였다.** 표면을 타면 다른 팔은 몸통을 돌아가야 하므로
  같은 팔(짧다)과 다른 팔(길다)이 확실히 갈린다.

⚠ 2라운드 머리(Grape Octopus)는 이 방법이 잘 듣지 않는다. 팔이 큰 아치라 뿌리가 한 치마로
  뭉쳐 있다. 그 모델은 옛 방식(WarriorsKrakenTentacleDeformer 의 정점 변형) 그대로 둔다.

⚠ 원본 FBX 는 건드리지 않는다 — 새 파일로만 쓴다.
"""
import bpy, sys, os, bmesh, mathutils, heapq, json, math

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, DST, OUTDIR = argv[0], argv[1], argv[2]
BONES = int(argv[3]) if len(argv) > 3 else 5
LEGS_WANTED = int(argv[4]) if len(argv) > 4 else 8
POSE_DEG = float(argv[5]) if len(argv) > 5 else 14.0
TIP_CUT = float(argv[6]) if len(argv) > 6 else 0.75   # dmax 의 이 비율보다 먼 점만 다리 끝 후보
TIP_SEP = float(argv[7]) if len(argv) > 7 else 0.15   # 후보끼리 최소 직선 간격
MERGE_GEO = float(argv[8]) if len(argv) > 8 else 0.80  # 표면 거리가 이보다 가까운 후보는 같은 팔로 본다

os.makedirs(OUTDIR, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
obj.name = "KrakenMesh"

bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
co = [obj.matrix_world @ v.co for v in bm.verts]
n = len(co)
nbr = [[e.other_vert(bm.verts[i]).index for e in bm.verts[i].link_edges] for i in range(n)]
bm.free()

mins = mathutils.Vector((min(c[i] for c in co) for i in range(3)))
maxs = mathutils.Vector((max(c[i] for c in co) for i in range(3)))
center = (mins + maxs) / 2
size = max(maxs[i] - mins[i] for i in range(3))


def dijkstra(sources):
    INF = float('inf')
    d = [INF] * n
    prev = [-1] * n
    who = [-1] * n
    pq = []
    for tag, s in enumerate(sources):
        d[s] = 0.0; who[s] = tag
        heapq.heappush(pq, (0.0, s))
    while pq:
        cur, i = heapq.heappop(pq)
        if cur > d[i]: continue
        for j in nbr[i]:
            nd = cur + (co[i] - co[j]).length
            if nd < d[j]:
                d[j] = nd; prev[j] = i; who[j] = who[i]
                heapq.heappush(pq, (nd, j))
    return d, prev, who


src = min(range(n), key=lambda i: (co[i] - center).length)
dist, prev, _ = dijkstra([src])
dmax = max(dist)

# ------------------------------------------------------------
# 다리 끝
# ------------------------------------------------------------
# 1) 후보: 표면 거리가 먼 국소 최대점들
cands = []
for i in sorted(range(n), key=lambda i: -dist[i]):
    if dist[i] < TIP_CUT * dmax: break
    if all((co[i] - co[t]).length > TIP_SEP for t in cands):
        cands.append(i)

# 2) **같은 팔에 찍힌 후보를 합친다.** 직선 거리로는 못 가른다 — 팔이 서로 스쳐 지나가
#    옆 팔 끝이 더 가까운 일이 흔하다. 팔끼리는 표면을 타면 몸통을 돌아가야 하므로
#    표면 거리로 재면 같은 팔(짧다)과 다른 팔(길다)이 확실히 갈린다.
groups = []          # 각 무리의 후보 목록
for c in cands:
    dc, _, _ = dijkstra([c])
    placed = False
    for g in groups:
        if min(dc[o] for o in g) < MERGE_GEO:
            g.append(c); placed = True; break
    if not placed:
        groups.append([c])

tips = [max(g, key=lambda i: dist[i]) for g in groups]
tips.sort(key=lambda i: -dist[i])
print(f"CANDIDATES {len(cands)} -> ARMS {len(tips)} (표면 거리 {MERGE_GEO} 로 합침)")
tips = tips[:LEGS_WANTED] if LEGS_WANTED > 0 else tips
tips.sort(key=lambda i: -math.atan2(co[i].z - center.z, co[i].x - center.x))
print(f"TIPS {len(tips)} geo=" + " ".join(f"{dist[t]:.2f}" for t in tips))

# 정점마다 **표면으로 가장 가까운 팔**. 가시도 자기 팔에 붙는다.
_, _, owner = dijkstra(tips)

BASE = 0.42   # 이 비율보다 중심에 가까우면 몸통(Root). 다리 뿌리가 몸통에 잠기는 깊이다.
BLEND = 0.12  # 몸통 ↔ 다리 첫 뼈 사이에서 섞는 폭 (비율)


def chain_for(t):
    p, cur = [], t
    while cur != -1:
        p.append(cur); cur = prev[cur]
    base_geo = BASE * dist[t]
    p = [i for i in p if dist[i] >= base_geo]
    joints = []
    for s in range(BONES + 1):
        want = base_geo + (dist[t] - base_geo) * (s / BONES)
        anchor = min(p, key=lambda i: abs(dist[i] - want))
        band = [co[i] for i in range(n)
                if owner[i] == tips.index(t)
                and abs(dist[i] - want) < 0.04 * size
                and (co[i] - co[anchor]).length < 0.12 * size]
        if band:
            m = mathutils.Vector((0, 0, 0))
            for q in band: m += q
            joints.append(m / len(band))
        else:
            joints.append(co[anchor].copy())
    joints[-1] = co[t].copy()
    return joints, base_geo


legs, bases = [], []
for t in tips:
    j, b = chain_for(t)
    legs.append(j); bases.append(b)

# ------------------------------------------------------------
# 아마추어
# ------------------------------------------------------------
arm_data = bpy.data.armatures.new("KrakenRig")
arm = bpy.data.objects.new("KrakenRig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')

root = arm_data.edit_bones.new("Root")
root.head = center - mathutils.Vector((0, 0, 0.10 * size))
root.tail = center + mathutils.Vector((0, 0, 0.10 * size))

bone_names = []
for k, joints in enumerate(legs):
    parent, names = root, []
    for s in range(BONES):
        b = arm_data.edit_bones.new(f"Leg{k}_{s + 1}")
        b.head = joints[s]; b.tail = joints[s + 1]
        b.parent = parent; b.use_connect = s > 0
        parent = b; names.append(b.name)
    bone_names.append(names)
bpy.ops.object.mode_set(mode='OBJECT')

# ------------------------------------------------------------
# 웨이트 — 표면 거리로 직접
# ------------------------------------------------------------
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE')     # 뼈대에 묶기만 한다 (웨이트는 우리가 준다)

for g in list(obj.vertex_groups):
    obj.vertex_groups.remove(g)
groups = {"Root": obj.vertex_groups.new(name="Root")}
for names in bone_names:
    for name in names:
        groups[name] = obj.vertex_groups.new(name=name)

# --- 웨이트: **표면을 따라 어느 팔에 붙어 있는가**로 고른다.
#     ⚠ 직선 거리로 고르면 안 된다. 팔 옆으로 튀어나온 **가시**가 뼈 사슬에서 멀어
#       "몸통(Root)" 으로 넘어가 버린다. 그러면 팔은 움직이는데 가시 끝만 제자리에 못 박혀
#       살이 늘어나 보인다 — 화면에서 실제로 그랬다.
#       표면으로 재면 가시는 자기 팔에 붙어 있으므로 팔과 함께 움직인다.
#     ⚠ 이 방법은 팔이 제대로 갈렸을 때만 쓴다. 한 팔에 끝점이 둘 찍히면
#       그 팔이 두 사슬로 쪼개져 더 크게 늘어난다 (위의 끝점 합치기 참고).
counts = [0] * (len(legs) + 1)
for vi in range(n):
    k = owner[vi]
    tip_geo = dist[tips[k]] if 0 <= k < len(tips) else 0.0
    base_geo = bases[k] if 0 <= k < len(bases) else 0.0

    if k < 0 or tip_geo <= base_geo or dist[vi] <= base_geo:
        groups["Root"].add([vi], 1.0, 'REPLACE')
        counts[-1] += 1
        continue

    t = (dist[vi] - base_geo) / (tip_geo - base_geo)      # 0 뿌리 … 1 끝
    f = min(max(t, 0.0), 1.0) * BONES - 0.5
    i0 = int(math.floor(f))
    frac = f - i0

    pairs = []
    for idx, w in ((i0, 1.0 - frac), (i0 + 1, frac)):
        if w <= 0.001: continue
        if idx < 0:
            pairs.append(("Root", w))
        elif idx >= BONES:
            pairs.append((bone_names[k][BONES - 1], w))
        else:
            pairs.append((bone_names[k][idx], w))

    # 뿌리 근처는 몸통과 섞어 경계가 접히지 않게 한다
    if t < BLEND:
        mix = 1.0 - t / BLEND
        pairs = [(nm, w * (1.0 - mix)) for nm, w in pairs] + [("Root", mix)]

    tot = sum(w for _, w in pairs) or 1.0
    for nm, w in pairs:
        if w > 0.0005:
            groups[nm].add([vi], w / tot, 'ADD')
    counts[k] += 1

print("WEIGHTS per leg", counts[:-1], "root", counts[-1])

# ------------------------------------------------------------
# 확인 렌더 — 면 안쪽(월드 Y 축)으로 휘게
# ------------------------------------------------------------
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = scene.render.resolution_y = 640
scene.display.shading.light = 'STUDIO'
scene.display.shading.show_cavity = True
cd = bpy.data.cameras.new("Cam"); cd.type = 'ORTHO'; cd.ortho_scale = size * 1.3
cam = bpy.data.objects.new("Cam", cd); scene.collection.objects.link(cam); scene.camera = cam
cam.location = center + mathutils.Vector((0, -1, 0)) * size * 3
cam.rotation_euler = (center - cam.location).normalized().to_track_quat('-Z', 'Y').to_euler()


def shot(tag):
    scene.render.filepath = os.path.join(OUTDIR, f"{tag}.png")
    bpy.ops.render.render(write_still=True)
    print("WROTE", scene.render.filepath)


shot("rest")
# --- 디버그: 면마다 "가장 크게 잡고 있는" 그룹 색으로 칠해 렌더한다
def debug_paint():
    import colorsys
    me2 = obj.data
    me2.materials.clear()
    mats = []
    for i in range(len(legs) + 1):
        m = bpy.data.materials.new(f"dbg{i}")
        h = i / (len(legs) + 1)
        r, g, b = colorsys.hsv_to_rgb(h, 0.75 if i < len(legs) else 0.0, 0.9)
        m.diffuse_color = (r, g, b, 1)
        me2.materials.append(m)
        mats.append(m)
    gi = {g.index: g.name for g in obj.vertex_groups}
    def leg_of(v):
        best, bw = None, -1
        for ge in v.groups:
            if ge.weight > bw:
                bw = ge.weight; best = gi.get(ge.group, "Root")
        if best is None or best == "Root": return len(legs)
        return int(best[3:best.index("_")])
    per_vert = [leg_of(v) for v in me2.vertices]
    for poly in me2.polygons:
        votes = [per_vert[i] for i in poly.vertices]
        poly.material_index = max(set(votes), key=votes.count)
    scene.display.shading.color_type = 'MATERIAL'
    shot("weights")
    scene.display.shading.color_type = 'SINGLE'
    me2.materials.clear()

debug_paint()

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for k, names in enumerate(bone_names):
    for s, name in enumerate(names):
        pb = arm.pose.bones[name]
        ang = math.radians(POSE_DEG) * (1 if k % 2 == 0 else -1) * (0.5 + 0.2 * s)
        local_axis = pb.bone.matrix_local.to_3x3().inverted() @ mathutils.Vector((0, 1, 0))
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = mathutils.Quaternion(local_axis.normalized(), ang)
bpy.context.view_layer.update()
shot("posed")

for names in bone_names:
    for name in names:
        arm.pose.bones[name].rotation_quaternion = (1, 0, 0, 0)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

# ------------------------------------------------------------
# 내보내기
# ------------------------------------------------------------
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, add_leaf_bones=False,
                         bake_anim=False, path_mode='STRIP', mesh_smooth_type='FACE')
print("EXPORTED", DST, os.path.getsize(DST), "bytes")

with open(os.path.join(OUTDIR, "rig.json"), "w") as f:
    json.dump({"source": SRC, "bones_per_leg": BONES, "center": list(center), "size": size,
               "legs": [{"name": f"Leg{k}", "bones": bone_names[k], "tip": list(legs[k][-1]),
                         "joints": [list(j) for j in legs[k]]} for k in range(len(legs))]}, f, indent=1)
print("WROTE", os.path.join(OUTDIR, "rig.json"))
