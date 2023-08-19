﻿namespace Omnibus.Test

open Omnibus
open System

open Microsoft.VisualStudio.TestTools.UnitTesting

type Data() =
    static member config = {
        Workflow = [
            "Ready", "In Progress"
            "In Progress", "Pending Review"
            "Pending Review", "In Review"
            "In Review", "Merge & environment QA"
            "Merge & environment QA", "Ready for release"
            "Ready for release", "Done"
            
            "Ready", "Archived"
            "In Progress", "Archived"
            "Pending Review", "Archived"
            "In Review", "Archived"
            "Merge & environment QA", "Archived"
            "Ready for release", "Archived"
        ]
        InProgress = Set [
            "In Progress"
            "Pending Review"
            "In Review"
            "Merge & environment QA"
            "Ready for release"
        ]
    }

[<TestClass>]
type ConfigTest() =
    let config = Data.config

    [<TestMethod>]
    member _.EndStatuses() =
        let result = Config.endStatuses config
        Assert.AreEqual(Set ["Done"; "Archived"], result)

    [<TestMethod>]
    member _.ValidateOk() =
        let result = Config.validate config
        Assert.AreEqual(Ok(), result)
        
    [<TestMethod>]
    member _.``Validate - fail when InProgress contains a status not defined in the process``() =
        let result = Config.validate { config with InProgress = Set.union config.InProgress (Set ["rhubarb"]) }
        Assert.AreEqual(Error("Status: 'rhubarb' is not defined in transitions"), result)

    [<TestMethod>]
    member _.``Validate - fail when status in progress is an end status``() =
        let result = Config.validate { config with InProgress = Set.union config.InProgress (Set ["Done"]) }
        Assert.AreEqual(Error($"Status: 'Done' is marked as 'in progress'. End statuses are not allowed to be marked as 'in progress'. End statuses: Archived, Done"), result)
        
    [<TestMethod>]
    member _.StateOrder() =
        let order = Config.stateOrder config
        Assert.AreEqual(1, order["Ready"])
        Assert.AreEqual(2, order["In Progress"])
        Assert.AreEqual(3, order["Pending Review"])
        Assert.AreEqual(4, order["In Review"])
        Assert.AreEqual(5, order["Merge & environment QA"])
        Assert.AreEqual(6, order["Ready for release"])
        Assert.AreEqual(7, order["Done"])
        Assert.AreEqual(7, order["Archived"])

[<TestClass>]
type GlueStatuses() =
    let config = Data.config

    [<TestMethod>]
    member _.Immediate() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [])

    [<TestMethod>]
    member _.SingleStep() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [TimeSpan.FromDays(2)])

    [<TestMethod>]
    member _.StandardFlow() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-05"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-07"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [TimeSpan.FromDays 5])

    [<TestMethod>]
    member _.FlowWithBreaks() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-15"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-21"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
            |> Seq.toArray
        
        CollectionAssert.AreEqual(results, [|
            TimeSpan.FromDays 1
            TimeSpan.FromDays 2
            TimeSpan.FromDays 3
            TimeSpan.FromDays 4
            TimeSpan.FromDays 5
        |])

    [<TestMethod>]
    member _.FlowWithLessBreaks() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-05"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-08"; State = "Done" }
            ]
            |> Seq.toArray
        
        CollectionAssert.AreEqual(results, [|
            TimeSpan.FromDays 2
            TimeSpan.FromDays 3
        |])

    [<TestMethod>]
    member _.``Glue together when status change occurs on the same day``() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-22"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
            |> Seq.toList

        Assert.AreEqual(results, [ TimeSpan.FromDays 8; TimeSpan.FromDays 11 ])

    [<TestMethod>]
    member _.``Glue together when status change occurs on the same day - skip multiple``() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "Backlog" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-22"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
            |> Seq.toList

        Assert.AreEqual(results, [ TimeSpan.FromDays 8; TimeSpan.FromDays 11 ])

    [<TestMethod>]
    member _.SkippedNonWipStateSimple() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-01"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual([TimeSpan.FromDays 3], results)

    [<TestMethod>]
    member _.SkippedNonWipStateMany() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-05"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-07"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [TimeSpan.FromDays 5])

    [<TestMethod>]
    member _.RealExample() =
        let results =
            glueStatuses config [
                { Date = DateTime.Parse "13/03/2023"; State = "Ready" }
                { Date = DateTime.Parse "29/03/2023"; State = "In Progress" }
                { Date = DateTime.Parse "29/03/2023"; State = "Ready" }
                { Date = DateTime.Parse "04/04/2023"; State = "In Progress" }
                { Date = DateTime.Parse "06/04/2023"; State = "Ready" }
                { Date = DateTime.Parse "13/04/2023"; State = "Done" }
            ] |> Seq.toList
        
        Assert.AreEqual([
            TimeSpan.FromDays 0
            TimeSpan.FromDays 2
        ], results)


