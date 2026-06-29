namespace Terminal.Gui.Elmish

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.Text
open Terminal.Gui.Elmish.Elements


type prop =
    static member inline children(children: TerminalElement list) = Interop.mkprop "children" children
    static member inline ref(reference: View -> unit) = Interop.mkprop "ref" reference

    static member inline id(value: string) = Interop.mkprop "id" value
    static member inline title(value: string) = Interop.mkprop "title" value

    static member inline tabStop(value: TabBehavior) = Interop.mkprop "tabStop" value
    static member inline canFocus(value: bool) = Interop.mkprop "canFocus" value

    static member inline enabled = Interop.mkprop "enabled" true
    static member inline disabled = Interop.mkprop "enabled" false
    static member inline visible(value: bool) = Interop.mkprop "visible" value

    /// Applies a full Terminal.Gui v2 <see cref="Scheme"/> to the view.
    static member inline scheme(value: Scheme) = Interop.mkprop "scheme" value
    /// References a named scheme registered with the SchemeManager.
    static member inline schemeName(value: string) = Interop.mkprop "schemeName" value
    /// Convenience: builds a single-attribute <see cref="Scheme"/> from foreground/background colors.
    static member color(foreground: Color, background: Color) =
        let mutable fg = foreground
        let mutable bg = background
        let attribute = Attribute(&fg, &bg)
        Interop.mkprop "scheme" (Scheme attribute)

    static member inline borderStyle(value: LineStyle) = Interop.mkprop "borderStyle" value

    // events
    static member inline onKeyDown(f: Key -> unit) = Interop.mkprop "onKeyDown" f
    static member inline onKeyUp(f: Key -> unit) = Interop.mkprop "onKeyUp" f
    static member inline onEnabledChanged(f: unit -> unit) = Interop.mkprop "onEnabledChanged" f
    static member inline onVisibleChanged(f: unit -> unit) = Interop.mkprop "onVisibleChanged" f
    static member inline onHasFocusChanged(f: HasFocusEventArgs -> unit) = Interop.mkprop "onHasFocusChanged" f
    static member inline onMouseLeave(f: unit -> unit) = Interop.mkprop "onMouseLeave" f


module prop =

    module position =

        type x =
            static member inline at(i: int) = Interop.mkprop "x" (Pos.Absolute i)
            static member inline center = Interop.mkprop "x" (Pos.Center())
            static member inline percent(i: int) = Interop.mkprop "x" (Pos.Percent i)
            static member inline anchorEnd = Interop.mkprop "x" (Pos.AnchorEnd())

        type y =
            static member inline at(i: int) = Interop.mkprop "y" (Pos.Absolute i)
            static member inline center = Interop.mkprop "y" (Pos.Center())
            static member inline percent(i: int) = Interop.mkprop "y" (Pos.Percent i)
            static member inline anchorEnd = Interop.mkprop "y" (Pos.AnchorEnd())
            /// Anchor to the bottom, leaving `margin` rows below.
            static member inline fromBottom(margin: int) = Interop.mkprop "y" (Pos.AnchorEnd margin)


    type width =
        static member inline sized(i: int) = Interop.mkprop "width" (Dim.Absolute i)
        static member inline filled = Interop.mkprop "width" (Dim.Fill())
        static member inline fill(margin: int) = Interop.mkprop "width" (Dim.Fill(Dim.Absolute margin))
        static member inline percent(percent: int) = Interop.mkprop "width" (Dim.Percent percent)
        static member inline auto = Interop.mkprop "width" (Dim.Auto())


    type height =
        static member inline sized(i: int) = Interop.mkprop "height" (Dim.Absolute i)
        static member inline filled = Interop.mkprop "height" (Dim.Fill())
        static member inline fill(margin: int) = Interop.mkprop "height" (Dim.Fill(Dim.Absolute margin))
        static member inline percent(percent: int) = Interop.mkprop "height" (Dim.Percent percent)
        static member inline auto = Interop.mkprop "height" (Dim.Auto())


    type textAlignment =
        static member inline left = Interop.mkprop "textAlignment" Alignment.Start
        static member inline centered = Interop.mkprop "textAlignment" Alignment.Center
        static member inline right = Interop.mkprop "textAlignment" Alignment.End
        static member inline justified = Interop.mkprop "textAlignment" Alignment.Fill

    type textDirection =
        static member inline leftRight_topBottom = Interop.mkprop "textDirection" TextDirection.LeftRight_TopBottom
        static member inline topBottom_leftRight = Interop.mkprop "textDirection" TextDirection.TopBottom_LeftRight


