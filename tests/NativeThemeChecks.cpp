// Deterministic rendering only; no desktop window, probes or terminal.
#ifdef THEME_BASELINE
#include "NativeBackground-baseline.cpp"
#else
#define BATTLESTATION_TESTING
#include "../src/Battlestation.Native/NativeBackground.cpp"
#endif
static long Preview(const wchar_t* resources,const wchar_t* output,int selected,double seconds,const unsigned int* colors,const float* panels,float sceneFade){
    using namespace NativeBackground;
    struct ComScope{ComScope(){CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);}~ComScope(){CoUninitialize();}} com;
    try{
        images=resources;elapsed=seconds;easedX=0;easedY=0;bass=0;middle=0;treble=0;glassOpacity=.46f;
#ifndef THEME_BASELINE
        for(int i=0;i<3;i++){SetPalette(i,colors+i*6);weights[i]=i==selected?1.f:0.f;}
        SetSceneFade(sceneFade);
#endif
        constexpr int slots=(int)(sizeof(glassRects)/sizeof(glassRects[0]));
        for(int i=0;i<slots;i++){const float* p=panels+i*4;glassRects[i]=D2D1::RectF(p[0],p[1],p[0]+p[2],p[1]+p[3]);}
        SetEnvironmentVariableW(L"LOCALAPPDATA",output);
        std::filesystem::create_directories(Output());
        ComPtr<ID2D1Factory> factory;Check(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,factory.GetAddressOf()));
        ComPtr<IWICImagingFactory> wic;Check(CoCreateInstance(CLSID_WICImagingFactory,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&wic)));
        SaveFrame(factory.Get(),wic.Get());return 0;
    }catch(HRESULT hr){return hr;}catch(...){return E_FAIL;}
}
extern "C" __declspec(dllexport) long ThemePreview(const wchar_t* resources,const wchar_t* output,int selected,double seconds,const unsigned int* colors,const float* panels){
    return Preview(resources,output,selected,seconds,colors,panels,1.f);
}
#ifndef THEME_BASELINE
extern "C" __declspec(dllexport) int WallpaperRotationChecks(){
    using namespace NativeBackground;
    wallpaperSeconds=0;bureauSeconds=0;
    AdvanceWallpaper(299,true,true,true);if(WallpaperIndex()!=0||WallpaperBlend()!=1)return 1;
    AdvanceWallpaper(60,false,true,true);AdvanceWallpaper(60,true,false,true);AdvanceWallpaper(60,true,true,false);
    if(wallpaperSeconds!=299)return 2;
    AdvanceWallpaper(1,true,true,true);if(WallpaperIndex()!=1||WallpaperBlend()!=0)return 3;
    AdvanceWallpaper(1,true,true,true);if(WallpaperBlend()!=.5f)return 4;
    AdvanceWallpaper(1,true,true,true);if(WallpaperBlend()!=1)return 5;
    AdvanceWallpaper(298,true,true,true);if(WallpaperIndex()!=2||WallpaperBlend()!=0)return 6;
    AdvanceWallpaper(300,true,true,true);if(WallpaperIndex()!=0||WallpaperBlend()!=0)return 7;
    AdvanceWallpaper(301,true,true,true,2);
    if(bureauSeconds!=301||wallpaperSeconds!=900||WallpaperIndex(bureauSeconds,2)!=1||WallpaperBlend(bureauSeconds)!=.5f)return 8;
    AdvanceWallpaper(60,false,true,true,2);AdvanceWallpaper(60,true,false,true,2);AdvanceWallpaper(60,true,true,false,2);AdvanceWallpaper(60,true,true,true,1);
    if(bureauSeconds!=301||wallpaperSeconds!=900)return 9;
    AdvanceWallpaper(299,true,true,true,2);if(WallpaperIndex(bureauSeconds,2)!=0||WallpaperBlend(bureauSeconds)!=0)return 10;
    wallpaperSeconds=0;bureauSeconds=0;return 0;
}
extern "C" __declspec(dllexport) int WallpaperPersistenceChecks(const wchar_t* directory){
    using namespace NativeBackground;
    auto file=std::filesystem::path(directory)/L"wallpaper-progress-test.txt";
    wallpaperSeconds=301.25;bureauSeconds=487.5;if(!SaveWallpaperProgress(file))return 1;
    wallpaperSeconds=0;LoadWallpaperProgress(file);
    if(wallpaperSeconds!=301.25||bureauSeconds!=487.5||WallpaperIndex()!=1||WallpaperBlend()!=.625f)return 2;
    AdvanceWallpaper(300,false,true,true);SaveWallpaperProgress(file);wallpaperSeconds=0;LoadWallpaperProgress(file);
    if(wallpaperSeconds!=301.25)return 3;
    {std::ofstream legacy(file);legacy<<"123.5";}
    LoadWallpaperProgress(file);if(wallpaperSeconds!=123.5||bureauSeconds!=0)return 4;
    wallpaperSeconds=0;bureauSeconds=0;return 0;
}
extern "C" __declspec(dllexport) long PhotoEffectsPreview(const wchar_t* resources,const wchar_t* output,int selected,int enabled,const unsigned int* colors,const float* panels){
    NativeBackground::testPhotoEffects=enabled!=0;
    auto result=Preview(resources,output,selected,15,colors,panels,1.f);
    NativeBackground::testPhotoEffects=true;return result;
}
extern "C" __declspec(dllexport) long WallpaperPreview(const wchar_t* resources,const wchar_t* output,double seconds,const unsigned int* colors,const float* panels){
    NativeBackground::wallpaperSeconds=seconds;
    auto result=Preview(resources,output,0,15,colors,panels,1.f);
    NativeBackground::wallpaperSeconds=0;return result;
}
extern "C" __declspec(dllexport) long BureauWallpaperPreview(const wchar_t* resources,const wchar_t* output,double seconds,const unsigned int* colors,const float* panels){
    NativeBackground::bureauSeconds=seconds;
    auto result=Preview(resources,output,2,15,colors,panels,1.f);
    NativeBackground::bureauSeconds=0;return result;
}
// Same frame on another monitor set: the canvas is the virtual desktop box, so
// a laptop panel or an ultrawide must render at its own size without falling
// back to the authored 2 x 2560 x 1440 canvas.
extern "C" __declspec(dllexport) long ThemePreviewCanvas(const wchar_t* resources,const wchar_t* output,int selected,double seconds,const unsigned int* colors,const float* panels,float sceneFade,int left,int top,int width,int height,int seam){
    using namespace NativeBackground;
    canvasLeft=left;canvasTop=top;canvasWidth=width;canvasHeight=height;seamX=seam;
    return Preview(resources,output,selected,seconds,colors,panels,sceneFade);
}
// Scene change: the same frame with the glass panels dissolved.
extern "C" __declspec(dllexport) long ThemePreviewFade(const wchar_t* resources,const wchar_t* output,int selected,double seconds,const unsigned int* colors,const float* panels,float sceneFade){
    return Preview(resources,output,selected,seconds,colors,panels,sceneFade);
}
#endif
