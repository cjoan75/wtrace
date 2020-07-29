namespace LowLevelDesign.WTrace

open FSharp.Control.Reactive
open System
open System.Reactive
open System.Threading
open Microsoft.Diagnostics.Tracing.Etlx
open Microsoft.Diagnostics.Tracing.Session
open Microsoft.Diagnostics.Tracing
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Events
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open System.Collections.Generic

type TraceSessionFilter = 
| KernelOnly
| Process of Id : int32
| ProcessWithChildren of Id : int32
| Everything

type TraceSessionControl (sessionFilter, enableStacks : bool) =

    let eventsBroadcast = new Subjects.Subject<WTraceEvent>()
    let callStacksBroadcast = new Subjects.Subject<WTraceEventCallStack>()
    let cts = new CancellationTokenSource()

    member _.EtwHandlers : array<ITraceEtwHandler> = 
        match sessionFilter with
        | KernelOnly -> [| IsrDpcEtwHandler() |]
        | _ -> [| FileIoEtwHandler() |]

    member _.Filter = sessionFilter

    member _.EnableStacks = enableStacks

    member _.CancellationToken = cts.Token

    member _.EventsBroadcast = eventsBroadcast

    member _.CallStacksBroadcast = callStacksBroadcast

    member _.StopSession() = cts.Cancel()

    interface IDisposable with
        member _.Dispose() =
            eventsBroadcast.Dispose()
            callStacksBroadcast.Dispose()
            cts.Dispose()


module TraceSession =

    [<AutoOpen>]
    module private Private =

        type ProcessTree (traceSource : TraceEventSource, pid) =
            // everything is synchronous so the hashset could be a regular hashset
            let processes = HashSet<int32>()

            let onProcessStartAction = 
                Action<ProcessTraceData>(
                    fun p -> if (processes.Contains(p.ParentID)) then processes.Add(p.ProcessID) |> ignore)
            let onProcessStopAction = 
                Action<ProcessTraceData>(
                    fun p -> processes.Remove(p.ProcessID) |> ignore)

            do
                traceSource.Kernel.add_ProcessStart(onProcessStartAction)
                traceSource.Kernel.add_ProcessStop(onProcessStopAction)

                processes.Add(pid) |> ignore

            member _.Contains pid = processes.Contains(pid)

            interface IDisposable with
                member _.Dispose() = 
                    traceSource.Kernel.remove_ProcessStart(onProcessStartAction)
                    traceSource.Kernel.remove_ProcessStop(onProcessStopAction)

        type ProcessFilter =
        | SingleProcess of int
        | ProcessTree of ProcessTree
        | AllProcesses

        let registerCustomProviders (sessionControl : TraceSessionControl) (traceSession : TraceEventSession) filter =
            let etwHandlers = sessionControl.EtwHandlers
            let customProvidersById = etwHandlers |> Seq.collect (fun h -> h.UserModeProviders)
                                                  |> Seq.groupBy (fun provider -> provider.Id)

            // register custom parsers (only one by provider id) and create Observables
            let customParsers = 
                customProvidersById
                |> Seq.map (fun (providerId, registrations) -> (providerId, registrations |> Seq.head))
                |> Seq.map (fun (providerId, registration) -> 
                                (providerId, registration.RegisterParser(traceSession.Source :> TraceEventSource)
                                |> Observable.filter filter))
                |> Map.ofSeq

            // enable custom mode providers
            let traceEventLevel = TraceEventLevel.Always
            let traceEventOptions = TraceEventProviderOptions(StacksEnabled = sessionControl.EnableStacks)
            // CHECKME: traceEventOptions.ProcessIDFilter - more efficient filtering for SingleProcess

            customProvidersById 
            |> Seq.map (fun (providerId, registrations) -> (providerId, registrations |> Seq.fold (fun k reg -> k ||| reg.Keywords) 0UL))
            |> Seq.iter (fun (providerId, keywords) -> 
                traceSession.EnableProvider(providerId, traceEventLevel, keywords, traceEventOptions) |> ignore
            )

            customParsers


    // Starts the ETW session
    let StartProcessingEvents (sessionControl : TraceSessionControl) = 
        let etwHandlers = sessionControl.EtwHandlers

        let requiredKernelFlags = NtKeywords.Process ||| NtKeywords.Thread ||| NtKeywords.ImageLoad
        let kernelFlags = etwHandlers |> Seq.fold (fun flag hndlr -> flag ||| hndlr.KernelFlags) requiredKernelFlags
        let kernelStackFlags =
            if sessionControl.EnableStacks then
                etwHandlers |> Seq.fold (fun flag hndlr -> flag ||| hndlr.KernelStackFlags) NtKeywords.None
            else NtKeywords.None

        use traceSession = new TraceEventSession("wtrace-rt")

        use _ctr = sessionControl.CancellationToken.Register(fun () -> traceSession.Stop() |> ignore)

        traceSession.EnableKernelProvider(kernelFlags, kernelStackFlags) |> ignore

        let processFilter = match sessionControl.Filter with
                            | ProcessWithChildren pid -> ProcessTree (new ProcessTree(traceSession.Source, pid))
                            | Process pid -> SingleProcess pid
                            | _ -> AllProcesses

        let observableFilter = match processFilter with
                               | SingleProcess pid -> fun (ev : EtwTraceEvent) -> ev.ProcessID = pid
                               | ProcessTree tree -> fun ev -> tree.Contains(ev.ProcessID)
                               | _ ->
                                    let currentProcessId = Diagnostics.Process.GetCurrentProcess().Id
                                    fun ev -> ev.ProcessID <> currentProcessId

        // prepare custom providers and parsers
        let customProviderBroadcasts = 
            match sessionControl.Filter with
            | KernelOnly -> Map.empty<Guid, IObservable<EtwTraceEvent>>
            | _ -> registerCustomProviders sessionControl traceSession observableFilter

        // CreateFromTraceEventSession enables kernel provider so must be run after the EnableKernelProvider call
        use traceLogSource = TraceLog.CreateFromTraceEventSession(traceSession)

        use kernelBroadcast = new Subjects.Subject<EtwTraceEvent>()
        use _kernelSub = traceLogSource.Kernel.Observe() 
                         |> Observable.filter observableFilter
                         |> Observable.subscribeObserver kernelBroadcast

        // FIXME: subscribe the call stack observer

        // collect observables for all the ETW handlers
        let observables = 
            let createObservables (h : ITraceEtwHandler) = 
                // subscribe a given observable to all its parsers
                let customObservables =
                    h.UserModeProviders
                    |> Seq.choose (fun p -> customProviderBroadcasts |> Map.tryFind p.Id)
                    |> Seq.map (fun parser -> h.Observe parser)

                if h.KernelFlags <> NtKeywords.None then 
                    customObservables |> Seq.append (Seq.singleton (h.Observe kernelBroadcast))
                else customObservables
            
            etwHandlers 
            |> Seq.collect createObservables
            |> Array.ofSeq

        // we will merge and broadcast events from all the handlers
        use _broadcastSubscription = observables |> Array.map(fun o -> o :> IObservable<_>)
                                     |> Observable.mergeArray 
                                     |> Observable.subscribeObserver sessionControl.EventsBroadcast

        traceLogSource.Process() |> ignore
        
        sessionControl.EventsBroadcast.OnCompleted()

        observables |> Seq.iter (fun o -> o.Dispose())

        match processFilter with
        | ProcessTree tree -> (tree :> IDisposable).Dispose()
        | _ -> ()

        // FIXME: here we will need to transmit some meta events (call stacks etc.)

