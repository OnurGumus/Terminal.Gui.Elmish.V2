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
      History: int list
      Spinning: bool
      Frame: int
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
    | ToggleSpin
    | SpinStep
    | NameChanged of string
    | SubscribedChanged of bool
    | ThemeChanged of int
    | FruitChanged of string
    | ItemSelected of int
    | Tick of string
    | ShowAbout
    | Quit

let fruits = [ "Apple"; "Banana"; "Cherry"; "Date"; "Elderberry" ]

/// Keep the most recent 48 samples for the sparkline.
let private pushHistory (c: int) (h: int list) =
    let l = h @ [ c ]
    List.skip (max 0 (List.length l - 48)) l

/// Re-arm the auto-spin loop. Dispatch happens from a thread-pool thread, which the
/// Elmish host marshals back onto the UI thread.
let private spinCmd: Cmd<Msg> =
    [ fun dispatch -> async {
                          do! Async.Sleep 110
                          dispatch SpinStep
                      }
                      |> Async.StartImmediate ]

let init () : Model * Cmd<Msg> =
    { Page = Counter
      Count = 0
      History = [ 0 ]
      Spinning = false
      Frame = 0
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
    | Increment ->
        let c = model.Count + 1
        { model with Count = c; History = pushHistory c model.History; LastAction = "Incremented" }, Cmd.none
    | Decrement ->
        let c = model.Count - 1
        { model with Count = c; History = pushHistory c model.History; LastAction = "Decremented" }, Cmd.none
    | Reset -> { model with Count = 0; History = pushHistory 0 model.History; LastAction = "Reset" }, Cmd.none
    | ToggleSpin ->
        let spinning = not model.Spinning
        { model with Spinning = spinning; LastAction = (if spinning then "Spinning..." else "Stopped") }, (if spinning then spinCmd else Cmd.none)
    | SpinStep ->
        if model.Spinning then
            let c = model.Count + 1
            { model with Count = c; History = pushHistory c model.History }, spinCmd
        else
            model, Cmd.none
    | NameChanged n -> { model with Name = n }, Cmd.none
    | SubscribedChanged b -> { model with Subscribed = b; LastAction = (if b then "Subscribed" else "Unsubscribed") }, Cmd.none
    | ThemeChanged i -> { model with Theme = i; LastAction = "Theme changed" }, Cmd.none
    | FruitChanged f -> { model with Fruit = f; LastAction = $"Picked {f}" }, Cmd.none
    | ItemSelected i -> { model with Selected = i; LastAction = $"Selected {fruits.[i]}" }, Cmd.none
    | Tick t -> { model with Clock = t; Frame = model.Frame + 1 }, Cmd.none
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
    // Fast enough to animate the title spinner; the clock string only changes each second.
    let timer = new System.Timers.Timer(150.0)
    timer.AutoReset <- true
    timer.Elapsed.Add(fun _ -> dispatch (Tick(DateTime.Now.ToString "HH:mm:ss")))
    timer.Start()

/// Braille spinner frames + a unicode block sparkline of recent values.
let spinnerFrames = [| "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴"; "⠦"; "⠧"; "⠇"; "⠏" |]

let spinner (model: Model) = spinnerFrames.[((model.Frame % spinnerFrames.Length) + spinnerFrames.Length) % spinnerFrames.Length]

let sparkline (values: int list) =
    match values with
    | []
    | [ _ ] -> ""
    | _ ->
        let blocks = "▁▂▃▄▅▆▇█"
        let mn = List.min values
        let mx = List.max values
        let range = max 1 (mx - mn)
        values
        |> List.map (fun v -> blocks.[(v - mn) * (blocks.Length - 1) / range])
        |> Array.ofSeq
        |> System.String

let themeColors theme =
    match theme with
    | 1 -> col "White", col "Black" // Dark
    | 2 -> col "Black", col "Gray" // Slate
    | _ -> col "White", col "BrightBlue" // Default (classic Terminal.Gui blue)

// ---------------------------------------------------------------- Pages

// Page content is placed directly in the window (one focus group) starting at row `y0`,
// so plain Tab reaches every control. No `isDefault` button: Enter/Space act on the
// focused button rather than always hitting one default.
let y0 = 4

let counterPage model dispatch =
    let _, bg = themeColors model.Theme

    [ View.label [ prop.id "lblCount"; prop.position.x.at 2; prop.position.y.at (y0 + 1); label.text "Counter value:" ]

      View.label [
          prop.id "count"
          prop.position.x.at 18
          prop.position.y.at (y0 + 1)
          prop.color (col "BrightGreen", col "Black")
          label.text $" {model.Count} "
      ]

      View.progressBar [
          prop.id "bar"
          prop.position.x.at 2
          prop.position.y.at (y0 + 3)
          prop.width.sized 40
          progressBar.fraction (float (((model.Count % 21) + 21) % 21) / 20.0)
          progressBar.style.blocks
      ]

      View.button [ prop.id "inc"; prop.position.x.at 2; prop.position.y.at (y0 + 5); button.text "Up"; button.onClick (fun () -> dispatch Increment) ]
      View.button [ prop.id "dec"; prop.position.x.at 12; prop.position.y.at (y0 + 5); button.text "Down"; button.onClick (fun () -> dispatch Decrement) ]
      View.button [ prop.id "rst"; prop.position.x.at 24; prop.position.y.at (y0 + 5); button.text "Reset"; button.onClick (fun () -> dispatch Reset) ]

      View.button [
          prop.id "spin"
          prop.position.x.at 36
          prop.position.y.at (y0 + 5)
          button.text (if model.Spinning then "Stop Spin" else "Auto Spin")
          button.onClick (fun () -> dispatch ToggleSpin)
      ]

      View.label [ prop.id "lblHist"; prop.position.x.at 2; prop.position.y.at (y0 + 7); label.text "History:" ]
      View.label [
          prop.id "spark"
          prop.position.x.at 11
          prop.position.y.at (y0 + 7)
          prop.color (col "BrightCyan", bg)
          label.text (sparkline model.History)
      ] ]

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
          radioGroup.radioLabels [ "Default"; "Dark"; "Slate" ]
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

/// A page-nav button. The active page is highlighted with an inverted colour scheme
/// (the Button already draws its own `[ ]`, so don't add brackets here).
let navButton (model: Model) dispatch (page: Page) (text: string) (x: int) =
    let active = model.Page = page

    View.button (
        [ prop.id $"nav{page}"
          prop.position.x.at x
          prop.position.y.at 0
          button.text text
          button.onClick (fun () -> dispatch (GoTo page)) ]
        @ (if active then [ prop.color (col "Black", col "BrightYellow") ] else [])
    )

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
                window.title $"  {spinner model}  Terminal.Gui.Elmish Demo  -  {model.Clock}  "
                window.children (
                    [ navButton model dispatch Counter "Counter" 2
                      navButton model dispatch Form "Form" 17
                      navButton model dispatch Lists "Lists" 29

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
