#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2bth.h>
#pragma comment(lib,"Ws2_32.lib")

// Receive Samsung status notifications only. No commands, pairing or audio changes.
extern "C" __declspec(dllexport) int __cdecl BluetoothBudsReadStatus(unsigned long long address,unsigned char* data,int capacity){
    if(!address||(address>>48)||!data||capacity<1028)return -WSAEINVAL;
    WSADATA startup{};int error=WSAStartup(MAKEWORD(2,2),&startup);if(error)return -error;
    struct Cleanup{SOCKET socket=INVALID_SOCKET;~Cleanup(){if(socket!=INVALID_SOCKET)closesocket(socket);WSACleanup();}} call;
    call.socket=socket(AF_BTH,SOCK_STREAM,BTHPROTO_RFCOMM);if(call.socket==INVALID_SOCKET)return -WSAGetLastError();
    SOCKADDR_BTH target{};target.addressFamily=AF_BTH;target.btAddr=address;
    target.serviceClassId={0x2e73a4ad,0x332d,0x41fc,{0x90,0xe2,0x16,0xbe,0xf0,0x65,0x23,0xf2}};
    u_long nonblocking=1;if(ioctlsocket(call.socket,FIONBIO,&nonblocking))return -WSAGetLastError();
    if(connect(call.socket,reinterpret_cast<sockaddr*>(&target),sizeof(target))==SOCKET_ERROR){
        error=WSAGetLastError();if(error!=WSAEWOULDBLOCK)return -error;
        fd_set writable,failed;FD_ZERO(&writable);FD_ZERO(&failed);FD_SET(call.socket,&writable);FD_SET(call.socket,&failed);
        timeval timeout{1,500000};int ready=select(0,nullptr,&writable,&failed,&timeout);
        if(ready<=0)return ready==0?-WSAETIMEDOUT:-WSAGetLastError();
        int length=sizeof(error);if(getsockopt(call.socket,SOL_SOCKET,SO_ERROR,reinterpret_cast<char*>(&error),&length))return -WSAGetLastError();
        if(error)return -error;
    }
    int received=0;ULONGLONG deadline=GetTickCount64()+1500;
    while(received<capacity&&GetTickCount64()<deadline){
        fd_set readable;FD_ZERO(&readable);FD_SET(call.socket,&readable);timeval timeout{0,150000};
        int ready=select(0,&readable,nullptr,nullptr,&timeout);
        if(ready==SOCKET_ERROR)return -WSAGetLastError();
        if(!ready)continue;
        int count=recv(call.socket,reinterpret_cast<char*>(data)+received,capacity-received,0);
        if(count==0)break;
        if(count==SOCKET_ERROR){error=WSAGetLastError();if(error==WSAEWOULDBLOCK)continue;return -error;}
        received+=count;
    }
    return received;
}
