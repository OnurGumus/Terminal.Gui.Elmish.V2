namespace Terminal.Gui.Elmish

open Terminal.Gui.App
open Terminal.Gui.Views
open Terminal.Gui.Elmish.Elements


type View =

    static member inline page(props: IProperty list) = PageElement(props) :> TerminalElement
    static member inline page(children: TerminalElement list) = PageElement([ prop.children children ]) :> TerminalElement

    static member inline window(props: IProperty list) = WindowElement(props) :> TerminalElement
    static member inline window(children: TerminalElement list) = WindowElement([ prop.children children ]) :> TerminalElement

    static member inline frameView(props: IProperty list) = FrameViewElement(props) :> TerminalElement
    static member inline frameView(children: TerminalElement list) = FrameViewElement([ prop.children children ]) :> TerminalElement

    static member inline label(props: IProperty list) = LabelElement(props) :> TerminalElement
    static member inline label(x: int, y: int, text: string) =
        LabelElement([ prop.position.x.at x; prop.position.y.at y; label.text text ]) :> TerminalElement

    static member inline button(props: IProperty list) = ButtonElement(props) :> TerminalElement

    static member inline checkBox(props: IProperty list) = CheckBoxElement(props) :> TerminalElement

    static member inline textField(props: IProperty list) = TextFieldElement(props) :> TerminalElement

    static member inline textView(props: IProperty list) = TextViewElement(props) :> TerminalElement

    static member inline progressBar(props: IProperty list) = ProgressBarElement(props) :> TerminalElement

    static member inline lineView(props: IProperty list) = LineElement(props) :> TerminalElement

    static member inline listView(props: IProperty list) = ListViewElement(props) :> TerminalElement

    /// v1 radioGroup -> v2 OptionSelector.
    static member inline radioGroup(props: IProperty list) = OptionSelectorElement(props) :> TerminalElement

    /// v1 comboBox -> v2 DropDownList.
    static member inline comboBox(props: IProperty list) = DropDownListElement(props) :> TerminalElement

    /// v1 dateField -> v2 DatePicker.
    static member inline dateField(props: IProperty list) = DatePickerElement(props) :> TerminalElement

    static member inline colorPicker(props: IProperty list) = ColorPickerElement(props) :> TerminalElement

    static member inline textValidateField(props: IProperty list) = TextValidateFieldElement(props) :> TerminalElement

    /// v1 tabView -> v2 Tabs.
    static member inline tabView(props: IProperty list) = TabsElement(props) :> TerminalElement
    static member inline tabView(children: TerminalElement list) = TabsElement([ prop.children children ]) :> TerminalElement

    static member inline tableView(props: IProperty list) = TableViewElement(props) :> TerminalElement

    static member inline treeView(props: IProperty list) = TreeViewElement(props) :> TerminalElement

    static member inline hexView(props: IProperty list) = HexViewElement(props) :> TerminalElement

    static member inline graphView(props: IProperty list) = GraphViewElement(props) :> TerminalElement

    static member inline menuBar(props: IProperty list) = MenuBarElement(props) :> TerminalElement

    static member inline statusBar(props: IProperty list) = StatusBarElement(props) :> TerminalElement

    static member inline wizard(props: IProperty list) = WizardElement(props) :> TerminalElement


/// Modal dialogs and message boxes. These use the currently running application instance.
module Dialogs =

    open System

    /// Shows a query message box and returns the chosen button text (empty if dismissed).
    let messageBox title text (buttons: string list) =
        let app = ElmishApp.require ()
        let result = MessageBox.Query(app, title, text, List.toArray buttons)

        match buttons with
        | [] -> ""
        | _ when not result.HasValue -> ""
        | _ when result.Value < 0 || result.Value > buttons.Length - 1 -> ""
        | _ -> buttons.[result.Value]

    /// Shows an error message box and returns the chosen button text (empty if dismissed).
    let errorBox title text (buttons: string list) =
        let app = ElmishApp.require ()
        let result = MessageBox.ErrorQuery(app, title, text, List.toArray buttons)

        match buttons with
        | [] -> ""
        | _ when not result.HasValue -> ""
        | _ when result.Value < 0 || result.Value > buttons.Length - 1 -> ""
        | _ -> buttons.[result.Value]

    /// Shows an open-file dialog and returns the selected path, if any.
    let openFileDialog (title: string) : string option =
        let app = ElmishApp.require ()
        use dia = new OpenDialog(Title = title)
        app.Run(dia, null) |> ignore

        if dia.Canceled then
            None
        else
            dia.FilePaths |> Seq.tryHead

    /// Shows a save-file dialog and returns the selected path, if any.
    let saveFileDialog (title: string) : string option =
        let app = ElmishApp.require ()
        use dia = new SaveDialog(Title = title)
        app.Run(dia, null) |> ignore

        if dia.Canceled then
            None
        else
            dia.Path |> Option.ofObj
