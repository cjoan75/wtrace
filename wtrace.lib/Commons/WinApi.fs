module LowLevelDesign.WTrace.WinApi

open PInvoke

let CheckResultBool b = 
    if b then Ok () else Error (Win32Exception().Message)

let CheckResultHandle h =
    if h = Kernel32.INVALID_HANDLE_VALUE then Error (Win32Exception().Message)
    else Ok h

let Win32ErrorMessage (err : int) = 
    Win32Exception(err).Message
