
module LowLevelDesign.WTrace.Program

let main (argv : array<string>) =
    let args = argv |> CommandLine.parseArgs
    printfn "Message from fantastic library"

