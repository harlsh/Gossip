#r "nuget: Akka.Serialization.Hyperion"
open Akka.Serialization
open Akka.Actor
open System

type Message =
    | Gossip of int list list * int
    | StopGossip of IActorRef
    | StartGossip of int list list