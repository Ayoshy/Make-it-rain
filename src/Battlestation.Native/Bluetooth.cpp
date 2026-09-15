#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <winioctl.h>
#include <bthsdpdef.h>
#include <bthdef.h>
#include <bluetoothapis.h>
#include <bthioctl.h>
#include <mmdeviceapi.h>
#include <devicetopology.h>
#include <functiondiscoverykeys_devpkey.h>
#include <ks.h>
#include <ksmedia.h>
#include <ksproxy.h>
#include <wrl/client.h>
#include <propidl.h>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace {
struct ComCall {
    HRESULT result=CoInitializeEx(nullptr,COINIT_MULTITHREADED);
    bool ownsApartment=SUCCEEDED(result);
    ComCall(){
        // A caller such as WPF may already own an STA on this thread.  COM is
        // usable there; only uninitialize an apartment initialized by us.
        if(result==RPC_E_CHANGED_MODE){result=S_OK;ownsApartment=false;}
    }
    ~ComCall(){if(ownsApartment)CoUninitialize();}
};

struct AudioEndpoint {
    ComPtr<IMMDevice> device;
    DWORD state=0;
};

HRESULT EnumerateTarget(IMMDeviceEnumerator* enumerator,const GUID& container,std::vector<AudioEndpoint>& result,bool& partial){
    ComPtr<IMMDeviceCollection> collection;
    HRESULT hr=enumerator->EnumAudioEndpoints(eAll,DEVICE_STATEMASK_ALL,&collection);
    if(FAILED(hr))return hr;
    UINT count=0;hr=collection->GetCount(&count);if(FAILED(hr))return hr;
    for(UINT index=0;index<count;++index){
        ComPtr<IMMDevice> endpoint;
        hr=collection->Item(index,&endpoint);if(FAILED(hr)){partial=true;continue;}
        DWORD state=0;hr=endpoint->GetState(&state);if(FAILED(hr)){partial=true;continue;}
        // A stale endpoint is not a profile that can be connected or disconnected.
        if((state&DEVICE_STATE_NOTPRESENT)!=0)continue;
        ComPtr<IPropertyStore> store;hr=endpoint->OpenPropertyStore(STGM_READ,&store);
        if(FAILED(hr)){partial=true;continue;}
        PROPVARIANT value;PropVariantInit(&value);
        hr=store->GetValue(PKEY_Device_ContainerId,&value);
        bool matches=SUCCEEDED(hr)&&value.vt==VT_CLSID&&value.puuid&&IsEqualGUID(*value.puuid,container);
        PropVariantClear(&value);
        if(FAILED(hr)){partial=true;continue;}
        if(!matches)continue;
        if(state!=DEVICE_STATE_ACTIVE&&state!=DEVICE_STATE_UNPLUGGED){partial=true;continue;}
        result.push_back({endpoint,state});
    }
    return S_OK;
}

HRESULT OpenKsControl(IMMDeviceEnumerator* enumerator,IMMDevice* endpoint,ComPtr<IKsControl>& control){
    ComPtr<IDeviceTopology> topology;
    HRESULT hr=endpoint->Activate(__uuidof(IDeviceTopology),CLSCTX_ALL,nullptr,&topology);if(FAILED(hr))return hr;
    ComPtr<IConnector> connector;hr=topology->GetConnector(0,&connector);if(FAILED(hr))return hr;
    LPWSTR connectedId=nullptr;hr=connector->GetDeviceIdConnectedTo(&connectedId);
    struct CoTaskFree{LPWSTR value;~CoTaskFree(){if(value)CoTaskMemFree(value);}} freeId{connectedId};
    if(FAILED(hr))return hr;
    if(!connectedId||!connectedId[0])return HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
    ComPtr<IMMDevice> filter;hr=enumerator->GetDevice(connectedId,&filter);if(FAILED(hr))return hr;
    return filter->Activate(__uuidof(IKsControl),CLSCTX_ALL,nullptr,&control);
}

bool SupportsGet(IKsControl* control,ULONG propertyId){
    KSPROPERTY property{KSPROPSETID_BtAudio,propertyId,KSPROPERTY_TYPE_BASICSUPPORT};
    ULONG access=0,bytes=0;
    HRESULT hr=control->KsProperty(&property,sizeof(property),&access,sizeof(access),&bytes);
    return SUCCEEDED(hr)&&bytes>=sizeof(access)&&(access&KSPROPERTY_TYPE_GET)!=0;
}

