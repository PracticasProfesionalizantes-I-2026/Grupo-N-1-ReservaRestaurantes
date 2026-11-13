# Verifica que los nombres de tests citados en las matrices de los CU existan en tests/**/*.cs.
# Uso: python scripts/verificar_trazabilidad.py [--check]   (--check devuelve código 1 si hay nombres inexistentes)
import re, glob, os, sys
root = os.getcwd()
# Nombres reales de tests
real = set()
for f in glob.glob(os.path.join(root, "tests", "**", "*.cs"), recursive=True):
    if os.sep + "obj" + os.sep in f or os.sep + "bin" + os.sep in f:
        continue
    txt = open(f, encoding="utf-8").read()
    for m in re.finditer(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(", txt):
        real.add(m.group(1))
# Nombres documentados en matrices (celdas de tabla con backticks, identificadores con guion bajo)
doc = {}
for f in sorted(glob.glob(os.path.join(root, "Documentacion", "DocumentosNuestros", "cu-*.md"))):
    for i, line in enumerate(open(f, encoding="utf-8"), 1):
        if not line.startswith("|"):
            continue
        for name in re.findall(r"`([A-Za-z]\w*_\w+)`", line):
            doc.setdefault(name, []).append(f"{os.path.basename(f)}:{i}")
missing = {k: v for k, v in doc.items() if k not in real}
print(f"Tests reales: {len(real)}  |  nombres documentados: {len(doc)}  |  no encontrados: {len(missing)}")
for k, v in sorted(missing.items()):
    print(f"  {k}  <- {', '.join(v)}")
sys.exit(1 if missing and "--check" in sys.argv else 0)
