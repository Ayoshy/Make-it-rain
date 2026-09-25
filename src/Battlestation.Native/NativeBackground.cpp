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
// Single source of truth for the glass panel slots: 0-14 are the historical
// docks, 15-22 are room for the newer blocks. WPF only sends the slots it uses.
static constexpr int PanelSlots=23;
static HANDLE thread=nullptr, stopEvent=nullptr,wakeEvent=nullptr;
static std::atomic<bool> capture=false;
static std::atomic<bool> canvasDirty=false;
static std::atomic<int> pendingTheme=-1;
static D2D1_COLOR_F palettes[3][6];
static float weights[3]={1,0,0},fromWeights[3]={1,0,0};
static int themeIndex=0;
static double themeFade=1;
static D2D1_COLOR_F ThemeColor(int role,float alpha=1){D2D1_COLOR_F c={0,0,0,alpha};for(int i=0;i<3;i++){c.r+=palettes[i][role].r*weights[i];c.g+=palettes[i][role].g*weights[i];c.b+=palettes[i][role].b*weights[i];}return c;}
static HWND parentWindow=nullptr;
static std::wstring images;
// The renderer covers the real virtual desktop, in physical pixels. The
// authored canvas (two 2560 x 1440 screens) stays the default so a system
// running that pair keeps the exact same frame.
static int canvasWidth=5120,canvasHeight=1440,canvasLeft=0,canvasTop=0,seamX=2560;
static double elapsed=0, easedX=0, easedY=0;
static std::atomic<bool> wallpaperRotation=false;
static double wallpaperSeconds=0;
static void AdvanceWallpaper(double dt,bool visible,bool animated,bool game){
    if(visible&&animated&&game)wallpaperSeconds+=dt;
}
static int WallpaperIndex(){return int(wallpaperSeconds/300)%2;}
static float WallpaperBlend(){return wallpaperSeconds<300?1.f:float(std::min(1.0,std::fmod(wallpaperSeconds,300.0)/2.0));}
static std::mutex glassMutex;
static D2D1_RECT_F glassRects[PanelSlots]={};
static std::atomic<bool> glassDirty=true;
static std::atomic<bool> animateBackground=true;
static std::atomic<int> visibleMonitors=3;
static std::atomic<float> glassOpacity=.46f;
// Alpha shared by every glass panel during a scene change; 1 outside transitions.
static std::atomic<float> sceneAlpha=1.f;
static std::atomic<int> frontPanel=-1;
static std::atomic<float> audioBass=0,audioMiddle=0,audioTreble=0,audioIntensity=0;
static float bass=0,middle=0,treble=0;
#ifdef BATTLESTATION_TESTING
static bool testPhotoEffects=true;
static std::atomic<bool> testLoseTarget=false;
static std::atomic<uint64_t> testFrames=0,testLosses=0;
extern "C" __declspec(dllexport) void BackgroundTestLoseTarget(){testLoseTarget=true;}
extern "C" __declspec(dllexport) uint64_t BackgroundTestFrames(){return testFrames.load();}
extern "C" __declspec(dllexport) uint64_t BackgroundTestLosses(){return testLosses.load();}
#endif
static float fract(double value){return float(value-std::floor(value));}
static void Check(HRESULT hr){if(FAILED(hr))throw hr;}
static LRESULT CALLBACK Proc(HWND window,UINT msg,WPARAM w,LPARAM l){return DefWindowProcW(window,msg,w,l);}
static std::filesystem::path Output(){wchar_t local[MAX_PATH];GetEnvironmentVariableW(L"LOCALAPPDATA",local,MAX_PATH);
return std::filesystem::path(local)/L"Battlestation";
}
static void LoadWallpaperProgress(const std::filesystem::path& file){
    double saved=0;std::ifstream input(file);
    if(input>>saved&&std::isfinite(saved)&&saved>=0)wallpaperSeconds=saved;
}
static bool SaveWallpaperProgress(const std::filesystem::path& file){
    auto pending=file;pending+=L".tmp";
    {std::ofstream output(pending);output.precision(15);output<<wallpaperSeconds;output.close();if(!output)return false;}
    return MoveFileExW(pending.c_str(),file.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH)!=0;
}
struct Paint {
    ID2D1RenderTarget* target;
    ComPtr<ID2D1Bitmap> art,alternateArt,posters[2],glows[3],bokeh[3],vignette;
    ComPtr<ID2D1SolidColorBrush> brush;
    ComPtr<ID2D1LinearGradientBrush> fade,posterFade;
    ComPtr<ID2D1Layer> layer,themeLayer,sceneLayer;
    ComPtr<ID2D1RadialGradientBrush> clouds[2][2];
    ComPtr<ID2D1RadialGradientBrush> windowLight,waterLight;
    ComPtr<ID2D1PathGeometry> ribbons[4];
    ComPtr<ID2D1BitmapRenderTarget> frost;
    std::unique_ptr<Paint> frostPaint;
    ComPtr<ID2D1LinearGradientBrush> sheen,themeSheen[2];
    ComPtr<ID2D1RoundedRectangleGeometry> masks[PanelSlots];
    D2D1_ROUNDED_RECT maskShapes[PanelSlots]={};
    Paint(ID2D1RenderTarget* rt,IWICImagingFactory* wic,bool auxiliary=false):target(rt){
        auto load=[&](const wchar_t* file,ComPtr<ID2D1Bitmap>& bitmap){
            ComPtr<IWICBitmapDecoder> decoder;Check(wic->CreateDecoderFromFilename((std::filesystem::path(images)/file).c_str(),nullptr,GENERIC_READ,WICDecodeMetadataCacheOnLoad,&decoder));
            ComPtr<IWICBitmapFrameDecode> frame;Check(decoder->GetFrame(0,&frame));
            ComPtr<IWICFormatConverter> converter;Check(wic->CreateFormatConverter(&converter));
            Check(converter->Initialize(frame.Get(),GUID_WICPixelFormat32bppPBGRA,WICBitmapDitherTypeNone,nullptr,0,WICBitmapPaletteTypeCustom));
            if(wcscmp(file,L"jason-lucia.jpg")==0||wcscmp(file,L"jason-lucia-03-painted.png")==0){
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
        // Decode once on the native render thread, also when recovering a lost target.
        auto optional=[&](const wchar_t* file,ComPtr<ID2D1Bitmap>& bitmap){
            std::filesystem::path source=std::filesystem::path(images)/file;std::error_code missing;
            if(!std::filesystem::exists(source,missing))return;
            try{
                ComPtr<IWICBitmapDecoder> decoder;Check(wic->CreateDecoderFromFilename(source.c_str(),nullptr,GENERIC_READ,WICDecodeMetadataCacheOnLoad,&decoder));
                ComPtr<IWICBitmapFrameDecode> frame;Check(decoder->GetFrame(0,&frame));
                ComPtr<IWICFormatConverter> converter;Check(wic->CreateFormatConverter(&converter));
                Check(converter->Initialize(frame.Get(),GUID_WICPixelFormat32bppPBGRA,WICBitmapDitherTypeNone,nullptr,0,WICBitmapPaletteTypeCustom));
                Check(rt->CreateBitmapFromWicBitmap(converter.Get(),nullptr,&bitmap));
            }catch(HRESULT){bitmap.Reset();}
        };
        load(L"jason-lucia.jpg",art);
        load(L"jason-lucia-03-painted.png",alternateArt);
        optional(L"obsidienne-photo.png",posters[0]);
        optional(L"aurore-photo.png",posters[1]);
        load(L"vignette.png",vignette);
        for(int i=0;i<3;i++){load((L"glow"+std::to_wstring(i)+L".png").c_str(),glows[i]);load((L"bokeh"+std::to_wstring(i)+L".png").c_str(),bokeh[i]);}
        Check(rt->CreateSolidColorBrush(D2D1::ColorF(0,0,0),&brush));
        D2D1_GRADIENT_STOP stops[]={{0,D2D1::ColorF(1,1,1,1)},{.88f,D2D1::ColorF(1,1,1,1)},{1,D2D1::ColorF(1,1,1,0)}};
        ComPtr<ID2D1GradientStopCollection> collection;Check(rt->CreateGradientStopCollection(stops,3,&collection));
        Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(2714,0)),collection.Get(),&fade));
        // Same profile as the Vice City mask, owned by the ambiance posters so the
        // per-frame parallax never rewrites the Vice City brush.
        Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(2714,0)),collection.Get(),&posterFade));
        Check(rt->CreateLayer(&layer));Check(rt->CreateLayer(&themeLayer));Check(rt->CreateLayer(&sceneLayer));
        for(int theme=0;theme<2;theme++)for(int light=0;light<2;light++){
            auto color=palettes[theme+1][light+1];auto transparent=color;transparent.a=0;
            D2D1_GRADIENT_STOP stops[]={{0,color},{1,transparent}};ComPtr<ID2D1GradientStopCollection> collection;
            Check(rt->CreateGradientStopCollection(stops,2,&collection));
            Check(rt->CreateRadialGradientBrush(D2D1::RadialGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(0,0),1,1),collection.Get(),&clouds[theme][light]));
        }
        auto photoLight=[&](D2D1_COLOR_F color,ComPtr<ID2D1RadialGradientBrush>& light){
            auto transparent=color;transparent.a=0;
            D2D1_GRADIENT_STOP stops[]={{0,color},{.18f,D2D1::ColorF(color.r,color.g,color.b,.35f)},{1,transparent}};
            ComPtr<ID2D1GradientStopCollection> colors;Check(rt->CreateGradientStopCollection(stops,3,&colors));
            Check(rt->CreateRadialGradientBrush(D2D1::RadialGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(0,0),1,1),colors.Get(),&light));
        };
        photoLight(D2D1::ColorF(1,.79f,.48f),windowLight);
        photoLight(D2D1::ColorF(.65f,.79f,.92f),waterLight);
        ComPtr<ID2D1Factory> factory;rt->GetFactory(&factory);
        for(int i=0;i<4;i++){
            Check(factory->CreatePathGeometry(&ribbons[i]));ComPtr<ID2D1GeometrySink> sink;Check(ribbons[i]->Open(&sink));
            sink->BeginFigure(D2D1::Point2F(-800,550+i*150.f),D2D1_FIGURE_BEGIN_FILLED);
            sink->AddBezier(D2D1::BezierSegment(D2D1::Point2F(900,-850+i*180.f),D2D1::Point2F(1800,1600-i*100.f),D2D1::Point2F(6000,100+i*140.f)));
            sink->AddBezier(D2D1::BezierSegment(D2D1::Point2F(2200,1500-i*90.f),D2D1::Point2F(1100,-700+i*170.f),D2D1::Point2F(-800,680+i*150.f)));
            sink->EndFigure(D2D1_FIGURE_END_CLOSED);Check(sink->Close());
        }
        if(!auxiliary){
            D2D1_SIZE_F logical=D2D1::SizeF(float(canvasWidth),float(canvasHeight));
            D2D1_SIZE_U pixels=D2D1::SizeU(UINT(std::max(1,canvasWidth/8)),UINT(std::max(1,canvasHeight/8)));
            Check(rt->CreateCompatibleRenderTarget(&logical,&pixels,nullptr,D2D1_COMPATIBLE_RENDER_TARGET_OPTIONS_NONE,&frost));
            frostPaint=std::make_unique<Paint>(frost.Get(),wic,true);
            D2D1_GRADIENT_STOP shine[]={{0,D2D1::ColorF(1,.86f,1,.17f)},{.38f,D2D1::ColorF(.9f,.65f,1,.02f)},{1,D2D1::ColorF(.7f,.6f,1,.07f)}};
            ComPtr<ID2D1GradientStopCollection> collection;Check(rt->CreateGradientStopCollection(shine,3,&collection));
            Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(1,1)),collection.Get(),&sheen));
            for(int i=0;i<2;i++){
                auto edge=palettes[i+1][5],light=palettes[i+1][1],rim=palettes[i+1][4];edge.a=.17f;light.a=.02f;rim.a=.07f;
                D2D1_GRADIENT_STOP stops[]={{0,edge},{.38f,light},{1,rim}};ComPtr<ID2D1GradientStopCollection> colors;
                Check(rt->CreateGradientStopCollection(stops,3,&colors));Check(rt->CreateLinearGradientBrush(D2D1::LinearGradientBrushProperties(D2D1::Point2F(0,0),D2D1::Point2F(1,1)),colors.Get(),&themeSheen[i]));
            }
        }
    }
    D2D1_COLOR_F GlassColor(float alpha){auto c=ThemeColor(3,alpha);c.r+=(.06f-palettes[0][3].r)*weights[0];c.g+=(.022f-palettes[0][3].g)*weights[0];c.b+=(.10f-palettes[0][3].b)*weights[0];return c;}
    D2D1_COLOR_F RimColor(float alpha){auto c=ThemeColor(4,alpha);c.r+=(.95f-palettes[0][4].r)*weights[0];c.g+=(.77f-palettes[0][4].g)*weights[0];c.b+=(1-palettes[0][4].b)*weights[0];return c;}
    D2D1_COLOR_F EdgeColor(float alpha){auto c=ThemeColor(5,alpha);c.r+=(1-palettes[0][5].r)*weights[0];c.g+=(.92f-palettes[0][5].g)*weights[0];c.b+=(1-palettes[0][5].b)*weights[0];return c;}
    void Fill(D2D1_RECT_F rect,D2D1_COLOR_F color){brush->SetColor(color);target->FillRectangle(rect,brush.Get());}
    void ViceCity(){auto rt=target;
        auto selected=WallpaperIndex()==0?art.Get():alternateArt.Get();
        auto previous=WallpaperIndex()==0?alternateArt.Get():art.Get();
        float mix=WallpaperBlend();
        Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),D2D1::ColorF(.09f,.055f,.14f));
        auto backdrop=[&](ID2D1Bitmap* bitmap,float alpha){auto size=bitmap->GetSize();rt->DrawBitmap(bitmap,D2D1::RectF(0,0,canvasWidth,canvasHeight),alpha,D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,D2D1::RectF(size.width*.75f,size.height*.38f,size.width,size.height*.51f));};
        if(mix<1)backdrop(previous,.5f);
        backdrop(selected,.5f*mix);
        Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),D2D1::ColorF(.16f,.08f,.24f,.75f));
        float dx=float(std::sin(elapsed*.14)*20.48-easedX*15),dy=float(std::cos(elapsed*.11)*5.76-easedY*10);
        fade->SetStartPoint(D2D1::Point2F(-77+dx,0));fade->SetEndPoint(D2D1::Point2F(2637+dx,0));
        rt->PushLayer(D2D1::LayerParameters(D2D1::RectF(0,0,2800,canvasHeight),nullptr,D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,D2D1::Matrix3x2F::Identity(),1.f,fade.Get()),layer.Get());
        if(mix<1)rt->DrawBitmap(previous,D2D1::RectF(-77+dx,-43+dy,2637+dx,1483+dy));
        rt->DrawBitmap(selected,D2D1::RectF(-77+dx,-43+dy,2637+dx,1483+dy),mix);rt->PopLayer();
        Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),D2D1::ColorF(.063f,.027f,.118f,.14f));
        rt->DrawBitmap(glows[1].Get(),D2D1::RectF(2450,-150,5350,1600),.18f);
        rt->DrawBitmap(glows[2].Get(),D2D1::RectF(200,-200,2800,1600),.09f);
        // Broad pearl light and thin ripples stay behind the glass.
        if(bass+middle+treble>.002f){
            float bloom=150+bass*260;
            rt->DrawBitmap(glows[1].Get(),D2D1::RectF(2500-bloom,800-bloom,5100+bloom,1800+bloom),bass*.34f);
            rt->DrawBitmap(glows[2].Get(),D2D1::RectF(2950,-450,5200,950),treble*.27f);
            for(int wave=0;wave<3;wave++){
                D2D1_POINT_2F previous{};
                for(int step=0;step<=80;step++){
                    float x=seamX+step*32.f;
                    float y=1260+wave*35+std::sin(step*.075f+float(elapsed)*.8f+wave*.7f)*(12+middle*65)+std::sin(step*.17f-float(elapsed)*1.2f)*bass*22;
                    float edge=std::sin(step/80.f*3.1415926f);
                    brush->SetColor(D2D1::ColorF(.76f+wave*.07f,.85f-wave*.06f,1,(bass*.22f+middle*.17f+treble*.10f)*edge));
                    if(step)rt->DrawLine(previous,D2D1::Point2F(x,y),brush.Get(),1.4f+treble*1.5f);
                    previous=D2D1::Point2F(x,y);
                }
            }
        }
        double t=elapsed*.65;
        for(int i=0;i<3;i++){
            float x=float(canvasWidth*(.26+i*.28+std::sin(t*.075+i)*.05));
            rt->SetTransform(D2D1::Matrix3x2F::Rotation(float(17+std::sin(t*.06+i*.9)*8),D2D1::Point2F(x,-430)));
            rt->DrawBitmap(glows[(i+1)%3].Get(),D2D1::RectF(x-220,-900,x+220,2150),.075f);rt->SetTransform(D2D1::Matrix3x2F::Identity());
        }
        auto quiet=[](float x,float y){double a=(x-4599.5)/(779*.68),b=(y-720)/(1150*.7);return float(.25+.75*(1-std::exp(-(a*a+b*b)*1.7)));};
        for(int i=0;i<14;i++){
            int n=i*7;double phase=n*2.39996,depth=fract(n*.414213562+.12);
            float x=float((fract(n*.61803398875+.09)+std::sin(t*.07+phase)*.045)*canvasWidth);
            float y=(fract(fract(n*.754877666+.31)-t*.0025)*1.25f-.125f)*canvasHeight;
            float r=float((25+depth*65)*1.333);
            rt->DrawBitmap(bokeh[i%3].Get(),D2D1::RectF(x-r,y-r,x+r,y+r),float((.33+.25*std::sin(t*.14+phase))*.7)*quiet(x,y));
        }
        for(int i=0;i<117;i++){
            double phase=i*2.39996,depth=fract(i*.414213562+.12);
            float x=(fract(fract(i*.61803398875+.09)+t*.0012*(depth+.2)+std::sin(t*.11+phase)*.013)*1.08f-.04f)*canvasWidth;
            float y=(fract(fract(i*.754877666+.31)-t*(.007+(i%7)*.0014))*1.12f-.06f)*canvasHeight;
            float r=float((2.8+depth*8)*1.333),alpha=float((.35+depth*.6)*(.5+.5*std::pow((std::sin(t*.7+phase)+1)/2,2))*.7)*quiet(x,y);
            rt->DrawBitmap(glows[i%3].Get(),D2D1::RectF(x-r,y-r,x+r,y+r),alpha);
        }
        for(int i=0;i<3;i++){
            float phase=fract((t+i*9+3)/(22+i*5));if(phase>.34f)continue;
            float progress=phase/.34f,x=(-.15f+progress*1.4f)*canvasWidth,y=(.86f-i*.25f-progress*.18f)*canvasHeight,len=(145+i*40)*1.333f;
            brush->SetColor(D2D1::ColorF(.95f,.6f,.85f,float(std::sin(progress*3.1415926))*.22f*quiet(x,y)));
            rt->DrawLine(D2D1::Point2F(x-len,y+len*.16f),D2D1::Point2F(x,y),brush.Get(),2);
            rt->DrawBitmap(glows[i].Get(),D2D1::RectF(x-12,y-12,x+12,y+12),.5f*quiet(x,y));
        }
        rt->DrawBitmap(vignette.Get(),D2D1::RectF(0,0,canvasWidth,canvasHeight));
    }
    D2D1_COLOR_F Tint(int theme,int role,float alpha){auto c=palettes[theme][role];c.a=alpha;return c;}
    void Blit(ComPtr<ID2D1Bitmap>& bitmap,float weight){
        // Cover the full desktop without stretching the architecture or introducing a seam.
        auto size=bitmap->GetSize();float scale=std::max(canvasWidth/size.width,canvasHeight/size.height)*1.025f;
        float width=size.width*scale,height=size.height*scale;
        float x=(canvasWidth-width)/2+float(std::sin(elapsed*.035)*6-easedX*5);
        float y=(canvasHeight-height)/2+float(std::cos(elapsed*.027)*3-easedY*3);
        target->DrawBitmap(bitmap.Get(),D2D1::RectF(x,y,x+width,y+height),weight);
    }
    // Photo effects follow the image, using two cached brushes rather than the GTA particles.
    void Poster(int theme,float weight){
        auto rt=target;Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),palettes[theme][0]);
        Blit(posters[theme-1],weight);
        Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),Tint(theme,3,.06f*weight));
        auto size=posters[theme-1]->GetSize();float scale=std::max(canvasWidth/size.width,canvasHeight/size.height)*1.025f;
        float w=size.width*scale,h=size.height*scale;
        float ox=(canvasWidth-w)/2+float(std::sin(elapsed*.035)*6-easedX*5);
        float oy=(canvasHeight-h)/2+float(std::cos(elapsed*.027)*3-easedY*3);
        float t=float(elapsed),energy=bass*.3f+middle*.2f;
        auto glow=[&](ID2D1RadialGradientBrush* light,float x,float y,float rx,float ry,float alpha){
            light->SetCenter(D2D1::Point2F(x,y));light->SetRadiusX(rx);light->SetRadiusY(ry);light->SetOpacity(alpha*weight);
            rt->FillEllipse(D2D1::Ellipse(D2D1::Point2F(x,y),rx,ry),light);
        };
