namespace Terminal.Gui.Elmish

module Differ =

    open System.Collections.Generic
    open Terminal.Gui.ViewBase
    open Terminal.Gui.Elmish.Elements

    /// The optional stable identity supplied via <c>prop.id</c>. Empty when absent.
    let toElementId (element: TerminalElement) =
        element.properties
        |> List.tryPick (fun e ->
            match e with
            | :? KeyValue as KeyValue("id", id) -> Some(string id)
            | _ -> None)
        |> Option.defaultValue ""

    /// Reconciliation key: the element kind plus its optional id. Two nodes with the
    /// same key are considered "the same view" across renders.
    let key (element: TerminalElement) = element.name + "_" + (toElementId element)


    /// Creates the backing views for a freshly-built subtree and attaches them.
    let rec initializeTree (parent: View option) (tree: TerminalElement) =
        tree.create parent
        tree.children |> List.iter (fun child -> initializeTree (Some tree.element) child)


    /// Incremented whenever a view is removed. The host loop watches this to request a
    /// full screen clear, because Terminal.Gui leaves a removed view's cells on screen.
    let mutable disposals = 0

    /// Detaches and disposes a node's backing view. Removal goes through the *actual*
    /// <see cref="View.SuperView"/>, not the logical parent.
    let private dispose (node: TerminalElement) =
        let superView = node.element.SuperView

        if not (isNull superView) then
            superView.Remove node.element |> ignore
            superView.SetNeedsDraw()

        node.element.RemoveAll()
        node.element.Dispose()
        disposals <- disposals + 1


    /// Reconciles one node that is known to share a key with its previous incarnation.
    let rec private updateNode (oldNode: TerminalElement) (newNode: TerminalElement) =
        if newNode.canUpdate oldNode.element oldNode.properties then
            newNode.update oldNode.element oldNode.properties
            reconcileChildren oldNode.element oldNode.children newNode.children
        else
            // Kind matches but the change can't be applied in place (e.g. an absolute
            // dimension became relative). Recreate the whole node.
            let parent = oldNode.element |> Interop.getParent
            dispose oldNode
            initializeTree parent newNode


    /// Keyed reconciliation of a sibling list. Nodes are matched by key (stable when
    /// <c>prop.id</c> is provided, positional among same-kind siblings otherwise), updated
    /// in place, created when new, and removed/disposed when gone.
    and private reconcileChildren (parent: View) (olds: TerminalElement list) (news: TerminalElement list) =
        let oldByKey = Dictionary<string, Queue<TerminalElement>>()

        olds
        |> List.iter (fun o ->
            let k = key o

            match oldByKey.TryGetValue k with
            | true, q -> q.Enqueue o
            | _ ->
                let q = Queue<TerminalElement>()
                q.Enqueue o
                oldByKey.[k] <- q)

        let matched = HashSet<TerminalElement>(HashIdentity.Reference)

        news
        |> List.iter (fun n ->
            let k = key n

            match oldByKey.TryGetValue k with
            | true, q when q.Count > 0 ->
                let o = q.Dequeue()
                matched.Add o |> ignore
                updateNode o n
            | _ -> initializeTree (Some parent) n)

        olds
        |> List.iter (fun o ->
            if not (matched.Contains o) then
                dispose o)


    /// Entry point: diff the previously rendered tree against the next one.
    let update (rootTree: TerminalElement) (newTree: TerminalElement) =
        if key rootTree <> key newTree then
            let parent = rootTree.element |> Interop.getParent
            dispose rootTree
            initializeTree parent newTree
        else
            updateNode rootTree newTree
