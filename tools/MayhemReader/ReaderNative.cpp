#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dwmapi.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Globalization.h>
#include <winrt/Windows.Media.Ocr.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Data.Json.h>
#include <algorithm>
#include <memory>
using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
using namespace winrt::Windows::Graphics::Imaging;
using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::Data::Json;
using namespace winrt::Windows::Media::Ocr;
namespace {
com_ptr<ID3D11Device> device;
com_ptr<ID3D11DeviceContext> context;
com_ptr<ID3D11Texture2D> staging;
IDirect3DDevice direct{nullptr};
GraphicsCaptureItem item{nullptr};
Direct3D11CaptureFramePool pool{nullptr};
GraphicsCaptureSession session{nullptr};
OcrEngine ocr{nullptr};
HWND target=nullptr;
int poolWidth=0,poolHeight=0;
int stage=0;
}
extern "C" __declspec(dllexport) int __cdecl ReaderStage(){return stage;}
// All exports are invoked on one dedicated MTA worker thread.
extern "C" __declspec(dllexport) int __cdecl ReaderInit() {
    try {init_apartment(apartment_type::multi_threaded);
        ocr=OcrEngine::TryCreateFromLanguage(winrt::Windows::Globalization::Language(L"en-US"));
        if(!ocr)return HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);
        return S_OK;
    }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) void __cdecl ReaderStop() {
    if(session){session.Close();session=nullptr;}if(pool){pool.Close();pool=nullptr;}
    item=nullptr;staging=nullptr;context=nullptr;device=nullptr;direct=nullptr;target=nullptr;
}
extern "C" __declspec(dllexport) int __cdecl ReaderStart(HWND window) {
    try {
        ReaderStop();target=window;stage=1;
        check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nullptr,0,D3D11_SDK_VERSION,device.put(),nullptr,context.put()));
        stage=2;auto dxgi=device.as<IDXGIDevice>();com_ptr<IInspectable> inspectable;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(),inspectable.put()));direct=inspectable.as<IDirect3DDevice>();
        stage=3;auto factory=get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();
        check_hresult(factory->CreateForWindow(window,guid_of<GraphicsCaptureItem>(),put_abi(item)));
        stage=4;auto size=item.Size();poolWidth=size.Width;poolHeight=size.Height;
        pool=Direct3D11CaptureFramePool::CreateFreeThreaded(direct,DirectXPixelFormat::B8G8R8A8UIntNormalized,2,size);
        stage=5;session=pool.CreateCaptureSession(item);session.IsCursorCaptureEnabled(false);stage=6;session.StartCapture();stage=7;return S_OK;
    }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) int __cdecl ReaderFrame(unsigned char* data,int capacity,int* width,int* height,
    long long* captureTicks,int* cropX,int* cropY,int* clientWidth,int* clientHeight) {
    try {
        if(!pool)return S_FALSE;
        auto frame=pool.TryGetNextFrame();if(!frame)return S_FALSE;
        if(auto next=pool.TryGetNextFrame()){frame.Close();frame=std::move(next);}
        auto size=frame.ContentSize();
        if(size.Width!=poolWidth||size.Height!=poolHeight){
            frame.Close();poolWidth=size.Width;poolHeight=size.Height;
            pool.Recreate(direct,DirectXPixelFormat::B8G8R8A8UIntNormalized,2,size);return S_FALSE;
        }
        auto access=frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
        com_ptr<ID3D11Texture2D> source;check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D),source.put_void()));
        D3D11_TEXTURE2D_DESC sd{};source->GetDesc(&sd);
        RECT bounds{},client{};POINT origin{};
        check_hresult(DwmGetWindowAttribute(target,DWMWA_EXTENDED_FRAME_BOUNDS,&bounds,sizeof(bounds)));
        if(!GetClientRect(target,&client)||!ClientToScreen(target,&origin))return E_FAIL;
        int left=std::clamp(int(origin.x-bounds.left),0,int(sd.Width)-1);
        int top=std::clamp(int(origin.y-bounds.top),0,int(sd.Height)-1);
        int cw=std::clamp(int(client.right),1,int(sd.Width)-left),ch=std::clamp(int(client.bottom),1,int(sd.Height)-top);
        // Wide central card band. No full desktop or full-window image is saved.
        int x=int(cw*.12),y=int(ch*.22),w=int(cw*.76),h=int(ch*.40);
        if(w<1||h<1||w>4096||h>2160||int64_t(w)*h*4>capacity)return E_INVALIDARG;
        D3D11_TEXTURE2D_DESC old{};if(staging)staging->GetDesc(&old);
        if(!staging||old.Width!=w||old.Height!=h){
            staging=nullptr;D3D11_TEXTURE2D_DESC d{};d.Width=w;d.Height=h;d.MipLevels=d.ArraySize=1;
            d.Format=DXGI_FORMAT_B8G8R8A8_UNORM;d.SampleDesc.Count=1;d.Usage=D3D11_USAGE_STAGING;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
            check_hresult(device->CreateTexture2D(&d,nullptr,staging.put()));
        }
        D3D11_BOX box{UINT(left+x),UINT(top+y),0,UINT(left+x+w),UINT(top+y+h),1};
        context->CopySubresourceRegion(staging.get(),0,0,0,0,source.get(),0,&box);
        D3D11_MAPPED_SUBRESOURCE mapped{};check_hresult(context->Map(staging.get(),0,D3D11_MAP_READ,0,&mapped));
        for(int row=0;row<h;row++)memcpy(data+size_t(row)*w*4,(unsigned char*)mapped.pData+size_t(row)*mapped.RowPitch,w*4);
        context->Unmap(staging.get(),0);
        *width=w;*height=h;*captureTicks=frame.SystemRelativeTime().count();
        *cropX=x;*cropY=y;*clientWidth=cw;*clientHeight=ch;frame.Close();return S_OK;
    }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) int __cdecl ReaderOcr(unsigned char* data,int width,int height,wchar_t* output,int capacity) {
    try {
        DataWriter writer;writer.WriteBytes(array_view<const uint8_t>(data,data+size_t(width)*height*4));
        auto bitmap=SoftwareBitmap::CreateCopyFromBuffer(writer.DetachBuffer(),BitmapPixelFormat::Bgra8,width,height,BitmapAlphaMode::Ignore);
        auto result=ocr.RecognizeAsync(bitmap).get();JsonArray lines;
        for(auto line:result.Lines()){
            double x=1e9,y=1e9,right=0,bottom=0;
            for(auto word:line.Words()){auto r=word.BoundingRect();x=std::min(x,double(r.X));y=std::min(y,double(r.Y));right=std::max(right,double(r.X+r.Width));bottom=std::max(bottom,double(r.Y+r.Height));}
            JsonObject obj;obj.SetNamedValue(L"text",JsonValue::CreateStringValue(line.Text()));
            obj.SetNamedValue(L"x",JsonValue::CreateNumberValue(x));obj.SetNamedValue(L"y",JsonValue::CreateNumberValue(y));
            obj.SetNamedValue(L"w",JsonValue::CreateNumberValue(right-x));obj.SetNamedValue(L"h",JsonValue::CreateNumberValue(bottom-y));lines.Append(obj);
        }
        auto json=lines.Stringify();if(json.size()+1>capacity)return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
        wcscpy_s(output,capacity,json.c_str());bitmap.Close();writer.Close();return S_OK;
    }catch(...){return to_hresult();}
}
