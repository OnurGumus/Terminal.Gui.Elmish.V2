module Showcase

open Terminal.Gui.Elmish

type Model =
    { Name: string
      Subscribed: bool
      Selected: int }

type Msg =
    | NameChanged of string
    | SubscribedChanged of bool
    | SelectedChanged of int
    | Quit

let init () : Model * Cmd<Msg> =
    { Name = ""; Subscribed = false; Selected = 0 }, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | NameChanged n -> { model with Name = n }, Cmd.none
    | SubscribedChanged b -> { model with Subscribed = b }, Cmd.none
    | SelectedChanged i -> { model with Selected = i }, Cmd.none
    | Quit ->
        Program.requestStop ()
        model, Cmd.none

let view (model: Model) (dispatch: Msg -> unit) =
    View.page [
        prop.children [
            View.menuBar [
                menuBar.menus [
                    menu.sub ("_File", [ menu.item ("_Quit", fun () -> dispatch Quit) ])
                ]
            ]

            View.window [
                window.title "Showcase (Esc/Ctrl+Q to quit)"
                // Leave row 0 for the menu bar and the last row for the status bar.
                prop.position.x.at 0
                prop.position.y.at 1
                prop.width.filled
                prop.height.fill 1
                window.children [
                    View.label [
                        prop.id "greeting"
                        prop.position.x.at 1
                        prop.position.y.at 1
                        label.text (
                            let who = if model.Name = "" then "stranger" else model.Name
                            let sub = if model.Subscribed then " (subscribed)" else ""
                            $"Hello, {who}!{sub} Picked #{model.Selected}"
                        )
                    ]

                    View.label [ prop.position.x.at 1; prop.position.y.at 3; label.text "Name:" ]

                    View.textField [
                        prop.id "name"
                        prop.position.x.at 8
                        prop.position.y.at 3
                        prop.width.sized 24
                        textField.text model.Name
                        textField.onTextChanged (fun t -> dispatch (NameChanged t))
                    ]

                    View.checkBox [
                        prop.id "sub"
                        prop.position.x.at 1
                        prop.position.y.at 5
                        checkBox.text "Subscribe"
                        checkBox.isChecked model.Subscribed
                        checkBox.onToggled (fun e -> dispatch (SubscribedChanged e.current))
                    ]

                    View.listView [
                        prop.id "list"
                        prop.position.x.at 1
                        prop.position.y.at 7
                        prop.width.sized 24
                        prop.height.sized 3
                        listView.source [ "Apple"; "Banana"; "Cherry" ]
                        listView.onSelectedItemChanged (fun i -> dispatch (SelectedChanged i))
                    ]

                    View.button [
                        prop.id "quit"
                        prop.position.x.at 1
                        prop.position.y.at 12
                        button.text "Quit"
                        button.onClick (fun () -> dispatch Quit)
                    ]
                ]
            ]

            View.statusBar [
                statusBar.items [ statusItem.create ("Quit", fun () -> dispatch Quit) ]
            ]
        ]
    ]

[<EntryPoint>]
let main _ =
    match System.Environment.GetEnvironmentVariable "TGE_FORCE_DRIVER" with
    | null | "" -> ()
    | driver -> Terminal.Gui.App.Application.ForceDriver <- driver

    Program.mkProgram init update view
    |> Program.run

    0
