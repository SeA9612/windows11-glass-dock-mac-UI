from pathlib import Path
import hashlib,struct,zipfile,io,json
root=Path('/workspace/windows-dock')
out=Path('/workspace/shared/downloads')
old=(root/'artifacts/win-x64/GlassDock.exe').read_bytes()
new=(root/'artifacts/v02-single/GlassDock.exe').read_bytes()
old_hash=hashlib.sha256(old).digest();new_hash=hashlib.sha256(new).digest()
patch=io.BytesIO();patch.write(b'GDUPD002'+old_hash+new_hash+struct.pack('<Q',len(new)))
pos=0;literal=bytearray();copies=0

def flush():
 if literal:
  patch.write(b'\x02'+struct.pack('<Q',len(literal))+literal);literal.clear()

while pos<len(new):
 key=new[pos:pos+4096]
 match=old.find(key) if len(key)==4096 else -1
 if match<0:
  literal.extend(key);pos+=len(key);continue
 flush();length=4096
 while new[pos+length:pos+length+65536] and old[match+length:match+length+65536]==new[pos+length:pos+length+65536]:
  length+=len(new[pos+length:pos+length+65536])
 lo=0;hi=min(65536,len(new)-pos-length,len(old)-match-length)
 while lo<hi:
  middle=(lo+hi+1)//2
  if old[match+length:match+length+middle]==new[pos+length:pos+length+middle]:lo=middle
  else:hi=middle-1
 length+=lo
 patch.write(b'\x01'+struct.pack('<QQ',match,length));pos+=length;copies+=1
flush();patch.write(b'\x00');data=patch.getvalue()
# Reconstruct from the final serialized patch, not intermediate instructions.
f=io.BytesIO(data);assert f.read(8)==b'GDUPD002';assert f.read(32)==old_hash;assert f.read(32)==new_hash
size=struct.unpack('<Q',f.read(8))[0];reconstructed=hashlib.sha256();total=0
while True:
 op=f.read(1)
 if op==b'\x00':break
 if op==b'\x01':
  off,n=struct.unpack('<QQ',f.read(16));block=old[off:off+n]
 elif op==b'\x02':
  n=struct.unpack('<Q',f.read(8))[0];block=f.read(n)
 else:raise ValueError(op)
 assert len(block)==n;reconstructed.update(block);total+=n
assert total==size==len(new) and reconstructed.digest()==new_hash
(root/'artifacts/GlassDock-0.2.delta').write_bytes(data)
(root/'artifacts/delta-info.json').write_text(json.dumps({'base_sha256':old_hash.hex(),'new_sha256':new_hash.hex(),'new_bytes':len(new),'patch_bytes':len(data),'copy_operations':copies,'roundtrip_verified':True},indent=2))
print((root/'artifacts/delta-info.json').read_text())
