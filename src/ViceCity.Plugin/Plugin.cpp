#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define NETHOST_USE_AS_STATIC
#include <windows.h>
#include <string>
#include <filesystem>
#include <fstream>
#include <vector>
#include "NativeBackground.h"
#include <nethost.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
using StartFn = int(__cdecl*)(const wchar_t*);
using ReadFn = int(__cdecl*)(const wchar_t*, wchar_t*, int);
using CommandFn = void(__cdecl*)(const wchar_t*);
static HMODULE module;
static StartFn startFn;
static ReadFn readFn;
static CommandFn commandFn;
static void(__cdecl* stopFn)();
static int references;
static std::wstring failure;
struct Measure { wchar_t text[8192] = L"—"; std::wstring metric = L"summary", resources; HWND window = nullptr; bool background = false; };
static HWND worker = nullptr;
static BOOL CALLBACK FindWorker(HWND window, LPARAM) {
    if (FindWindowExW(window, nullptr, L"SHELLDLL_DefView", nullptr)) {
        auto next = FindWindowExW(nullptr, window, L"WorkerW", nullptr);
        if (next) worker = next;
    }
    return TRUE;
}
static HWND DesktopParent() {
    auto progman = FindWindowW(L"Progman", nullptr);
    if (!progman) return nullptr;
    DWORD_PTR result;
    SendMessageTimeoutW(progman, 0x052C, 0xD, 1, SMTO_ABORTIFHUNG, 1000, &result);
    worker = FindWindowExW(progman, nullptr, L"WorkerW", nullptr);
    if (!worker) EnumWindows(FindWorker, 0);
    return worker;
}
static void Capture(HWND window, const std::wstring& name) {
    RECT rect; GetClientRect(window, &rect); int width=rect.right, height=rect.bottom;
    if (width<=0 || height<=0 || width>10000 || height>5000) return;
    auto dc=GetDC(window); auto memory=CreateCompatibleDC(dc); auto bitmap=CreateCompatibleBitmap(dc,width,height);
    auto previous=SelectObject(memory,bitmap); PrintWindow(window,memory,2);
    BITMAPINFOHEADER header{};header.biSize=sizeof(header);header.biWidth=width;header.biHeight=-height;header.biPlanes=1;header.biBitCount=32;header.biCompression=BI_RGB;
    std::vector<unsigned char> pixels(size_t(width)*height*4);
    GetDIBits(memory,bitmap,0,height,pixels.data(),(BITMAPINFO*)&header,DIB_RGB_COLORS);
    wchar_t local[MAX_PATH];GetEnvironmentVariableW(L"LOCALAPPDATA",local,MAX_PATH);
    auto path=std::filesystem::path(local)/L"ViceCityRainmeter"/(L"native-"+name+L".bmp");
    BITMAPFILEHEADER file{};file.bfType=0x4d42;file.bfOffBits=sizeof(file)+sizeof(header);file.bfSize=file.bfOffBits+(DWORD)pixels.size();
    std::ofstream out(path,std::ios::binary);out.write((char*)&file,sizeof(file));out.write((char*)&header,sizeof(header));out.write((char*)pixels.data(),pixels.size());
    SelectObject(memory,previous);DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(window,dc);
    GetWindowRect(window,&rect);RECT parent{};GetWindowRect(GetParent(window),&parent);
    std::ofstream info(path.replace_extension(L".txt")); info<<"window="<<rect.left<<","<<rect.top<<","<<rect.right<<","<<rect.bottom<<" client="<<width<<","<<height<<" parent="<<parent.left<<","<<parent.top<<","<<parent.right<<","<<parent.bottom;
}
static bool Fullscreen() {
    HWND window=GetForegroundWindow(); if(!window)return false;
    DWORD process;GetWindowThreadProcessId(window,&process);if(process==GetCurrentProcessId())return false;
    wchar_t cls[128];GetClassNameW(window,cls,128);
    if(wcscmp(cls,L"Progman")==0 || wcscmp(cls,L"WorkerW")==0 || wcscmp(cls,L"Shell_TrayWnd")==0)return false;
    MONITORINFO monitor{sizeof(monitor)};GetMonitorInfoW(MonitorFromWindow(window,MONITOR_DEFAULTTONEAREST),&monitor);
    RECT r;GetWindowRect(window,&r);return r.left<=monitor.rcMonitor.left && r.top<=monitor.rcMonitor.top && r.right>=monitor.rcMonitor.right && r.bottom>=monitor.rcMonitor.bottom;
}
static bool LoadManaged() {
    if (startFn) return true;
    wchar_t own[MAX_PATH]; GetModuleFileNameW(module, own, MAX_PATH);
    auto folder = std::filesystem::path(own).parent_path() / L"ViceCityNative";
    wchar_t path[MAX_PATH]; size_t size = MAX_PATH;
    if (get_hostfxr_path(path, &size, nullptr) != 0) { failure = L".NET 10 requis"; return false; }
    auto fxr = LoadLibraryW(path);
    if (!fxr) return false;
    auto init = (hostfxr_initialize_for_runtime_config_fn)GetProcAddress(fxr, "hostfxr_initialize_for_runtime_config");
    auto get = (hostfxr_get_runtime_delegate_fn)GetProcAddress(fxr, "hostfxr_get_runtime_delegate");
    auto close = (hostfxr_close_fn)GetProcAddress(fxr, "hostfxr_close");
    hostfxr_handle context = nullptr;
    if (!init || !get || !close || init((folder / L"ViceCity.Core.runtimeconfig.json").c_str(), nullptr, &context) < 0 || !context) { failure = L"Runtime incompatible"; return false; }
    load_assembly_and_get_function_pointer_fn load = nullptr;
    int code = get(context, hdt_load_assembly_and_get_function_pointer, (void**)&load); close(context);
    if (code || !load) return false;
    auto bind = [&](const wchar_t* name, void** ptr) { return load((folder / L"ViceCity.Core.dll").c_str(), L"ViceCity.Entry, ViceCity.Core", name, UNMANAGEDCALLERSONLY_METHOD, nullptr, ptr) == 0; };
    StartFn start = nullptr;
    if (!bind(L"Start", (void**)&start) || !bind(L"ReadMetric", (void**)&readFn) || !bind(L"Stop", (void**)&stopFn) || !bind(L"Command", (void**)&commandFn)) { failure = L"Plugin indisponible"; return false; }
    startFn = start; return true;
}
extern "C" __declspec(dllexport) void Initialize(void** data, void* rm) {
    auto m = new Measure(); *data = m;
    using GetFn = void*(__stdcall*)(void*, int);
    auto get = (GetFn)GetProcAddress(GetModuleHandleW(L"Rainmeter.dll"), "RmGet");
    if (get) m->window = (HWND)get(rm,4);
    try { if (LoadManaged() && references++ == 0) { wchar_t local[MAX_PATH]; GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH); startFn((std::filesystem::path(local) / L"ViceCityRainmeter").c_str()); } }
    catch (...) { failure = L"Erreur plugin"; }
}
extern "C" __declspec(dllexport) void Reload(void* data, void* rm, double* max) {
    *max = 100;
    using ReadStringFn = const wchar_t*(__stdcall*)(void*, const wchar_t*, const wchar_t*, BOOL);
    auto read = (ReadStringFn)GetProcAddress(GetModuleHandleW(L"Rainmeter.dll"), "RmReadString");
    if (read) ((Measure*)data)->metric = read(rm, L"Metric", L"summary", TRUE);
    if (read) ((Measure*)data)->resources = read(rm,L"ResourcePath",L"",TRUE);
    if (read && commandFn) {std::wstring target=read(rm,L"TargetDate",L"",TRUE);if(!target.empty())commandFn((L"Target:"+target).c_str());}
}
extern "C" __declspec(dllexport) double Update(void* data) {
    auto m = (Measure*)data;
    if (m->metric==L"fullscreen") { bool active=Fullscreen();wcscpy_s(m->text,active?L"1":L"0");return active?1:0; }
    if (m->metric == L"mouseX" || m->metric == L"mouseY" || m->metric == L"mouseDown") {
        POINT p; GetCursorPos(&p);
        double v = m->metric == L"mouseX" ? p.x : m->metric == L"mouseY" ? p.y : (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        swprintf_s(m->text, L"%.0f", v); return v;
    }
    if (!failure.empty()) wcsncpy_s(m->text, failure.c_str(), _TRUNCATE);
    else if (readFn) readFn(m->metric.c_str(), m->text, 8192);
    return wcstod(m->text, nullptr);
}
extern "C" __declspec(dllexport) const wchar_t* GetString(void* data) { return ((Measure*)data)->text; }
extern "C" __declspec(dllexport) void ExecuteBang(void* data, const wchar_t* command) {
    auto m = (Measure*)data;
    if (_wcsicmp(command,L"Capture")==0) { if(m->background)NativeBackground::Capture();else Capture(m->window,m->metric);return; }
    if(wcsncmp(command,L"Glass:",6)==0){float x,cy,my,w,ch,mh;if(swscanf_s(command+6,L"%f:%f:%f:%f:%f:%f",&x,&cy,&my,&w,&ch,&mh)==6)NativeBackground::SetGlass(x,cy,my,w,ch,mh);return;}
    if (_wcsicmp(command,L"StartBackground")==0) { auto parent=DesktopParent();if(parent&&!m->resources.empty()){NativeBackground::Start(parent,m->resources);m->background=true;}else failure=L"Ancrage bureau indisponible";return; }
    if (commandFn) commandFn(command);
}
extern "C" __declspec(dllexport) void Finalize(void* data) { if(((Measure*)data)->background)NativeBackground::Stop();if (references > 0 && --references == 0 && stopFn) stopFn(); delete (Measure*)data; }
BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID) { if (reason == DLL_PROCESS_ATTACH) { module = h; DisableThreadLibraryCalls(h); } return TRUE; }
