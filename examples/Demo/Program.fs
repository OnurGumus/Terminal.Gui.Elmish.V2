module Demo

open System
open Terminal.Gui.Elmish
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Input
open Terminal.Gui.Elmish.Elements

/// Build a Color from a Terminal.Gui v2 named color (avoids the inref ctor overload).
let col (name: string) : Color = Color name

// ---------------------------------------------------------------- Model

type Page =
    | Counter
    | Form
    | Lists
    | Chat
    | Data
    | Chart

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
      ChatLog: string
      ChatInput: string
      Pending: string
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
    | ChatInputChanged of string
    | SendChat
    | StreamTick
    | Tick of string
    | ShowAbout
    | ConfirmReset
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

/// Re-arm the chat "typewriter" stream (one character per tick).
let private streamCmd: Cmd<Msg> =
    [ fun dispatch -> async {
                          do! Async.Sleep 12
                          dispatch StreamTick
                      }
                      |> Async.StartImmediate ]

/// A canned, offline "assistant" reply so the demo needs no network.
let cannedReply (prompt: string) =
    let p = prompt.ToLowerInvariant()

    if p.Contains "hello" || p.Contains "hi" then
        "Hello there! I'm a pretend assistant rendered entirely in the terminal with Terminal.Gui.Elmish."
    elif p.Contains "elmish" || p.Contains "mvu" then
        "Elmish is the Model-View-Update pattern: your view is a pure function of state, and messages drive updates. This whole UI is built that way."
    elif p.Contains "?" then
        "Great question! In this demo I only have a few scripted answers - but the streaming, scrolling, and input are all real."
    else
        $"You said: \"{prompt}\". I'm streaming this reply one character at a time over an async Cmd, marshaled back onto the UI thread."

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
      ChatLog = "Claude: Hi! I'm a canned, offline demo assistant.\nType a message below and press Enter (or Send) for a streamed reply.\n"
      ChatInput = ""
      Pending = ""
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
    | ChatInputChanged s -> { model with ChatInput = s }, Cmd.none
    | SendChat ->
        let text = model.ChatInput.Trim()

        if text = "" then
            model, Cmd.none
        else
            let log = model.ChatLog + $"\nYou: {text}\n\nClaude: "
            { model with ChatLog = log; ChatInput = ""; Pending = cannedReply text; LastAction = "Sent message" }, streamCmd
    | StreamTick ->
        match model.Pending with
        | "" -> model, Cmd.none
        | pending ->
            { model with ChatLog = model.ChatLog + string pending.[0]; Pending = pending.[1..] }, streamCmd
    | ConfirmReset ->
        match Dialogs.messageBox "Confirm" "Reset the counter to zero?" [ "Yes"; "No" ] with
        | "Yes" -> { model with Count = 0; History = pushHistory 0 model.History; LastAction = "Reset (confirmed)" }, Cmd.none
        | _ -> { model with LastAction = "Reset cancelled" }, Cmd.none
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

// Page content lives inside a borderless FrameView (which repaints its whole viewport,
// so switching pages leaves no stale cells). The frame's TabStop is NoStop so its
// controls still join the window's single tab order. Positions are frame-relative.
let y0 = 0

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
      View.button [ prop.id "rst"; prop.position.x.at 24; prop.position.y.at (y0 + 5); button.text "Reset"; button.onClick (fun () -> dispatch ConfirmReset) ]

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

/// A Claude-style chat: a scrollable read-only transcript that auto-streams a canned reply.
let chatPage model dispatch =
    [ View.textView [
          prop.id "transcript"
          prop.position.x.at 2
          prop.position.y.at (y0 + 1)
          prop.width.fill 2
          prop.height.fill 3
          textView.readOnly true
          textView.multiline true
          textView.wordWrap true
          textView.scrollToEnd
          textView.text model.ChatLog
      ]

      View.label [ prop.id "youlbl"; prop.position.x.at 2; prop.position.y.fromBottom 2; label.text "You:" ]
      View.textField [
          prop.id "chatinput"
          prop.position.x.at 7
          prop.position.y.fromBottom 2
          prop.width.fill 12
          textField.text model.ChatInput
          textField.onTextChanged (ChatInputChanged >> dispatch)
          prop.onKeyDown (fun k -> if k.KeyCode = Key.Enter.KeyCode then dispatch SendChat)
      ]
      View.button [ prop.id "send"; prop.position.x.anchorEnd; prop.position.y.fromBottom 2; button.text "Send"; button.onClick (fun () -> dispatch SendChat) ] ]

