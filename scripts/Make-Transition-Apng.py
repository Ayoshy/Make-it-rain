from pathlib import Path
import struct,zlib,sys
folder=Path(sys.argv[1])
frames=sorted(folder.glob('frame-*.png'))
def chunks(path):
    b=path.read_bytes();pos=8;result=[]
    while pos<len(b):
        n=struct.unpack('!I',b[pos:pos+4])[0];result.append((b[pos+4:pos+8],b[pos+8:pos+8+n]));pos+=n+12
    return result
def chunk(kind,data):return struct.pack('!I',len(data))+kind+data+struct.pack('!I',zlib.crc32(kind+data)&0xffffffff)
header=next(v for k,v in chunks(frames[0]) if k==b'IHDR')
w,h=struct.unpack('!II',header[:8]);out=bytearray(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'acTL',struct.pack('!II',len(frames),0)))
seq=0
for i,path in enumerate(frames):
    out.extend(chunk(b'fcTL',struct.pack('!IIIIIHHBB',seq,w,h,0,0,1,30,0,0)));seq+=1
    for k,v in chunks(path):
        if k!=b'IDAT':continue
        if i==0:out.extend(chunk(k,v))
        else:out.extend(chunk(b'fdAT',struct.pack('!I',seq)+v));seq+=1
out.extend(chunk(b'IEND',b''));(folder.parent/(folder.name+'.png')).write_bytes(out)
