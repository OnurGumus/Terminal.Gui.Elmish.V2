module Demo

open System
open Terminal.Gui.Elmish
open Terminal.Gui.Drawing
open Terminal.Gui.Elmish.Elements

/// Build a Color from a Terminal.Gui v2 named color (avoids the inref ctor overload).
let col (name: string) : Color = Color name

// ---------------------------------------------------------------- Model

type Page =
    | Counter
    | Form
    | Lists

type Model =
    { Page: Page
      Count: int
      Name: string
      Subscribed: bool
      Theme: int
      Fruit: string
      Selected: int
      Clock: string
      LastAction: string }

type Msg =
    | GoTo of Page
    | Increment
    | Decrement
    | Reset
    | NameChanged of string
    | SubscribedChanged of bool
    | ThemeChanged of int
    | FruitChanged of string
    | ItemSelected of int
    | Tick of string
    | ShowAbout
    | Quit

let fruits = [ "Apple"; "Banana"; "Cherry"; "Date"; "Elderberry" ]

let init () : Model * Cmd<Msg> =
    { Page = Counter
      Count = 0
      Name = ""
      Subscribed = false
      Theme = 0
      Fruit = "Apple"
      Selected = 0
      Clock = DateTime.Now.ToString "HH:mm:ss"
      LastAction = "Welcome!" },
    Cmd.none

// ---------------------------------------------------------------- Update

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | GoTo p -> { model with Page = p; LastAction = $"Opened {p}" }, Cmd.none
    | Increment -> { model with Count = model.Count + 1; LastAction = "Incremented" }, Cmd.none
    | Decrement -> { model with Count = model.Count - 1; LastAction = "Decremented" }, Cmd.none
    | Reset -> { model with Count = 0; LastAction = "Reset" }, Cmd.none
    | NameChanged n -> { model with Name = n }, Cmd.none
    | SubscribedChanged b -> { model with Subscribed = b; LastAction = (if b then "Subscribed" else "Unsubscribed") }, Cmd.none
    | ThemeChanged i -> { model with Theme = i; LastAction = "Theme changed" }, Cmd.none
    | FruitChanged f -> { model with Fruit = f; LastAction = $"Picked {f}" }, Cmd.none
    | ItemSelected i -> { model with Selected = i; LastAction = $"Selected {fruits.[i]}" }, Cmd.none
    | Tick t -> { model with Clock = t }, Cmd.none
    | ShowAbout ->
        Dialogs.messageBox
            "About"
            "Terminal.Gui.Elmish demo\n\nA Feliz-style MVU wrapper around Terminal.Gui v2."
            [ "Nice!" ]
        |> ignore
        { model with LastAction = "Showed About" }, Cmd.none
    | Quit ->
        Program.requestStop ()
        model, Cmd.none

// ---------------------------------------------------------------- Subscriptions

let clock dispatch =
    let timer = new System.Timers.Timer(1000.0)
    timer.AutoReset <- true
    timer.Elapsed.Add(fun _ -> dispatch (Tick(DateTime.Now.ToString "HH:mm:ss")))
    timer.Start()

// ---------------------------------------------------------------- Pages

// Page content is placed directly in the window (one focus group) starting at row `y0`,
// so plain Tab reaches every control. No `isDefault` button: Enter/Space act on the
// focused button rather than always hitting one default.
let y0 = 4

let counterPage model dispatch =
    [ View.label [
          prop.id "count"
          prop.position.x.center
          prop.position.y.at (y0 + 1)
          prop.textAlignment.centered
          prop.color (col "BrightGreen", col "Black")
          label.text $"  Count = {model.Count}  "
      ]

      View.progressBar [
          prop.id "bar"
          prop.position.x.center
          prop.position.y.at (y0 + 3)
          prop.width.percent 50
          progressBar.fraction (float (((model.Count % 21) + 21) % 21) / 20.0)
          progressBar.style.blocks
      ]

      View.button [ prop.id "inc"; prop.position.x.center; prop.position.y.at (y0 + 5); button.text "Up"; button.onClick (fun () -> dispatch Increment) ]
      View.button [ prop.id "dec"; prop.position.x.center; prop.position.y.at (y0 + 7); button.text "Down"; button.onClick (fun () -> dispatch Decrement) ]
      View.button [ prop.id "rst"; prop.position.x.center; prop.position.y.at (y0 + 9); button.text "Reset"; button.onClick (fun () -> dispatch Reset) ] ]

