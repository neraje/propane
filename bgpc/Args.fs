﻿module Args

type Spec = 
    | Unit of (unit -> unit)
    | String of (string -> unit)

type Format = 
    | IR 
    | Template

type T = 
    {PolFile: string option;
     OutFile: string option;
     Format: Format;
     Test: bool;
     Debug: int; 
     DebugDir: string}

exception InvalidFormatException of string

let polFile = ref None
let outFile = ref None
let format = ref IR
let test = ref false
let debug = ref 0
let debugDir = ref "debug/"

let settings = ref None


let cleanDir dir = 
    if System.IO.Directory.Exists dir then
        System.IO.Directory.Delete(dir, true)
    System.IO.Directory.CreateDirectory(dir).Create()

let setDebugDir s =
    debugDir := s

let setDebug s = 
    let i = int s
    if i < 0 || i > 3 then 
        failwith ("Invalid debug level: " + s)
    debug := i

let setFormat s = 
    match s with 
    | "IR" -> format := IR 
    | "Template" -> format := Template
    | _ -> raise (InvalidFormatException s)

let usage = "Usage: bgpc.exe [options]"
let args = 
    [|("--pol", String (fun s -> polFile := Some s), "Policy file");
      ("--out", String (fun s -> outFile := Some s), "Output file");
      ("--debug-dir", String setDebugDir, "Debugging directory (default 'debug')");
      ("--debug:0|1|2|3", String setDebug, "Debug level (lowest 0)");
      ("--format:IR|Templ", String setFormat, "Output format (IR, Template)");
      ("--test", Unit (fun () -> test := true), "Run unit tests");
      ("--help", Unit (fun () -> ()), "Display this message");
    |]

let printHelp () = 
    let (s,_,_) = Array.maxBy (fun (s,_, _) -> String.length s) args
    let max = String.length s
    printfn "\n%s" usage
    for (param, _, descr) in args do
        let nspaces = max - (String.length param) + 3
        let spaces = String.replicate nspaces " "
        printfn "%s%s%s" param spaces descr

let exit () = 
    printHelp () 
    exit 0

let lookup (s: string) next i = 
    try 
        let arr = s.Split(':')
        let s = arr.[0]
        let (p, run, descr) = Array.find (fun (s' : string,_,_) -> s = s'.Split(':').[0]) args
        match run with
        | Unit f -> f (); i + 1
        | String f ->
            if arr.Length > 1 then 
                let s' = arr.[1]
                f s'; i + 1
            else
                match next with 
                | None ->
                    printfn "Invalid usage: %s, %s" p descr
                    exit ()
                | Some s' -> f s'; i + 2
    with
        | InvalidFormatException s ->
            printfn "Invalid format: %s" s
            exit ()
        |_ ->
            printfn "Unknown parameter: %s" s
            exit ()

let parse (argv: string[]) : unit =
    if Array.isEmpty argv then 
        exit () 
    else
    try 
        Array.find (fun s -> s = "--help") argv |> ignore
        exit ()
    with _ -> 
        let mutable i = 0
        while (i < Array.length argv) do 
            let curr = argv.[i]
            let next = if (i = Array.length argv - 1) then None else Some argv.[i+1]
            i <- lookup curr next i
    cleanDir !debugDir
    settings := 
        Some {PolFile = !polFile; OutFile = !outFile; 
              Format = !format; Test = !test; 
              Debug = !debug; DebugDir = !debugDir}

let getSettings () = 
    match !settings with
    | Some s -> s
    | None -> 
        printfn "Error: no settings found"
        exit ()