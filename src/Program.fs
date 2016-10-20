﻿module Program

open System
open Util.Debug
open Util.Format

let runUnitTests() = 
   writeFormatted (header "Running unit tests ")
   Topology.Test.run()
   Abgp.Test.run()

[<EntryPoint>]
let main argv = 
   ignore (Args.parse argv)
   let settings = Args.getSettings()
   if settings.Test then 
      runUnitTests()
      exit 0
   if settings.Bench then 
      Benchmark.generate()
      exit 0
   let (topoInfo, settings), t1 = 
      match settings.TopoFile with
      | None -> errorLine "No topology file specified, use --help to see options"
      | Some f -> Util.Profile.time Topology.readTopology f
   if settings.IsAbstract then AbstractAnalysis.checkWellformedTopology topoInfo
   match settings.PolFile with
   | None -> errorLine "No policy file specified, use --help to see options"
   | Some polFile -> 
      Util.File.createDir settings.OutDir
      Util.File.createDir settings.DebugDir
      let (lines, defs, cs), t2 = Util.Profile.time Input.readFromFile polFile
      
      let ast : Ast.T = 
         { Input = lines
           TopoInfo = topoInfo
           Defs = defs
           CConstraints = cs }
      
      let polInfo, t3 = Util.Profile.time Ast.build ast
      let res = Abgp.compileAllPrefixes polInfo
      match res.AggSafety with
      | Some safetyInfo -> 
         let i = safetyInfo.NumFailures
         
         let bad, warn = 
            match settings.Failures with
            | None -> true, true
            | Some j -> i < j, false
         if bad then 
            let x = Topology.router safetyInfo.PrefixLoc topoInfo
            let y = Topology.router safetyInfo.AggregateLoc topoInfo
            let p = safetyInfo.Prefix
            let agg = safetyInfo.Aggregate
            let msg = 
               sprintf "Could only prove aggregation black-hole safety for up to %d failures. " i 
               + sprintf "It may be possible to disconnect the prefix %s at location %s from the " 
                    (string p) x 
               + sprintf "aggregate prefix %s at %s after %d failures. " (string agg) y (i + 1) 
               + sprintf 
                    "Consider using the --failures=k flag to specify a tolerable failure level."
            if warn then warning msg
            else error msg
      | _ -> ()
      let genStats, genTime = 
         if settings.CheckOnly then None, int64 0
         else 
            let stats, t = Util.Profile.time (Generate.generate res) topoInfo
            Some(stats), t
      if settings.Stats then Stats.print res.Stats genStats (t1 + t2 + t3) genTime
   0