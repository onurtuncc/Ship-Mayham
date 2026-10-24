"""Drive llama.cpp (OpenAI API) + blender-mcp (HTTP) to build one asset and render it.

usage: gen_asset.py <name> "<description>" [--variants N]
Outputs: D:\\blender\\out\\<name>\\v<i>.blend / v<i>.png / v<i>.log
"""
import argparse
import base64
import asyncio
import json
import os
import re
import sys
import urllib.request

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

REVIEW_ROUNDS = int(os.environ.get("REVIEW_ROUNDS", "0"))  # image review needs a vision model (mmproj)
LAST_OK = [True]
THINKING = os.environ.get("THINKING", "0") == "1"
LLAMA ="http://127.0.0.1:8080/v1/chat/completions"
MCP = "http://127.0.0.1:9191/"
OUT = r"D:\blender\out"

STYLE = """You are a 3D modeler working in Blender 5.x through the execute_blender_code tool.
Build ONE game-ready asset for a first-person chaotic co-op ship game in the art style of Sea of Thieves x Dear Passengers (see docs/art.md): stylized, painterly hand-painted look, chunky exaggerated proportions, warm cozy-chaotic mood (Unity, URP).
Workflow rules:
- Write ONE complete self-contained script per call. The driver wipes the scene before every run, so rebuild the whole asset from scratch each call; never rely on earlier calls. After a Python error, fix that exact line and resend the whole script.
- Do NOT use bpy.ops that depend on UI context (join, origin_set, select_all, mode_set, transform_apply, cursor stuff). Build with the data API: bpy.data.meshes.new + bmesh (bmesh.ops.create_cube / create_cone / create_cylinder(cap_ends=True, radius1=, radius2=, depth=, segments=) / create_uvsphere / create_icosphere, bmesh.ops.bevel), then bm.to_mesh(me), bm.free(). Position parts by translating vertices (bmesh.ops.translate / rotate) or object.location, and parent parts to one root Empty or join through a single bmesh.
- bmesh faces take BMVert objects, not indices (bm.verts.ensure_lookup_table()). Scene unit length is 'METERS'. Link objects with bpy.context.scene.collection.objects.link(obj). Materials: bpy.data.materials.new(name); mat.use_nodes=True; set nodes['Principled BSDF'] Base Color / Roughness; me.materials.append(mat).
- If unsure about an API name, call search_api_docs or get_python_api_docs once instead of guessing.
Asset rules:
- Meters, real-world scale. Chunky and friendly: slightly bulging, organic, never razor-sharp 90 degree boxes (bevel generously); interactive parts (valves, levers, buttons, gauges, handles) are clearly oversized and readable. Hand-painted palette: worn painted wood in 2-3 neighbouring tones (a darker base and lighter worn/edge planks), brushed brass and copper accents, saturated but warm colors; one simple material per color part (roughness 0.7-0.9) with clear names. Under 3000 triangles.
- Object origin at the bottom center of the asset (build the geometry so its lowest point is at z=0 and it is centered on x=0,y=0), +Y forward, Z up. Name the root object exactly as asked.
- BUILD ONLY WITH THE TESTED HELPER LIBRARY (do not write your own bmesh/primitive code; it caused floating and exploded parts). Start every script with exactly:
  import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
  API (meters, Z up, +Y front; every position is the CENTER of the part):
    mat(name, (r,g,b), rough=0.8)                      -> register a material (LINEAR colors), returns name
    box((sx,sy,sz), (x,y,z), matname, bevel=0.0, rot=(rx,ry,rz degrees))
    cyl(radius, length, (x,y,z), matname, axis='Z'|'X'|'Y', radius2=None, segments=12, bevel=0.0)   # radius2 = radius at the +axis end (cone/taper)
    sphere(radius, (x,y,z), matname, segments=12, squash=(1,1,1))
    halfcyl(radius, length, (x,y,z), matname, segments=14, bevel=0.0, rot=(rx,ry,rz degrees))   # domed/arched lid: upper half of a cylinder along X; (x,y,z) is the middle of its FLAT underside; rot=(-35,0,0) opens a lid hinged at the back
    finish('ExactAssetName')                           -> creates the single mesh object; call once at the end
  Plan first: write named constants (W, D, H, leg_x ...) at the top and DERIVE every part's size and center from them with arithmetic so touching parts overlap by ~0.01-0.02 m. Never guess a position. Each part must be placed with its center such that it touches or overlaps its neighbour (e.g. a part resting on top of another has center z = top_z + half_height - 0.01).
- Principled BSDF Base Color is LINEAR: use dark saturated values (e.g. wood (0.12,0.055,0.02)-(0.25,0.12,0.045), iron (0.03,0.03,0.035)); sRGB-looking values render washed-out and pale.
- Bevel every box/cylinder (bmesh.ops.bevel offset ~0.02, segments 2) for a chunky cartoony look. Parts must physically touch or overlap (no floating parts): derive part lengths from the positions of the parts they connect to.
- Use 3-5 clearly different materials per asset. Use one mesh object named exactly as asked (no extra root Empty).
- Do NOT render, save, or add lights/camera: the driver does that. Finish with one short sentence."""

