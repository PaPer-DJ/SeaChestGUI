#!/usr/bin/python3
import sys,base64,time
if sys.argv[1:]==['wait']: time.sleep(10)
if sys.argv[1:]==['exit3']:
 print('FAKE STDERR',file=sys.stderr)
 sys.exit(3)
for v in sys.argv[1:]: print(base64.b64encode(v.encode()).decode())
