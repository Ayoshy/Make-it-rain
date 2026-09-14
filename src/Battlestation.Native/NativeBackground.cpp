#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include "NativeBackground.h"
#include <d2d1.h>
#include <wincodec.h>
#include <wrl/client.h>
#include <filesystem>
#include <fstream>
#include <atomic>
#include <algorithm>
#include <cmath>
#include <chrono>
#include <vector>
#include <memory>
#include <mutex>
using Microsoft::WRL::ComPtr;
namespace NativeBackground {
static HANDLE thread=nullptr, stopEvent=nullptr,wakeEvent=nullptr;
static std::atomic<bool> capture=false;
static HWND parentWindow=nullptr;
static std::wstring images;
static double elapsed=0, easedX=0, easedY=0;
static std::mutex glassMutex;
static D2D1_RECT_F glassRects[16]={};
static std::atomic<bool> glassDirty=true;
static std::atomic<bool> animateBackground=true;
static std::atomic<float> glassOpacity=.46f;
static std::atomic<int> frontPanel=-1;
static float fract(double value){return float(value-std::floor(value));}
static void Check(HRESULT hr){if(FAILED(hr))throw hr;}
static LRESULT CALLBACK Proc(HWND window,UINT msg,WPARAM w,LPARAM l){return DefWindowProcW(window,msg,w,l);}
static std::filesystem::path Output(){wchar_t local[MAX_PATH];GetEnvironmentVariableW(L"LOCALAPPDATA",local,MAX_PATH);
return std::filesystem::path(local)/L"Battlestation";
}
struct Paint {
    ID2D1RenderTarget* target;
    ComPtr<ID2D1Bitmap> art,glows[3],bokeh[3],vignette;
    ComPtr<ID2D1SolidColorBrush> brush;
    ComPtr<ID2D1LinearGradientBrush> fade;
    ComPtr<ID2D1Layer> layer;
    ComPtr<ID2D1BitmapRenderTarget> frost;
    std::unique_ptr<Paint> frostPaint;
    ComPtr<ID2D1LinearGradientBrush> sheen;
    Paint(ID2D1RenderTarget* rt,IWICImagingFactory* wic,bool auxiliary=false):target(rt){
        auto load=[&](const wchar_t* file,ComPtr<ID2D1Bitmap>& bitmap){
            ComPtr<IWICBitmapDecoder> decoder;Check(wic->CreateDecoderFromFilename((std::filesystem::path(images)/file).c_str(),nullptr,GENERIC_READ,WICDecodeMetadataCacheOnLoad,&decoder));
            ComPtr<IWICBitmapFrameDecode> frame;Check(decoder->GetFrame(0,&frame));
            ComPtr<IWICFormatConverter> converter;Check(wic->CreateFormatConverter(&converter));
            Check(converter->Initialize(frame.Get(),GUID_WICPixelFormat32bppPBGRA,WICBitmapDitherTypeNone,nullptr,0,WICBitmapPaletteTypeCustom));
            if(wcscmp(file,L"jason-lucia.jpg")==0){
                UINT width,height;Check(converter->GetSize(&width,&height));std::vector<BYTE> pixels(size_t(width)*height*4);Check(converter->CopyPixels(nullptr,width*4,(UINT)pixels.size(),pixels.data()));
                for(size_t p=0;p<pixels.size();p+=4){
                    float r=pixels[p+2]/255.f,g=pixels[p+1]/255.f,b=pixels[p]/255.f;
                    float grey=.213f*r+.715f*g+.072f*b;r=(grey+(r-grey)*.86f)*.7f;g=(grey+(g-grey)*.86f)*.7f;b=(grey+(b-grey)*.86f)*.7f;
                    float lum=.3f*r+.59f*g+.11f*b,shift=lum-(.3f*35+.59f*33+.11f*100)/255;
                    r=(r+std::clamp(35/255.f+shift,0.f,1.f))*.5f;g=(g+std::clamp(33/255.f+shift,0.f,1.f))*.5f;b=(b+std::clamp(100/255.f+shift,0.f,1.f))*.5f;
                    pixels[p+2]=(BYTE)(r*255);pixels[p+1]=(BYTE)(g*255);pixels[p]=(BYTE)(b*255);
                }
                Check(rt->CreateBitmap(D2D1::SizeU(width,height),pixels.data(),width*4,D2D1::BitmapProperties(D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED)),&bitmap));
            }else Check(rt->CreateBitmapFromWicBitmap(converter.Get(),nullptr,&bitmap));
        };
        load(L"jason-lucia.jpg",art);
        load(L"vignette.png",vignette);
        for(int i=0;i<3;i++){load((L"glow"+std::to_wstring(i)+L".png").c_str(),glows[i]);load((L"bokeh"+std::to_wstring(i)+L".png").c_str(),bokeh[i]);}
        Check(rt->CreateSolidColorBrush(D2D1::ColorF(0,0,0),&brush));
        D2D1_GRADIENT_STOP stops[]={{0,D2D1::ColorF(1,1,1,1)},{.88f,D2D1::ColorF(1,1,1,1)},{1,D2D1::ColorF(1,1,1,0)}};
        ComPtr<ID2D1GradientStopCollection> collection;Check(rt->CreateGradientStopCollection(stops,3,&collection));
        Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(2714,0)),collection.Get(),&fade));
        Check(rt->CreateLayer(&layer));
        if(!auxiliary){
            D2D1_SIZE_F logical=D2D1::SizeF(5120,1440);D2D1_SIZE_U pixels=D2D1::SizeU(640,180);
            Check(rt->CreateCompatibleRenderTarget(&logical,&pixels,nullptr,D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS_NONE,&frost));
            frostPaint=std::make_unique<Paint>(frost.Get(),wic,true);
            D2D1_GRADIENT_STOP shine[]={{0,D2D1::ColorF(1,.86f,1,.17f)},{.38f,D2D1::ColorF(.9f,.65f,1,.02f)},{1,D2D1::ColorF(.7f,.6f,1,.07f)}};
            ComPtr<ID2D1GradientStopCollection> collection;Check(rt->CreateGradientStopCollection(shine,3,&collection));
            Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(1,1)),collection.Get(),&sheen));
        }
    }
    void Fill(D2D1_RECT_F rect,D2D1_COLOR_F color){brush->SetColor(color);target->FillRectangle(rect,brush.Get());}
    void Draw(){
        ComPtr<ID2D1Bitmap> frosted;
        if(frostPaint){frostPaint->Draw();Check(frost->GetBitmap(&frosted));}
        auto rt=target;rt->BeginDraw();rt->SetTransform(D2D1::Matrix3x2F::Identity());rt->Clear(D2D1::ColorF(.09f,.055f,.14f));
        rt->DrawBitmap(art.Get(),D2D1::RectF(0,0,5120,1440),.5f,D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,D2D1::RectF(2880,830,3840,1100));
        Fill(D2D1::RectF(0,0,5120,1440),D2D1::ColorF(.16f,.08f,.24f,.75f));
        float dx=float(std::sin(elapsed*.14)*20.48-easedX*15),dy=float(std::cos(elapsed*.11)*5.76-easedY*10);
        fade->SetStartPoint(D2D1::Point2F(-77+dx,0));fade->SetEndPoint(D2D1::Point2F(2637+dx,0));
        rt->PushLayer(D2D1::LayerParameters(D2D1::RectF(0,0,2800,1440),nullptr,D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,D2D1::Matrix3x2F::Identity(),1.f,fade.Get()),layer.Get());
        rt->DrawBitmap(art.Get(),D2D1::RectF(-77+dx,-43+dy,2637+dx,1483+dy));rt->PopLayer();
        Fill(D2D1::RectF(0,0,5120,1440),D2D1::ColorF(.063f,.027f,.118f,.14f));
        rt->DrawBitmap(glows[1].Get(),D2D1::RectF(2450,-150,5350,1600),.18f);
        rt->DrawBitmap(glows[2].Get(),D2D1::RectF(200,-200,2800,1600),.09f);
        double t=elapsed*.65;
        for(int i=0;i<3;i++){
            float x=float(5120*(.26+i*.28+std::sin(t*.075+i)*.05));
            rt->SetTransform(D2D1::Matrix3x2F::Rotation(float(17+std::sin(t*.06+i*.9)*8),D2D1::Point2F(x,-430)));
            rt->DrawBitmap(glows[(i+1)%3].Get(),D2D1::RectF(x-220,-900,x+220,2150),.075f);rt->SetTransform(D2D1::Matrix3x2F::Identity());
        }
        auto quiet=[](float x,float y){double a=(x-4599.5)/(779*.68),b=(y-720)/(1150*.7);return float(.25+.75*(1-std::exp(-(a*a+b*b)*1.7)));};
        for(int i=0;i<14;i++){
            int n=i*7;double phase=n*2.39996,depth=fract(n*.414213562+.12);
            float x=float((fract(n*.61803398875+.09)+std::sin(t*.07+phase)*.045)*5120);
            float y=(fract(fract(n*.754877666+.31)-t*.0025)*1.25f-.125f)*1440;
            float r=float((25+depth*65)*1.333);
            rt->DrawBitmap(bokeh[i%3].Get(),D2D1::RectF(x-r,y-r,x+r,y+r),float((.33+.25*std::sin(t*.14+phase))*.7)*quiet(x,y));
        }
        for(int i=0;i<117;i++){
            double phase=i*2.39996,depth=fract(i*.414213562+.12);
            float x=(fract(fract(i*.61803398875+.09)+t*.0012*(depth+.2)+std::sin(t*.11+phase)*.013)*1.08f-.04f)*5120;
            float y=(fract(fract(i*.754877666+.31)-t*(.007+(i%7)*.0014))*1.12f-.06f)*1440;
            float r=float((2.8+depth*8)*1.333),alpha=float((.35+depth*.6)*(.5+.5*std::pow((std::sin(t*.7+phase)+1)/2,2))*.7)*quiet(x,y);
            rt->DrawBitmap(glows[i%3].Get(),D2D1::RectF(x-r,y-r,x+r,y+r),alpha);
        }
        for(int i=0;i<3;i++){
            float phase=fract((t+i*9+3)/(22+i*5));if(phase>.34f)continue;
            float progress=phase/.34f,x=(-.15f+progress*1.4f)*5120,y=(.86f-i*.25f-progress*.18f)*1440,len=(145+i*40)*1.333f;
            brush->SetColor(D2D1::ColorF(.95f,.6f,.85f,float(std::sin(progress*3.1415926))*.22f*quiet(x,y)));
            rt->DrawLine(D2D1::Point2F(x-len,y+len*.16f),D2D1::Point2F(x,y),brush.Get(),2);
            rt->DrawBitmap(glows[i].Get(),D2D1::RectF(x-12,y-12,x+12,y+12),.5f*quiet(x,y));
        }
        rt->DrawBitmap(vignette.Get(),D2D1::RectF(0,0,5120,1440));
        if(frosted){
            D2D1_RECT_F rects[16];{std::lock_guard<std::mutex> lock(glassMutex);std::copy(std::begin(glassRects),std::end(glassRects),rects);}
            ComPtr<ID2D1Factory> factory;rt->GetFactory(&factory);
            int front=frontPanel.load();
            for(int order=0;order<=16;order++){
                int index=order==16?front:order;
                if(index<0||(order<16&&index==front))continue;
                auto r=rects[index];
                if(r.right<=r.left||r.bottom<=r.top)continue;
                bool clearPopup=index==8;
                float radius=clearPopup?22:index==2?34:index>=3?24:24*(r.right-r.left)/779;
                for(int shadow=4;shadow>=1;shadow--){float d=shadow*3.f;brush->SetColor(D2D1::ColorF(.015f,.005f,.03f,.035f));rt->FillRoundedRectangle(D2D1::RoundedRect(D2D1::RectF(r.left-d,r.top+4,r.right+d,r.bottom+d+4),radius+d,radius+d),brush.Get());}
                ComPtr<ID2D1RoundedRectangleGeometry> mask;Check(factory->CreateRoundedRectangleGeometry(D2D1::RoundedRect(r,radius,radius),&mask));
                rt->PushLayer(D2D1::LayerParameters(r,mask.Get()),layer.Get());
                float shift=float(easedX*3);
                rt->DrawBitmap(frosted.Get(),r,clearPopup?.22f:1.f,D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,D2D1::RectF(r.left-4+shift,r.top-3,r.right+4+shift,r.bottom+3));
                Fill(r,D2D1::ColorF(.06f,.022f,.10f,glassOpacity.load()*(clearPopup?.08f/.46f:1.f)));
                sheen->SetStartPoint(D2D1::Point2F(r.left,r.top));sheen->SetEndPoint(D2D1::Point2F(r.right,r.bottom));
                if(!clearPopup)rt->FillRectangle(r,sheen.Get());rt->PopLayer();
                if(clearPopup)continue; // The WPF rim shares the exact popup shape.
                brush->SetColor(D2D1::ColorF(.95f,.77f,1,.25f));rt->DrawRoundedRectangle(D2D1::RoundedRect(r,radius,radius),brush.Get(),1);
                rt->PushAxisAlignedClip(D2D1::RectF(r.left,r.top,r.right,r.top+25),D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
                brush->SetColor(D2D1::ColorF(1,.92f,1,.34f));rt->DrawRoundedRectangle(D2D1::RoundedRect(D2D1::RectF(r.left+1,r.top+1,r.right-1,r.bottom-1),radius-1,radius-1),brush.Get(),1);rt->PopAxisAlignedClip();
            }
        }
        Check(rt->EndDraw());
    }
};
static bool Fullscreen(){
    auto window=GetForegroundWindow();DWORD process=0;GetWindowThreadProcessId(window,&process);if(!window||process==GetCurrentProcessId())return false;
    wchar_t cls[128];GetClassNameW(window,cls,128);if(wcscmp(cls,L"Progman")==0||wcscmp(cls,L"WorkerW")==0)return false;
    RECT r;GetWindowRect(window,&r);MONITORINFO m{sizeof(m)};GetMonitorInfoW(MonitorFromWindow(window,MONITOR_DEFAULTTONEAREST),&m);
    return r.left<=m.rcMonitor.left && r.top<=m.rcMonitor.top && r.right>=m.rcMonitor.right && r.bottom>=m.rcMonitor.bottom;
}
static void SaveFrame(ID2D1Factory* factory,IWICImagingFactory* wic){
    ComPtr<IWICBitmap> bitmap;Check(wic->CreateBitmap(5120,1440,GUID_WICPixelFormat32bppPBGRA,WICBitmapCacheOnLoad,&bitmap));
    ComPtr<ID2D1RenderTarget> target;Check(factory->CreateWicBitmapRenderTarget(bitmap.Get(),D2D1::RenderTargetProperties(),&target));
    Paint paint(target.Get(),wic);paint.Draw();
    ComPtr<IWICStream> stream;Check(wic->CreateStream(&stream));Check(stream->InitializeFromFilename((Output()/L"native-background-frame.png").c_str(),GENERIC_WRITE));
    ComPtr<IWICBitmapEncoder> encoder;Check(wic->CreateEncoder(GUID_ContainerFormatPng,nullptr,&encoder));Check(encoder->Initialize(stream.Get(),WICBitmapEncoderNoCache));
    ComPtr<IWICBitmapFrameEncode> frame;ComPtr<IPropertyBag2> bag;Check(encoder->CreateNewFrame(&frame,&bag));Check(frame->Initialize(bag.Get()));Check(frame->SetSize(5120,1440));
    WICPixelFormatGUID format=GUID_WICPixelFormat32bppPBGRA;Check(frame->SetPixelFormat(&format));Check(frame->WriteSource(bitmap.Get(),nullptr));Check(frame->Commit());Check(encoder->Commit());
}
static DWORD WINAPI Run(void*){
    CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
    HWND window=nullptr;
    const char* stage="register window class";
    try{
        WNDCLASSW cls{};cls.lpfnWndProc=Proc;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"BattlestationBackground";RegisterClassW(&cls);
        stage="create layered child window";SetLastError(ERROR_SUCCESS);
        window=CreateWindowExW(WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE|WS_EX_LAYERED,cls.lpszClassName,L"Battlestation background",WS_CHILD|WS_VISIBLE,0,0,5120,1440,parentWindow,nullptr,cls.hInstance,nullptr);
        if(!window){auto error=GetLastError();throw error?HRESULT_FROM_WIN32(error):E_FAIL;}
        stage="configure layered window";
        if(!SetLayeredWindowAttributes(window,0,255,LWA_ALPHA)){auto error=GetLastError();throw error?HRESULT_FROM_WIN32(error):E_FAIL;}
        stage="create Direct2D resources";
        ComPtr<ID2D1Factory> factory;Check(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,factory.GetAddressOf()));
        ComPtr<IWICImagingFactory> wic;Check(CoCreateInstance(CLSID_WICImagingFactory,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&wic)));
        ComPtr<ID2D1HwndRenderTarget> target;Check(factory->CreateHwndRenderTarget(D2D1::RenderTargetProperties(),D2D1::HwndRenderTargetProperties(window,D2D1::SizeU(5120,1440),D2D1_PRESENT_OPTIONS_IMMEDIATELY),&target));
        stage="load artwork";Paint paint(target.Get(),wic.Get());stage="render frames";
        auto last=std::chrono::steady_clock::now();bool first=true;
        while(WaitForSingleObject(stopEvent,0)!=WAIT_OBJECT_0){
            MSG msg;while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){TranslateMessage(&msg);DispatchMessageW(&msg);}
            bool paused=Fullscreen()||!animateBackground.load();auto now=std::chrono::steady_clock::now();double dt=std::min(.1,std::chrono::duration<double>(now-last).count());last=now;
            if(!paused||first||glassDirty.exchange(false)){if(!paused)elapsed+=dt;POINT p;GetCursorPos(&p);double blend=1-std::exp(-dt*3);easedX+=((p.x/5120.0*2-1)-easedX)*blend;easedY+=((p.y/1440.0*2-1)-easedY)*blend;paint.Draw();first=false;}
            if(capture.exchange(false)){try{SaveFrame(factory.Get(),wic.Get());}catch(...){std::ofstream log(Output()/L"native-capture-error.txt");log<<"Capture failed; renderer remains running";}}
            static int frames=0;if(++frames%30==0||paused){std::ofstream state(Output()/L"native-renderer-state.json");state<<"{\"pid\":"<<GetCurrentProcessId()<<",\"paused\":"<<(paused?"true":"false")<<",\"elapsed\":"<<elapsed<<",\"hwnd\":"<<(uintptr_t)window<<",\"parent\":"<<(uintptr_t)parentWindow<<",\"width\":5120,\"height\":1440}";}
            HANDLE events[]={stopEvent,wakeEvent};WaitForMultipleObjects(2,events,FALSE,paused?500:33);
        }
    }catch(HRESULT hr){std::ofstream log(Output()/L"native-renderer-error.txt");log<<stage<<": HRESULT "<<std::hex<<hr;}catch(...){std::ofstream log(Output()/L"native-renderer-error.txt");log<<stage<<": native renderer failed";}
    if(window)DestroyWindow(window);UnregisterClassW(L"BattlestationBackground",GetModuleHandleW(nullptr));CoUninitialize();return 0;
}
void Start(HWND parent,const std::wstring& resources){if(thread)return;images=resources;parentWindow=parent;stopEvent=CreateEventW(nullptr,TRUE,FALSE,nullptr);wakeEvent=CreateEventW(nullptr,FALSE,FALSE,nullptr);thread=CreateThread(nullptr,0,Run,nullptr,0,nullptr);}
void Stop(){if(!thread)return;SetEvent(stopEvent);WaitForSingleObject(thread,INFINITE);CloseHandle(thread);CloseHandle(stopEvent);CloseHandle(wakeEvent);thread=nullptr;stopEvent=nullptr;wakeEvent=nullptr;}
void Capture(){capture=true;}
void SetAppearance(bool animate,float opacity){
    if(!std::isfinite(opacity)||opacity<.05f||opacity>.85f)return;
    animateBackground=animate;glassOpacity=opacity;glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetPanelFront(int slot){if(slot<0||slot>15)return;frontPanel=slot;glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);}
