"""Read the real DualSense without calling rumble, LED or trigger-effect APIs."""
import ctypes
import json
import os
import sys
import time
from pathlib import Path
folder=Path(sys.argv[1]).resolve()
with os.add_dll_directory(str(folder)):
    native=ctypes.CDLL(str(folder/"Battlestation.Controller.dll"))
    class State(ctypes.Structure):
        _fields_=[(name,ctypes.c_int) for name in ("connected","transport","battery","power","lx","ly","rx","ry","lt","rt")]+[("buttons",ctypes.c_uint),("device",ctypes.c_uint),("enhancedReports",ctypes.c_int),("rawLt",ctypes.c_int),("rawRt",ctypes.c_int),("touchAvailable",ctypes.c_int),("touch1",ctypes.c_int),("touch2",ctypes.c_int),("touch1X",ctypes.c_float),("touch1Y",ctypes.c_float),("touch2X",ctypes.c_float),("touch2Y",ctypes.c_float)]
    assert ctypes.sizeof(State)==88
    native.PadRead.argtypes=[ctypes.POINTER(State)]
    native.PadInitialize.argtypes=[ctypes.c_int]
    enhanced="--enhanced" in sys.argv
    assert native.PadInitialize(1 if enhanced else 0)==1,"SDL3 initialization failed"
    try:
        state=State()
        for _ in range(150):
            native.PadRead(ctypes.byref(state));time.sleep(.02)
        assert state.enhancedReports==int(enhanced),"Report mode must match the explicitly requested mode"
        print(json.dumps({name:getattr(state,name) for name,_ in State._fields_}))
    finally:
        native.PadShutdown()
