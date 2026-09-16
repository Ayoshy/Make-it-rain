#include "NativeBackground.h"
extern "C" __declspec(dllexport) void BackgroundVisibility(int monitors){NativeBackground::SetVisibility(monitors);}
extern "C" __declspec(dllexport) void BackgroundStart(HWND parent,const wchar_t* images){NativeBackground::Start(parent,images);}
extern "C" __declspec(dllexport) void BackgroundStop(){NativeBackground::Stop();}
extern "C" __declspec(dllexport) void BackgroundCapture(){NativeBackground::Capture();}
extern "C" __declspec(dllexport) void BackgroundAppearance(int animate,float opacity){NativeBackground::SetAppearance(animate!=0,opacity);}
extern "C" __declspec(dllexport) void BackgroundAudio(float bass,float middle,float treble,float intensity){NativeBackground::SetAudio(bass,middle,treble,intensity);}
extern "C" __declspec(dllexport) void BackgroundPanelFront(int slot){NativeBackground::SetPanelFront(slot);}
extern "C" __declspec(dllexport) void BackgroundGlass(float x,float cy,float my,float w,float ch,float mh){NativeBackground::SetGlass(x,cy,my,w,ch,mh);}
extern "C" __declspec(dllexport) void BackgroundDock(float x,float y,float w,float h){NativeBackground::SetDockGlass(x,y,w,h);}
extern "C" __declspec(dllexport) void BackgroundPanel(int slot,float x,float y,float w,float h){NativeBackground::SetPanelGlass(slot,x,y,w,h);}

extern "C" __declspec(dllexport) void BackgroundPalette(int index,const unsigned int* colors){NativeBackground::SetPalette(index,colors);}
extern "C" __declspec(dllexport) void BackgroundTheme(int index,int immediate){NativeBackground::SetTheme(index,immediate!=0);}
