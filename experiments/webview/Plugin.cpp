#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define NETHOST_USE_AS_STATIC
#include <windows.h>
#include <string>
#include <filesystem>
#include <nethost.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>

using StartFn = int(__cdecl*)(const wchar_t*);
using ReadFn = int(__cdecl*)(wchar_t*, int);
using VoidFn = void(__cdecl*)();
using DesktopFn = void(__cdecl*)(void*);
static DesktopFn desktopFn;
static HMODULE module;
static StartFn startFn;
static ReadFn readFn;
static VoidFn stopFn, refreshFn;
static VoidFn previewFn, captureFn, closePreviewFn;
static VoidFn verifyFn;
static int references;
static std::wstring failure;
struct Measure { wchar_t text[1024] = L"Initialisation…"; void* window = nullptr; bool desktop = false; };

static bool LoadManaged() {
    if (startFn) return true;
    wchar_t own[MAX_PATH]; GetModuleFileNameW(module, own, MAX_PATH);
    auto folder = std::filesystem::path(own).parent_path() / L"ViceCity";
    wchar_t fxrPath[MAX_PATH]; size_t size = MAX_PATH;
    if (get_hostfxr_path(fxrPath, &size, nullptr) != 0) { failure = L".NET 10 requis"; return false; }
    HMODULE fxr = LoadLibraryW(fxrPath);
    if (!fxr) { failure = L"hostfxr indisponible"; return false; }
    auto init = (hostfxr_initialize_for_runtime_config_fn)GetProcAddress(fxr, "hostfxr_initialize_for_runtime_config");
    auto get = (hostfxr_get_runtime_delegate_fn)GetProcAddress(fxr, "hostfxr_get_runtime_delegate");
    auto close = (hostfxr_close_fn)GetProcAddress(fxr, "hostfxr_close");
    if (!init || !get || !close) { failure = L"ABI hostfxr incompatible"; return false; }
    hostfxr_handle context = nullptr;
    int code = init((folder / L"ViceCity.Core.runtimeconfig.json").c_str(), nullptr, &context);
    if (code < 0 || !context) { failure = L"Runtime incompatible : " + std::to_wstring(code); return false; }
    load_assembly_and_get_function_pointer_fn load = nullptr;
    code = get(context, hdt_load_assembly_and_get_function_pointer, (void**)&load);
    close(context);
    if (code || !load) { failure = L"Chargement .NET impossible"; return false; }
    auto assembly = folder / L"ViceCity.Core.dll";
    auto bind = [&](const wchar_t* name, void** ptr) {
        return load(assembly.c_str(), L"ViceCity.Entry, ViceCity.Core", name, UNMANAGEDCALLERSONLY_METHOD, nullptr, ptr) == 0;
    };
    StartFn start = nullptr;
    if (!bind(L"Start", (void**)&start) || !bind(L"Read", (void**)&readFn) || !bind(L"Stop", (void**)&stopFn) || !bind(L"Refresh", (void**)&refreshFn)) {
        failure = L"Interface .NET indisponible"; return false;
    }
    startFn = start;
    bind(L"Preview", (void**)&previewFn);
    bind(L"Capture", (void**)&captureFn);
    bind(L"ClosePreview", (void**)&closePreviewFn);
    bind(L"Verify", (void**)&verifyFn);
    bind(L"Desktop", (void**)&desktopFn);
    return true;
}

extern "C" __declspec(dllexport) void Initialize(void** data, void* rm) {
    auto measure = new Measure();
    *data = measure;
    using GetFn = void*(__stdcall*)(void*, int);
    auto get = (GetFn)GetProcAddress(GetModuleHandleW(L"Rainmeter.dll"), "RmGet");
    if (get) measure->window = get(rm, 4);
    try {
        if (LoadManaged() && references++ == 0) {
            wchar_t local[MAX_PATH]; GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH);
            if (startFn((std::filesystem::path(local) / L"ViceCityRainmeter").c_str()) != 0) failure = L"Démarrage impossible";
        }
    } catch (...) { failure = L"Erreur du plugin"; }
}
extern "C" __declspec(dllexport) void Reload(void*, void*, double* max) { *max = 1; }
extern "C" __declspec(dllexport) double Update(void* data) {
    auto m = (Measure*)data;
    if (!failure.empty()) wcsncpy_s(m->text, failure.c_str(), _TRUNCATE);
    else if (readFn) readFn(m->text, 1024);
    return failure.empty() ? 1 : 0;
}
extern "C" __declspec(dllexport) const wchar_t* GetString(void* data) { return ((Measure*)data)->text; }
extern "C" __declspec(dllexport) void ExecuteBang(void* data, const wchar_t* args) {
    auto measure = (Measure*)data;
    if (desktopFn && measure->window && _wcsicmp(args, L"Desktop") == 0) { measure->desktop = true; desktopFn(measure->window); }
    if (refreshFn && _wcsicmp(args, L"Refresh") == 0) refreshFn();
    if (previewFn && _wcsicmp(args, L"Preview") == 0) previewFn();
    if (captureFn && _wcsicmp(args, L"Capture") == 0) captureFn();
    if (closePreviewFn && _wcsicmp(args, L"ClosePreview") == 0) closePreviewFn();
    if (verifyFn && _wcsicmp(args, L"Verify") == 0) verifyFn();
}
extern "C" __declspec(dllexport) void Finalize(void* data) {
    if (((Measure*)data)->desktop && closePreviewFn) closePreviewFn();
    if (references > 0 && --references == 0 && stopFn) stopFn();
    delete (Measure*)data;
}
BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) { module = h; DisableThreadLibraryCalls(h); }
    return TRUE;
}
