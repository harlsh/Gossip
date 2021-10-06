#load "Message.fsx"

#r "nuget: Akka.Remote"
#r "nuget: Akka.FSharp"

open Akka.FSharp
open Akka.Actor
open System
open Message

let system = System.create "my-system" (Configuration.defaultConfig())

let numNodes = 64
let random = Random()

let mutable actors : IActorRef list = [];

let calculateNeighborsFor3D (i: int) (j: int) (k: int) (n: int)= 
    if i < 0 || j < 0 || k < 0 || i > n || j > n || k > n then
        -1
    else
    n*n*i + n*j + k
        

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
        let size = (numNodes |> float) ** (1.0/3.0) |> int       
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
                                        calculateNeighborsFor3D i j (k-1) n] |> List.filter (fun x -> x <> -1)] 

        result
    
    | "imperfect-3d" -> 
        let size = (numNodes |> float) ** (1.0/3.0) |> int       
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
                                        random.Next(numNodes)] |> List.filter (fun x -> x <> -1)] 

        result
    | _ -> 
        printfn "Wrong message"
        []


let Master( mailbox: Actor<_>) = 
    let mutable count = 0
    
    let rec loop() = actor{
        let! message = mailbox.Receive();
        match message with
        | StartGossip(topology) ->
            printfn "Started gossip"
            let actorNumber = random.Next(numNodes)
            actors.[actorNumber] <! Gossip(topology, actorNumber)

        | StopGossip(actor) -> 
            printfn "%s stopped" (actor.ToString())
            mailbox.Context.Stop(mailbox.Sender())
        | _ -> printfn "Wrong message"   
        return! loop()
    }
    loop()

let master = spawn system "master" Master

let Worker(mailbox: Actor<_>) = 
    let mutable count = 0

    let rec loop() = actor{
        let! message = mailbox.Receive()

        match message with
        | Gossip(topology, actorNumber) -> 
            if count = 10 then
                master <! StopGossip(mailbox.Self)
            count <- count + 1
            let currentActor = topology.[actorNumber]
            let randomNumber = random.Next(currentActor.Length)
            let neighborNumber = currentActor.[randomNumber]
            actors.[neighborNumber] <! Gossip(topology, neighborNumber)

        | _ -> printfn "Wrong message"
        return! loop()
    }
    loop()

actors <- [ for i in 0 .. numNodes-1 do yield spawn system ($"worker{i}") Worker ]

let topology = generateTopology "3d" numNodes
printfn "%A" topology
master <! StartGossip(topology)





        














