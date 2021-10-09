### Team member: Harish Reddy Bollavaram(The one and only)

### How to run the program

`dotnet fsi Project2.fsx <number-of-nodes> <topology> <protocol>`  
Example,  
`dotnet fsi Project2.fsx 1000 3d gossip`  
Options for topologies: `line`, `full`, `3d`, `imperfect-3d`  
Options for protocols: `gossip`, `push-sum`

### What is working

1. All topologies line, full, 3d and imperfect-3d are working with both gossip and push-sum protocol.
2. All nodes in all topologies will always `converge`.
3. In case of `Gossip`, `Convergence` means that every actor in the topology listens to the rumour atleast 10 times.
4. For gossip, if the rumour stops spreading, then a helper actor will always wake up one of the other unconverged nodes and pass on the rumour to that node.

### Largest network

#### Gossip

| Network | Line  | Full | 3D    | Imperfect 3D |
| ------- | ----- | ---- | ----- | ------------ |
| Nodes   | 16000 | 4000 | 32000 | 32000        |

#### Push-sum

| Network | Line  | Full | 3D    | Imperfect 3D |
| ------- | ----- | ---- | ----- | ------------ |
| Nodes   | 16000 | 4000 | 32000 | 32000        |

My neighbor generation function is too slow for Full topology because there are N-1 neighbors for each node.
