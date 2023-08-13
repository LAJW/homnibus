namespace Omnibus.Test

open Omnibus
open System

open Microsoft.VisualStudio.TestTools.UnitTesting

module Data =
    let config() = {
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
    let config = Data.config()

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
    
[<TestClass>]
type GlueStatuses() =
    let config = Data.config()

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

[<TestClass>]
type CycleTime() =
    let config = Data.config()

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
