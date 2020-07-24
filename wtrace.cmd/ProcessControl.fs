module LowLevelDesign.WTrace.ProcessControl

open System
open System.Runtime.InteropServices
open PInvoke


let launchProcessSuspended args newConsole = result {
    let mutable pi = Kernel32.PROCESS_INFORMATION()

    do!
        let mutable si = Kernel32.STARTUPINFO(hStdInput = IntPtr.Zero, hStdOutput = IntPtr.Zero, hStdError = IntPtr.Zero)
        let flags = Kernel32.CreateProcessFlags.CREATE_SUSPENDED |||
                    Kernel32.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT
        let flags = if newConsole then flags ||| Kernel32.CreateProcessFlags.CREATE_NEW_CONSOLE
                    else flags

        if not (Kernel32.CreateProcess(null, args |> String.concat " ", IntPtr.Zero, IntPtr.Zero, 
                                       false, flags, IntPtr.Zero, null, &si, &pi)) then
            Error (WinApi.Win32ErrorMessage (Marshal.GetLastWin32Error()))
        else Ok ()

    return (pi.dwProcessId, new Kernel32.SafeObjectHandle(pi.hProcess), new Kernel32.SafeObjectHandle(pi.hThread))
}

let traceRunningProcess pid = result {
    let! hProcess = 
        let accessMask = Kernel32.ACCESS_MASK(uint32(Kernel32.ACCESS_MASK.StandardRight.SYNCHRONIZE))
        let h = Kernel32.OpenProcess(accessMask, false, pid)
        if h.IsInvalid then Error (WinApi.Win32ErrorMessage (Marshal.GetLastWin32Error()))
        else Ok h

    return (pid, hProcess);
}
