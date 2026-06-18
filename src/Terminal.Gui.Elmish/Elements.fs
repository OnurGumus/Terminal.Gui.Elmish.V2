namespace Terminal.Gui.Elmish.Elements

open System
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Data
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.Text
open Terminal.Gui.Elmish


/// Base class of every element in the virtual view tree. Each element knows how to
/// create its backing Terminal.Gui <see cref="View"/>, update it in place, and decide
/// whether an in-place update is possible.
[<AbstractClass>]
type TerminalElement(props: IProperty list) =
    let mutable view: View = null
    let mutable p: View option = None
    let mutable addProps = []
    let c = props |> Interop.getValueDefault<TerminalElement list> "children" []

    member this.parent
        with get () = p
        and set v = p <- v

    member this.element
        with get () = view
        and set v = view <- v

    member this.additionalProps
        with get () = addProps
        and set v = addProps <- v

    member _.properties = props @ addProps
    member _.children = c

    abstract create: parent: View option -> unit
    abstract update: prevElement: View -> oldProps: IProperty list -> unit
    abstract canUpdate: prevElement: View -> oldProps: IProperty list -> bool
    abstract name: string


[<RequireQualifiedAccess>]
module ViewElement =

    /// Wires the common <see cref="View"/> events. All subscriptions go through the
    /// subscribe-once bridge so reconciliation only swaps the active handler.
    let setEvents (view: View) props =
        props
        |> Interop.getValue<unit -> unit> "onEnabledChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "EnabledChanged" (fun v b -> v.EnabledChanged.Add (fun _ -> b.invoke null)) (fun _ -> f ()))

        props
        |> Interop.getValue<unit -> unit> "onVisibleChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "VisibleChanged" (fun v b -> v.VisibleChanged.Add (fun _ -> b.invoke null)) (fun _ -> f ()))

        props
        |> Interop.getValue<Key -> unit> "onKeyDown"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "KeyDown" (fun v b -> v.KeyDown.Add (fun k -> b.invoke (box k))) (fun o -> f (o :?> Key)))

        props
        |> Interop.getValue<Key -> unit> "onKeyUp"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "KeyUp" (fun v b -> v.KeyUp.Add (fun k -> b.invoke (box k))) (fun o -> f (o :?> Key)))

        props
        |> Interop.getValue<HasFocusEventArgs -> unit> "onHasFocusChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "HasFocusChanged" (fun v b -> v.HasFocusChanged.Add (fun e -> b.invoke (box e))) (fun o -> f (o :?> HasFocusEventArgs)))

        props
        |> Interop.getValue<unit -> unit> "onMouseLeave"
        |> Option.iter (fun f ->
            Interop.bridgeEvent view "MouseLeave" (fun v b -> v.MouseLeave.Add (fun _ -> b.invoke null)) (fun _ -> f ()))


    let setProps (view: View) props =
        setEvents view props
        props |> Interop.getValue<Pos> "x" |> Option.iter (fun v -> view.X <- v)
        props |> Interop.getValue<Pos> "y" |> Option.iter (fun v -> view.Y <- v)
        props |> Interop.getValue<Dim> "width" |> Option.iter (fun v -> view.Width <- v)
        props |> Interop.getValue<Dim> "height" |> Option.iter (fun v -> view.Height <- v)

        props |> Interop.getValue<Alignment> "textAlignment" |> Option.iter (fun v -> view.TextAlignment <- v)
        props |> Interop.getValue<TextDirection> "textDirection" |> Option.iter (fun v -> view.TextDirection <- v)
        props |> Interop.getValue<string> "id" |> Option.iter (fun v -> view.Id <- v)
        props |> Interop.getValue<string> "title" |> Option.iter (fun v -> view.Title <- v)
        props |> Interop.getValue<bool> "enabled" |> Option.iter (fun v -> view.Enabled <- v)
        props |> Interop.getValue<bool> "visible" |> Option.iter (fun v -> view.Visible <- v)
        props |> Interop.getValue<bool> "canFocus" |> Option.iter (fun v -> view.CanFocus <- v)
        props |> Interop.getValue<TabBehavior> "tabStop" |> Option.iter (fun v -> view.TabStop <- v)
        props |> Interop.getValue<LineStyle> "borderStyle" |> Option.iter (fun v -> view.BorderStyle <- v)

        // Modernized color model: a full Scheme, or a named scheme.
        props |> Interop.getValue<Scheme> "scheme" |> Option.iter (fun v -> view.SetScheme v |> ignore)
        props |> Interop.getValue<string> "schemeName" |> Option.iter (fun v -> view.SchemeName <- v)


    let removeProps (view: View) props =
        props |> Interop.getValue<unit -> unit> "onEnabledChanged" |> Option.iter (fun _ -> Interop.clearEvent view "EnabledChanged")
        props |> Interop.getValue<unit -> unit> "onVisibleChanged" |> Option.iter (fun _ -> Interop.clearEvent view "VisibleChanged")
        props |> Interop.getValue<Key -> unit> "onKeyDown" |> Option.iter (fun _ -> Interop.clearEvent view "KeyDown")
        props |> Interop.getValue<Key -> unit> "onKeyUp" |> Option.iter (fun _ -> Interop.clearEvent view "KeyUp")
        props |> Interop.getValue<HasFocusEventArgs -> unit> "onHasFocusChanged" |> Option.iter (fun _ -> Interop.clearEvent view "HasFocusChanged")
        props |> Interop.getValue<unit -> unit> "onMouseLeave" |> Option.iter (fun _ -> Interop.clearEvent view "MouseLeave")

        props |> Interop.getValue<bool> "enabled" |> Option.iter (fun _ -> view.Enabled <- true)
        props |> Interop.getValue<bool> "visible" |> Option.iter (fun _ -> view.Visible <- true)
        props |> Interop.getValue<Scheme> "scheme" |> Option.iter (fun _ -> view.SetScheme null |> ignore)
        props |> Interop.getValue<string> "schemeName" |> Option.iter (fun _ -> view.SchemeName <- null)
        props |> Interop.getValue<Alignment> "textAlignment" |> Option.iter (fun _ -> view.TextAlignment <- Alignment.Start)


    /// An in-place update is only possible when the position/dimension *kinds* match.
    /// Switching e.g. an absolute X to a centered X requires recreating the view.
    let canUpdate (view: View) props removedProps =
        let isPosCompatible (a: Pos) (b: Pos) =
            let nameA = a.GetType().Name
            let nameB = b.GetType().Name
            (nameA = "PosAbsolute") = (nameB = "PosAbsolute")

        let isDimCompatible (a: Dim) (b: Dim) =
            let nameA = a.GetType().Name
            let nameB = b.GetType().Name
            (nameA = "DimAbsolute") = (nameB = "DimAbsolute")

        let positionX = props |> Interop.getValue<Pos> "x" |> Option.map (fun v -> isPosCompatible view.X v) |> Option.defaultValue true
        let positionY = props |> Interop.getValue<Pos> "y" |> Option.map (fun v -> isPosCompatible view.Y v) |> Option.defaultValue true
        let width = props |> Interop.getValue<Dim> "width" |> Option.map (fun v -> isDimCompatible view.Width v) |> Option.defaultValue true
        let height = props |> Interop.getValue<Dim> "height" |> Option.map (fun v -> isDimCompatible view.Height v) |> Option.defaultValue true

        let widthNotRemoved = removedProps |> Interop.valueExists "width" |> not
        let heightNotRemoved = removedProps |> Interop.valueExists "height" |> not

        [ positionX; positionY; width; height; widthNotRemoved; heightNotRemoved ] |> List.forall id


