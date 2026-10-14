"""Compile using Mono's compiler and Microsoft's actual Windows reference API.
Usage: python3 src/build_linux.py /path/to/net48/build/.NETFramework/v4.8
Reference package: Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3.
Do not compile against Mono runtime assemblies: they expose incompatible APIs.
"""
from pathlib import Path
import subprocess
import sys
root = Path(__file__).resolve().parent.parent
refs = Path(sys.argv[1]).resolve()
names = ['mscorlib', 'System', 'System.Core', 'System.Drawing',
         'System.Windows.Forms', 'System.Management']
for name in names:
    if not (refs / (name + '.dll')).is_file():
        raise SystemExit('Missing reference: ' + name)
subprocess.run([sys.executable, str(root / 'src/build_manifest_resource.py')], check=True)
cmd = ['mcs', '-noconfig', '-nostdlib', '-target:winexe', '-optimize+',
       '-platform:anycpu', '-win32res:' + str(root / 'src/app.res'),
       '-out:' + str(root / 'SeaChestGUI.exe')]
cmd += ['-r:' + str(refs / (name + '.dll')) for name in names]
cmd += [str(root / 'src/Core.cs'), str(root / 'src/Program.cs'), str(root / 'src/WindowsDiskMetadata.cs'), str(root / 'src/GuideUI.cs'), str(root / 'src/GuideContent.cs')]
subprocess.run(cmd, check=True)
subprocess.run([sys.executable, str(root / 'tests/VerifyManifest.py'),
                str(root / 'SeaChestGUI.exe')], check=True)
