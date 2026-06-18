namespace Terminal.Gui.Elmish

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
