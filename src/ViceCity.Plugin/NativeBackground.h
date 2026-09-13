#pragma once
#include <windows.h>
#include <string>
namespace NativeBackground {
void Start(HWND parent, const std::wstring& resources);
void Stop();
void Capture();
void SetGlass(float x,float conradY,float codexY,float width,float conradHeight,float codexHeight);
}
