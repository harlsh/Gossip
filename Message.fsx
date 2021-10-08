#r "nuget: Akka.Serialization.Hyperion"
open Akka.Serialization
open Akka.Actor
open System

type GossipMessageTypes =
    | InitNeighbors of IActorRef list
    | InitializeVariables of int
    | HeardMessage
    | StartPushSum of Double
    | ComputePushSum of Double * Double * Double
    | Result of Double * Double
    | InitTimer of int
    | TotalNodes of int
    | ActivateWorker 
    | DoWork
    | AddNeighbors