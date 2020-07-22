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

type TraceSessionFilter = 
| Process of Id : int32 * IncludeChildren : bool
| SystemOnly
| Everything

type TraceSessionControl (sessionFilter) =

    let broadcast = new Subjects.Subject<WTraceEvent>()
    let cts = new CancellationTokenSource()

    // FIXME: I need to think how I should trace the ISR/DPC events
    let etwHandlers : array<ITraceEtwHandler> = match sessionFilter with
                                                | SystemOnly -> [| IsrDpcEtwHandler() |]
                                                | _ -> [| FileIoEtwHandler() |]

    // subscribe the ETW broadcast to the main ETW session - we pass all the events to the
    // observables. They are responsible for rejecting events that does not interest them.
    let etwEventFilter = match sessionFilter with
                         | Process (pid, _) -> fun (ev : EtwTraceEvent) -> ev.ProcessID = pid // FIXME: children!!!
                         | SystemOnly -> fun (ev : EtwTraceEvent) -> ev.TaskGuid = IsrDpcEtwHandler.TaskGuid
                         | Everything -> fun _ -> true

    member _.EtwHandlers = etwHandlers

    member _.EtwEventFilter = etwEventFilter

    member _.CancellationToken = cts.Token

    member _.Broadcast = broadcast

    member _.StopSession() = cts.Cancel()

    interface IDisposable with
        member _.Dispose() =
            broadcast.Dispose()
            cts.Dispose()


module TraceSession =

    // It's a blocking call, to stop it cancel the CancellationToken
    let StartProcessingEvents (sessionControl: TraceSessionControl) = async {
        let etwHandlers = sessionControl.EtwHandlers

        let requiredKernelFlags = NtKeywords.Process ||| NtKeywords.Thread ||| NtKeywords.ImageLoad
        let kernelFlags = etwHandlers |> Seq.fold (fun flag hndlr -> flag ||| hndlr.KernelFlags) requiredKernelFlags
        let kernelStackFlags = etwHandlers |> Seq.fold (fun flag hndlr -> flag ||| hndlr.KernelStackFlags) NtKeywords.None

        use traceSession = new TraceEventSession("wtrace-rt")

        use _ctr = sessionControl.CancellationToken.Register(fun () -> traceSession.Stop() |> ignore)

        traceSession.EnableKernelProvider(kernelFlags, kernelStackFlags) |> ignore

        // enable user mode providers
        let traceEventLevel = TraceEventLevel.Always
        let traceEventOptions = TraceEventProviderOptions()
        // FIXME traceEventOptions.StacksEnabled

        etwHandlers 
        |> Seq.collect (fun h -> h.UserModeProviders)
        |> Seq.groupBy (fun provider -> provider.Id)
        |> Seq.map (fun (providerId, keywordSeq) -> (providerId, keywordSeq |> Seq.fold (fun k curr -> k ||| curr.Keywords) 0UL))
        |> Seq.iter (fun (providerId, keywords) -> 
            traceSession.EnableProvider(providerId, traceEventLevel, keywords, traceEventOptions) |> ignore
        )

        // FIXME: maybe I can make it nicer - CreateFromTraceEventSession enables
        // kernel provider so must be run after the EnableKernelProvider call
        use traceLogSource = TraceLog.CreateFromTraceEventSession(traceSession)


        // we will use a Subject as the default TraceEventSubscription clones the ETW event. As we want it
        // to happen only once per event, we will publish events.
        use etwbroadcast = new Subjects.Subject<EtwTraceEvent>()

        // collect observables for all the ETW handlers
        let observables = etwHandlers |> Array.map (fun h -> h.Observe(etwbroadcast))

        use _etwSubscription = traceLogSource.ObserveAll() 
                               |> Observable.filter sessionControl.EtwEventFilter
                               |> Observable.subscribeObserver etwbroadcast

        // we will merge and broadcast events from all the handlers
        use _broadcastSubscription = observables |> Array.map(fun o -> o :> IObservable<_>)
                                     |> Observable.mergeArray 
                                     |> Observable.subscribeObserver sessionControl.Broadcast

        traceLogSource.Process() |> ignore
        
        // FIXME: send OnComplete events

        observables |> Seq.iter (fun o -> o.Dispose())
    }

