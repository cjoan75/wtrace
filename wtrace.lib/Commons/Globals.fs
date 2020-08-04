[<AutoOpen>]
module LowLevelDesign.WTrace.Globals

open System.Diagnostics
open System
open System.Reactive.Linq
open System.IO
open System.Text

type TraceSource with
    member this.TraceError (ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, ex.ToString())

    member this.TraceErrorWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

    member this.TraceWarning msg =
        this.TraceEvent(TraceEventType.Warning, 0, msg)

    member this.TraceWarningWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))


type TraceWriterToLog (source : TraceSource, ?eventType : TraceEventType) =
    inherit TextWriter()

    let eventType = eventType |> Option.defaultValue TraceEventType.Verbose

    override _.Encoding with get () = Encoding.UTF8

    override _.Write (c : char) = 
        source.TraceEvent(eventType, 0, c.ToString())

    override _.Write (s : string) = 
        source.TraceEvent(eventType, 0, s)


type Observable with
    /// Creates an observable sequence from the specified Subscribe method implementation.
    static member CreateEx (subscribe: IObserver<'T> -> unit -> unit) =
        let subscribe o = 
            let m = subscribe o
            Action(m)
        Observable.Create(subscribe)


let result = ResultBuilder()


let (|?) lhs rhs = (if lhs = null then rhs else lhs)