module borderStyle =
    let none = LineStyle.None
    let single = LineStyle.Single
    let double = LineStyle.Double
    let rounded = LineStyle.Rounded
    let heavy = LineStyle.Heavy


type window =
    static member inline title(p: string) = Interop.mkprop "title" p
    static member inline text(p: string) = Interop.mkprop "text" p
    static member inline children(children: TerminalElement list) = Interop.mkprop "children" children


type frameView =
    static member inline title(value: string) = Interop.mkprop "title" value
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline children(children: TerminalElement list) = Interop.mkprop "children" children


type page =
    static member inline children(children: TerminalElement list) = Interop.mkprop "children" children


type label =
    static member inline text(value: string) = Interop.mkprop "text" value


type button =
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline onClick(f: unit -> unit) = Interop.mkprop "onClick" f
    static member inline isDefault(value: bool) = Interop.mkprop "isDefault" value


type checkBox =
    static member inline text(text: string) = Interop.mkprop "text" text
    static member inline isChecked(v: bool) = Interop.mkprop "checked" v
    static member inline onToggled(f: {| previous: bool; current: bool |} -> unit) = Interop.mkprop "toggled" f
    /// Render radio-button glyphs (●/○) instead of checkbox glyphs (☑/☐).
    static member inline radioStyle(value: bool) = Interop.mkprop "radioStyle" value
    /// Allow a third "none" state (checked / unchecked / none) on toggle.
    static member inline allowCheckStateNone(value: bool) = Interop.mkprop "allowCheckStateNone" value


type textField =
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline readOnly(value: bool) = Interop.mkprop "readOnly" value
    static member inline secret = Interop.mkprop "secret" true
    static member inline onTextChanged(value: string -> unit) = Interop.mkprop "onTextChanged" value


type textView =
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline readOnly(value: bool) = Interop.mkprop "readOnly" value
    static member inline wordWrap(value: bool) = Interop.mkprop "wordWrap" value
    static member inline multiline(value: bool) = Interop.mkprop "multiline" value
    /// For read-only views: scroll to the bottom whenever the text changes (e.g. a chat log).
    static member inline scrollToEnd = Interop.mkprop "scrollToEnd" true
    static member inline onTextChanged(value: string -> unit) = Interop.mkprop "onTextChanged" value
    /// Number of columns a tab character occupies.
    static member inline tabWidth(value: int) = Interop.mkprop "tabWidth" value


type progressBar =
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline fraction(value: float) = Interop.mkprop "fraction" value

module progressBar =
    type style =
        static member inline blocks = Interop.mkprop "progressBarStyle" ProgressBarStyle.Blocks
        static member inline continuous = Interop.mkprop "progressBarStyle" ProgressBarStyle.Continuous
        static member inline marqueeBlocks = Interop.mkprop "progressBarStyle" ProgressBarStyle.MarqueeBlocks
        static member inline marqueeContinuous = Interop.mkprop "progressBarStyle" ProgressBarStyle.MarqueeContinuous

    type format =
        static member inline simple = Interop.mkprop "progressBarFormat" ProgressBarFormat.Simple
        static member inline simplePlusPercentage = Interop.mkprop "progressBarFormat" ProgressBarFormat.SimplePlusPercentage


type lineView =
    static member inline horizontal = Interop.mkprop "orientation" Orientation.Horizontal
    static member inline vertical = Interop.mkprop "orientation" Orientation.Vertical
    /// The line's own glyph style (Single, Double, Heavy, Rounded, Dashed, Dotted, ...).
    static member inline lineStyle(value: LineStyle) = Interop.mkprop "lineStyle" value
    /// Override the scheme-derived color/style used to draw the line.
    static member inline attribute(value: Attribute) = Interop.mkprop "lineAttribute" value


type listView =
    static member inline source(items: string list) = Interop.mkprop "source" items
    static member inline selectedItem(index: int) = Interop.mkprop "selectedItem" index
    static member inline onSelectedItemChanged(f: int -> unit) = Interop.mkprop "onSelectedItemChanged" f
    /// Show mark glyphs ([x] / [ ]) in front of each item.
    static member inline showMarks(value: bool) = Interop.mkprop "showMarks" value
    /// Allow more than one item to be marked at once.
    static member inline markMultiple(value: bool) = Interop.mkprop "markMultiple" value