WIPE = """import bpy
for _o in list(bpy.data.objects): bpy.data.objects.remove(_o, do_unlink=True)
for _c in (bpy.data.meshes, bpy.data.materials, bpy.data.curves): 
    for _d in list(_c): _c.remove(_d)
"""

RENDER = r'''
import bpy, math, mathutils
sc = bpy.context.scene
objs = [o for o in sc.objects if o.type == 'MESH']
mn = mathutils.Vector((1e9,)*3); mx = mathutils.Vector((-1e9,)*3)
for o in objs:
    for c in o.bound_box:
        w = o.matrix_world @ mathutils.Vector(c)
        mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1)
for o in [o for o in sc.objects if o.type in ('CAMERA', 'LIGHT') and o.name.startswith('_drv')]:
    bpy.data.objects.remove(o)
cam = bpy.data.objects.new('_drvcam', bpy.data.cameras.new('_drvcam')); sc.collection.objects.link(cam)
d = size * 1.6
az = math.radians(__AZ__)
cam.location = ctr + mathutils.Vector((d*math.sin(az)*1.1, -d*math.cos(az)*1.1, d*0.55))
cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
for n, loc, e in (('_drvkey', (3, -4, 5), 3.0), ('_drvfill', (-4, -2, 2), 1.0)):
    l = bpy.data.objects.new(n, bpy.data.lights.new(n, 'SUN')); l.data.energy = e
    l.rotation_euler = (mathutils.Vector((0,0,0)) - mathutils.Vector(loc)).to_track_quat('-Z','Y').to_euler()
    sc.collection.objects.link(l)
w = sc.world or bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.82, 0.9, 1)
w.node_tree.nodes['Background'].inputs[1].default_value = 1.0
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x = sc.render.resolution_y = 768
sc.render.filepath = r'__PNG__'
bpy.ops.render.render(write_still=True)
tris = sum(len(o.data.polygons) for o in objs)
result = {'objects': [o.name for o in objs], 'faces': tris, 'size': list(mx - mn)}
'''

FINALIZE = r'''
import bpy, bmesh
sc = bpy.context.scene
meshes = [o for o in sc.objects if o.type == 'MESH']
mats, bm = [], bmesh.new()
for o in meshes:
    me = o.data
    slot_to_idx = {}
    for i, m in enumerate(me.materials):
        if m not in mats: mats.append(m)
        slot_to_idx[i] = mats.index(m) if m in mats else 0
    tmp = bmesh.new(); tmp.from_mesh(me); tmp.transform(o.matrix_world)
    for f in tmp.faces: f.material_index = slot_to_idx.get(f.material_index, 0)
    me2 = bpy.data.meshes.new('_t'); tmp.to_mesh(me2); tmp.free()
    bm.from_mesh(me2); bpy.data.meshes.remove(me2)
for o in meshes: bpy.data.objects.remove(o, do_unlink=True)
bm.normal_update()
zs = [v.co.z for v in bm.verts]; xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
cx, cy, mz = (min(xs)+max(xs))/2, (min(ys)+max(ys))/2, min(zs)
bmesh.ops.translate(bm, vec=(-cx, -cy, -mz), verts=bm.verts)
me = bpy.data.meshes.new('__NAME__'); bm.to_mesh(me); bm.free()
for m in mats: me.materials.append(m)
ob = bpy.data.objects.new('__NAME__', me); sc.collection.objects.link(ob)
for p in me.polygons: p.use_smooth = False
for o in sc.objects: o.select_set(o is ob)
bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=r'__FBX__', use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False)
dims = ob.dimensions
result = {'final_object': ob.name, 'materials': [m.name for m in mats], 'tris': sum(len(p.vertices)-2 for p in me.polygons), 'size_m': [dims.x, dims.y, dims.z]}
'''


