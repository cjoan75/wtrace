namespace LowLevelDesign.WTrace.Events

open System
open System.Diagnostics
open System.Reactive
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Etlx
open LowLevelDesign.WTrace
open FSharp.Control.Reactive
open Microsoft.Diagnostics.Symbols
open System.Collections.Generic

// FIXME: we need to pass the SymbolReader here
type CallStackObservable (sessionObservable : IObservable<EtwTraceEvent>) as this =
    let logger = TraceSource("WTrace.ETW.CallStacks")

    let symbolReader = new SymbolReader(new TraceWriterToLog(logger))
    
    let subscription = sessionObservable |> Observable.subscribeObserver this
    let subject = new Subjects.Subject<WTraceEventCallStack>()

    // stores information about symbols loaded for specific PDB GUIDs
    let symbolsForModules = Dictionary<string, bool>()

    // This is a rewrite of the GetSourceLine method from the TraceEvent library
    let resolveCodeAddress (addresses : TraceCodeAddresses, codeIdx : CodeAddressIndex) =
        let moduleFile = addresses.ModuleFile(codeIdx)
        if moduleFile = null then
            None
        else
            let addr = addresses.Address(codeIdx)
            logger.TraceInformation(sprintf "Resolving symbols for address 0x%x" addr)
            Some ()

    let queueCallStackToResolve (callStack : TraceCallStack) =
        //callStack.CodeAddress.Method
        ()

    do ()

    interface IObserver<TraceEvent> with
        // this method must be called synchronously (TraceCodeAddresses is not threadsafe)
        // the main application code
        member _.OnNext(ev) =

            // FIXME: try resolving immediately, postpone only if we lack symbols

            // FIXME: perform the stack resolution
            //codeAddress.GetSourceLine
            //let codeAddresses = callstack.CodeAddress.CodeAddresses
            // codeAddresses.LookupSymbolsForModule - this one finds all the addresses in the
            // range and loads them. It's bad
            ()

        member _.OnError(ex) = subject.OnError(ex)

        member _.OnCompleted() = 
            // FIXME: maybe try to resolve the pending addresses?
            subject.OnCompleted()

    interface IDisposableObservable<WTraceEventCallStack> with
        member _.Subscribe(o) =
            subject |> Observable.subscribeObserver o

        member _.Dispose() =
            subscription.Dispose()
            subject.Dispose()

