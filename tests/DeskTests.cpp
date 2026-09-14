#include <cwctype>
#include <cassert>
#include "../src/Battlestation.Native/DeskModel.h"
int main(){
    assert(ClockDigits(0)==L"00");assert(ClockDigits(9)==L"09");assert(ClockDigits(23)==L"23");assert(ClockDigits(59)==L"59");
    assert(IgnoreProjectDirectory(L"node_modules"));assert(IgnoreProjectDirectory(L".GIT"));assert(IgnoreProjectDirectory(L"Library"));assert(!IgnoreProjectDirectory(L"src"));
}
