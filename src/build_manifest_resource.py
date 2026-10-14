"""Build a Win32 .res manifest for Mono mcs, which ignores /win32manifest.
Windows csc can use app.manifest directly (see COMPILAR.cmd).
"""
from pathlib import Path
import struct

def entry(data, resource_type, resource_id, flags=0, language=0):
    header = struct.pack('<IIHHHHIHHII', len(data), 32,
                         0xffff, resource_type, 0xffff, resource_id,
                         0, flags, language, 0, 0)
    return header + data + bytes((-len(data)) % 4)

root = Path(__file__).resolve().parent
manifest = (root / 'app.manifest').read_bytes()
(root / 'app.res').write_bytes(entry(b'', 0, 0) + entry(manifest, 24, 1, 0x1030, 0x0409))
