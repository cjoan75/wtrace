
module LowLevelDesign.WTrace.Program

open System
open System.Diagnostics
open System.Reflection
open FSharp.Control.Reactive
open PInvoke
open LowLevelDesign.WTrace.Storage
open System.Threading.Tasks
open System.Runtime.InteropServices

type TraceTarget = 
| RunningProcess of Pid : int32 * IncludeChildren : bool
| NewProcess of Args : list<string> * NewConsole : bool * IncludeChildren : bool
| Everything
| SystemOnly

type TraceOutput = 
| ConsoleOutput
| TraceFile of string

type TraceOptions = {
    Target : TraceTarget
    NoSummary : bool
    WithStacks : bool
    Output : TraceOutput
}


let logger = new TraceSource("LowLevelDesign.WTrace")

let flags = seq { "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "withstacks"; "h"; "?"; "help" }

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
    printfn "--withstacks          Collects data required to resolve stacks (memory consumption is much higher)."
    printfn "--save=PATH           Save the events to a file instead of writing them to the console."
    printfn "-h, --help            Shows this message and exits."
    printfn ""
    // FIXME: save parameter and a parameter to resolve stacks


let isFlagEnabled args flags = flags |> Seq.exists (fun f -> args |> Map.containsKey f)

let parseCmdArgs args = result {
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
            | Some args -> NewProcess (args, Seq.singleton "newconsole" |> isFlagEnabled, seq { "c"; "children" } |> isFlagEnabled)

    let traceOutput =
        match args |> Map.tryFind "save" with
        | Some [ path ] -> TraceFile path
        | _ -> ConsoleOutput

    // FIXME filters
    return {
        Target = target
        NoSummary = Seq.singleton "nosummary" |> isFlagEnabled
        WithStacks = Seq.singleton "withstacks" |> isFlagEnabled
        Output = traceOutput
    }
}


let subscribeEventStore dbpath (traceSessionControl : TraceSessionControl) =
    do
        use conn = EventsDatabase.openConnection dbpath
        conn |> EventsDatabase.createOrUpdateDataModel

    let save events = 
        use conn = EventsDatabase.openConnection dbpath
        events 
        |> EventsDatabase.insertEvents conn

    let defaultBatchSize = 200

    traceSessionControl.EventsBroadcast
    |> Observable.bufferSpanCount (TimeSpan.FromSeconds(5.0)) defaultBatchSize
    |> Observable.subscribe save


let subscribeConsole (traceSessionControl : TraceSessionControl) = 
    let consoleOutput ev =
        printfn "%f (%d.%d) %s/%s: '%s' '%s' result: %s" 
            ev.TimeStampRelativeMSec ev.ProcessId ev.ThreadId ev.TaskName ev.OpcodeName 
            ev.Path ev.Details ev.Result
    traceSessionControl.EventsBroadcast 
    |> Observable.subscribe consoleOutput


type TraceTargetState =
| Suspended of hProcess : Kernel32.SafeObjectHandle * hThread : Kernel32.SafeObjectHandle
| Running of hProcess : Kernel32.SafeObjectHandle
| System


let launchTargetProcessIfNeeded = function
    | NewProcess (args, newConsole, includeChildren) -> result {
        let! (pid, hProcess, hThread) = ProcessControl.launchProcessSuspended args newConsole
        return if includeChildren then (TraceSessionFilter.ProcessWithChildren pid, Suspended (hProcess, hThread))
               else (TraceSessionFilter.Process pid, Suspended (hProcess, hThread))
      }
    | RunningProcess (pid, includeChildren) -> result {
        let! (pid, hProcess) = ProcessControl.traceRunningProcess pid
        return if includeChildren then (TraceSessionFilter.ProcessWithChildren pid, Running hProcess)
               else (TraceSessionFilter.Process pid, Running hProcess)
      }
    | SystemOnly -> Ok (TraceSessionFilter.KernelOnly, System)
    | _ -> Ok (TraceSessionFilter.Everything, System)


let start args = result {
    let checkElevated () = 
        if TraceSession.IsElevated () then Ok ()
        else Error "Must be elevated (Admin) to run this program."

    let! cmd = parseCmdArgs args

    do! checkElevated ()

    let! (filter, targetState) = launchTargetProcessIfNeeded cmd.Target

    use traceSessionControl = new TraceSessionControl(filter, cmd.WithStacks)

    // console output
    use _eventSub = 
        let subscribe = 
            match cmd.Output with
            | ConsoleOutput -> subscribeConsole
            | TraceFile dbpath -> subscribeEventStore dbpath
        traceSessionControl |> subscribe
      
    // setup Ctrl + C event
    Console.CancelKeyPress.Add(
        fun ev -> 
            ev.Cancel <- true;
            traceSessionControl.StopSession()
    )

    // if the process exists, stop the session
    let waitForProcess hProcess = 
        match Kernel32.WaitForSingleObject(hProcess, Constants.INFINITE) with
        | Kernel32.WaitForSingleObjectResult.WAIT_FAILED -> 
            logger.TraceErrorWithMessage("Wait for process failed", Win32Exception())
        | _ -> ()
        traceSessionControl.StopSession()

    match targetState with
    | Suspended (hProcess, hThread) -> 
        async {
            // wait few seconds for the ETW session to start so we will pick up
            // also the initial process events
            do! Task.Delay(TimeSpan.FromSeconds(2.0)) |> Async.AwaitTask
            if Kernel32.ResumeThread(hThread) = -1 then
                logger.TraceErrorWithMessage("ResumeThread", Win32Exception(Marshal.GetLastWin32Error()))
                hThread.Close()
            else
                waitForProcess hProcess
                hThread.Close()
        }|> Async.Start
    | Running hProcess -> async { waitForProcess hProcess } |> Async.Start
    | _ -> ()

    traceSessionControl
    |> TraceSession.StartProcessingEvents

    match targetState with
    | Suspended (hProcess, _)
    | Running hProcess -> hProcess.Close()
    | _ -> ()
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

