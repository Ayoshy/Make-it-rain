#pragma once
#include <windows.h>
#include <objidl.h>
#include <gdiplus.h>
#include <shlwapi.h>
#include <winrt/base.h>
#include <memory>
#include <vector>

using CoverBytes=std::shared_ptr<const std::vector<uint8_t>>;

// One current artwork in RAM. No thumbnail file, browser, or image cache on disk.
class AlbumCover {
    HWND window=nullptr;
    CoverBytes rendered;
    bool ready=false;
    static void InitializeGdi(){
        static ULONG_PTR token=[](){Gdiplus::GdiplusStartupInput input;ULONG_PTR result=0;Gdiplus::GdiplusStartup(&result,&input,nullptr);return result;}();
        (void)token;
    }
public:
    ~AlbumCover(){if(window)DestroyWindow(window);}
    bool Update(HWND owner,int x,int y,int size,const CoverBytes& bytes){
        RECT ownerRect{};GetWindowRect(owner,&ownerRect);
        if(!bytes||bytes->empty()){rendered.reset();ready=false;if(window)ShowWindow(window,SW_HIDE);return false;}
        if(!window){
            WNDCLASSW cls{};cls.lpfnWndProc=DefWindowProcW;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"ViceCityAlbumCover";RegisterClassW(&cls);
            window=CreateWindowExW(WS_EX_LAYERED|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE|WS_EX_TRANSPARENT,cls.lpszClassName,L"Album artwork",WS_POPUP,
                ownerRect.left+x,ownerRect.top+y,size,size,owner,nullptr,cls.hInstance,nullptr);
        }
        if(rendered!=bytes){
            rendered=bytes;ready=false;InitializeGdi();
            winrt::com_ptr<IStream> stream;stream.attach(SHCreateMemStream(bytes->data(),(UINT)bytes->size()));
            if(!stream){ShowWindow(window,SW_HIDE);return false;}
            Gdiplus::Bitmap source(stream.get());
            if(source.GetLastStatus()!=Gdiplus::Ok||!source.GetWidth()||!source.GetHeight()||source.GetWidth()>4096||source.GetHeight()>4096){ShowWindow(window,SW_HIDE);return false;}
            BITMAPINFO info{};info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=size;info.bmiHeader.biHeight=-size;info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;
            auto screen=GetDC(nullptr);void* pixels=nullptr;auto dib=CreateDIBSection(screen,&info,DIB_RGB_COLORS,&pixels,nullptr,0);
            if(!dib){ReleaseDC(nullptr,screen);return false;}
            memset(pixels,0,size*size*4);
            {
                Gdiplus::Bitmap target(size,size,size*4,PixelFormat32bppPARGB,(BYTE*)pixels);
                Gdiplus::Graphics graphics(&target);graphics.Clear(Gdiplus::Color(0,0,0,0));
                graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
                Gdiplus::GraphicsPath clip;clip.AddEllipse(1.f,1.f,float(size-2),float(size-2));graphics.SetClip(&clip);
                float side=float(std::min(source.GetWidth(),source.GetHeight()));
                graphics.DrawImage(&source,Gdiplus::RectF(1,1,float(size-2),float(size-2)),(source.GetWidth()-side)/2,(source.GetHeight()-side)/2,side,side,Gdiplus::UnitPixel);
                graphics.ResetClip();Gdiplus::Pen border(Gdiplus::Color(110,208,190,221),1.2f);graphics.DrawEllipse(&border,1.f,1.f,float(size-2),float(size-2));
            }
            auto dc=CreateCompatibleDC(screen);auto old=SelectObject(dc,dib);POINT destination{ownerRect.left+x,ownerRect.top+y},origin{};SIZE dimensions{size,size};BLENDFUNCTION blend{AC_SRC_OVER,0,255,AC_SRC_ALPHA};
            ready=UpdateLayeredWindow(window,screen,&destination,&dimensions,dc,&origin,0,&blend,ULW_ALPHA)!=FALSE;
            SelectObject(dc,old);DeleteDC(dc);DeleteObject(dib);ReleaseDC(nullptr,screen);
        }
        if(ready){
            RECT current{};GetWindowRect(window,&current);
            if(current.left!=ownerRect.left+x||current.top!=ownerRect.top+y||!IsWindowVisible(window))
                SetWindowPos(window,HWND_BOTTOM,ownerRect.left+x,ownerRect.top+y,size,size,SWP_NOACTIVATE|SWP_SHOWWINDOW);
        }
        return ready;
    }
};
