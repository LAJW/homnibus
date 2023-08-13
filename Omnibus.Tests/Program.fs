﻿namespace Omnibus.Test

open Omnibus
open System

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type GlueStatuses() =
    [<TestMethod>]
    member _.Immediate() =
        let results =
            glueStatuses [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [])

    [<TestMethod>]
    member _.SingleStep() =
        let results =
            glueStatuses [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
            |> Seq.toList
        
        Assert.AreEqual(results, [TimeSpan.FromDays(2)])

    [<TestMethod>]
    member _.StandardFlow() =
        let results =
            glueStatuses [
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
            glueStatuses [
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
            glueStatuses [
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
    [<TestMethod>]
    member _.Immediate() =
        let result =
            cycleTime [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 1, result)

    [<TestMethod>]
    member _.SingleStep() =
        let result =
            cycleTime [
                { Date = DateTime.Parse "2020-01-01"; State = "Ready" }
                { Date = DateTime.Parse "2020-01-02"; State = "In Progress" }
                { Date = DateTime.Parse "2020-01-04"; State = "Done" }
            ]
        
        Assert.AreEqual(TimeSpan.FromDays 3, result)

    [<TestMethod>]
    member _.StandardFlow() =
        let result =
            cycleTime [
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
            cycleTime [
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
            cycleTime [
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