/// The application root. In v2 the top-level container is a <see cref="Runnable"/>.
type PageElement(props: IProperty list) =
    inherit TerminalElement(props)

    override _.name = "Page"

    override this.create parent =
        this.parent <- parent
        let el = new Runnable()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        this.element <- prevElement


type WindowElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: Window) props =
        props |> Interop.getValue<string> "title" |> Option.iter (fun v -> element.Title <- v)
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> element.Text <- v)

    override _.name = "Window"

    override this.create parent =
        this.parent <- parent
        let el = new Window()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> Window
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type LabelElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: Label) props =
        props
        |> Interop.getValue<string> "text"
        |> Option.iter (fun v -> if Checker.textChanged element v then element.Text <- v)

    override _.name = "Label"

    override this.create parent =
        this.parent <- parent
        let el = new Label()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> Label
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type ButtonElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: Button) props =
        props
        |> Interop.getValue<string> "text"
        |> Option.iter (fun v -> if Checker.textChanged element v then element.Text <- v)

        props |> Interop.getValue<bool> "isDefault" |> Option.iter (fun v -> element.IsDefault <- v)

        // v2: the click is the Accepted (post) command event.
        props
        |> Interop.getValue<unit -> unit> "onClick"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "Accepted" (fun v b -> (v :?> Button).Accepted.Add (fun e -> b.invoke (box e))) (fun _ -> f ()))

    let removeProps (element: Button) props =
        props |> Interop.getValue<bool> "isDefault" |> Option.iter (fun _ -> element.IsDefault <- false)
        props |> Interop.getValue<unit -> unit> "onClick" |> Option.iter (fun _ -> Interop.clearEvent element "Accepted")

    override _.name = "Button"

    override this.create parent =
        this.parent <- parent
        let el = new Button()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> Button
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type CheckBoxElement(props: IProperty list) =
    inherit TerminalElement(props)

    let toState b = if b then CheckState.Checked else CheckState.UnChecked

    let setProps (element: CheckBox) props =
        props
        |> Interop.getValue<string> "text"
        |> Option.iter (fun v -> if Checker.textChanged element v then element.Text <- v)

        props |> Interop.getValue<bool> "checked" |> Option.iter (fun v -> element.Value <- toState v)

        props
        |> Interop.getValue<{| previous: bool; current: bool |} -> unit> "toggled"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> CheckBox).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<CheckState>
                    f {| previous = (e.OldValue = CheckState.Checked); current = (e.NewValue = CheckState.Checked) |}))

    let removeProps (element: CheckBox) props =
        props |> Interop.getValue<bool> "checked" |> Option.iter (fun _ -> element.Value <- CheckState.UnChecked)
        props |> Interop.getValue<{| previous: bool; current: bool |} -> unit> "toggled" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "CheckBox"

    override this.create parent =
        this.parent <- parent
        let el = new CheckBox()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> CheckBox
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type FrameViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: FrameView) props =
        props |> Interop.getValue<string> "title" |> Option.iter (fun v -> element.Title <- v)
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> element.Text <- v)

    override _.name = "FrameView"

    override this.create parent =
        this.parent <- parent
        let el = new FrameView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> FrameView
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type TextFieldElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: TextField) props =
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> Checker.setEditableText element v)

        props |> Interop.getValue<bool> "readOnly" |> Option.iter (fun v -> element.ReadOnly <- v)
        props |> Interop.getValue<bool> "secret" |> Option.iter (fun v -> element.Secret <- v)

        props
        |> Interop.getValue<string -> unit> "onTextChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> TextField).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<string>
                    f (if isNull e.NewValue then "" else e.NewValue)))

    let removeProps (element: TextField) props =
        props |> Interop.getValue<bool> "readOnly" |> Option.iter (fun _ -> element.ReadOnly <- false)
        props |> Interop.getValue<bool> "secret" |> Option.iter (fun _ -> element.Secret <- false)
        props |> Interop.getValue<string -> unit> "onTextChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "TextField"

    override this.create parent =
        this.parent <- parent
        let el = new TextField()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> TextField
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type TextViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: TextView) props =
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> Checker.setEditableText element v)

        props |> Interop.getValue<bool> "readOnly" |> Option.iter (fun v -> element.ReadOnly <- v)
        props |> Interop.getValue<bool> "wordWrap" |> Option.iter (fun v -> element.WordWrap <- v)
        props |> Interop.getValue<bool> "multiline" |> Option.iter (fun v -> element.Multiline <- v)

        props
        |> Interop.getValue<string -> unit> "onTextChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ContentsChanged"
                (fun v b -> (v :?> TextView).ContentsChanged.Add (fun e -> b.invoke (box e)))
                (fun _ -> f element.Text))

    let removeProps (element: TextView) props =
        props |> Interop.getValue<bool> "readOnly" |> Option.iter (fun _ -> element.ReadOnly <- false)
        props |> Interop.getValue<string -> unit> "onTextChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ContentsChanged")

    override _.name = "TextView"

    override this.create parent =
        this.parent <- parent
        let el = new TextView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> TextView
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type ProgressBarElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: ProgressBar) props =
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> element.Text <- v)
        props |> Interop.getValue<float> "fraction" |> Option.iter (fun v -> element.Fraction <- float32 v)
        props |> Interop.getValue<ProgressBarStyle> "progressBarStyle" |> Option.iter (fun v -> element.ProgressBarStyle <- v)
        props |> Interop.getValue<ProgressBarFormat> "progressBarFormat" |> Option.iter (fun v -> element.ProgressBarFormat <- v)

    override _.name = "ProgressBar"

    override this.create parent =
        this.parent <- parent
        let el = new ProgressBar()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> ProgressBar
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type LineElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: Line) props =
        props |> Interop.getValue<Orientation> "orientation" |> Option.iter (fun v -> element.Orientation <- v)

    override _.name = "Line"

    override this.create parent =
        this.parent <- parent
        let el = new Line()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> Line
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type ListViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: ListView) props =
        props
        |> Interop.getValue<string list> "source"
        |> Option.iter (fun items -> element.SetSource<string>(ObservableCollection<string>(items)))

        props |> Interop.getValue<int> "selectedItem" |> Option.iter (fun v -> element.SelectedItem <- Nullable v)

        props
        |> Interop.getValue<int -> unit> "onSelectedItemChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> ListView).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<Nullable<int>>
                    if e.NewValue.HasValue then f e.NewValue.Value))

    let removeProps (element: ListView) props =
        props |> Interop.getValue<int -> unit> "onSelectedItemChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "ListView"

    override this.create parent =
        this.parent <- parent
        let el = new ListView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> ListView
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


