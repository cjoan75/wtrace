namespace LowLevelDesign.WTrace.ETW

open System
open System.Diagnostics
open System.Reactive
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Etlx
open LowLevelDesign.WTrace
open FSharp.Control.Reactive

// FIXME: we need to pass the SymbolReader here
type CallStackObservable (sessionObservable : IObservable<EtwTraceEvent>) as this =
    let logger = TraceSource("WTrace.ETW.CallStacks")
    
    let subscription = sessionObservable |> Observable.subscribeObserver this
    let subject = new Subjects.Subject<WTraceEventCallStack>()

    // FIXME: buffer for the past events

    do ()

    interface IObserver<TraceEvent> with
        member _.OnNext(ev) =
            // FIXME: perform the stack resolution
            let callstack = ev.CallStack()
            let codeAddresses = callstack.CodeAddress.CodeAddresses
            // codeAddresses.LookupSymbolsForModule - this one finds all the addresses in the
            // range and loads them. It's bad

        member _.OnError(ex) = subject.OnError(ex)

        member _.OnCompleted() = subject.OnCompleted()

    interface IDisposableObservable<WTraceEventCallStack> with
        member _.Subscribe(o) =
            subject |> Observable.subscribeObserver o

        member _.Dispose() =
            subscription.Dispose()
            subject.Dispose()

