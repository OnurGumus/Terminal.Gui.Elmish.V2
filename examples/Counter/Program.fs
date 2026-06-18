module Counter

open Terminal.Gui.Elmish

type Model = { Counter: int }

type Msg =
    | Increment
    | Decrement
    | Reset

let init () : Model * Cmd<Msg> = { Counter = 0 }, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | Increment -> { model with Counter = model.Counter + 1 }, Cmd.none
    | Decrement -> { model with Counter = model.Counter - 1 }, Cmd.none
    | Reset -> { model with Counter = 0 }, Cmd.none

let view (model: Model) (dispatch: Msg -> unit) =
    View.page [
        prop.children [
            View.window [
                window.title "Counter (Tab to move, Enter to press, Esc to quit)"
                window.children [
                    View.label [
                        prop.id "count"
                        prop.position.x.center
                        prop.position.y.at 1
                        prop.textAlignment.centered
                        label.text $"Count: {model.Counter}"
                    ]

                    View.button [
                        prop.id "up"
                        prop.position.x.center
                        prop.position.y.at 3
                        button.text "Up"
                        button.onClick (fun () -> dispatch Increment)
                    ]

                    View.button [
                        prop.id "down"
                        prop.position.x.center
                        prop.position.y.at 5
                        button.text "Down"
                        button.onClick (fun () -> dispatch Decrement)
                    ]

                    View.button [
                        prop.id "reset"
                        prop.position.x.center
                        prop.position.y.at 7
                        button.text "Reset"
                        button.onClick (fun () -> dispatch Reset)
                    ]
                ]
            ]
        ]
    ]

[<EntryPoint>]
let main _ =
    // Optional: force a specific driver (e.g. "dotnet", "ansi") for headless/PTY testing.
    match System.Environment.GetEnvironmentVariable "TGE_FORCE_DRIVER" with
    | null | "" -> ()
    | driver -> Terminal.Gui.App.Application.ForceDriver <- driver

    Program.mkProgram init update view
    |> Program.run

    0
