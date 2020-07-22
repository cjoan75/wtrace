namespace LowLevelDesign.WTrace.Events

open System
open System.Reactive
open FSharp.Control.Reactive
open LowLevelDesign.WTrace
open Microsoft.Diagnostics.Tracing

type IsrDpcObservable (traceSource : TraceEventSource) =

    // FIXME to implement
    let subject = new Subjects.Subject<WTraceEvent>()

    interface IDisposableObservable<WTraceEvent> with
        member _.Subscribe(o) =
            subject |> Observable.subscribeObserver o

        member _.Dispose() =
            subject.Dispose()

type IsrDpcEtwHandler () =

    static member TaskGuid = Guid(int32(0xce1dbfb4), int16(0x137e), int16(0x4da6), byte(0x87), byte(0xb0), byte(0x3f), byte(0x59), byte(0xaa), byte(0x10), byte(0x2c), byte(0xbc))

    interface ITraceEtwHandler with

        member _.KernelFlags with get() = NtKeywords.DeferedProcedureCalls ||| NtKeywords.Interrupt ||| NtKeywords.ImageLoad

        member _.KernelStackFlags with get() = NtKeywords.None

        member _.UserModeProviders with get() = Seq.empty<EtwProviderRegistration>

        member _.Observe traceSource _ =
            new IsrDpcObservable(traceSource) :> IDisposableObservable<WTraceEvent>

