"""Verify packaged framework resolution using Microsoft's native hosting API."""
import ctypes as c
from pathlib import Path
import sys

app = Path(sys.argv[1]).resolve()
runtime = app / "runtime"
hostfxr = next((runtime / "host" / "fxr").glob("*/hostfxr.dll"))
library = c.CDLL(str(hostfxr))

class Parameters(c.Structure):
    _fields_ = [("size", c.c_size_t), ("host_path", c.c_wchar_p), ("dotnet_root", c.c_wchar_p)]

initialize = library.hostfxr_initialize_for_runtime_config
initialize.argtypes = [c.c_wchar_p, c.POINTER(Parameters), c.POINTER(c.c_void_p)]
initialize.restype = c.c_int32
get_property = library.hostfxr_get_runtime_property_value
get_property.argtypes = [c.c_void_p, c.c_wchar_p, c.POINTER(c.c_wchar_p)]
get_property.restype = c.c_int32
get_delegate = library.hostfxr_get_runtime_delegate
get_delegate.argtypes = [c.c_void_p, c.c_int32, c.POINTER(c.c_void_p)]
get_delegate.restype = c.c_int32
close = library.hostfxr_close
close.argtypes = [c.c_void_p]
close.restype = c.c_int32

parameters = Parameters(c.sizeof(Parameters), str(app / "时砂.exe"), str(runtime))
context = c.c_void_p()
result = initialize(str(app / "时砂.runtimeconfig.json"), c.byref(parameters), c.byref(context))
assert result >= 0 and context.value, f"Runtime initialization failed: {result & 0xffffffff:08x}"
try:
    value = c.c_wchar_p()
    assert get_property(context, "TRUSTED_PLATFORM_ASSEMBLIES", c.byref(value)) == 0
    assemblies = [Path(p).resolve() for p in value.value.split(";") if p]
    assert any(p.name == "PresentationFramework.dll" for p in assemblies), "WPF framework missing"
    assert any(p.name == "System.Private.CoreLib.dll" for p in assemblies), "Core runtime missing"
    assert all(p.is_relative_to(runtime.resolve()) for p in assemblies), "A global runtime was used"
    pointer = c.c_void_p()
    # hdt_load_assembly_and_get_function_pointer = 5; this loads the CLR without opening the app UI.
    assert get_delegate(context, 5, c.byref(pointer)) == 0 and pointer.value, "Packaged CLR could not load"
    print(f"PASS packaged CoreCLR and WPF resolve locally ({len(assemblies)} framework assemblies)")
finally:
    close(context)