/// v1 RadioGroup -> v2 OptionSelector.
type OptionSelectorElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: OptionSelector) props =
        props
        |> Interop.getValue<string list> "radioLabels"
        |> Option.iter (fun items -> element.Labels <- (items |> List.toArray :> IReadOnlyList<string>))

        props |> Interop.getValue<int> "selectedItem" |> Option.iter (fun v -> element.Value <- Nullable v)

        props
        |> Interop.getValue<int -> unit> "onSelectedItemChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> OptionSelector).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<Nullable<int>>
                    if e.NewValue.HasValue then f e.NewValue.Value))

    let removeProps (element: OptionSelector) props =
        props |> Interop.getValue<int -> unit> "onSelectedItemChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "OptionSelector"

    override this.create parent =
        this.parent <- parent
        let el = new OptionSelector()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> OptionSelector
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


/// v1 ComboBox -> v2 DropDownList.
type DropDownListElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: DropDownList) props =
        props
        |> Interop.getValue<string list> "source"
        |> Option.iter (fun items -> element.Source <- ListWrapper<string>(ObservableCollection<string>(items)))

        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> Checker.setEditableText element v)
        props |> Interop.getValue<bool> "readonly" |> Option.iter (fun v -> element.ReadOnly <- v)

        props
        |> Interop.getValue<string -> unit> "onTextChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> DropDownList).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<string>
                    f (if isNull e.NewValue then "" else e.NewValue)))

    let removeProps (element: DropDownList) props =
        props |> Interop.getValue<string -> unit> "onTextChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "DropDownList"

    override this.create parent =
        this.parent <- parent
        let el = new DropDownList()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> DropDownList
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