/// v1 radioGroup -> v2 OptionSelector.
type radioGroup =
    static member inline radioLabels(labels: string list) = Interop.mkprop "radioLabels" labels
    static member inline selectedItem(index: int) = Interop.mkprop "selectedItem" index
    static member inline onSelectedItemChanged(f: int -> unit) = Interop.mkprop "onSelectedItemChanged" f
    /// Horizontal gap between options when laid out horizontally.
    static member inline horizontalSpace(value: int) = Interop.mkprop "horizontalSpace" value

module radioGroup =
    /// Layout direction of the options.
    type orientation =
        static member inline vertical = Interop.mkprop "orientation" Orientation.Vertical
        static member inline horizontal = Interop.mkprop "orientation" Orientation.Horizontal


/// v1 comboBox -> v2 DropDownList.
type comboBox =
    static member inline source(items: string list) = Interop.mkprop "source" items
    static member inline text(value: string) = Interop.mkprop "text" value
    static member inline readonly(value: bool) = Interop.mkprop "readonly" value
    static member inline onTextChanged(f: string -> unit) = Interop.mkprop "onTextChanged" f


/// v1 dateField -> v2 DatePicker.
type dateField =
    static member inline date(value: System.DateTime) = Interop.mkprop "date" value
    static member inline onDateChanged(f: System.DateTime -> unit) = Interop.mkprop "onDateChanged" f
    /// Culture used to format the date and lay out the calendar.
    static member inline culture(value: System.Globalization.CultureInfo) = Interop.mkprop "culture" value


type colorPicker =
    static member inline selectedColor(value: Color) = Interop.mkprop "selectedColor" value
    static member inline onColorChanged(f: Color -> unit) = Interop.mkprop "onColorChanged" f

module colorPicker =
    type style =
        /// Show the numeric channel input fields (default on).
        static member inline showTextFields(value: bool) = Interop.mkprop "style.showTextFields" value
        /// Show the color-name selector (default off).
        static member inline showColorName(value: bool) = Interop.mkprop "style.showColorName" value

    /// Color model the picker edits in.
    type colorModel =
        static member inline rgb = Interop.mkprop "style.colorModel" ColorModel.RGB
        static member inline hsv = Interop.mkprop "style.colorModel" ColorModel.HSV
        static member inline hsl = Interop.mkprop "style.colorModel" ColorModel.HSL


type textValidateField =
    static member inline provider(value: ITextValidateProvider) = Interop.mkprop "provider" value
    static member inline text(value: string) = Interop.mkprop "text" value


/// v1 tabView -> v2 Tabs. Tabs are the child elements; set each child's `prop.title`.
type tabView =
    static member inline children(children: TerminalElement list) = Interop.mkprop "children" children
    static member inline onSelectedTabChanged(f: View -> unit) = Interop.mkprop "onSelectedTabChanged" f
    /// Border style drawn around the tab headers.
    static member inline tabLineStyle(value: LineStyle) = Interop.mkprop "tabLineStyle" value
    /// Thickness of the tab header strip (rows when top/bottom, columns when left/right).
    static member inline tabDepth(value: int) = Interop.mkprop "tabDepth" value
    /// Gap between tabs; negative values overlap them.
    static member inline tabSpacing(value: int) = Interop.mkprop "tabSpacing" value

module tabView =
    /// Which side of the view the tab headers sit on.
    type side =
        static member inline top = Interop.mkprop "tabSide" Side.Top
        static member inline bottom = Interop.mkprop "tabSide" Side.Bottom
        static member inline left = Interop.mkprop "tabSide" Side.Left
        static member inline right = Interop.mkprop "tabSide" Side.Right


type tableView =
    static member inline table(value: System.Data.DataTable) = Interop.mkprop "table" value
    static member inline onSelectedCellChanged(f: TableSelection -> unit) = Interop.mkprop "onSelectedCellChanged" f