[<TestClass>]
type CycleTime() =
    let config = Data.config

    [<TestMethod>]
    member _.Immediate() =
        let result =
            cycleTime config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 1, result)

    [<TestMethod>]
    member _.SingleStep() =
        let result =
            cycleTime config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 3, result)

    [<TestMethod>]
    member _.StandardFlow() =
        let result =
            cycleTime config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-05"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-07"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 6, result)

    [<TestMethod>]
    member _.FlowWithBreaks() =
        let result =
            cycleTime config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-15"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-21"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
        
        // 1 + 2 + 3 + 4 + 5 + 1 * 5
        Assert.AreEqual(TimeSpan.FromDays 20, result)

    [<TestMethod>]
    member _.FlowWithLessBreaks() =
        let result =
            cycleTime config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-05"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-08"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 7, result)

[<TestClass>]
type ExtraStats() =
    let config = Data.config

    [<TestMethod>]
    member _.Immediate() =
        let input =
            [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(1, countSkips config input)
        Assert.AreEqual(1, countProcessViolations config input)
        Assert.AreEqual(0, countPushbacks config input)

    [<TestMethod>]
    member _.SingleStep() =
        let input =
            [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(1, countSkips config input)
        Assert.AreEqual(1, countProcessViolations config input)
        Assert.AreEqual(0, countPushbacks config input)

    [<TestMethod>]
    member _.StandardFlow() =
        let input =
            [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-05"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-07"; State = "Done" }
            ]
        
        Assert.AreEqual(0, countSkips config input)
        Assert.AreEqual(0, countProcessViolations config input)
        Assert.AreEqual(0, countPushbacks config input)

    [<TestMethod>]
    member _.FlowWithBreaks() =
        let input =
            [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-15"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-21"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
        
        Assert.AreEqual(5, countSkips config input)
        Assert.AreEqual(10, countProcessViolations config input)
        Assert.AreEqual(5, countPushbacks config input)

    [<TestMethod>]
    member _.FlowWithLessBreaks() =
        let input =
            [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-03"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-05"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-06"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-08"; State = "Done" }
            ]
        
        Assert.AreEqual(1, countSkips config input)
        Assert.AreEqual(2, countProcessViolations config input)
        Assert.AreEqual(1, countPushbacks config input)

[<TestClass>]
type PickLastOnGivenDate() =
    let config = Data.config
    
    [<TestMethod>]
    member _.``Empty sequence``() =
        let results = pickInProgressAndLastOnGivenDate config [] |> Seq.toArray
        CollectionAssert.AreEqual(results, [| |])
    
    [<TestMethod>]
    member _.``Glue together when status change occurs on the same day``() =
        let results =
            pickInProgressAndLastOnGivenDate config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-07"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-22"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
            |> Seq.toArray

        CollectionAssert.AreEqual(results, [|
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
            { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
            { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
            { Date = DateTime.Parse "2020-01-22"; State = "Done" }
        |])

    [<TestMethod>]
    member _.``Glue together when status change occurs on the same day - skip multiple``() =
        let results =
            pickInProgressAndLastOnGivenDate config [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
                { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
                { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
                { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
                { Date = DateTime.Parse "2020-01-22"; State = "Done" }
            ]
            |> Seq.toArray

        CollectionAssert.AreEqual(results, [|
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-04"; State = "Pending Review" }
            { Date = DateTime.Parse "2020-01-07"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-10"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-11"; State = "Merge & environment QA" }
            { Date = DateTime.Parse "2020-01-16"; State = "Ready for release" }
            { Date = DateTime.Parse "2020-01-22"; State = "Done" }
        |])

    [<TestMethod>]
    member _.``Pick when in progress occurs before the last state of the day``() =
        let results =
            pickInProgressAndLastOnGivenDate config [
                { Date = DateTime.Parse "13/03/2023"; State = "Ready" }
                { Date = DateTime.Parse "29/03/2023"; State = "In Progress" }
                { Date = DateTime.Parse "29/03/2023"; State = "In Review" }
                { Date = DateTime.Parse "29/03/2023"; State = "Ready" }
                { Date = DateTime.Parse "04/04/2023"; State = "In Progress" }
                { Date = DateTime.Parse "06/04/2023"; State = "Ready" }
                { Date = DateTime.Parse "13/04/2023"; State = "Done" }
            ] |> Seq.toList
        
        Assert.AreEqual([
            { Date = DateTime.Parse "13/03/2023"; State = "Ready" }
            { Date = DateTime.Parse "29/03/2023"; State = "In Progress" }
            { Date = DateTime.Parse "29/03/2023"; State = "Ready" }
            { Date = DateTime.Parse "04/04/2023"; State = "In Progress" }
            { Date = DateTime.Parse "06/04/2023"; State = "Ready" }
            { Date = DateTime.Parse "13/04/2023"; State = "Done" }
        ], results)

[<TestClass>]
type MinCycleTime() =
    let config = Data.config

    [<TestMethod>]
    member _.``Zero statuses should have minimum cycle time of 1 day``() =
        let result = minCycleTime config []
        Assert.AreEqual(result, TimeSpan.FromDays 1)

    [<TestMethod>]
    member _.``Lack of items in progress - ticket was immediately moved to done``() =
        let result = minCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-22"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 1)

    [<TestMethod>]
    member _.``Calculate cycle time from the last in progress (if exists) + 1``() =
        let result = minCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<DataTestMethod>]
    [<DataRow("In Progress")>]
    [<DataRow("Pending Review")>]
    [<DataRow("In Review")>]
    [<DataRow("Merge & environment QA")>]
    [<DataRow("Ready for release")>]
    member _.``Fall back to picking any other progress-like state``(state : string) =
        let result = minCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = state }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<TestMethod>]
    member _.``Pick progress in order of as according to the workflow``() =
        let result = minCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = "Pending Review" }
            { Date = DateTime.Parse "2020-01-15"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<TestMethod>]
    member _.``Pick the last time something went into progress``() =
        let result = minCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-05"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-10"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-15"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 6)

[<TestClass>]
type MaxCycleTime() =
    let config = Data.config

    [<TestMethod>]
    member _.``Zero statuses should have minimum cycle time of 1 day``() =
        let result = maxCycleTime config []
        Assert.AreEqual(result, TimeSpan.FromDays 1)

    [<TestMethod>]
    member _.``Lack of items in progress - ticket was immediately moved to done``() =
        let result = maxCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-22"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 1)

    [<TestMethod>]
    member _.``Calculate cycle time from the last in progress (if exists) + 1``() =
        let result = maxCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<DataTestMethod>]
    [<DataRow("In Progress")>]
    [<DataRow("Pending Review")>]
    [<DataRow("In Review")>]
    [<DataRow("Merge & environment QA")>]
    [<DataRow("Ready for release")>]
    member _.``Fall back to picking any other progress-like state``(state : string) =
        let result = maxCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = state }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<TestMethod>]
    member _.``Pick progress in order of as according to the workflow``() =
        let result = maxCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-10"; State = "Pending Review" }
            { Date = DateTime.Parse "2020-01-15"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 11)

    [<TestMethod>]
    member _.``Pick the first time something went into progress``() =
        let result = maxCycleTime config [
            { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
            { Date = DateTime.Parse "2020-01-05"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-10"; State = "In Review" }
            { Date = DateTime.Parse "2020-01-15"; State = "In Progress" }
            { Date = DateTime.Parse "2020-01-20"; State = "Done" }
        ]
        Assert.AreEqual(result, TimeSpan.FromDays 16)

[<TestClass>]
type CsvParserTest() =
    let get result =
        match result with
        | Ok value -> value
        | Error _ -> failwith "Returned error"
    
    [<TestMethod>]
    member _.Lex() =
        let result = lex "where,is,\"here\"" |> Seq.toArray
        CollectionAssert.AreEqual([| "where"; ","; "is"; ","; "\""; "here"; "\"" |], result)

    [<DataTestMethod>]
    [<DataRow("", "")>]
    [<DataRow("\"\"", "")>]
    [<DataRow("foo", "foo")>]
    [<DataRow("\"foo\"", "foo")>]
    [<DataRow("\"foo, bar\"", "foo, bar")>]
    [<DataRow("\"foo \"sarcastic\" bar\"", "foo \"sarcastic\" bar")>]
    member _.SingleItemParse(input : string, expected : string) =
        let result = parse input |> get
        Assert.AreEqual([expected], result)

    [<TestMethod>]
    member _.EmptyStrings2() =
        let result = parse "," |> get
        Assert.AreEqual([""; ""], result)

    [<TestMethod>]
    member _.EmptyStrings3() =
        let result = parse ",," |> get
        Assert.AreEqual([""; ""; ""], result)

    [<TestMethod>]
    member _.EmptyStringsWithQuotes2() =
        let result = parse "\"\",\"\"" |> get
        Assert.AreEqual([""; ""], result)

    [<TestMethod>]
    member _.EmptyStringsWithQuotes3() =
        let result = parse "\"\",\"\",\"\"" |> get
        Assert.AreEqual([""; ""; ""], result)

    [<TestMethod>]
    member _.ParseNoQuotes() =
        let result = parse "where,is,here"
        Assert.AreEqual(Ok["where"; "is"; "here"], result)

    [<TestMethod>]
    member _.ParseMixedQuotes() =
        let result = parse "where,is,\"here\""
        Assert.AreEqual(Ok["where"; "is"; "here"], result)

    [<TestMethod>]
    member _.ParseAllQuotes() =
        let result = parse "\"where\",\"is\",\"here\""
        Assert.AreEqual(Ok["where"; "is"; "here"], result)

    [<TestMethod>]
    member _.DonNotSplitOnComma() =
        let result = parse "\"where,oh where\",\"is\",\"here\""
        Assert.AreEqual(Ok["where,oh where"; "is"; "here"], result)

    [<TestMethod>]
    member _.ParseInternalQuotes() =
        let result = parse "\"where \"sarcastically\" is here\",\"is\",\"here\"" |> get
        Assert.AreEqual(["where \"sarcastically\" is here"; "is"; "here"], result)
