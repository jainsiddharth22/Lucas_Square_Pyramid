#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
    | Calc of int*int*int
    | Sendc of bool*int
    | StartP of int*int
    | Done of int


let perfectSquare (x: bigint) =
    let ans1 = sqrt <| float (x)
    let ans2 = floor ans1
    ans1 = ans2

let lucas (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | Calc(x,y,window) -> 
            let mutable sum = bigint 0
            let win = bigint window
            let inc = bigint 1
            for i=x to x+window-1 do
                let j = bigint i
                sum<-sum+j*j
            
            for i = x+1 to y do
                let res = perfectSquare sum
                let j = bigint i
                sum<-sum - (j-inc)*(j-inc)
                sum<-sum + (j+win-inc)*(j+win-inc) 
                
                if(res) then mailbox.Sender () <! Sendc(res,i-1)

            mailbox.Sender () <! Done(0)
        return! loop()
    }
    loop()

//Timing Calculation
//Get current process
let proc = Process.GetCurrentProcess()

//Get current cpu time stamp
let cpu_time_stamp = proc.TotalProcessorTime

//Start the timer for real world time
let timer = new Stopwatch()
timer.Start()

let parent (mailbox: Actor<_>) =
    let mutable actors = 0
    let mutable completedActors = 0
    let rec loop () = actor {

        let! message = mailbox.Receive ()
        match message with
        | StartP(x,y) -> 
            let window = x/4
            let mutable count = 1
            while (count+window-1<=x) do
                let name = count.ToString()+"child"
                let childref = spawn system name lucas
                childref <! Calc(count,count+window-1,y)
                count<-count+window
                actors<-actors+1
             
            if(count<=x) then 
                let name = count.ToString()+"child"
                let childref = spawn system name lucas
                childref <! Calc(count,x,y)
                actors<-actors+1

        | Sendc(b,x) ->
            printfn "%i" x
        | Done(x) ->
            completedActors<-completedActors+1
        
        if(completedActors=actors) then 
            let cpu_time = int64 (proc.TotalProcessorTime-cpu_time_stamp).TotalMilliseconds
            let realTime = timer.ElapsedMilliseconds
            printfn ""
            printfn "CPU time = %dms" (cpu_time)
            printfn "Absolute time = %dms" realTime
            

            //If the problem was not very small, parallelism will show up
            if cpu_time > realTime then
                printfn "Cores Used = %f" ((float cpu_time)/ (float realTime))


            printfn ""
            printfn "Press Any Key To Close"

            //Close all actors
            system.Terminate() |> ignore
        return! loop()
    }
    loop()

let args : string array = fsi.CommandLineArgs |> Array.tail

//Extract and convert to Int
let first = args.[0]|> int
let second = args.[1]|> int

let parentActor = spawn system "parent" parent
parentActor <! StartP(first,second)

System.Console.ReadKey() |> ignore