/// v1 DateField -> v2 DatePicker.
type DatePickerElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: DatePicker) props =
        props |> Interop.getValue<DateTime> "date" |> Option.iter (fun v -> element.Value <- v)

        props
        |> Interop.getValue<DateTime -> unit> "onDateChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> DatePicker).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o -> f ((o :?> ValueChangedEventArgs<DateTime>).NewValue)))

    let removeProps (element: DatePicker) props =
        props |> Interop.getValue<DateTime -> unit> "onDateChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "DatePicker"

    override this.create parent =
        this.parent <- parent
        let el = new DatePicker()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> DatePicker
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type ColorPickerElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: ColorPicker) props =
        props |> Interop.getValue<Color> "selectedColor" |> Option.iter (fun v -> element.SelectedColor <- v)

        props
        |> Interop.getValue<Color -> unit> "onColorChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> ColorPicker).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<Nullable<Color>>
                    if e.NewValue.HasValue then f e.NewValue.Value))

    let removeProps (element: ColorPicker) props =
        props |> Interop.getValue<Color -> unit> "onColorChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "ColorPicker"

    override this.create parent =
        this.parent <- parent
        let el = new ColorPicker()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> ColorPicker
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type TextValidateFieldElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: TextValidateField) props =
        props |> Interop.getValue<ITextValidateProvider> "provider" |> Option.iter (fun v -> element.Provider <- v)
        props |> Interop.getValue<string> "text" |> Option.iter (fun v -> Checker.setEditableText element v)

    override _.name = "TextValidateField"

    override this.create parent =
        this.parent <- parent
        let el = new TextValidateField()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> TextValidateField
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