#ifdef BATTLESTATION_TESTING
        if(testPhotoEffects){
#endif
        if(theme==2){
            // Warm moving reflections on four curtain-wall facades, two restrained optical flares.
            const float xs[]={.146f,.438f,.761f,.934f},ys[]={.31f,.23f,.20f,.39f};
            for(int i=0;i<4;i++){
                float pulse=.5f+.5f*std::sin(t*.34f+i*1.9f);
                float x=ox+w*xs[i],y=oy+h*ys[i]+std::sin(t*.12f+i)*h*.008f;
                glow(windowLight.Get(),x,y,w*.004f,h*.16f,.14f+pulse*.20f+energy*.15f);
                if(i==1||i==2){
                    float alpha=.10f+pulse*.22f+energy*.16f;
                    glow(windowLight.Get(),x,y,w*.033f,h*.045f,alpha);
                    glow(windowLight.Get(),x,y,w*.007f,h*.11f,alpha*.5f);
                    glow(windowLight.Get(),x+w*.06f,y+h*.06f,w*.016f,h*.025f,alpha*.18f);
                }
            }
        }else{
            // Soft moon/cloud glow and irregular light paths on the water.
            glow(waterLight.Get(),ox+w*.30f,oy+h*.55f,w*.12f,h*.10f,.06f+.025f*std::sin(t*.15f)+energy*.04f);
            const float xs[]={.25f,.573f,.868f};
            for(int source=0;source<3;source++)for(int i=0;i<18;i++){
                float depth=i/18.f,phase=t*.65f+i*2.7f+source;
                float x=ox+w*xs[source]+std::sin(phase)*w*(.0008f+depth*.0015f);
                float y=oy+h*(.625f+depth*.29f);
                float length=w*(.0015f+depth*.004f)*(1+.4f*std::sin(phase*.7f));
                float alpha=(.045f+.045f*std::sin(phase))*(1-depth*.65f)*weight*(1+energy);
                brush->SetColor(source==0?D2D1::ColorF(.70f,.79f,.89f,alpha):D2D1::ColorF(.95f,.73f,.44f,alpha));
                rt->DrawLine(D2D1::Point2F(x-length,y),D2D1::Point2F(x+length,y),brush.Get(),1.f+depth);
            }
        }
#ifdef BATTLESTATION_TESTING
        }