HRESULT IssueAudioCommands(const GUID& container,bool desired){
    ComPtr<IMMDeviceEnumerator> enumerator;
    HRESULT hr=CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&enumerator));if(FAILED(hr))return hr;
    std::vector<AudioEndpoint> endpoints;bool partial=false;
    hr=EnumerateTarget(enumerator.Get(),container,endpoints,partial);if(FAILED(hr))return hr;
    if(endpoints.empty())return HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
    HRESULT lastFailure=partial?HRESULT_FROM_WIN32(ERROR_PARTIAL_COPY):S_OK;
    for(auto& endpoint:endpoints){
        ComPtr<IKsControl> control;
        hr=OpenKsControl(enumerator.Get(),endpoint.device.Get(),control);
        if(FAILED(hr)){lastFailure=hr;continue;}
        bool already=(desired&&endpoint.state==DEVICE_STATE_ACTIVE)||(!desired&&endpoint.state==DEVICE_STATE_UNPLUGGED);
        ULONG propertyId=desired?0u:1u;
        if(!SupportsGet(control.Get(),propertyId)){lastFailure=HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);continue;}
        if(already)continue;
        // BtAudio property 0 reconnects and property 1 disconnects.  Both are
        // deliberately issued as a GET one-shot, as required by the KS contract.
        KSPROPERTY command{KSPROPSETID_BtAudio,desired?0u:1u,KSPROPERTY_TYPE_GET};ULONG bytes=0;
        hr=control->KsProperty(&command,sizeof(command),nullptr,0,&bytes);
        if(FAILED(hr))lastFailure=hr;
    }
    return lastFailure;
}

HRESULT ParseContainer(const wchar_t* text,GUID& value){
    if(!text||!text[0])return E_INVALIDARG;
    HRESULT hr=CLSIDFromString(text,&value);if(FAILED(hr)||IsEqualGUID(value,GUID_NULL))return E_INVALIDARG;
    return S_OK;
}

template<typename Action> HRESULT RunAudio(Action&& action){
    ComCall apartment;if(FAILED(apartment.result))return apartment.result;
    try{return action();}catch(...){return E_FAIL;}
}
}

extern "C" __declspec(dllexport) int BluetoothAudioState(const wchar_t* containerText,int* connected,int* endpointCount){
    if(!connected||!endpointCount)return E_POINTER;*connected=-1;*endpointCount=0;
    return RunAudio([&]{
        GUID container;HRESULT hr=ParseContainer(containerText,container);if(FAILED(hr))return hr;
        ComPtr<IMMDeviceEnumerator> enumerator;hr=CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&enumerator));if(FAILED(hr))return hr;
        std::vector<AudioEndpoint> endpoints;bool partial=false;hr=EnumerateTarget(enumerator.Get(),container,endpoints,partial);if(FAILED(hr))return hr;
        *endpointCount=(int)endpoints.size();if(endpoints.empty())return HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
        bool active=false,unplugged=false;
        for(auto& endpoint:endpoints){active|=endpoint.state==DEVICE_STATE_ACTIVE;unplugged|=endpoint.state==DEVICE_STATE_UNPLUGGED;}
        if(partial){*connected=-1;return HRESULT_FROM_WIN32(ERROR_PARTIAL_COPY);}
        if(active)*connected=1;else if(unplugged)*connected=0;else *connected=-1;
        return *connected<0?HRESULT_FROM_WIN32(ERROR_INVALID_STATE):S_OK;
    });
}

extern "C" __declspec(dllexport) int BluetoothAudioSetConnected(const wchar_t* containerText,int desired){
    if(desired!=0&&desired!=1)return E_INVALIDARG;
    return RunAudio([&]{GUID container;HRESULT hr=ParseContainer(containerText,container);if(FAILED(hr))return hr;return IssueAudioCommands(container,desired!=0);});
}

extern "C" __declspec(dllexport) int BluetoothDisconnectPaired(unsigned long long address){
    if(address==0||(address&0xFFFF000000000000ULL)!=0)return E_INVALIDARG;
    BLUETOOTH_FIND_RADIO_PARAMS params{sizeof(params)};HANDLE radio=nullptr;
    HBLUETOOTH_RADIO_FIND radios=BluetoothFindFirstRadio(&params,&radio);
    if(!radios)return HRESULT_FROM_WIN32(GetLastError()?GetLastError():ERROR_NOT_FOUND);
    HRESULT result=HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
    do{
        BLUETOOTH_DEVICE_INFO info{sizeof(info)};info.Address.ullLong=address;
        DWORD error=BluetoothGetDeviceInfo(radio,&info);
        if(error==ERROR_SUCCESS&&info.fAuthenticated){
            DWORD returned=0;unsigned long long target=address;
            if(DeviceIoControl(radio,IOCTL_BTH_DISCONNECT_DEVICE,&target,sizeof(target),nullptr,0,&returned,nullptr)){result=S_OK;break;}
            result=HRESULT_FROM_WIN32(GetLastError());
        }
        CloseHandle(radio);radio=nullptr;
    }while(BluetoothFindNextRadio(radios,&radio));
    if(radio)CloseHandle(radio);BluetoothFindRadioClose(radios);return result;
}
