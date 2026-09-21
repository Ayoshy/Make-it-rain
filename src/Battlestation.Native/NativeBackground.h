#pragma once
#include <windows.h>
#include <string>
namespace NativeBackground {
void Start(HWND parent, const std::wstring& resources,int left,int top,int width,int height,int seam);
void Stop();
void SetVisibility(int monitors);
void SetCanvas(int left,int top,int width,int height,int seam);
void Capture();
void SetPalette(int index,const unsigned int* colors);
void SetTheme(int index,bool immediate);
void SetAppearance(bool animate,float opacity);
void SetAudio(float bass,float middle,float treble,float intensity);
void SetSceneFade(float alpha);
void SetPanelFront(int slot);
void SetGlass(float x,float conradY,float codexY,float width,float conradHeight,float codexHeight);
void SetDockGlass(float x,float y,float width,float height);
void SetPanelGlass(int slot,float x,float y,float width,float height);
}
