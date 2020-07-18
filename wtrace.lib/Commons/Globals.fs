[<AutoOpen>]
module LowLevelDesign.WTrace.Globals

open System.Diagnostics
open System

type TraceSource with
    member this.TraceError (ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, ex.ToString())

    member this.TraceErrorWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

    member this.TraceWarning msg =
        this.TraceEvent(TraceEventType.Warning, 0, msg)

    member this.TraceWarningWithMessage (msg, ex : Exception) =
        this.TraceEvent(TraceEventType.Error, 0, sprintf "%s\nDETAILS: %s" msg (ex.ToString()))

let result = ResultBuilder()