#endif
        rt->DrawBitmap(vignette.Get(),D2D1::RectF(0,0,canvasWidth,canvasHeight),.45f*weight);
    }
    void Abstract(int theme){
        auto rt=target;Fill(D2D1::RectF(0,0,canvasWidth,canvasHeight),palettes[theme][0]);
        float t=float(elapsed),energy=bass*.3f+middle*.2f;
        for(int i=0;i<7;i++){
            auto light=clouds[theme-1][i%2].Get();
            float x=500+i*720.f+std::sin(t*.045f+i*1.7f)*260;
            float y=500+std::sin(t*.036f+i*1.2f)*580;
            light->SetCenter(D2D1::Point2F(x,y));light->SetRadiusX(1150+std::sin(t*.027f+i)*260);light->SetRadiusY(500+i%3*130.f);
            light->SetOpacity((theme==1?.055f:.13f)+energy*.045f);
            rt->FillRectangle(D2D1::RectF(0,0,canvasWidth,canvasHeight),light);
        }
        for(int i=0;i<4;i++){
            auto light=clouds[theme-1][i%2].Get();light->SetCenter(D2D1::Point2F(1700+i*700.f,600));light->SetRadiusX(2400);light->SetRadiusY(850);
            light->SetOpacity((theme==1?.047f:.11f)+treble*.025f);
            rt->SetTransform(D2D1::Matrix3x2F::Translation(std::sin(t*.028f+i)*180,std::cos(t*.037f+i)*90));
            rt->FillGeometry(ribbons[i].Get(),light);
        }
        rt->SetTransform(D2D1::Matrix3x2F::Identity());
        rt->DrawBitmap(vignette.Get(),D2D1::RectF(0,0,canvasWidth,canvasHeight),.65f);
    }
    void Draw(int monitors=3){
        ComPtr<ID2D1Bitmap> frosted;
        if(frostPaint){frostPaint->Draw(monitors);Check(frost->GetBitmap(&frosted));}
        auto rt=target;rt->BeginDraw();rt->SetTransform(D2D1::Matrix3x2F::Identity());
        // One bit per monitor: bit 1 is the leftmost screen, every further
        // screen shares bit 2. The seam is the width of that first screen.
        rt->PushAxisAlignedClip(D2D1::RectF(monitors==2?float(seamX):0.f,0,monitors==1?float(seamX):float(canvasWidth),float(canvasHeight)),D2D1_ANTIALIAS_MODE_ALIASED);
        float accumulated=0;
        for(int theme=0;theme<3;theme++){
            if(weights[theme]<.0001f)continue;
            accumulated+=weights[theme];float opacity=weights[theme]/accumulated;
            if(opacity<.9999f)rt->PushLayer(D2D1::LayerParameters(D2D1::InfiniteRect(),nullptr,D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,D2D1::Matrix3x2F::Identity(),opacity),themeLayer.Get());
            if(theme==0)ViceCity();else if(posters[theme-1].Get())Poster(theme,weights[theme]);else Abstract(theme);
            if(opacity<.9999f)rt->PopLayer();
        }
        // One shared layer keeps a scene change to a single alpha for the whole glass
        // panel pass, whatever the theme, the audio or the number of docks.
        float scene=sceneAlpha.load();
        if(frosted&&scene>.002f){
            D2D1_RECT_F rects[PanelSlots];{std::lock_guard<std::mutex> lock(glassMutex);std::copy(std::begin(glassRects),std::end(glassRects),rects);}
            bool sceneBlend=scene<.999f;
            if(sceneBlend)rt->PushLayer(D2D1::LayerParameters(D2D1::InfiniteRect(),nullptr,D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,D2D1::Matrix3x2F::Identity(),scene),sceneLayer.Get());
            ComPtr<ID2D1Factory> factory;rt->GetFactory(&factory);
            int front=frontPanel.load();
            for(int order=0;order<=PanelSlots;order++){
                int index=order==PanelSlots?front:order;
                if(index<0||(order<PanelSlots&&index==front))continue;
                auto r=rects[index];
                if(r.right<=r.left||r.bottom<=r.top)continue;
                bool clearPopup=index==8;
                // Keep dock corners at 24 physical pixels, matching Surface.Panel at every size.
                float radius=clearPopup?22:24;
                for(int shadow=4;shadow>=1;shadow--){float d=shadow*3.f;brush->SetColor(D2D1::ColorF(.015f,.005f,.03f,.035f));rt->FillRoundedRectangle(D2D1::RoundedRect(D2D1::RectF(r.left-d,r.top+4,r.right+d,r.bottom+d+4),radius+d,radius+d),brush.Get());}
                auto shape=D2D1::RoundedRect(r,radius,radius);auto& old=maskShapes[index];
                if(!masks[index]||old.rect.left!=r.left||old.rect.top!=r.top||old.rect.right!=r.right||old.rect.bottom!=r.bottom||old.radiusX!=radius){
                    masks[index].Reset();Check(factory->CreateRoundedRectangleGeometry(shape,&masks[index]));old=shape;
                }
                rt->PushLayer(D2D1::LayerParameters(r,masks[index].Get()),layer.Get());
                float shift=float(easedX*3);
                rt->DrawBitmap(frosted.Get(),r,clearPopup?.22f:1.f,D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,D2D1::RectF(r.left-4+shift,r.top-3,r.right+4+shift,r.bottom+3));
                Fill(r,GlassColor(glassOpacity.load()*(clearPopup?.08f/.46f:1.f)));
                sheen->SetStartPoint(D2D1::Point2F(r.left,r.top));sheen->SetEndPoint(D2D1::Point2F(r.right,r.bottom));
                if(!clearPopup){
                    if(weights[0]>0){sheen->SetOpacity(weights[0]);rt->FillRectangle(r,sheen.Get());}
                    for(int i=0;i<2;i++)if(weights[i+1]>0){auto tint=themeSheen[i].Get();tint->SetStartPoint(D2D1::Point2F(r.left,r.top));tint->SetEndPoint(D2D1::Point2F(r.right,r.bottom));tint->SetOpacity(weights[i+1]);rt->FillRectangle(r,tint);}
                }rt->PopLayer();
                if(clearPopup)continue; // The WPF rim shares the exact popup shape.
                brush->SetColor(RimColor(.25f));rt->DrawRoundedRectangle(D2D1::RoundedRect(r,radius,radius),brush.Get(),1);
                rt->PushAxisAlignedClip(D2D1::RectF(r.left,r.top,r.right,r.top+25),D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
                brush->SetColor(EdgeColor(.34f));rt->DrawRoundedRectangle(D2D1::RoundedRect(D2D1::RectF(r.left+1,r.top+1,r.right-1,r.bottom-1),radius-1,radius-1),brush.Get(),1);rt->PopAxisAlignedClip();
            }
            if(sceneBlend)rt->PopLayer();
        }
        rt->PopAxisAlignedClip();Check(rt->EndDraw());
    }
};
static void SaveFrame(ID2D1Factory* factory,IWICImagingFactory* wic){
    ComPtr<IWICBitmap> bitmap;Check(wic->CreateBitmap(UINT(canvasWidth),UINT(canvasHeight),GUID_WICPixelFormat32bppPBGRA,WICBitmapCacheOnLoad,&bitmap));
    ComPtr<ID2D1RenderTarget> target;Check(factory->CreateWicBitmapRenderTarget(bitmap.Get(),D2D1::RenderTargetProperties(),&target));
    Paint paint(target.Get(),wic);paint.Draw();
    ComPtr<IWICStream> stream;Check(wic->CreateStream(&stream));Check(stream->InitializeFromFilename((Output()/L"native-background-frame.png").c_str(),GENERIC_WRITE));
    ComPtr<IWICBitmapEncoder> encoder;Check(wic->CreateEncoder(GUID_ContainerFormatPng,nullptr,&encoder));Check(encoder->Initialize(stream.Get(),WICBitmapEncoderNoCache));
    ComPtr<IWICBitmapFrameEncode> frame;ComPtr<IPropertyBag2> bag;Check(encoder->CreateNewFrame(&frame,&bag));Check(frame->Initialize(bag.Get()));Check(frame->SetSize(UINT(canvasWidth),UINT(canvasHeight)));
    WICPixelFormatGUID format=GUID_WICPixelFormat32bppPBGRA;Check(frame->SetPixelFormat(&format));Check(frame->WriteSource(bitmap.Get(),nullptr));Check(frame->Commit());Check(encoder->Commit());
}
static DWORD WINAPI Run(void*){
    CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
    HWND window=nullptr;
    const auto progressFile=Output()/L"wallpaper-progress.txt";LoadWallpaperProgress(progressFile);
    double savedProgress=wallpaperSeconds;ULONGLONG nextProgress=GetTickCount64()+5000;
    const char* stage="register window class";
    try{
        WNDCLASSW cls{};cls.lpfnWndProc=Proc;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"BattlestationBackground";RegisterClassW(&cls);
        stage="create layered child window";SetLastError(ERROR_SUCCESS);
        window=CreateWindowExW(WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE|WS_EX_LAYERED,cls.lpszClassName,L"Battlestation background",WS_CHILD|WS_VISIBLE,0,0,canvasWidth,canvasHeight,parentWindow,nullptr,cls.hInstance,nullptr);
        if(!window){auto error=GetLastError();throw error?HRESULT_FROM_WIN32(error):E_FAIL;}
        stage="configure layered window";
        if(!SetLayeredWindowAttributes(window,0,255,LWA_ALPHA)){auto error=GetLastError();throw error?HRESULT_FROM_WIN32(error):E_FAIL;}
        stage="create Direct2D resources";
        ComPtr<ID2D1Factory> factory;Check(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,factory.GetAddressOf()));
        ComPtr<IWICImagingFactory> wic;Check(CoCreateInstance(CLSID_WICImagingFactory,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&wic)));
        ComPtr<ID2D1HwndRenderTarget> target;std::unique_ptr<Paint> paint;ULONGLONG retryAt=0,nextState=0;stage="render frames";
        auto last=std::chrono::steady_clock::now();bool first=true;unsigned long long rendered=0;double renderMs=0;
        while(WaitForSingleObject(stopEvent,0)!=WAIT_OBJECT_0){
            MSG msg;while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){TranslateMessage(&msg);DispatchMessageW(&msg);}
            int monitors=visibleMonitors.load();bool paused=monitors==0||!animateBackground.load();auto now=std::chrono::steady_clock::now();double dt=std::min(.1,std::chrono::duration<double>(now-last).count());last=now;
            if(canvasDirty.exchange(false)){SetWindowPos(window,nullptr,0,0,canvasWidth,canvasHeight,SWP_NOZORDER|SWP_NOACTIVATE);paint.reset();target.Reset();first=true;retryAt=0;}
            float intensity=audioIntensity.load();
            auto smooth=[&](float value,float desired){return value+(desired-value)*float(1-std::exp(-dt*(desired>value?12:3)));};
            bass=smooth(bass,audioBass.load()*intensity);middle=smooth(middle,audioMiddle.load()*intensity);treble=smooth(treble,audioTreble.load()*intensity);
            int requested=pendingTheme.exchange(-1);
            if(requested>=0){themeIndex=requested%4;themeFade=requested>=4?1:0;for(int i=0;i<3;i++){fromWeights[i]=weights[i];if(requested>=4)weights[i]=i==themeIndex?1.f:0.f;}}
            bool transitioning=themeFade<1;
            if(transitioning){themeFade=std::min(1.0,themeFade+dt/.24);for(int i=0;i<3;i++)weights[i]=fromWeights[i]+((i==themeIndex?1.f:0.f)-fromWeights[i])*float(themeFade);}
            bool dirty=glassDirty.exchange(false)||transitioning;
            if(monitors!=0&&(!paused||first||dirty)&&GetTickCount64()>=retryAt){
                try{
                    if(!paint){Check(factory->CreateHwndRenderTarget(D2D1::RenderTargetProperties(),D2D1::HwndRenderTargetProperties(window,D2D1::SizeU(canvasWidth,canvasHeight),D2D1_PRESENT_OPTIONS_IMMEDIATELY),&target));paint=std::make_unique<Paint>(target.Get(),wic.Get());}
#ifdef BATTLESTATION_TESTING
                    if(testLoseTarget.exchange(false)){++testLosses;throw HRESULT(D2DERR_RECREATE_TARGET);}
#endif
                    if(!paused)elapsed+=dt;
                    AdvanceWallpaper(dt,monitors!=0,animateBackground.load(),wallpaperRotation.load()&&themeIndex==0);
                    POINT p;GetCursorPos(&p);double blend=1-std::exp(-dt*3);
                    easedX+=(((p.x-canvasLeft)/double(canvasWidth)*2-1)-easedX)*blend;easedY+=(((p.y-canvasTop)/double(canvasHeight)*2-1)-easedY)*blend;
                    auto began=std::chrono::steady_clock::now();paint->Draw(monitors);renderMs+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-began).count();++rendered;first=false;
#ifdef BATTLESTATION_TESTING
                    ++testFrames;
#endif
                }catch(HRESULT hr){paint.reset();target.Reset();retryAt=GetTickCount64()+1000;first=true;std::ofstream log(Output()/L"native-renderer-error.txt");log<<"Recovering render target: HRESULT "<<std::hex<<hr;}
            }
            if(capture.exchange(false)){try{SaveFrame(factory.Get(),wic.Get());}catch(...){std::ofstream log(Output()/L"native-capture-error.txt");log<<"Capture failed; renderer remains running";}}
            if(GetTickCount64()>=nextState){nextState=GetTickCount64()+(paused?5000:1000);
                std::ofstream state(Output()/L"native-renderer-state.json");state<<"{\"pid\":"<<GetCurrentProcessId()<<",\"paused\":"<<(paused?"true":"false")<<",\"elapsed\":"<<elapsed<<",\"hwnd\":"<<(uintptr_t)window<<",\"parent\":"<<(uintptr_t)parentWindow<<",\"width\":"<<canvasWidth<<",\"height\":"<<canvasHeight<<",\"renderedFrames\":"<<rendered<<",\"renderMilliseconds\":"<<renderMs<<",\"wallpaperSeconds\":"<<wallpaperSeconds<<",\"wallpaperIndex\":"<<WallpaperIndex()<<",\"wallpaperBlend\":"<<WallpaperBlend()<<",\"sceneAlpha\":"<<sceneAlpha.load()<<",\"panels\":[";
                std::lock_guard<std::mutex> lock(glassMutex);bool comma=false;
                for(int i=0;i<PanelSlots;i++){auto r=glassRects[i];if(r.right<=r.left||r.bottom<=r.top)continue;if(comma)state<<",";comma=true;state<<"{\"slot\":"<<i<<",\"x\":"<<r.left<<",\"y\":"<<r.top<<",\"width\":"<<r.right-r.left<<",\"height\":"<<r.bottom-r.top<<"}";}state<<"]}";
            }
            if(GetTickCount64()>=nextProgress){nextProgress=GetTickCount64()+5000;if(wallpaperSeconds-savedProgress>=1&&SaveWallpaperProgress(progressFile))savedProgress=wallpaperSeconds;}
            HANDLE events[]={stopEvent,wakeEvent};WaitForMultipleObjects(2,events,FALSE,paused&&!transitioning?500:33);
        }
    }catch(HRESULT hr){std::ofstream log(Output()/L"native-renderer-error.txt");log<<stage<<": HRESULT "<<std::hex<<hr;}catch(...){std::ofstream log(Output()/L"native-renderer-error.txt");log<<stage<<": native renderer failed";}
    if(wallpaperSeconds!=savedProgress)SaveWallpaperProgress(progressFile);
    if(window)DestroyWindow(window);UnregisterClassW(L"BattlestationBackground",GetModuleHandleW(nullptr));CoUninitialize();return 0;
}
void SetPalette(int index,const unsigned int* colors){if(index<0||index>2||!colors)return;for(int i=0;i<6;i++)palettes[index][i]=D2D1::ColorF(colors[i]);}
void SetTheme(int index,bool immediate){if(index<0||index>2)return;pendingTheme=index+(immediate?4:0);glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);}
void SetWallpaperRotation(bool enabled){wallpaperRotation=enabled;}
void Start(HWND parent,const std::wstring& resources,int left,int top,int width,int height,int seam){
    if(thread)return;
    // Virtual desktop box in physical pixels, and the width of its first
    // (leftmost) monitor, which the visibility mask clips against.
    canvasLeft=left;canvasTop=top;canvasWidth=std::max(1,width);canvasHeight=std::max(1,height);seamX=std::clamp(seam,0,canvasWidth);
    images=resources;parentWindow=parent;stopEvent=CreateEventW(nullptr,TRUE,FALSE,nullptr);wakeEvent=CreateEventW(nullptr,FALSE,FALSE,nullptr);thread=CreateThread(nullptr,0,Run,nullptr,0,nullptr);
}
void Stop(){if(!thread)return;SetEvent(stopEvent);WaitForSingleObject(thread,INFINITE);CloseHandle(thread);CloseHandle(stopEvent);CloseHandle(wakeEvent);thread=nullptr;stopEvent=nullptr;wakeEvent=nullptr;}
void Capture(){capture=true;if(wakeEvent)SetEvent(wakeEvent);}
void SetVisibility(int monitors){monitors&=3;if(visibleMonitors.exchange(monitors)!=monitors){glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);}}
// A monitor swap moves or resizes the covered desktop: the canvas follows it.
void SetCanvas(int left,int top,int width,int height,int seam){
    canvasLeft=left;canvasTop=top;canvasWidth=std::max(1,width);canvasHeight=std::max(1,height);seamX=std::clamp(seam,0,canvasWidth);
    canvasDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetAudio(float low,float mid,float high,float intensity){
    if(!std::isfinite(low+mid+high+intensity))return;
    audioBass=std::clamp(low,0.f,1.f);audioMiddle=std::clamp(mid,0.f,1.f);audioTreble=std::clamp(high,0.f,1.f);audioIntensity=std::clamp(intensity,0.f,1.f);
}
void SetAppearance(bool animate,float opacity){
    if(!std::isfinite(opacity)||opacity<.05f||opacity>.85f)return;
    animateBackground=animate;glassOpacity=opacity;glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetPanelFront(int slot){if(slot<0||slot>=PanelSlots)return;frontPanel=slot;glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);}
void SetSceneFade(float alpha){
    if(!std::isfinite(alpha))return;
    alpha=std::clamp(alpha,0.f,1.f);
    if(std::abs(sceneAlpha.load()-alpha)<.004f)return;
    sceneAlpha=alpha;glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetGlass(float x,float cy,float my,float w,float ch,float mh){
    x-=canvasLeft;cy-=canvasTop;my-=canvasTop;
    if(w<0||w>1200||ch<0||ch>canvasHeight||mh<0||mh>canvasHeight||x<0||x+w>canvasWidth)return;
    {std::lock_guard<std::mutex> lock(glassMutex);glassRects[0]=D2D1::RectF(x,cy,x+w,cy+ch);glassRects[1]=D2D1::RectF(x,my,x+w,my+mh);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetDockGlass(float x,float y,float w,float h){
    x-=canvasLeft;y-=canvasTop;
    if(!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(w)||!std::isfinite(h)||w<0||h<0||h>canvasHeight||x<0||x+w>canvasWidth||y<0||y+h>canvasHeight)return;
    {std::lock_guard<std::mutex> lock(glassMutex);auto& r=glassRects[2];
        if(r.left==x&&r.top==y&&r.right==x+w&&r.bottom==y+h)return;
        r=D2D1::RectF(x,y,x+w,y+h);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
void SetPanelGlass(int slot,float x,float y,float w,float h){
    x-=canvasLeft;y-=canvasTop;
    if(slot<0||slot>=PanelSlots||!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(w)||!std::isfinite(h)||w<0||h<0||h>canvasHeight||x<0||x+w>canvasWidth||y<0||y+h>canvasHeight)return;
    {std::lock_guard<std::mutex> lock(glassMutex);auto& r=glassRects[slot];if(r.left==x&&r.top==y&&r.right==x+w&&r.bottom==y+h)return;r=D2D1::RectF(x,y,x+w,y+h);}
    glassDirty=true;if(wakeEvent)SetEvent(wakeEvent);
}
}
