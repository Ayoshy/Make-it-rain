#define SDL_MAIN_HANDLED
#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>
#include <cstdint>
#include <algorithm>
// Every call is owned by the one managed input worker. No SDL video subsystem/window.
namespace {
SDL_Gamepad* pad=nullptr;
bool initialized=false;
Uint64 nextSearch=0;
struct PadState {
    int connected,transport,battery,power;
    int lx,ly,rx,ry,lt,rt;
    uint32_t buttons,device;
    int enhancedReports,rawLt,rawRt;
    int touchAvailable,touch1,touch2;
    float touch1X,touch1Y,touch2X,touch2Y;
};
static_assert(sizeof(PadState)==88);
}
extern "C" __declspec(dllexport) int PadInitialize(int touch){
    SDL_SetMainReady();
    // Override inherited environment hints before HIDAPI opens any controller.
    SDL_SetHintWithPriority(SDL_HINT_JOYSTICK_ENHANCED_REPORTS,touch?"1":"0",SDL_HINT_OVERRIDE);
    SDL_SetHintWithPriority(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS,"1",SDL_HINT_OVERRIDE);
    if(SDL_GetHintBoolean(SDL_HINT_JOYSTICK_ENHANCED_REPORTS,true)!=(touch!=0))return 0;
    initialized=SDL_Init(SDL_INIT_GAMEPAD);nextSearch=0;
    return initialized?1:0;
}
extern "C" __declspec(dllexport) void PadRead(PadState* state){
    if(!state)return;*state={};state->battery=-1;
    if(!initialized)return;
    SDL_PumpEvents();SDL_FlushEvents(SDL_EVENT_FIRST,SDL_EVENT_LAST); // Only this process's SDL queue.
    if(pad&&!SDL_GamepadConnected(pad)){SDL_CloseGamepad(pad);pad=nullptr;nextSearch=0;}
    if(!pad&&SDL_GetTicks()>=nextSearch){
        nextSearch=SDL_GetTicks()+1000;int count=0;auto ids=SDL_GetGamepads(&count);
        for(int i=0;i<count;i++)if(SDL_GetGamepadVendorForID(ids[i])==0x054c&&SDL_GetGamepadTypeForID(ids[i])==SDL_GAMEPAD_TYPE_PS5){pad=SDL_OpenGamepad(ids[i]);if(pad)break;}
        SDL_free(ids);
    }
    state->enhancedReports=SDL_GetHintBoolean(SDL_HINT_JOYSTICK_ENHANCED_REPORTS,true)?1:0;
    if(!pad)return;
    state->connected=1;state->device=SDL_GetGamepadID(pad);
    auto connection=SDL_GetGamepadConnectionState(pad);
    state->transport=connection==SDL_JOYSTICK_CONNECTION_WIRED?1:connection==SDL_JOYSTICK_CONNECTION_WIRELESS?2:0;
    state->power=(int)SDL_GetGamepadPowerInfo(pad,&state->battery);
    // Unfiltered SDL axis values. Sticks use signed 16-bit coordinates; triggers use 0..32767.
    auto joystick=SDL_GetGamepadJoystick(pad);
    state->lx=SDL_GetJoystickAxis(joystick,0);state->ly=SDL_GetJoystickAxis(joystick,1);
    state->rx=SDL_GetJoystickAxis(joystick,2);state->ry=SDL_GetJoystickAxis(joystick,3);
    state->rawLt=SDL_GetJoystickAxis(joystick,4);state->rawRt=SDL_GetJoystickAxis(joystick,5);
    state->lt=SDL_GetGamepadAxis(pad,SDL_GAMEPAD_AXIS_LEFT_TRIGGER);state->rt=SDL_GetGamepadAxis(pad,SDL_GAMEPAD_AXIS_RIGHT_TRIGGER);
    SDL_GamepadButton buttons[]={SDL_GAMEPAD_BUTTON_SOUTH,SDL_GAMEPAD_BUTTON_EAST,SDL_GAMEPAD_BUTTON_WEST,SDL_GAMEPAD_BUTTON_NORTH,SDL_GAMEPAD_BUTTON_BACK,SDL_GAMEPAD_BUTTON_GUIDE,SDL_GAMEPAD_BUTTON_START,SDL_GAMEPAD_BUTTON_LEFT_STICK,SDL_GAMEPAD_BUTTON_RIGHT_STICK,SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER,SDL_GAMEPAD_BUTTON_DPAD_UP,SDL_GAMEPAD_BUTTON_DPAD_DOWN,SDL_GAMEPAD_BUTTON_DPAD_LEFT,SDL_GAMEPAD_BUTTON_DPAD_RIGHT,SDL_GAMEPAD_BUTTON_TOUCHPAD,SDL_GAMEPAD_BUTTON_MISC1};
    for(int i=0;i<17;i++)if(SDL_GetGamepadButton(pad,buttons[i]))state->buttons|=1u<<i;
    state->touchAvailable=SDL_GetNumGamepadTouchpads(pad)>0?1:0;
    if(state->touchAvailable){
        bool down=false;float pressure=0;
        if(SDL_GetGamepadTouchpadFinger(pad,0,0,&down,&state->touch1X,&state->touch1Y,&pressure))state->touch1=down?1:0;
        down=false;
        if(SDL_GetNumGamepadTouchpadFingers(pad,0)>1&&SDL_GetGamepadTouchpadFinger(pad,0,1,&down,&state->touch2X,&state->touch2Y,&pressure))state->touch2=down?1:0;
    }
}
extern "C" __declspec(dllexport) void PadShutdown(){
    if(pad){SDL_CloseGamepad(pad);pad=nullptr;}
    if(initialized){SDL_Quit();initialized=false;}
}
