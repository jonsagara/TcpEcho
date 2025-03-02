﻿namespace ServerFSharp

open System.Buffers
open System.IO.Pipelines
open System.Net
open System.Net.Sockets
open System.Text

module Program =

    let private processLine (buffer : inref<ReadOnlySequence<byte>>) =
        for segment in buffer do
            printf "%s" (Encoding.UTF8.GetString(segment.Span))

        printfn ""

    let private tryReadLine (buffer : byref<ReadOnlySequence<byte>>) (line : outref<ReadOnlySequence<byte>>) =
        // Look for a EOL in the buffer.
        let newlinePosition = buffer.PositionOf((byte)'\n')

        if not(newlinePosition.HasValue) then
            // No \n character found. We have not yet read a complete line, so there is no line data to set in
            //   the line variable; make it an empty sequence.
            line <- ReadOnlySequence<byte>.Empty

            // Return false so that the caller doesn't try to process a line.
            false
        else
            // Get the entire line, but not the newline.
            line <- buffer.Slice(0, newlinePosition.Value)

            // Move the buffer reading position past the line we just read and the \n character.
            buffer <- buffer.Slice(start = buffer.GetPosition(offset = 1, origin = newlinePosition.Value))

            // Return true so that the caller knows it can process a line.
            true

    let private processLinesAsync (socket : Socket) =
        task {
            printfn $"[{socket.RemoteEndPoint}]: connected"

            // Create a PipeReader over the network stream.
            use stream = new NetworkStream(socket)
            let reader = PipeReader.Create(stream)

            let mutable continueLooping = true

            while continueLooping do
                let! result = reader.ReadAsync()
                let mutable buffer = result.Buffer
                let mutable line = ReadOnlySequence<byte>.Empty

                while tryReadLine &buffer &line do
                    // Process the line.
                    processLine &line

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End)

                // Stop reading if there's no more data coming.
                if result.IsCompleted then do
                    continueLooping <- false

            // Mark the PipeReader as complete.
            do! reader.CompleteAsync()

            printfn $"[{socket.RemoteEndPoint}]: disconnected"
        }

    let private runServerAsync () =
        task {
            use listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            listenSocket.Bind(IPEndPoint(IPAddress.Loopback, 8087))

            printfn "Listening on port 8087"

            listenSocket.Listen(backlog = 120)

            while true do
                let! socket = listenSocket.AcceptAsync()
                
                // We don't await the task. We start it and let it continue, returning immediately so that we
                //   can accept and process any new connections.
                processLinesAsync socket |> ignore

        }
    
    [<EntryPoint>]
    let main argv =
        
        runServerAsync ()
        |> Async.AwaitTask
        |> Async.RunSynchronously

        0
