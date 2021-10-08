#load "Message.fsx"

#r "nuget: Akka.FSharp"

open Akka.FSharp
open Akka.Actor
open System
open Message
open System.Collections.Generic



let topology = Environment.GetCommandLineArgs().[3]
let protocol = Environment.GetCommandLineArgs().[4]

let timer = Diagnostics.Stopwatch()
let system = System.create "my-system" (Configuration.defaultConfig())

let nodes =
    match topology with
    | "3d" | "imperfect-3d" -> 
        (((Environment.GetCommandLineArgs().[2] |> float) ** 0.333)|>ceil ) ** 3.0 |> int
    | _ -> Environment.GetCommandLineArgs().[2] |> int


let calculateNeighborsFor3D (i: int) (j: int) (k: int) (n: int)= 
    if i < 0 || j < 0 || k < 0 || i > n || j > n || k > n then
        -1 
    else
    (n+1)*(n+1)*i + (n+1)*j + k

let generateTopology(topology: string) (numNodes: int) : int list list= 
    match topology with
    | "line" ->    
        [0 .. numNodes - 1]
        |> List.map(fun i -> 
            if i = 0 then 
                [1]   
            elif i = numNodes - 1 then 
                [i - 1]
            else 
                [i-1; i+1]
                )
    | "full" ->
        [0 .. numNodes - 1]
        |> List.map(fun i -> [0 .. i-1] @ [i+1 .. numNodes-1])

    | "3d" ->
        let size = (numNodes |> float) ** (1.0/3.0) |> ceil |> int       
        let n = size-1
        printfn "size=%d" size
        let mutable result : int list list = []

        for i in [0 .. n] do
            for j in [0 .. n] do
                for k in [0 .. n] do
                    result <- result @ [[calculateNeighborsFor3D (i+1) j k n;
                                        calculateNeighborsFor3D i (j+1) k n;
                                        calculateNeighborsFor3D i j (k+1) n;
                                        calculateNeighborsFor3D (i-1) j k n;
                                        calculateNeighborsFor3D i (j-1) k n;
                                        calculateNeighborsFor3D i j (k-1) n] |> List.filter (fun x -> x <> -1)] 

        result
    
    | "imperfect-3d" -> 
        let size = (numNodes |> float) ** (0.333) |> ceil |> int       
        let n = size-1
        let mutable result : int list list = []
        
        for i in [0 .. n] do
            for j in [0 .. n] do
                for k in [0 .. n] do
                    result <- result @ [[calculateNeighborsFor3D (i+1) j k n;
                                        calculateNeighborsFor3D i (j+1) k n;
                                        calculateNeighborsFor3D i j (k+1) n;
                                        calculateNeighborsFor3D (i-1) j k n;
                                        calculateNeighborsFor3D i (j-1) k n;
                                        calculateNeighborsFor3D i j (k-1) n;
                                        Random().Next(numNodes)] |> List.filter (fun x -> x <> -1)] 

        result
    | _ -> 
        printfn "Wrong message"
        []


let mutable  workerArray : IActorRef list = []

let Coordinator(mailbox: Actor<_>) =
    
    let mutable count = 0
    let mutable start = 0
    let mutable totalNodes = 0

    let rec loop()= actor{
        let! msg = mailbox.Receive();
        match msg with 
        | HeardMessage -> 
            let ending = DateTime.Now.TimeOfDay.Milliseconds
            count <- count + 1
            if count = totalNodes then
                timer.Stop()
                printfn "Time for convergence: %f ms" timer.Elapsed.TotalMilliseconds
                Environment.Exit(0)
        | Result (sum, weight) ->
            count <- count + 1
            if count = totalNodes then
                timer.Stop()
                
                printfn "Time for convergence: %f ms" timer.Elapsed.TotalMilliseconds
                Environment.Exit(0)
        | InitTimer strtTime -> start <- strtTime
        | TotalNodes n -> totalNodes <- n
        | _ -> ()

        return! loop()
    }            
    loop()

let coordinator = spawn system "Supervisor" Coordinator
let visited = new Dictionary<IActorRef, bool>()

