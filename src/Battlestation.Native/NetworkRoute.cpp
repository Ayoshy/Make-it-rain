#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <WinSock2.h>
#include <ws2tcpip.h>
#include <Windows.h>
#include <iphlpapi.h>
#include <cstdint>
extern "C" __declspec(dllexport) int NetworkDefaultInterface(){
    const ADDRESS_FAMILY families[]={AF_INET,AF_INET6};
    for(auto family:families){
        MIB_IPFORWARD_TABLE2* table=nullptr;
        if(GetIpForwardTable2(family,&table)!=NO_ERROR)continue;
        NET_IFINDEX best=0;uint64_t metric=UINT64_MAX;
        for(ULONG i=0;i<table->NumEntries;i++){
            const auto& route=table->Table[i];
            if(route.DestinationPrefix.PrefixLength!=0||route.Loopback)continue;
            MIB_IPINTERFACE_ROW link{};InitializeIpInterfaceEntry(&link);
            link.Family=family;link.InterfaceLuid=route.InterfaceLuid;link.InterfaceIndex=route.InterfaceIndex;
            if(GetIpInterfaceEntry(&link)!=NO_ERROR||!link.Connected)continue;
            uint64_t score=uint64_t(route.Metric)+link.Metric;
            if(score<metric){metric=score;best=route.InterfaceIndex;}
        }
        FreeMibTable(table);if(best)return int(best);
    }
    return 0;
}
