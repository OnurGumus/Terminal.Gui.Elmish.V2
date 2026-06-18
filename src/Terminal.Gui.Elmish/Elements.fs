namespace Terminal.Gui.Elmish.Elements

open System
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
        props
        |> Interop.getValue<string> "text"
        |> Option.iter (fun v -> if Checker.textChanged element v then element.Text <- v)

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
        props
        |> Interop.getValue<string> "text"
        |> Option.iter (fun v -> if Checker.textChanged element v then element.Text <- v)

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