let Worker(mailbox: Actor<_>) =
    let mutable rumourCount = 0
    let mutable neighbours: IActorRef list = []
    let mutable sum = 0 |>double
    let mutable weight = 1.0
    let mutable round = 1
    let mutable hasConverged = false
    
    
    let rec loop()= actor{
        let! message = mailbox.Receive();
        
        match message with 

        | InitNeighbors aref ->    
            neighbours <- aref

        | ActivateWorker ->
            if rumourCount < 11 then
                let rnd = Random().Next(0, neighbours.Length)
                
                if not visited.[neighbours.[rnd]] then
                    neighbours.[rnd] <! DoWork
                mailbox.Self <! ActivateWorker

        | DoWork ->
            if rumourCount = 0 then 
                mailbox.Self <! ActivateWorker
            if (rumourCount = 10) then 
                coordinator <! HeardMessage
                visited.[mailbox.Self] <- true
            rumourCount <- rumourCount + 1
            
        | InitializeVariables number ->
            sum <- number |> double

        | StartPushSum delta ->
            let index = Random().Next(0, neighbours.Length)

            sum <- sum / 2.0
            weight <- weight / 2.0
            neighbours.[index] <! ComputePushSum(sum, weight, delta)

        | ComputePushSum (s: float, w, delta) ->
            let newSum = sum + s
            let newWeight = weight + w
            let cal = sum / weight - newSum / newWeight |> abs

            if hasConverged then
                let index = Random().Next(0, neighbours.Length)
                neighbours.[index] <! ComputePushSum(s, w, delta)
            
            else
                if cal > delta then
                    round <- 0
                else 
                    round <- round + 1

                if  round = 3 then
                    round <- 0
                    hasConverged <- true
                    coordinator <! Result(sum, weight)
            
                sum <- newSum / 2.0
                weight <- newWeight / 2.0
                let index = Random().Next(0, neighbours.Length)
                neighbours.[index] <! ComputePushSum(sum, weight, delta)
        | _ -> ()
        return! loop()
    }            
    loop()



let HelperActor (mailbox: Actor<_>) =
    let neighbors = new List<IActorRef>()
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with 
        | AddNeighbors ->
            for i in [0..nodes-1] do
                    neighbors.Add workerArray.[i]
            mailbox.Self <! ActivateWorker
        | ActivateWorker ->
            if neighbors.Count > 0 then
                let randomNumber = Random().Next(neighbors.Count)
                let randomActor = neighbors.[randomNumber]
                
                if (visited.[neighbors.[randomNumber]]) then  
                    (neighbors.Remove randomActor) |>ignore
                else 
                    randomActor <! DoWork
                mailbox.Self <! ActivateWorker 
        | _ -> ()
        return! loop()
    }
    loop()


let helperActor = spawn system "ActorWorker" HelperActor

let topologyStructure = generateTopology topology nodes

workerArray <- [for i=0 to nodes do yield spawn system ($"worker{i}") Worker]

for i = 0 to nodes-1 do
    let neighborIndices = topologyStructure.[i]
    let neighbors = neighborIndices
                    |> List.map(fun i -> workerArray.[i])
                    
    visited.Add(workerArray.[i], false)
    workerArray.[i] <! InitNeighbors(neighbors)

let leader = Random().Next(0, nodes)
timer.Start()

match protocol with
   | "gossip" -> 
       coordinator <! TotalNodes(nodes)
       coordinator <! InitTimer(DateTime.Now.TimeOfDay.Milliseconds)
       printfn "Starting Protocol Gossip"
       workerArray.[leader] <! ActivateWorker
       helperActor<! AddNeighbors
       
   | "push-sum" -> 
       coordinator <! TotalNodes(nodes)
       coordinator <! InitTimer(DateTime.Now.TimeOfDay.Milliseconds)
       printfn "Starting Push Sum Protocol for Line"
       workerArray.[leader] <! StartPushSum(10.0 ** -10.0)     
   | _ ->
       printfn "Wrong protocol"

system.WhenTerminated.Wait()


