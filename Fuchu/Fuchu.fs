﻿namespace Fuchu

open System
open System.Runtime.CompilerServices
open FSharpx

type TestCode = unit -> Choice<unit, string>

type Test = 
    | TestCase of TestCode
    | TestList of Test list
    | TestLabel of string * Test

[<AutoOpen>]
module F =

    let withLabel lbl t = TestLabel (lbl, t)

    [<Extension>]
    let WithLabel = flip withLabel

    type TestResult = 
        | Passed
        | Failed of string
        | Exception of exn

    let testResultToString =
        function
        | Passed -> "Passed"
        | Failed error -> "Failed: " + error
        | Exception e -> "Exception: " + e.ToString()

    type TestResults = (string * TestResult) list

    let testResultsToString (results: TestResults) =
        results 
        |> Seq.map (fun (n,t) -> sprintf "%s: %s" n (testResultToString t))
        |> String.concat "\n"

    type TestResultCounts = {
        Passed: int
        Failed: int
        Errored: int
    }

    let sumTestResults (results: TestResults) =
        let counts = 
            results 
            |> Seq.map snd
            |> Seq.countBy (function
                            | Passed -> 0
                            | Failed _ -> 1
                            | Exception _ -> 2)
            |> dict
        let get i = 
            counts |> Dictionary.tryFind i |> Option.getOrDefault
        { Passed = get 0
          Failed = get 1
          Errored = get 2 }

    [<CompiledName("Ok")>]
    let ok : Choice<unit, string> = Choice1Of2 ()

    [<CompiledName("Fail")>]
    let fail (msg: string) : Choice<unit, string> = Choice2Of2 msg
    let failf fmt = Printf.ksprintf Choice2Of2 fmt

    [<CompiledName("AssertEqual")>]
    let assertEqual expected actual = 
        if actual = expected
            then ok
            else failf "Expected %A but was %A" expected actual

    let exec =
        let rec loop (parentName: string) (partialResults: TestResults) =
            function
            | TestLabel (name, test) -> loop (parentName + "/" + name) partialResults test
            | TestCase test -> 
                try
                    match test() with
                    | Choice1Of2() -> (parentName, Passed)::partialResults
                    | Choice2Of2 error -> (parentName, Failed error)::partialResults
                with e -> (parentName, Exception e)::partialResults
            | TestList tests -> List.collect (loop parentName partialResults) tests

        loop "" []

    [<Extension>]
    [<CompiledName("Run")>]
    let run tests = 
        let results = exec tests
        let report = testResultsToString results
        let summary = sumTestResults results
        printfn "%s" report
        printfn "%d tests run: %d passed, %d failed, %d errored"
            (summary.Errored + summary.Failed + summary.Passed)
            summary.Passed
            summary.Failed
            summary.Errored


type Test with
    static member NewCase (f: Func<Choice<unit, string>>) = 
        TestCase f.Invoke
    static member NewList ([<ParamArray>] tests: Test array) = 
        Array.toList tests |> TestList
    static member NewList ([<ParamArray>] tests: Func<Choice<unit, string>> array) =
        tests |> Array.map Test.NewCase |> Test.NewList