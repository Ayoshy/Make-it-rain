#pragma once
#include <algorithm>
#include <cstdint>
#include <string>

inline std::wstring ClockDigits(int value){return (value<10?L"0":L"")+std::to_wstring(value);}

inline bool IgnoreProjectDirectory(std::wstring name){
    std::transform(name.begin(),name.end(),name.begin(),[](wchar_t c){return (wchar_t)towlower(c);});
    const std::wstring ignored[]={L".git",L".codex",L".godot",L".vs",L".venv",L"venv",L"node_modules",L"bin",L"obj",L"build",L"dist",L"artifacts",L"backups",L"vendor",L"library",L"temp",L"logs",L"__pycache__",L".next",L".cache",L"_archive",L".dart_tool",L".gradle",L"target",L"out",L"cache",L".yarn",L".pnpm-store",L".turbo",L"saved",L"intermediate",L"binaries",L"deriveddatacache"};
    return std::find(std::begin(ignored),std::end(ignored),name)!=std::end(ignored);
}
