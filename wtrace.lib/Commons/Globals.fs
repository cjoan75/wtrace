[<AutoOpen>]
module LowLevelDesign.WTrace.Globals

open System.Diagnostics
open System
open System.Reactive.Linq

type TraceSource with
    member this.TraceError (ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, ex.ToString())

    member this.TraceErrorWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

    member this.TraceWarning msg =
        this.TraceEvent(TraceEventType.Warning, 0, msg)

    member this.TraceWarningWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))


type Observable with
    /// Creates an observable sequence from the specified Subscribe method implementation.
    static member CreateEx (subscribe: IObserver<'T> -> unit -> unit) =
        let subscribe o = 
            let m = subscribe o
            Action(m)
        Observable.Create(subscribe)


let result = ResultBuilder()


let (|?) lhs rhs = (if lhs = null then rhs else lhs)
