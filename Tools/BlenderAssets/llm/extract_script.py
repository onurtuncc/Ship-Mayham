"""Write the last executed asset script of out/<Name>/v<N>.log to out/<Name>/v<N>_script.py."""
import re, sys
name, n = sys.argv[1], sys.argv[2]
log = open(rf'D:\blender\out\{name}\v{n}.log', encoding='utf-8').read()
blocks = [b for b in re.findall(r'-> execute_blender_code\n(.*?)\n<- ', log, re.S) if ('import bpy' in b or 'llm_helpers' in b)]
# last block that actually ran OK is the one followed by a success result
ok = [m.group(1) for m in re.finditer(r'-> execute_blender_code\n(.*?)\n<- \{\s*"status": "ok"', log, re.S) if ('import bpy' in m.group(1) or 'llm_helpers' in m.group(1)) and 'render.render' not in m.group(1)]
code = (ok or blocks)[-1]
code = re.split(r'"{0,3}\}?<tool_call\|>|<\|channel>|<\|tool_call>', code)[0].rstrip()
open(rf'D:\blender\out\{name}\v{n}_script.py', 'w', encoding='utf-8').write(code)
print(name, n, len(code))