let formPage model dispatch =
    [ View.label [ prop.id "lblName"; prop.position.x.at 2; prop.position.y.at (y0 + 1); label.text "Name:" ]
      View.textField [
          prop.id "name"
          prop.position.x.at 11
          prop.position.y.at (y0 + 1)
          prop.width.sized 30
          textField.text model.Name
          textField.onTextChanged (NameChanged >> dispatch)
      ]

      View.label [
          prop.id "greeting"
          prop.position.x.at 2
          prop.position.y.at (y0 + 3)
          prop.color (col "BrightYellow", col "Black")
          label.text (
              let who = if model.Name = "" then "stranger" else model.Name
              $"Hello, {who}!"
          )
      ]

      View.checkBox [
          prop.id "sub"
          prop.position.x.at 2
          prop.position.y.at (y0 + 5)
          checkBox.text "Subscribe to the newsletter"
          checkBox.isChecked model.Subscribed
          checkBox.onToggled (fun e -> dispatch (SubscribedChanged e.current))
      ]

      View.label [ prop.id "lblTheme"; prop.position.x.at 2; prop.position.y.at (y0 + 7); label.text "Theme:" ]
      View.radioGroup [
          prop.id "theme"
          prop.position.x.at 11
          prop.position.y.at (y0 + 7)
          radioGroup.radioLabels [ "Default"; "Dark"; "Ocean" ]
          radioGroup.selectedItem model.Theme
          radioGroup.onSelectedItemChanged (ThemeChanged >> dispatch)
      ]

      View.label [ prop.id "lblFruit"; prop.position.x.at 2; prop.position.y.at (y0 + 11); label.text "Fruit:" ]
      View.comboBox [
          prop.id "fruit"
          prop.position.x.at 11
          prop.position.y.at (y0 + 11)
          prop.width.sized 20
          comboBox.source fruits
          comboBox.text model.Fruit
          comboBox.onTextChanged (FruitChanged >> dispatch)
      ] ]

let listsPage model dispatch =
    [ View.label [ prop.id "lblPick"; prop.position.x.at 2; prop.position.y.at (y0 + 1); label.text "Pick a fruit:" ]
      View.listView [
          prop.id "list"
          prop.position.x.at 2
          prop.position.y.at (y0 + 3)
          prop.width.sized 24
          prop.height.sized 6
          listView.source fruits
          listView.selectedItem model.Selected
          listView.onSelectedItemChanged (ItemSelected >> dispatch)
      ]

      View.lineView [ prop.id "sep"; prop.position.x.at 29; prop.position.y.at (y0 + 3); prop.height.sized 6; lineView.vertical ]

      View.label [
          prop.id "detail"
          prop.position.x.at 32
          prop.position.y.at (y0 + 4)
          prop.color (col "BrightMagenta", col "Black")
          label.text $"You chose:\n  {fruits.[model.Selected]}"
      ] ]

// ---------------------------------------------------------------- View

let themeColors theme =
    match theme with
    | 1 -> col "White", col "Black"
    | 2 -> col "BrightYellow", col "Blue"
    | _ -> col "Black", col "Gray"

/// A page-nav button that highlights the active page.
let navButton (model: Model) dispatch (page: Page) (text: string) (x: int) =
    View.button [
        prop.id $"nav{page}"
        prop.position.x.at x
        prop.position.y.at 0
        button.text (if model.Page = page then $"[ {text} ]" else $"  {text}  ")
        button.onClick (fun () -> dispatch (GoTo page))
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let fg, bg = themeColors model.Theme

    let pageContent =
        match model.Page with
        | Counter -> counterPage model dispatch
        | Form -> formPage model dispatch
        | Lists -> listsPage model dispatch

    View.page [
        prop.children [
            View.menuBar [
                menuBar.menus [
                    menu.sub ("_File", [ menu.item ("_Quit", fun () -> dispatch Quit) ])
                    menu.sub ("_Help", [ menu.item ("_About", fun () -> dispatch ShowAbout) ])
                ]
            ]

            View.window [
                prop.position.x.at 0
                prop.position.y.at 1
                prop.width.filled
                prop.height.fill 1
                prop.color (fg, bg)
                window.title $"  Terminal.Gui.Elmish Demo  -  {model.Clock}  "
                window.children (
                    [ navButton model dispatch Counter "Counter" 1
                      navButton model dispatch Form "Form" 14
                      navButton model dispatch Lists "Lists" 24

                      View.lineView [ prop.id "navrule"; prop.position.x.at 0; prop.position.y.at 2; prop.width.filled; lineView.horizontal ]

                      View.label [
                          prop.id "pagetitle"
                          prop.position.x.at 2
                          prop.position.y.at 3
                          prop.color (col "BrightCyan", bg)
                          label.text $"- {model.Page} -"
                      ] ]
                    @ pageContent
                    @ [ View.label [
                            prop.id "status"
                            prop.position.x.at 1
                            prop.position.y.anchorEnd
                            prop.color (col "BrightGreen", bg)
                            label.text $"Last action: {model.LastAction}"
                        ] ]
                )
            ]

            View.statusBar [
                statusBar.items [
                    statusItem.create ("Quit (Esc)", fun () -> dispatch Quit)
                    statusItem.create ("About", fun () -> dispatch ShowAbout)
                    statusItem.create ("Tab moves - Enter/Space act", ignore)
                ]
            ]
        ]
    ]

// ---------------------------------------------------------------- Bootstrap

[<EntryPoint>]
let main _ =
    match Environment.GetEnvironmentVariable "TGE_FORCE_DRIVER" with
    | null | "" -> ()
    | driver -> Terminal.Gui.App.Application.ForceDriver <- driver

    Program.mkProgram init update view
    |> Program.withSubscription (fun _ -> Cmd.ofSub clock)
    |> Program.run

    0