def parse_args(call, msgs, log):
    """Tool-call arguments can be cut off when the model hits max_tokens; tell the model instead of crashing."""
    try:
        args = json.loads(call["function"]["arguments"])
        if isinstance(args.get("code"), str):
            # the model sometimes leaks its own tool-call template tokens after the script
            code = re.split(r'"{0,3}\}?<tool_call\|>|<\|channel>|<\|tool_call>', args["code"])[0].rstrip()
            args["code"] = re.sub(r'\n"""\s*$', "\n", code)
        return args
    except json.JSONDecodeError:
        log.write("-> (truncated tool call arguments)\n")
        msgs.append({"role": "tool", "tool_call_id": call["id"], "content": "ERROR: your tool call was cut off (too long). Send a shorter, simpler complete script."})
        return None


def llama(messages, tools):
    body = json.dumps({"messages": messages, "tools": tools, "max_tokens": 12000, "temperature": 0.6, "top_p": 0.95, "top_k": 20, "min_p": 0.0,
                       # hidden reasoning cost ~20x more tokens than the answer at ~20 tok/s
                       "chat_template_kwargs": {"enable_thinking": THINKING}}).encode()
    req = urllib.request.Request(LLAMA, body, {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=1800) as r:
        msg = json.load(r)["choices"][0]["message"]
    # Gemma often answers with a ```python block instead of a tool call; run it as one.
    if not msg.get("tool_calls"):
        blocks = re.findall(r"```(?:python)?\n(.*?)```", msg.get("content") or "", re.S)
        code = max(blocks, key=len, default="")
        if "import bpy" in code:
            msg["tool_calls"] = [{"id": f"synth{abs(hash(code)) % 10**8}", "type": "function",
                                  "function": {"name": "execute_blender_code", "arguments": json.dumps({"code": code})}}]
    return msg


async def one(session, tools, name, desc, idx, outdir, instructions, feedback="", prev=""):
    user = f"Create asset '{name}': {desc}"
    if prev:
        user += f"\n\nPREVIOUS VERSION (working script):\n```python\n{prev}\n```\nREVIEWER FEEDBACK to fix in the new version (keep everything not criticised):\n{feedback}"
    msgs = [{"role": "system", "content": STYLE + "\n\n" + instructions},
            {"role": "user", "content": user}]
    log = open(os.path.join(outdir, f"v{idx}.log"), "w", encoding="utf-8")
    for _ in range(16):
        m = await asyncio.to_thread(llama, msgs, tools)
        msgs.append({k: v for k, v in m.items() if k in ("role", "content", "tool_calls")})
        calls = m.get("tool_calls") or []
        log.write(f"\n## assistant: {m.get('content')}\n")
        if not calls:
            break
        for c in calls:
            args = parse_args(c, msgs, log)
            if args is None:
                continue
            log.write(f"-> {c['function']['name']}\n{args.get('code', args)}\n")
            if "code" in args:
                args["code"] = WIPE + args["code"]
            r = await session.call_tool(c["function"]["name"], args)
            txt = "\n".join(i.text for i in r.content if i.type == "text")[:6000]
            LAST_OK[0] = '"status": "error"' not in txt
            log.write(f"<- {txt}\n")
            msgs.append({"role": "tool", "tool_call_id": c["id"], "content": txt})
    png = os.path.join(outdir, f"v{idx}.png")
    blend = os.path.join(outdir, f"v{idx}.blend")

    def text_of(res, limit):
        return "\n".join(i.text for i in res.content if i.type == "text")[:limit]

    async def run_tool_calls(calls):
        for c in calls:
            args = parse_args(c, msgs, log)
            if args is None:
                continue
            log.write(f"-> {c['function']['name']}\n{args.get('code', args)}\n")
            if "code" in args:
                args["code"] = WIPE + args["code"]
            res = await session.call_tool(c["function"]["name"], args)
            txt = text_of(res, 6000)
            LAST_OK[0] = '"status": "error"' not in txt
            log.write(f"<- {txt}\n")
            msgs.append({"role": "tool", "tool_call_id": c["id"], "content": txt})

    for rnd in range(REVIEW_ROUNDS + 1):
        views = []
        for k, az in enumerate((35, 215)):
            path = png if k == 0 else png.replace(".png", "_back.png")
            code = RENDER.replace("__PNG__", path.replace("\\", "/")).replace("__AZ__", str(az))
            res = await session.call_tool("execute_blender_code", {"code": code})
            log.write(f"render{rnd}.{k}: " + text_of(res, 800) + "\n")
            views.append(path)
        if rnd == REVIEW_ROUNDS:
            break
        content = [{"type": "text", "text": f"Here are two renders (front-right and back-left) of your result for the brief: '{desc}'. List what is missing or wrong versus the brief (missing parts, floating/misaligned parts, wrong proportions, wrong colors). If everything important is correct reply exactly: OK. Otherwise call execute_blender_code with a COMPLETE fixed script."}]
        for v in views:
            content.append({"type": "image_url", "image_url": {"url": "data:image/png;base64," + base64.b64encode(open(v, "rb").read()).decode()}})
        msgs.append({"role": "user", "content": content})
        fixed = False
        for _ in range(8):
            m = await asyncio.to_thread(llama, msgs, tools)
            msgs.append({k: v for k, v in m.items() if k in ("role", "content", "tool_calls")})
            calls = m.get("tool_calls") or []
            log.write(f"\n## review{rnd}: {m.get('content')}\n")
            if not calls:
                break
            await run_tool_calls(calls)
            fixed = True
        if not fixed:
            break
    fbx = os.path.join(outdir, f"v{idx}.fbx").replace("\\", "/")
    res = await session.call_tool("execute_blender_code", {"code": FINALIZE.replace("__NAME__", name).replace("__FBX__", fbx)})
    log.write("finalize: " + text_of(res, 1500) + "\n")
    if not LAST_OK[0]:
        log.write("FAILED: the last model script errored; v%d is not a valid result\n" % idx)
        open(os.path.join(outdir, f"v{idx}.FAILED"), "w").close()
    await session.call_tool("execute_blender_code", {"code": f"import bpy; bpy.ops.wm.save_as_mainfile(filepath=r'{blend}', copy=True)"})
    log.close()
    return png


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name"); ap.add_argument("desc"); ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--start", type=int, default=1); ap.add_argument("--feedback", default=""); ap.add_argument("--prev-script", default="")
    a = ap.parse_args()
    outdir = os.path.join(OUT, a.name); os.makedirs(outdir, exist_ok=True)
    async with streamablehttp_client(MCP) as (rd, wr, _):
        async with ClientSession(rd, wr) as s:
            init = await s.initialize()
            ts = (await s.list_tools()).tools
            keep = {"execute_blender_code", "get_python_api_docs", "search_api_docs", "get_objects_summary"}
            tools = [{"type": "function", "function": {"name": t.name, "description": t.description or "", "parameters": t.inputSchema}}
                     for t in ts if t.name in keep]
            prev = open(a.prev_script, encoding="utf-8").read() if a.prev_script else ""
            for i in range(a.start, a.start + a.variants):
                print(await one(s, tools, a.name, a.desc, i, outdir, init.instructions or "", a.feedback, prev))

asyncio.run(main())
