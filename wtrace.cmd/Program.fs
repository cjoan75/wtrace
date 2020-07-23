
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.Reflection
open System.Threading
open Microsoft.FSharp.Linq
open Microsoft.Diagnostics.Tracing.Session
open PInvoke
open System.Runtime.InteropServices

type TraceTarget = 
| RunningProcess of Pid : int32 * IncludeChildren : bool
| NewProcess of Args : list<string> * IncludeChildren : bool
| Everything
| SystemOnly

type TraceOptions = {
    Target : TraceTarget
    NewConsole : bool
    NoSummary : bool
    WithStacks : bool
}

let logger = new TraceSource("LowLevelDesign.WTrace")

(* Launching processes logic *)

#nowarn "9"

let launchProcessSuspended (args) = result {
    let mutable pi = Kernel32.PROCESS_INFORMATION()

    do!
        let mutable si = Kernel32.STARTUPINFO(hStdInput = IntPtr.Zero, hStdOutput = IntPtr.Zero, hStdError = IntPtr.Zero)
        let flags = Kernel32.CreateProcessFlags.CREATE_SUSPENDED |||
                    Kernel32.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT
        // FIXME: new console Kernel32.CreateProcessFlags.CREATE_NEW_CONSOLE
        if not (Kernel32.CreateProcess(null, args |> String.concat " ", IntPtr.Zero, IntPtr.Zero, 
                                       false, flags, IntPtr.Zero, null, &si, &pi)) then
            Error (WinApi.Win32ErrorMessage (Marshal.GetLastWin32Error()))
        else Ok ()

    return (pi.dwProcessId, new Kernel32.SafeObjectHandle(pi.hProcess), new Kernel32.SafeObjectHandle(pi.hThread))
}

let traceRunningProcess (pid) = result {
    let! hProcess = 
        let accessMask = Kernel32.ACCESS_MASK(uint32(Kernel32.ACCESS_MASK.StandardRight.SYNCHRONIZE))
        let h = Kernel32.OpenProcess(accessMask, false, pid)
        if h.IsInvalid then Error (WinApi.Win32ErrorMessage (Marshal.GetLastWin32Error()))
        else Ok h

    return (pid, hProcess);
}

(* Command line *)

let flags = seq { "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "withstacks"; "h"; "?"; "help" }

let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

let showHelp () =
    let appAssembly = Assembly.GetEntryAssembly();
    let appName = appAssembly.GetName();

    printfn "%s v%s - collects traces of Windows processes" appName.Name (appName.Version.ToString())
    let customAttrs = appAssembly.GetCustomAttributes(typeof<AssemblyCompanyAttribute>, true);
    assert (customAttrs.Length > 0)
    printfn "Copyright (C) %d %s" DateTime.Today.Year (customAttrs.[0] :?> AssemblyCompanyAttribute).Company
    printfn ""
    printfn "Usage: %s [OPTIONS] pid|imagename args" appName.Name
    printfn ""
    printfn "Options:"
    printfn "-f, --filter=FILTER   Displays only events which names contain the given keyword"
    printfn "                      (case insensitive). Does not impact the summary."
    printfn "-s, --system          Collects only system statistics (DPC/ISR) - shown in the summary."
    printfn "-c, --children        Collects traces from the selected process and all its children."
    printfn "--newconsole          Starts the process in a new console window."
    printfn "--nosummary           Prints only ETW events - no summary at the end."
    printfn "--withstacks          Collects data required to resolve stacks (memory consumption is much higher)"
    printfn "-h, --help            Shows this message and exits."
    printfn ""
    // FIXME: save parameter and a parameter to resolve stacks

let parseCmdArgs args = 
    let isFlagEnabled = isFlagEnabled args
    let isInteger v = 
        let r, _ = Int32.TryParse(v)
        r

    let target = 
        if seq { "s"; "system" } |> isFlagEnabled then
            SystemOnly
        else 
            match args |> Map.tryFind "" with 
            | None -> Everything
            | Some [ pid ] when isInteger pid -> RunningProcess (Int32.Parse(pid), seq { "c"; "children" } |> isFlagEnabled)
            | Some args -> NewProcess (args, seq { "c"; "children" } |> isFlagEnabled)

    // FIXME filters
    {
        Target = target
        NewConsole = Seq.singleton "newconsole" |> isFlagEnabled
        NoSummary = Seq.singleton "nosummary" |> isFlagEnabled
        WithStacks = Seq.singleton "withstacks" |> isFlagEnabled
    }

let checkElevated () = 
    if TraceEventSession.IsElevated() ?= true then Ok ()
    else Error "Must be elevated (Admin) to run this program."

let start args = result {
    let InvalidHandle = Kernel32.SafeObjectHandle.Invalid

    let cmdArgs = parseCmdArgs args

    do! checkElevated ()

    // FIXME: create tracing session and assign Observables
    let! (filter, hProcess, resumeThread) = 
        match cmdArgs.Target with
        | NewProcess (args, c) -> result {
            let! (pid, hProcess, hThread) = launchProcessSuspended args
            let resumeThread () = 
                if Kernel32.ResumeThread(hThread) = -1 then 
                    hThread.Close()
                    Error (WinApi.Win32ErrorMessage (Marshal.GetLastWin32Error()))
                else 
                    hThread.Close()
                    Ok ()
            return if c then (TraceSessionFilter.ProcessWithChildren pid, hProcess, resumeThread)
                   else (TraceSessionFilter.Process pid, hProcess, resumeThread)
          }
        | RunningProcess (pid, c) -> result {
            let! (pid, hProcess) = traceRunningProcess pid
            return if c then (TraceSessionFilter.ProcessWithChildren pid, hProcess, fun () -> Ok ())
                   else (TraceSessionFilter.Process pid, hProcess, fun () -> Ok ())
          }
        | SystemOnly -> Ok (TraceSessionFilter.KernelOnly, InvalidHandle, fun () -> Ok ())
        | _ -> Ok (TraceSessionFilter.Everything, InvalidHandle, fun () -> Ok ())

    use traceSessionControl = new TraceSessionControl(filter, cmdArgs.WithStacks)

    // subscribe to WTrace events
    // FIXME
    use _subs = traceSessionControl.Broadcast |> Observable.subscribe (fun ev -> printfn "%s/%s: '%s'" ev.TaskName ev.OpcodeName ev.Path)
   
    traceSessionControl
    |> TraceSession.StartProcessingEvents
    |> Async.Start

    // setup Ctrl + C event
    Console.CancelKeyPress.Add(
        fun ev -> 
            ev.Cancel <- true;
            traceSessionControl.StopSession()
    )

    // if the process exists, stop the session
    async { 
        match Kernel32.WaitForSingleObject(hProcess, Constants.INFINITE) with
        | Kernel32.WaitForSingleObjectResult.WAIT_FAILED -> 
            logger.TraceErrorWithMessage("Wait for process failed", Win32Exception())
        | _ -> ()
        traceSessionControl.StopSession()
    } |> Async.Start


    do! resumeThread ()

    // FIXME temporarily
    traceSessionControl.CancellationToken.WaitHandle.WaitOne() |> ignore
    Thread.Sleep(2000) // time to finish processings

    hProcess.Close()
}

let main (argv : array<string>) =
    let args = argv |> CommandLine.parseArgs flags

    if seq { "h"; "help"; "?" } |> isFlagEnabled args then
        showHelp ()
        0
    else
        match start args with
        | Ok _ -> 0
        | Error msg -> printfn "ERROR: %s" msg; 1