module tableView =
    /// Grid-line / header options mapped onto Terminal.Gui's `TableStyle`. Each takes a bool so
    /// callers can both enable a default-off line (e.g. `style.bottomLine true`) and disable a
    /// default-on one (e.g. `style.verticalCellLines false`).
    type style =
        /// Closing line along the bottom of the table (default off). Set true for a fully boxed table.
        static member inline bottomLine(value: bool) = Interop.mkprop "style.bottomLine" value
        /// Line above the header row (default on).
        static member inline headerOverline(value: bool) = Interop.mkprop "style.headerOverline" value
        /// Line below the header row (default on).
        static member inline headerUnderline(value: bool) = Interop.mkprop "style.headerUnderline" value
        /// Whether the header row is shown at all (default on).
        static member inline headers(value: bool) = Interop.mkprop "style.headers" value
        /// Vertical separators between cells (default on).
        static member inline verticalCellLines(value: bool) = Interop.mkprop "style.verticalCellLines" value
        /// Vertical separators between header cells (default on).
        static member inline verticalHeaderLines(value: bool) = Interop.mkprop "style.verticalHeaderLines" value
        /// Left edge line on the first column (default on).
        static member inline firstColumnLine(value: bool) = Interop.mkprop "style.firstColumnLine" value
        /// Right edge line on the last column (default on).
        static member inline lastColumnLine(value: bool) = Interop.mkprop "style.lastColumnLine" value


type treeView =
    static member inline nodes(value: ITreeNode list) = Interop.mkprop "nodes" value
    static member inline selectedObject(value: ITreeNode) = Interop.mkprop "selectedObject" value
    static member inline onSelectionChanged(f: ITreeNode -> unit) = Interop.mkprop "onSelectionChanged" f
    static member inline onObjectActivated(f: ITreeNode -> unit) = Interop.mkprop "onObjectActivated" f
    /// Allow selecting more than one node (default on).
    static member inline multiSelect(value: bool) = Interop.mkprop "multiSelect" value
    /// Allow jumping to nodes by typing their leading letters (default on).
    static member inline allowLetterBasedNavigation(value: bool) = Interop.mkprop "allowLetterBasedNavigation" value

module treeView =
    type style =
        /// Draw the vertical connector lines between branches (default on).
        static member inline showBranchLines(value: bool) = Interop.mkprop "style.showBranchLines" value
        /// Colorize the expand/collapse glyphs.
        static member inline colorExpandSymbol(value: bool) = Interop.mkprop "style.colorExpandSymbol" value
        /// Invert the expand/collapse glyph colors.
        static member inline invertExpandSymbolColors(value: bool) = Interop.mkprop "style.invertExpandSymbolColors" value
        /// Highlight only the node text rather than the whole row.
        static member inline highlightModelTextOnly(value: bool) = Interop.mkprop "style.highlightModelTextOnly" value
        /// Glyph shown for an expandable (collapsed) node, e.g. a Rune for '▶'.
        static member inline expandableSymbol(value: System.Text.Rune) = Interop.mkprop "style.expandableSymbol" value
        /// Glyph shown for a collapsible (expanded) node, e.g. a Rune for '▼'.
        static member inline collapseableSymbol(value: System.Text.Rune) = Interop.mkprop "style.collapseableSymbol" value


type hexView =
    static member inline source(value: System.IO.Stream) = Interop.mkprop "source" value
    static member inline allowEdits(value: bool) = Interop.mkprop "allowEdits" value
    static member inline onEdited(f: HexViewEditEventArgs -> unit) = Interop.mkprop "onEdited" f
    static member inline onPositionChanged(f: HexViewEventArgs -> unit) = Interop.mkprop "onPositionChanged" f
    /// Number of bytes shown per row.
    static member inline bytesPerLine(value: int) = Interop.mkprop "bytesPerLine" value
    /// Width (in characters) of the address column.
    static member inline addressWidth(value: int) = Interop.mkprop "addressWidth" value


/// Builders for v2 menu structures used by `View.menuBar`.
type menu =
    /// A leaf menu item that runs an action when chosen.
    static member item(title: string, action: unit -> unit) : MenuItem =
        MenuItem(commandText = title, action = System.Action(action))

    /// A horizontal separator line inside a menu.
    static member separator : View = new Line() :> View

    /// A top-level menu (or submenu) containing the given items.
    static member sub(title: string, items: MenuItem list) : MenuBarItem =
        MenuBarItem(title, items |> List.map (fun i -> i :> View) |> List.toSeq)


type menuBar =
    static member inline menus(menus: MenuBarItem list) = Interop.mkprop "menus" menus


/// Builders for v2 status bar items used by `View.statusBar`.
type statusItem =
    static member create(title: string, action: unit -> unit) : Shortcut =
        Shortcut(Key.Empty, title, System.Action(action))

    static member create(key: Key, title: string, action: unit -> unit) : Shortcut =
        Shortcut(key, title, System.Action(action))


type statusBar =
    static member inline items(items: Shortcut list) = Interop.mkprop "items" items
