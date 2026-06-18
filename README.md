# Terminal.Gui.Elmish (v2)

Elmish (MVU) wrapper around Terminal.Gui **v2** with a Feliz-like view DSL for F#.

Migrated from the v1 wrapper: instance-based `Application`, keyed tree reconciliation,
subscribe-once event bridging, and a modernized DSL (Scheme/LineStyle/Pos.Absolute/CheckState).

## Build

```bash
dotnet build src/Terminal.Gui.Elmish/Terminal.Gui.Elmish.fsproj
dotnet run --project examples/Counter
```