/// v1 TabView -> v2 Tabs. Each child element becomes a tab; its `title` is the tab label.
type TabsElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: Tabs) props =
        props
        |> Interop.getValue<View -> unit> "onSelectedTabChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> Tabs).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<View>
                    if not (isNull e.NewValue) then f e.NewValue))

    let removeProps (element: Tabs) props =
        props |> Interop.getValue<View -> unit> "onSelectedTabChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "Tabs"

    override this.create parent =
        this.parent <- parent
        let el = new Tabs()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> Tabs
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type TableViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: TableView) props =
        props |> Interop.getValue<DataTable> "table" |> Option.iter (fun v -> element.Table <- DataTableSource v)

        props
        |> Interop.getValue<TableSelection -> unit> "onSelectedCellChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "ValueChanged"
                (fun v b -> (v :?> TableView).ValueChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> ValueChangedEventArgs<TableSelection>
                    if not (isNull e.NewValue) then f e.NewValue))

    let removeProps (element: TableView) props =
        props |> Interop.getValue<TableSelection -> unit> "onSelectedCellChanged" |> Option.iter (fun _ -> Interop.clearEvent element "ValueChanged")

    override _.name = "TableView"

    override this.create parent =
        this.parent <- parent
        let el = new TableView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> TableView
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type TreeViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: TreeView<ITreeNode>) props =
        props
        |> Interop.getValue<ITreeNode list> "nodes"
        |> Option.iter (fun nodes ->
            element.ClearObjects()
            element.AddObjects(nodes))

        props |> Interop.getValue<ITreeNode> "selectedObject" |> Option.iter (fun v -> element.SelectedObject <- v)

        props
        |> Interop.getValue<ITreeNode -> unit> "onSelectionChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "SelectionChanged"
                (fun v b -> (v :?> TreeView<ITreeNode>).SelectionChanged.Add (fun e -> b.invoke (box e)))
                (fun o ->
                    let e = o :?> SelectionChangedEventArgs<ITreeNode>
                    if not (isNull e.NewValue) then f e.NewValue))

        // v2 has no ObjectActivated event; activation flows through the base Accept command.
        props
        |> Interop.getValue<ITreeNode -> unit> "onObjectActivated"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "Accepted"
                (fun v b -> v.Accepted.Add (fun _ -> b.invoke null))
                (fun _ ->
                    let o = element.SelectedObject
                    if not (isNull (box o)) then f o))

    let removeProps (element: TreeView<ITreeNode>) props =
        props |> Interop.getValue<ITreeNode -> unit> "onSelectionChanged" |> Option.iter (fun _ -> Interop.clearEvent element "SelectionChanged")
        props |> Interop.getValue<ITreeNode -> unit> "onObjectActivated" |> Option.iter (fun _ -> Interop.clearEvent element "Accepted")

    override _.name = "TreeView"

    override this.create parent =
        this.parent <- parent
        let el = new TreeView<ITreeNode>()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> TreeView<ITreeNode>
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


type HexViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    let setProps (element: HexView) props =
        props |> Interop.getValue<System.IO.Stream> "source" |> Option.iter (fun v -> element.Source <- v)
        props |> Interop.getValue<bool> "allowEdits" |> Option.iter (fun v -> element.ReadOnly <- not v)

        props
        |> Interop.getValue<HexViewEditEventArgs -> unit> "onEdited"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "Edited"
                (fun v b -> (v :?> HexView).Edited.Add (fun e -> b.invoke (box e)))
                (fun o -> f (o :?> HexViewEditEventArgs)))

        props
        |> Interop.getValue<HexViewEventArgs -> unit> "onPositionChanged"
        |> Option.iter (fun f ->
            Interop.bridgeEvent element "PositionChanged"
                (fun v b -> (v :?> HexView).PositionChanged.Add (fun e -> b.invoke (box e)))
                (fun o -> f (o :?> HexViewEventArgs)))

    let removeProps (element: HexView) props =
        props |> Interop.getValue<HexViewEditEventArgs -> unit> "onEdited" |> Option.iter (fun _ -> Interop.clearEvent element "Edited")
        props |> Interop.getValue<HexViewEventArgs -> unit> "onPositionChanged" |> Option.iter (fun _ -> Interop.clearEvent element "PositionChanged")

    override _.name = "HexView"

    override this.create parent =
        this.parent <- parent
        let el = new HexView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let element = prevElement :?> HexView
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        removeProps element removedProps
        ViewElement.setProps prevElement changedProps
        setProps element changedProps
        this.element <- prevElement


/// GraphView has a rich imperative API (axes, series). Configure it via `prop.ref`.
type GraphViewElement(props: IProperty list) =
    inherit TerminalElement(props)

    override _.name = "GraphView"

    override this.create parent =
        this.parent <- parent
        let el = new GraphView()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        this.element <- prevElement


/// MenuBar built from pre-constructed v2 menus (see the `menu` builders in Props.fs).
type MenuBarElement(props: IProperty list) =
    inherit TerminalElement(props)

    override _.name = "MenuBar"

    override this.create parent =
        this.parent <- parent
        let menus = props |> Interop.getValueDefault<MenuBarItem list> "menus" []
        let el = new MenuBar(menus |> List.map (fun m -> m :> MenuItem))
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    // The menu structure is fixed at creation. `menus` is rebuilt as new objects every
    // render, so never treat it as a change that forces a recreate — that would churn
    // focus on every dispatch. Update only the common view props in place.
    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        this.element <- prevElement


/// StatusBar built from pre-constructed v2 Shortcuts (see the `statusItem` builders in Props.fs).
type StatusBarElement(props: IProperty list) =
    inherit TerminalElement(props)

    override _.name = "StatusBar"

    override this.create parent =
        this.parent <- parent
        let items = props |> Interop.getValueDefault<Shortcut list> "items" []
        let el = new StatusBar(items)
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    // `items` is rebuilt as new objects every render; don't let that force a recreate
    // (which would churn focus on every dispatch). Update common view props in place.
    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        this.element <- prevElement


/// Wizard has a rich imperative API (steps, navigation). Configure it via `prop.ref`.
type WizardElement(props: IProperty list) =
    inherit TerminalElement(props)

    override _.name = "Wizard"

    override this.create parent =
        this.parent <- parent
        let el = new Wizard()
        parent |> Option.iter (fun p -> p.Add el |> ignore)
        ViewElement.setProps el props
        props |> Interop.getValue<View -> unit> "ref" |> Option.iter (fun v -> v el)
        this.element <- el

    override this.canUpdate prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.canUpdate prevElement changedProps removedProps

    override this.update prevElement oldProps =
        let (changedProps, removedProps) = Interop.filterProps oldProps props
        ViewElement.removeProps prevElement removedProps
        ViewElement.setProps prevElement changedProps
        this.element <- prevElement