void SetGlass(float x,float cy,float my,float w,float ch,float mh){
    if(w<0||w>1200||ch<0||ch>1440||mh<0||mh>1440||x<0||x+w>5120)return;
    {std::lock_guard<std::mutex> lock(glassMutex);glassRects[0]=D2D1::RectF(x,cy,x+w,cy+ch);glassRects[1]=D2D1::RectF(x,my,x+w,my+mh);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetDockGlass(float x,float y,float w,float h){
    if(!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(w)||!std::isfinite(h)||w<0||h<0||h>1440||x<0||x+w>5120||y<0||y+h>1440)return;
    {std::lock_guard<std::mutex> lock(glassMutex);auto& r=glassRects[2];
        if(r.left==x&&r.top==y&&r.right==x+w&&r.bottom==y+h)return;
        r=D2D1::RectF(x,y,x+w,y+h);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetPanelGlass(int slot,float x,float y,float w,float h){
    if(slot<0||slot>15||!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(w)||!std::isfinite(h)||w<0||h<0||h>1440||x<0||x+w>5120||y<0||y+h>1440)return;
    {std::lock_guard<std::mutex> lock(glassMutex);auto& r=glassRects[slot];if(r.left==x&&r.top==y&&r.right==x+w&&r.bottom==y+h)return;r=D2D1::RectF(x,y,x+w,y+h);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
}
