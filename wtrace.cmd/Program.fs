
module LowLevelDesign.WTrace.Program

open System
open System.Reflection
open Microsoft.FSharp.Linq
open Microsoft.Diagnostics.Tracing.Session

let flags = seq { "s"; "system"; "c"; "children"; "newconsole"; "nosummary"; "h"; "?"; "help" }

type TraceTarget = 
| ProcessId of Pid : int32 * IncludeChildren : bool
| NewProcess of Args : array<string> * IncludeChildren : bool
| System

type AppArgs = {
    Target : TraceTarget
    NewConsole : bool
    NoSummary : bool
}

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
    printfn "-s, --system          Collects system statistics (DPC/ISR) - shown in the summary."
    printfn "-c, --children        Collects traces from the selected process and all its children."
    printfn "--newconsole          Starts the process in a new console window."
    printfn "--nosummary           Prints only ETW events - no summary at the end."
    printfn "-h, --help            Shows this message and exits."
    printfn ""
    // FIXME: save parameter and a parameter to resolve stacks

let checkElevated () = 
    if TraceEventSession.IsElevated() ?= true then Ok ()
    else Error "Must be elevated (Admin) to run this program."

let start args = result {
    // FIXME: validate args

    do! checkElevated ()

    // FIXME: create tracing session and assign Observables
}

let main (argv : array<string>) =
    let args = argv |> CommandLine.parseArgs flags

    if seq { "h"; "help"; "?" } |> Seq.exists (fun a -> args |> Map.containsKey a) then
        showHelp ()
        0
    else
        match start args with
        | Ok _ -> 0
        | Error msg -> printfn "ERROR: %s" msg; 1

