#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <string>
#include "NativeBackground.h"
namespace {
struct Measure {std::wstring resources;bool started=false;};
HWND worker=nullptr;
BOOL CALLBACK FindWorker(HWND window,LPARAM){if(FindWindowExW(window,nullptr,L"SHELLDLL_DefView",nullptr)){auto next=FindWindowExW(nullptr,window,L"WorkerW",nullptr);if(next)worker=next;}return TRUE;}
HWND DesktopParent(){auto progman=FindWindowW(L"Progman",nullptr);if(!progman)return nullptr;DWORD_PTR result;SendMessageTimeoutW(progman,0x052C,0xD,1,SMTO_ABORTIFHUNG,1000,&result);worker=FindWindowExW(progman,nullptr,L"WorkerW",nullptr);if(!worker)EnumWindows(FindWorker,0);return worker;}
}
extern "C" __declspec(dllexport) void Initialize(void** data,void*){*data=new Measure();}
extern "C" __declspec(dllexport) void Reload(void* data,void* rm,double* max){
    *max=1;using ReadFn=const wchar_t*(__stdcall*)(void*,const wchar_t*,const wchar_t*,BOOL);
    auto read=(ReadFn)GetProcAddress(GetModuleHandleW(L"Rainmeter.dll"),"RmReadString");if(read)((Measure*)data)->resources=read(rm,L"ResourcePath",L"",TRUE);
}
extern "C" __declspec(dllexport) double Update(void* data){return ((Measure*)data)->started?1:0;}
extern "C" __declspec(dllexport) const wchar_t* GetString(void*){return L"Native Direct2D Glass";}
extern "C" __declspec(dllexport) void ExecuteBang(void* data,const wchar_t* command){
    auto m=(Measure*)data;
    if(_wcsicmp(command,L"StartBackground")==0){auto parent=DesktopParent();if(parent&&!m->resources.empty()){NativeBackground::Start(parent,m->resources);m->started=true;}}
    else if(_wcsicmp(command,L"Capture")==0)NativeBackground::Capture();
    else if(wcsncmp(command,L"Glass:",6)==0){float x,cy,my,w,ch,mh;if(swscanf_s(command+6,L"%f:%f:%f:%f:%f:%f",&x,&cy,&my,&w,&ch,&mh)==6)NativeBackground::SetGlass(x,cy,my,w,ch,mh);}
}
extern "C" __declspec(dllexport) void Finalize(void* data){if(((Measure*)data)->started)NativeBackground::Stop();delete (Measure*)data;}
