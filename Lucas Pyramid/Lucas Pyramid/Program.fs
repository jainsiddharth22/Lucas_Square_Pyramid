//Imports
open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor


//Message that will be passed to parents to start code as well as to reply from child to parent
type ParentMessage() = 

    //Ending number of the range
    [<DefaultValue>] val mutable endPoint: int

    //Length of the sequence
    [<DefaultValue>] val mutable length: int

    //Flag to indicate that its the starting message
    [<DefaultValue>] val mutable startFlag: bool

    //Flag to indicate that its the reply from a child
    [<DefaultValue>] val mutable replyFlag: bool

    //Result of computation of subproblem, true if sequence found, false otherwise
    [<DefaultValue>] val mutable replyVal: bool
  

//Message that will be passed to child to start working on a subproblem
type ChildMessage() = 

    //starting number of subproblem
    [<DefaultValue>] val mutable start: int

    //length of subproblem
    [<DefaultValue>] val mutable length: int


//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()


//Child Actor that will work on subproblems
let child (childMailbox:Actor<ChildMessage>) = 

    //Child Loop that will process a message on each iteration
    let rec childLoop() = actor {

        //Receive the message
        let! msg = childMailbox.Receive()

        //Sum counter that will keep track of sum so far
        let mutable sum = float 0

        //Starting value of the sequence to check
        let start = msg.start

        //Length of the sequence
        let length = msg.length

        //The answer to be returned, is false by default, will be changed to true if sequence is valid
        let mutable retVal = false

        //Run the loop from start of the sequence till end
        for i = start to start+length-1 do
                
                //Add to sum the square of each value
                sum <- sum + (float i* float i)
        
        //If the final sum has a whole number as square root then sequence is valid
        if sqrt sum % float 1 = 0.0 then
            
            //and change the answer to true
            retVal <- true

        //Get the parent to send the reply
        let sender = childMailbox.Sender()

        //Make the reply message
        let reply = new ParentMessage()

        //Reply flag is true to tell parent this message is coming from a child
        reply.replyFlag <- true

        //Start flag is false as this is not the first message to parent to start the whole computation
        reply.startFlag <- false

        //Store the start point of this sequence
        reply.endPoint <- start

        //Store the length of this sequence
        reply.length <- length

        //Store the result of this sequence
        reply.replyVal <- retVal

        //Send the message back to parent
        sender <! reply

        //Keep the loop running
        return! childLoop()
    }

    //Call to start the child loop
    childLoop()

//Timing Calculation
//Get current process
let proc = Process.GetCurrentProcess()

//Get current cpu time stamp
let cpu_time_stamp = proc.TotalProcessorTime

//Start the timer for real world time
let timer = new Stopwatch()
timer.Start()

//Parent Actor
let parent (parentMailbox:Actor<ParentMessage>) =

    //Current point at any time to start a sub problem from
    let mutable startPoint = 0

    //End point to look out for and stop calculation when reached till here
    let mutable endPoint = 0

    //length of the sequences
    let mutable length  = 0

    //A flag to know if any sequence was found, will get updated to true if atleast 1 subproblem has a solution
    let mutable result = false

    //Number of Child Actors to be made, can also be called work unit
    let mutable workSize = 10

    //The parent loop that will send subproblems to children or receive their response
    let rec parentLoop() = actor {

        //Receive a message
        let! (msg: ParentMessage) = parentMailbox.Receive()

        //If start flag is true then its a starting message to start the computation
        if(msg.startFlag) then

            //Every time the start point is from 1
            startPoint <- 1

            //End point is specified in the message
            endPoint <- msg.endPoint

            //Length is also specified in the message
            length <- msg.length

            //If the end point is less then work unit then all the children donot have to be made
            if endPoint <= workSize then
                
                //reduce the number of children to the number of subproblems
                workSize<- endPoint

            //Loop to make the children in the work unit
            for i = 1 to workSize do

                //Make a distinct name for each child
                let name:string = "child" + i.ToString()

                //Create the child
                let child = spawn parentMailbox name child

                //Make the message to send
                let sendMessage = new ChildMessage()

                //The subproblem start value that this child will work on
                sendMessage.start <- i

                //Length of subproblem
                sendMessage.length <- msg.length

                //Send the message to the child
                child <! sendMessage

                //update the start point to keep track of what subproblem have been assigned already
                startPoint <- i
        
        //reply flag is true this message is from a child
        if(msg.replyFlag) then

            //If the result is true then a valid sequence is found
            if msg.replyVal then

                //print the sequence start point on console
                printfn "%i" msg.endPoint

                //update the result flag to keep track that a valid result has been found
                result <- true

            //If the start point is less than end point there are still subproblems to be assigned
            if startPoint < endPoint then
                
                //Increment the start point to get the next subproblem to solve
                startPoint <- startPoint + 1

                //Get the child who returned the answer
                let sender = parentMailbox.Sender()

                //And send it the new message containing the next subproblem to solve
                let sendMessage = new ChildMessage()

                //Store the start point of the subproblem
                sendMessage.start <- startPoint

                //Store the length of the subproblem
                sendMessage.length <- msg.length

                //Send the message to child
                sender <! sendMessage

            //start point = end point then all subproblems have been assigned, now just start the wrap up to close everything
            else

                //Subtract the work unit on each reply coming here to see when all children are done processing
                workSize <- workSize - 1

                //Close the Child Actors
                let sender = parentMailbox.Sender()
                sender <! PoisonPill.Instance

                //If work unit reaches 0, all children are done processing so close everything and display results
                if workSize = 0 then
                    
                    //If result is still false then no valid sequence was found
                    if result = false then

                        //inform the user this
                        printfn "No sequence found"

                    //Get cpu time used by getting current cpu time and subracting the time stamp from start
                    //Similarly stop timer to get real world time used
                    //Display all the timing details
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
                    system.Terminate()
        
        //Keep parent loop running
        return! parentLoop()
    }

    //Start the parent loop
    parentLoop()


//Start point of the code, inputs passed here
[<EntryPoint>]
let main(args) =    
    
    //Create a first parent message to start the computation
    let mainMessage = new ParentMessage()

    //Store the end point as first argument passed
    mainMessage.endPoint <- args.[0] |> int

    //Store the length of the sequence as the second argument passed
    mainMessage.length <- args.[1] |> int

    //Make start flag as true since this is the first message
    mainMessage.startFlag <- true

    //Make reply flag as false since this is not a reply from a child
    mainMessage.replyFlag <- false

    //Reply value doesnot matter, just making false to be safe
    mainMessage.replyVal <- false
    
    //Create the parent actor
    let parentActor = spawn system "parent" parent
   
    //And pass it the message
    parentActor <! mainMessage
    
    //Keep the console open by making it wait for key press
    System.Console.ReadKey() |> ignore

    0
    

                        






