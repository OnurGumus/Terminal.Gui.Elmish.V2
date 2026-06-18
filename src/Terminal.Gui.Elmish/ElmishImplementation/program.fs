(**

Note: the elmish core (Cmd/Program/RingBuffer) is adapted from https://github.com/elmish/elmish.
Only the `runWith` host loop is specific to Terminal.Gui and was rewritten for the v2
instance-based `IApplication` model.

*)

namespace Terminal.Gui.Elmish

open System
open Terminal.Gui.App
open Terminal.Gui.Views
open Terminal.Gui.Elmish.Elements


/// Program type captures various aspects of program behavior
type Program<'arg, 'model, 'msg, 'view> = private {
    init: 'arg -> 'model * Cmd<'msg>
    update: 'msg -> 'model -> 'model * Cmd<'msg>
    subscribe: 'model -> Cmd<'msg>
    view: 'model -> Dispatch<'msg> -> TerminalElement
    setState: 'model -> Dispatch<'msg> -> unit
    onError: (string * exn) -> unit
    syncDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

/// Program module - functions to manipulate program instances
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Program =

    /// Typical program, new commands are produced by `init` and `update` along with the new state.
    let mkProgram
        (init: 'arg -> 'model * Cmd<'msg>)
        (update: 'msg -> 'model -> 'model * Cmd<'msg>)
        (view: 'model -> Dispatch<'msg> -> TerminalElement) =
        { init = init
          update = update
          view = view
          setState = fun model -> view model >> ignore
          subscribe = fun _ -> Cmd.none
          onError = Log.onError
          syncDispatch = id }

    /// Simple program that produces only new state with `init` and `update`.
    let mkSimple
        (init: 'arg -> 'model)
        (update: 'msg -> 'model -> 'model)
        (view: 'model -> Dispatch<'msg> -> TerminalElement) =
        { init = init >> fun state -> state, Cmd.none
          update = fun msg -> update msg >> fun state -> state, Cmd.none
          view = view
          setState = fun model -> view model >> ignore
          subscribe = fun _ -> Cmd.none
          onError = Log.onError
          syncDispatch = id }

    /// Subscribe to external source of events.
    let withSubscription (subscribe: 'model -> Cmd<'msg>) (program: Program<'arg, 'model, 'msg, 'view>) =
        let sub model =
            Cmd.batch [ program.subscribe model; subscribe model ]

        { program with subscribe = sub }

    /// Trace all the updates to the console
    let withConsoleTrace (program: Program<'arg, 'model, 'msg, 'view>) =
        let traceInit (arg: 'arg) =
            let initModel, cmd = program.init arg
            Log.toConsole ("Initial state:", initModel)
            initModel, cmd

        let traceUpdate msg model =
            Log.toConsole ("New message:", msg)
            let newModel, cmd = program.update msg model
            Log.toConsole ("Updated state:", newModel)
            newModel, cmd

        { program with
            init = traceInit
            update = traceUpdate }

    /// Trace all the messages as they update the model
    let withTrace trace (program: Program<'arg, 'model, 'msg, 'view>) =
        { program with update = fun msg model -> trace msg model; program.update msg model }

    /// Handle dispatch loop exceptions
    let withErrorHandler onError (program: Program<'arg, 'model, 'msg, 'view>) =
        { program with onError = onError }

    /// For library authors only: map existing error handler and return new `Program`
    let mapErrorHandler map (program: Program<'arg, 'model, 'msg, 'view>) =
        { program with onError = map program.onError }

    /// For library authors only: function to render the view with the latest state
    let withSetState (setState: 'model -> Dispatch<'msg> -> unit) (program: Program<'arg, 'model, 'msg, 'view>) =
        { program with setState = setState }

    /// For library authors only: return the function to render the state
    let setState (program: Program<'arg, 'model, 'msg, 'view>) = program.setState

    /// For library authors only: return the view function
    let view (program: Program<'arg, 'model, 'msg, 'view>) = program.view

    /// For library authors only: function to synchronize the dispatch function
    let withSyncDispatch (syncDispatch: Dispatch<'msg> -> Dispatch<'msg>) (program: Program<'arg, 'model, 'msg, 'view>) =
        { program with syncDispatch = syncDispatch }

    /// For library authors only: map the program type
    let map mapInit mapUpdate mapView mapSetState mapSubscribe (program: Program<'arg, 'model, 'msg, 'view>) =
        { init = mapInit program.init
          update = mapUpdate program.update
          view = mapView program.view
          setState = mapSetState program.setState
          subscribe = mapSubscribe program.subscribe
          onError = program.onError
          syncDispatch = id }


    /// Start the program loop.
    /// arg: argument to pass to the init() function.
    /// program: program created with 'mkSimple' or 'mkProgram'.
    let runWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, 'view>) =
        use app = Application.Create().Init()
        ElmishApp.current <- Some app

        let (model, cmd) = program.init arg
        let rb = RingBuffer 10
        let mutable reentered = false
        let mutable pendingRender = false
        let mutable state = model
        let mutable currentTreeState: TerminalElement option = None

        let isUiThread () =
            app.MainThreadId.HasValue && app.MainThreadId.Value = Environment.CurrentManagedThreadId

        // Schedules a single, coalesced reconcile on the next main-loop iteration. The render
        // is built from the LATEST `state` at execution time (not captured at dispatch time),
        // so it never writes a stale value back into a view the user is editing — by the time
        // it runs, the model and the focused field already agree, so the write is a no-op.
        // Deferring also keeps the reconcile out of the in-flight key/mouse event handler,
        // which otherwise makes v2 re-resolve focus and kick the user out of the field.
        let rec scheduleRender () =
            if pendingRender then
                ()
            else
                pendingRender <- true

                app.Invoke (
                    System.Action (fun () ->
                        pendingRender <- false

                        match currentTreeState with
                        | None -> ()
                        | Some currentState ->
                            let focused =
                                match app.Navigation with
                                | null -> None
                                | nav -> nav.GetFocused() |> Option.ofObj

                            let nextTreeState = program.view state syncDispatch
                            let disposalsBefore = Differ.disposals
                            Differ.update currentState nextTreeState
                            currentTreeState <- Some nextTreeState

                            // If the structure changed (views were removed), the cells they
                            // occupied linger — Terminal.Gui doesn't clear them on remove, and
                            // ClearContents alone desyncs from the terminal. Emit a real clear
                            // escape (as the resize handler does) and force a full repaint.
                            if Differ.disposals <> disposalsBefore then
                                app.Driver
                                |> Option.ofObj
                                |> Option.iter (fun d ->
                                    d.WriteRaw "[2J[H"
                                    d.ClearContents())

                                app.LayoutAndDraw true
                            else
                                app.LayoutAndDraw false

                            // Safety net: if reconciliation/relayout moved focus, restore it.
                            try
                                match focused with
                                | Some v when not (isNull v.SuperView) && not v.HasFocus -> v.SetFocus() |> ignore
                                | _ -> ()
                            with _ -> ())
                )

        // The MVU step. Model updates run synchronously (so chained commands see fresh state);
        // the view reconcile is coalesced and deferred via `scheduleRender`.
        and dispatch msg =
            if reentered then
                rb.Push msg
            else
                reentered <- true
                let mutable nextMsg = Some msg

                while Option.isSome nextMsg do
                    let msg = nextMsg.Value

                    try
                        let (model', cmd') = program.update msg state
                        state <- model'
                        scheduleRender ()
                        cmd' |> Cmd.exec syncDispatch
                    with ex ->
                        program.onError (sprintf "Unable to process the message: %A" msg, ex)

                    nextMsg <- rb.Pop()

                reentered <- false

        // Ensures dispatch always executes on the UI thread; off-thread (async Cmd)
        // dispatches are marshaled via the main loop.
        and syncDispatch: Dispatch<'msg> =
            let inner = program.syncDispatch dispatch

            fun msg ->
                if isUiThread () then
                    inner msg
                else
                    app.Invoke (System.Action (fun () -> inner msg))

        program.setState model syncDispatch

        let startState = program.view model syncDispatch
        Differ.initializeTree None startState
        currentTreeState <- Some startState

        match startState.element with
        | null -> failwith "error: the root element was not initialized"
        | topElement ->
            match topElement with
            | :? Runnable as runnable ->
                let sub =
                    try
                        program.subscribe model
                    with ex ->
                        program.onError ("Unable to subscribe:", ex)
                        Cmd.none

                sub @ cmd |> Cmd.exec syncDispatch
                // Force the first paint once the main loop is running; otherwise the
                // initial tree is laid out but not drawn until the first dispatch.
                app.Invoke (System.Action (fun () -> app.LayoutAndDraw true))
                app.Run (runnable, null) |> ignore
            | _ -> failwith "the first/root element must be `View.page`"

        ElmishApp.current <- None


    /// Start the dispatch loop with `unit` for the init() function.
    let run (program: Program<unit, 'model, 'msg, 'view>) = runWith () program

    /// Requests the running application to stop (the clean way to exit an Elmish program).
    let requestStop () =
        ElmishApp.current |> Option.iter (fun app -> app.RequestStop())

    let quitWithErrorCode errorcode =
        System.Console.Clear()
        System.Environment.Exit(errorcode)

    let quit () = quitWithErrorCode 0
