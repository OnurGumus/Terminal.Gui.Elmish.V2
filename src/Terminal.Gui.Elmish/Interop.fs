namespace Terminal.Gui.Elmish

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Terminal.Gui.App
open Terminal.Gui.ViewBase

/// Holds the currently running application instance so standalone helpers (dialogs,
/// message boxes) can reach the v2 <see cref="IApplication"/> they now require.
[<RequireQualifiedAccess>]
module ElmishApp =
    let mutable current: IApplication option = None

    /// The running application. Throws if accessed before <c>Program.run</c> has started.
    let require () =
        match current with
        | Some app -> app
        | None -> failwith "No running Terminal.Gui.Elmish application. Call this from within a running Program."

/// A single mutable event bridge: the CLR event is subscribed exactly once and
/// invokes <c>invoke</c>, which reconciliation swaps in-place. This avoids the
/// fragile reflection-based delegate field poking used in the v1 wrapper.
type EventBridge = { mutable invoke: obj -> unit }

[<RequireQualifiedAccess>]
module internal EventStore =

    // Per-view table of named event bridges. Keyed weakly so disposed views are collected.
    let private table = ConditionalWeakTable<View, Dictionary<string, EventBridge>>()

    let bridges (view: View) = table.GetValue(view, fun _ -> Dictionary<string, EventBridge>())


[<RequireQualifiedAccess>]
module Interop =

    let inline valueExists name (props: IProperty list) =
        props
        |> Seq.cast<KeyValue>
        |> Seq.exists (function | KeyValue (pname, _) -> name = pname)

    let inline getValueGeneric<'a, 'b when 'b :> IProperty> (name: string) (props: 'b list) =
        props
        |> Seq.cast<KeyValue>
        |> Seq.tryFind (function | KeyValue (pname, _) -> name = pname)
        |> Option.map (function | KeyValue (_, value) -> value :?> 'a)

    let inline getValueDefaultGeneric<'a, 'b when 'b :> IProperty> (name: string) (defaultValue: 'a) (props: 'b list) =
        props
        |> getValueGeneric<'a, 'b> name
        |> Option.defaultValue defaultValue

    let inline mkprop (name: string) (data: obj) : IProperty = KeyValue (name, data)
    let inline getValue<'a> name props = getValueGeneric<'a, IProperty> name props
    let inline getValueDefault<'a> name defaultVal (props: IProperty list) = getValueDefaultGeneric<'a, IProperty> name defaultVal props

    let inline mkMenuProp (name: string) (data: obj) : IMenuProperty = KeyValue (name, data)
    let inline getMenuValue<'a> name (props: IMenuProperty list) = getValueGeneric<'a, IMenuProperty> name props
    let inline getMenuValueDefault<'a> name defaultVal (props: IMenuProperty list) = getValueDefaultGeneric<'a, IMenuProperty> name defaultVal props

    let inline mkMenuBarProp (name: string) (data: obj) : IMenuBarProperty = KeyValue (name, data)
    let inline getMenuBarValue<'a> name (props: IMenuBarProperty list) = getValueGeneric<'a, IMenuBarProperty> name props
    let inline getMenuBarValueDefault<'a> name defaultVal (props: IMenuBarProperty list) = getValueDefaultGeneric<'a, IMenuBarProperty> name defaultVal props

    let mkTabProp (name: string) (data: obj) : ITabProperty = KeyValue (name, data)
    let getTabValue<'a> name (props: ITabProperty list) = getValueGeneric<'a, ITabProperty> name props

    let mkTabItemProp (name: string) (data: obj) : ITabItemProperty = KeyValue (name, data)
    let getTabItemValue<'a> name (props: ITabItemProperty list) = getValueGeneric<'a, ITabItemProperty> name props

    let inline mkstyle (name: string) (data: obj) : IStyle = KeyValue (name, data)

    let inline styleExists name (styles: IStyle list) =
        styles
        |> Seq.cast<KeyValue>
        |> Seq.exists (function | KeyValue (pname, _) -> name = pname)

    let inline getStyle<'a> name (styles: IStyle list) =
        styles
        |> Seq.cast<KeyValue>
        |> Seq.tryFind (function | KeyValue (pname, _) -> name = pname)
        |> Option.map (function | KeyValue (_, value) -> value :?> 'a)

    let inline getStyleDefault<'a> name defaultVal (styles: IStyle list) =
        styles
        |> getStyle<'a> name
        |> Option.defaultValue defaultVal


    /// Splits new props into (changed, removed) relative to the previous render's props.
    let filterProps (oldprops: IProperty list) (newprops: IProperty list) =
        let get (KeyValue (a, b)) = (a, b)

        let changedProps =
            ([], newprops)
            ||> List.fold (fun resultProps newProp ->
                let kv = newProp :?> KeyValue
                let (name, newValue) = kv |> get
                let oldValue = oldprops |> getValue name

                match oldValue with
                | None -> resultProps @ [ newProp ]
                | Some oldValue when oldValue = newValue -> resultProps
                | Some _ -> resultProps @ [ newProp ])

        let removedProps =
            ([], oldprops)
            ||> List.fold (fun resultProps oldProp ->
                let op = oldProp :?> KeyValue
                let (name, _) = op |> get
                let stillThere = newprops |> valueExists name

                if stillThere then resultProps else resultProps @ [ oldProp ])

        (changedProps, removedProps)


    let inline csharpList (list: 'a list) = System.Linq.Enumerable.ToList list


    /// The effective SuperView, skipping over Terminal.Gui adornment/content wrappers.
    let rec getParent (view: View) =
        view.SuperView
        |> Option.ofObj
        |> Option.bind (fun p ->
            if p.GetType().Name.Contains("Adornment") then
                getParent p
            else
                Some p)


    // ----- Event bridging (subscribe-once, latest-wins) -----

    /// Registers (or refreshes) a single CLR subscription for <paramref name="name"/> on
    /// <paramref name="view"/>. <c>subscribeOnce</c> wires the actual Terminal.Gui event the
    /// first time; <c>invoke</c> is the latest handler and is swapped in on every update.
    let bridgeEvent (view: View) (name: string) (subscribeOnce: View -> EventBridge -> unit) (invoke: obj -> unit) =
        let d = EventStore.bridges view

        match d.TryGetValue name with
        | true, b -> b.invoke <- invoke
        | _ ->
            let b = { invoke = invoke }
            d.[name] <- b
            subscribeOnce view b

    /// Detaches the logical handler for an event (the CLR subscription stays, but becomes a no-op).
    let clearEvent (view: View) (name: string) =
        let d = EventStore.bridges view

        match d.TryGetValue name with
        | true, b -> b.invoke <- (fun _ -> ())
        | _ -> ()


[<RequireQualifiedAccess>]
module internal Checker =

    let textChanged (element: View) (text: string) = element.Text <> text

    /// Writes a model-provided value into an editable text view only when it actually
    /// differs. While the user types, the coalesced render always rebuilds from the latest
    /// model (kept in sync via the change event), so the value already matches and this is
    /// a no-op — no cursor fighting. When the model changes the text programmatically
    /// (e.g. clearing an input after submit), the values differ and the write applies.
    let setEditableText (element: View) (text: string) =
        if element.Text <> text then
            element.Text <- text
