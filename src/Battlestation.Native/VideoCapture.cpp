#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dwmapi.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <algorithm>
#include <memory>
#include <vector>
#include <utility>
#include <climits>
using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
namespace {
// Only the explicitly selected application window is captured. Frames live in
// bounded memory; this module never writes images, sound, URLs or window titles.
struct Capture {
    com_ptr<ID3D11Device> device;
    com_ptr<ID3D11DeviceContext> context;
    com_ptr<ID3D11Texture2D> staging;
    com_ptr<ID3D11Texture2D> sampled,scaled;
    com_ptr<ID3D11ShaderResourceView> sampleView;
    com_ptr<ID3D11RenderTargetView> scaleView;
    com_ptr<ID3D11VertexShader> vertexShader;
    com_ptr<ID3D11PixelShader> pixelShader;
    com_ptr<ID3D11SamplerState> sampler;
    com_ptr<ID3D11Buffer> cropBuffer;
    int requestedWidth=1280,requestedHeight=720;
    uint64_t delivered=0;
    IDirect3DDevice direct{nullptr};
    GraphicsCaptureItem item{nullptr};
    Direct3D11CaptureFramePool pool{nullptr};
    GraphicsCaptureSession session{nullptr};
    std::vector<unsigned char> pixels;
    int width=0,height=0;
    uint64_t serial=0,lastFrame=0;
    HRESULT error=S_OK;
    HWND window=nullptr;
    bool closed=false;
    void Start(HWND sourceWindow) {
        window=sourceWindow;if(!IsWindow(window))throw hresult_invalid_argument();
        if(!GraphicsCaptureSession::IsSupported())throw hresult_error(E_NOTIMPL);
        check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nullptr,0,D3D11_SDK_VERSION,device.put(),nullptr,context.put()));
        const char shader[]=R"(
cbuffer Crop : register(b0) { float4 crop; }
struct Vertex { float4 position : SV_POSITION; float2 uv : TEXCOORD; };
Vertex VS(uint id : SV_VertexID) { Vertex o; float2 uv=float2((id<<1)&2,id&2); o.position=float4(uv*float2(2,-2)+float2(-1,1),0,1);o.uv=uv;return o; }
Texture2D source : register(t0); SamplerState linearSampler : register(s0);
float4 PS(Vertex i) : SV_TARGET { return float4(source.Sample(linearSampler,crop.xy+i.uv*crop.zw).rgb,1); }
)";
        com_ptr<ID3DBlob> vs,ps,errors;
        check_hresult(D3DCompile(shader,sizeof(shader)-1,nullptr,nullptr,nullptr,"VS","vs_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,vs.put(),errors.put()));
        errors=nullptr;check_hresult(D3DCompile(shader,sizeof(shader)-1,nullptr,nullptr,nullptr,"PS","ps_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,ps.put(),errors.put()));
        check_hresult(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,vertexShader.put()));
        check_hresult(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,pixelShader.put()));
        D3D11_SAMPLER_DESC sd{};sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sd.MaxLOD=D3D11_FLOAT32_MAX;
        check_hresult(device->CreateSamplerState(&sd,sampler.put()));
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=16;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check_hresult(device->CreateBuffer(&bd,nullptr,cropBuffer.put()));
        auto dxgi=device.as<IDXGIDevice>();com_ptr<IInspectable> inspectable;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(),inspectable.put()));direct=inspectable.as<IDirect3DDevice>();
        OpenSession();
    }
    void OpenSession() {
        if(session){session.Close();session=nullptr;}if(pool){pool.Close();pool=nullptr;}
        item=nullptr;
        auto factory=get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();
        check_hresult(factory->CreateForWindow(window,guid_of<GraphicsCaptureItem>(),put_abi(item)));
        pool=Direct3D11CaptureFramePool::CreateFreeThreaded(direct,DirectXPixelFormat::B8G8R8A8UIntNormalized,2,item.Size());
        session=pool.CreateCaptureSession(item);session.IsCursorCaptureEnabled(false);
        session.StartCapture();
    }
    void Frame() noexcept {
        if(closed||!pool||!session)return;
        try {
            auto frame=pool.TryGetNextFrame();if(!frame)return;
            // The UI consumes the latest of the two buffered frames at 30 Hz.
            // Polling avoids losing the last still frame to callback throttling.
            if(auto next=pool.TryGetNextFrame()){frame.Close();frame=std::move(next);}
            auto size=frame.ContentSize();if(size.Width<=0||size.Height<=0)return;
            if(size.Width>8192||size.Height>8192||(int64_t)size.Width*size.Height>32*1024*1024)throw hresult_error(E_INVALIDARG);
            auto now=GetTickCount64();
            auto access=frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();com_ptr<ID3D11Texture2D> texture;
            check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D),texture.put_void()));
            D3D11_TEXTURE2D_DESC desc{};texture->GetDesc(&desc);
            // Resizing invalidates the old frame dimensions. Discard that frame,
            // recreate the pool and never sample outside the actual texture.
            if(desc.Width!=(UINT)size.Width||desc.Height!=(UINT)size.Height) {
                texture=nullptr;access=nullptr;frame.Close();frame=nullptr;staging=nullptr;
                // Request a fresh first frame as well: Recreate alone can leave
                // a static resized window without a new FrameArrived event.
                OpenSession();return;
            }
            // Exclude standard title bars/borders. Chromium and Stremio retain
            // control of their own video overlays and subtitles.
            RECT bounds{},client{};POINT origin{};int left=0,top=0,cw=size.Width,ch=size.Height;
            if(SUCCEEDED(DwmGetWindowAttribute(window,DWMWA_EXTENDED_FRAME_BOUNDS,&bounds,sizeof(bounds)))&&GetClientRect(window,&client)&&ClientToScreen(window,&origin)) {
                left=std::clamp((int)(origin.x-bounds.left),0,size.Width-1);top=std::clamp((int)(origin.y-bounds.top),0,size.Height-1);
                cw=std::clamp((int)(client.right-client.left),1,size.Width-left);ch=std::clamp((int)(client.bottom-client.top),1,size.Height-top);
            }
            // Fit the dock before crossing GPU/CPU. Never enlarge the source.
            double scale=std::min({1.0,(double)requestedWidth/cw,(double)requestedHeight/ch});
            int w=std::max(1,(int)(cw*scale)),h=std::max(1,(int)(ch*scale));
            D3D11_TEXTURE2D_DESC old{};if(sampled)sampled->GetDesc(&old);
            if(!sampled||old.Width!=desc.Width||old.Height!=desc.Height){sampleView=nullptr;sampled=nullptr;auto d=desc;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_SHADER_RESOURCE;d.CPUAccessFlags=d.MiscFlags=0;check_hresult(device->CreateTexture2D(&d,nullptr,sampled.put()));check_hresult(device->CreateShaderResourceView(sampled.get(),nullptr,sampleView.put()));}
            old={};if(scaled)scaled->GetDesc(&old);
            if(!scaled||old.Width!=(UINT)w||old.Height!=(UINT)h){
                scaleView=nullptr;scaled=nullptr;staging=nullptr;
                D3D11_TEXTURE2D_DESC d{};d.Width=w;d.Height=h;d.MipLevels=d.ArraySize=1;d.Format=DXGI_FORMAT_B8G8R8A8_UNORM;d.SampleDesc.Count=1;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_RENDER_TARGET;
                check_hresult(device->CreateTexture2D(&d,nullptr,scaled.put()));check_hresult(device->CreateRenderTargetView(scaled.get(),nullptr,scaleView.put()));
                d.Usage=D3D11_USAGE_STAGING;d.BindFlags=0;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;check_hresult(device->CreateTexture2D(&d,nullptr,staging.put()));
            }
            context->CopyResource(sampled.get(),texture.get());
            float crop[]={left/(float)desc.Width,top/(float)desc.Height,cw/(float)desc.Width,ch/(float)desc.Height};context->UpdateSubresource(cropBuffer.get(),0,nullptr,crop,0,0);
            auto cb=cropBuffer.get();auto sr=sampleView.get();auto sm=sampler.get();auto rt=scaleView.get();D3D11_VIEWPORT viewport{0,0,(float)w,(float)h,0,1};
            context->RSSetViewports(1,&viewport);context->OMSetRenderTargets(1,&rt,nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            context->VSSetShader(vertexShader.get(),nullptr,0);context->PSSetShader(pixelShader.get(),nullptr,0);context->PSSetConstantBuffers(0,1,&cb);context->PSSetShaderResources(0,1,&sr);context->PSSetSamplers(0,1,&sm);context->Draw(3,0);
            sr=nullptr;context->PSSetShaderResources(0,1,&sr);context->OMSetRenderTargets(0,nullptr,nullptr);
            context->CopyResource(staging.get(),scaled.get());D3D11_MAPPED_SUBRESOURCE mapped{};
            check_hresult(context->Map(staging.get(),0,D3D11_MAP_READ,0,&mapped));
            try {
                pixels.resize(size_t(w)*h*4);
                for(int y=0;y<h;y++) {
                    auto row=(const unsigned char*)mapped.pData+size_t(y)*mapped.RowPitch;
                    memcpy(pixels.data()+size_t(y)*w*4,row,size_t(w)*4);
                }
            } catch(...) {context->Unmap(staging.get(),0);throw;}
            context->Unmap(staging.get(),0);width=w;height=h;serial++;lastFrame=now;error=S_OK;
        } catch(...) {error=to_hresult();}
    }
    void Stop() noexcept {
        closed=true;pixels.clear();
        try {if(session)session.Close();if(pool)pool.Close();}catch(...){}
    }
};
// Exports are called by the WPF dispatcher, never concurrently. The native
// capture service produces into its bounded frame pool on its own threads.
std::unique_ptr<Capture> active;
}
extern "C" __declspec(dllexport) void __cdecl VideoStop() {if(auto old=std::exchange(active,{}))old->Stop();}
extern "C" __declspec(dllexport) int __cdecl VideoStart(HWND window) {
    VideoStop();try {
        auto next=std::make_unique<Capture>();
        try {next->Start(window);}catch(...) {next->Stop();throw;}
        active=std::move(next);return S_OK;
    }catch(...) {return to_hresult();}
}
extern "C" __declspec(dllexport) void __cdecl VideoSize(int width,int height){if(active){active->requestedWidth=std::clamp(width,160,1920);active->requestedHeight=std::clamp(height,90,1080);}}
extern "C" __declspec(dllexport) int __cdecl VideoRead(unsigned char* buffer,int capacity,int* width,int* height,uint64_t* serial,int* age) {
    if(!active)return S_FALSE;
    active->Frame();
    *width=active->width;*height=active->height;*serial=active->serial;
    *age=active->lastFrame?(int)std::min<uint64_t>(INT_MAX,GetTickCount64()-active->lastFrame):INT_MAX;
    if(FAILED(active->error))return active->error;
    if(active->pixels.empty())return S_FALSE;
    if(active->serial==active->delivered)return S_FALSE;
    if(capacity<(int)active->pixels.size()||!buffer)return E_INVALIDARG;
    memcpy(buffer,active->pixels.data(),active->pixels.size());active->delivered=active->serial;return S_OK;
}
