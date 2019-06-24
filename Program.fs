module Program
open System
open System.Net
open Hopac
open Hopac.Infixes
open Logary
open Logary.Configuration
open Logary.Message
open Logary.Targets

module Util =
    open Microsoft.FSharp.Quotations.Patterns

    let getModuleType = function
    | PropertyGet (_, propertyInfo, _) -> propertyInfo.DeclaringType
    | _ -> failwith "Expression is no property"

module Cfg =
    let rec private _moduleType = Util.getModuleType <@ _moduleType @>
    
    open Logary
    let private defaultLogLevel = Info
    let private buildLogary() =
        let console = LiterateConsole.create LiterateConsole.empty "console"
        let logfileName = File.Naming ("{service}-{date}", "log")
        let logfile = File.create (File.FileConf.create Environment.CurrentDirectory logfileName) "file"

        Config.create "BitThicket.Bitcoin.Daemon" (Dns.GetHostName())
        |> Config.targets [console; logfile]
        |> Config.ilogger (ILogger.Console Info)
        |> Config.build
        |> run

    let logary = buildLogary()
    let getLogger = PointName.parse >> logary.getLogger
    let private log = getLogger _moduleType.FullName

    let updateLogLevel() =
        log.info (eventX <| sprintf "Setting log level: %A" Debug)
        logary.switchLoggerLevel (".*", Debug)

module Feature =
    let rec private _moduleType = Util.getModuleType <@ _moduleType @>
    let private _log = Cfg.getLogger _moduleType.FullName

    // first, start server job
    let startServer () = job {
        let ch = Ch<string>()
        let rec server () = job {
            _log.debug (eventX <| sprintf "waiting on message")
            let! msg = Ch.take ch
            _log.debug (eventX <| sprintf "got msg '%s'" msg)
            return! server()
        }
        do! Job.start (server())
        return ch
    }

    let convert (uuid:Guid) =
        _log.debug (eventX <| sprintf "converting %A to string" uuid)
        uuid.ToString("d") |> Alt.always

    let sendMsg (ch:Ch<string>) (msg:string) =
        _log.debug (eventX <| sprintf "got string, sending message")
        ch *<- msg

    let genMsg ch = 
        _log.debug (eventX <| sprintf "getting message")
        Guid.NewGuid()
        |> (fun g -> convert g ^=> sendMsg ch)

    // then, do "getSeedAddresses", which uses pipelines and combinators to compose jobs and send channel events to the server

let rec private _moduleType = Util.getModuleType <@ _moduleType @>

[<EntryPoint>]
let main argv =
    use mre = new System.Threading.ManualResetEventSlim(false);
    use sub = Console.CancelKeyPress.Subscribe(fun _ -> mre.Set())

    let log = Cfg.getLogger _moduleType.FullName
    log.info (eventX <| sprintf "starting logary#424 repro")

    Cfg.updateLogLevel()

    let ch = Feature.startServer() |> run
    Feature.genMsg ch |> run

    mre.Wait()
    0 // return an integer exit code
