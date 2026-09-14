#pragma once
#include <windows.h>
#include <string>
namespace NativeBackground {
void Start(HWND parent, const std::wstring& resources);
void Stop();
void Capture();
void SetAppearance(bool animate,float opacity);
void SetPanelFront(int slot);
void SetGlass(float x,float conradY,float codexY,float width,float conradHeight,float codexHeight);
void SetDockGlass(float x,float y,float width,float height);
void SetPanelGlass(int slot,float x,float y,float width,float height);
}
