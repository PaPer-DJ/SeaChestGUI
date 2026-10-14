"""Verify the UAC resource in the compiled EXE, not just its source XML."""
import struct
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

p=Path(sys.argv[1])
data=p.read_bytes()
u16=lambda n:struct.unpack_from('<H',data,n)[0]
u32=lambda n:struct.unpack_from('<I',data,n)[0]
pe=u32(0x3c)
assert data[pe:pe+4]==b'PE\0\0', 'Not a PE file'
optional=pe+24
section_table=optional+u16(pe+20)
sections=[]
for i in range(u16(pe+6)):
 s=section_table+i*40
 sections.append((u32(s+12),max(u32(s+8),u32(s+16)),u32(s+20)))
def offset(rva):
 for va,size,raw in sections:
  if va<=rva<va+size:return raw+rva-va
 raise AssertionError('RVA outside image sections')
directories=optional+(96 if u16(optional)==0x10b else 112)
base=offset(u32(directories+16))
def find(directory,identifier):
 at=base+directory
 for i in range(u16(at+12)+u16(at+14)):
  entry=at+16+i*8
  if u32(entry)==identifier:return u32(entry+4)
 raise AssertionError('Missing resource ID '+str(identifier))
kind=find(0,24)
assert kind & 0x80000000, 'Manifest type not a directory'
name=find(kind & 0x7fffffff,1)
assert name & 0x80000000, 'Manifest ID 1 missing'
lang=base+(name & 0x7fffffff)
assert u16(lang+12)+u16(lang+14)>0
leaf=base+u32(lang+20)
manifest=data[offset(u32(leaf)):offset(u32(leaf))+u32(leaf+4)]
root=ET.fromstring(manifest)
level=root.find('.//{urn:schemas-microsoft-com:asm.v3}requestedExecutionLevel')
assert level is not None and level.attrib.get('level')=='requireAdministrator'
assert level.attrib.get('uiAccess')=='false'
print('PASS: embedded RT_MANIFEST / ID 1 requests requireAdministrator; uiAccess=false.')
