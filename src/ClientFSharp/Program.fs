namespace ClientFSharp

open System
open System.Net
open System.Net.Sockets

module Program =

    let private runClientAsync () = 
        task {
            use clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp)

            printfn "Connecting to port 8087"

            clientSocket.Connect(IPEndPoint(IPAddress.Loopback, 8087))
            use stream = new NetworkStream(clientSocket)

            do! Console.OpenStandardInput().CopyToAsync(stream)
        }

    [<EntryPoint>]
    let main argv =
        runClientAsync ()
        |> Async.AwaitTask
        |> Async.RunSynchronously

        0

