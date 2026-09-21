// Deterministic rendering only; no desktop window, probes or terminal.
#ifdef THEME_BASELINE
#include "NativeBackground-baseline.cpp"
#else
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