/// A TableView bound to a System.Data.DataTable (built once so it isn't reset each render).
let dataTable: System.Data.DataTable =
    let t = new System.Data.DataTable()
    [ "Fruit"; "Colour"; "Tartness"; "In stock" ] |> List.iter (fun c -> t.Columns.Add c |> ignore)

    // Pass obj[] so DataRowCollection.Add(params object[]) spreads across columns
    // instead of treating a string[] as a single cell value.
    [ [| box "Apple"; box "Red"; box "Low"; box "120" |]
      [| box "Banana"; box "Yellow"; box "None"; box "80" |]
      [| box "Cherry"; box "Crimson"; box "Medium"; box "240" |]
      [| box "Date"; box "Brown"; box "None"; box "35" |]
      [| box "Elderberry"; box "Purple"; box "High"; box "12" |]
      [| box "Fig"; box "Green"; box "Low"; box "60" |]
      [| box "Grape"; box "Green"; box "Low"; box "300" |] ]
    |> List.iter (fun row -> t.Rows.Add row |> ignore)

    t

let dataPage model dispatch =
    [ View.label [ prop.id "datalbl"; prop.position.x.at 2; prop.position.y.at (y0 + 1); label.text "A TableView bound to a System.Data.DataTable (arrows to navigate):" ]
      View.tableView [
          prop.id "table"
          prop.position.x.at 2
          prop.position.y.at (y0 + 3)
          prop.width.fill 2
          prop.height.fill 2
          tableView.table dataTable
      ] ]

/// A live horizontal bar chart of the counter history (animates while auto-spinning).
let chartPage model dispatch =
    let _, bg = themeColors model.Theme
    let recent = model.History |> List.rev |> List.truncate 14 |> List.rev
    let mx = max 1 (recent |> List.map abs |> List.fold max 0)

    let bars =
        recent
        |> List.mapi (fun i v ->
            let len = abs v * 40 / mx
            View.label [
                prop.id $"bar{i}"
                prop.position.x.at 2
                prop.position.y.at (y0 + 2 + i)
                prop.color (col "BrightGreen", bg)
                label.text $"{v,5} |{String('█', len)}"
            ])

    View.label [ prop.id "chartlbl"; prop.position.x.at 2; prop.position.y.at (y0 + 1); label.text "Counter history (start Auto Spin on the Counter page):" ]
    :: bars

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
        | Chat -> chatPage model dispatch
        | Data -> dataPage model dispatch
        | Chart -> chartPage model dispatch

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
                window.children [
                    navButton model dispatch Counter "Counter" 2
                    navButton model dispatch Form "Form" 17
                    navButton model dispatch Lists "Lists" 28
                    navButton model dispatch Chat "Chat" 40
                    navButton model dispatch Data "Data" 51
                    navButton model dispatch Chart "Chart" 62

                    // Active page in a bordered frame. NoStop keeps its controls in the
                    // window's tab order; the frame repaints its viewport so page switches
                    // leave no leftover cells.
                    View.frameView (
                        [ prop.id "pageframe"
                          prop.position.x.at 1
                          prop.position.y.at 2
                          prop.width.fill 1
                          prop.height.fill 1
                          prop.tabStop TabBehavior.NoStop
                          prop.color (fg, bg)
                          frameView.title $" {model.Page} " ]
                        @ [ frameView.children pageContent ]
                    )

                    View.label [
                        prop.id "status"
                        prop.position.x.at 1
                        prop.position.y.anchorEnd
                        prop.color (col "BrightGreen", bg)
                        label.text $"Last action: {model.LastAction}"
                    ]
                ]
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

    // Poll/redraw faster than the default 25/sec so typing feels responsive.
    Terminal.Gui.App.Application.MaximumIterationsPerSecond <- 120us

    Program.mkProgram init update view
    |> Program.withSubscription (fun _ -> Cmd.ofSub clock)
    |> Program.run

    0
