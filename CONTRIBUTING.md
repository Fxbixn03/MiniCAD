# Contributing to MiniCAD

Thanks for taking a look! 🎉 MiniCAD is a small hobby project, so contributing is meant
to be relaxed and low-pressure. Whether you're fixing a typo, reporting a bug, or adding
a whole feature — it's all appreciated.

By taking part, you agree to follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to help

- **Found a bug?** Open a [bug report](https://github.com/Fxbixn03/MiniCAD/issues/new/choose).
- **Have an idea?** Open a [feature request](https://github.com/Fxbixn03/MiniCAD/issues/new/choose).
- **Just curious or stuck?** Open a question issue or start a discussion.
- **Want to write code?** See below.

## Getting set up

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download) (built against `10.0.104`).

```bash
git clone https://github.com/Fxbixn03/MiniCAD.git
cd MiniCAD

dotnet build                           # build the whole solution
dotnet run --project src/MiniCAD.App   # launch the app
dotnet test                            # run all tests
```

## Making a change

1. **Fork** the repo and create a branch for your change
   (e.g. `git checkout -b fix-circle-snapping`).
2. **Make your change.** Keep commits small and write a short message that says what the
   commit does — that's all the convention we need.
3. **Build and test** before you push: `dotnet build` and `dotnet test` should both pass.
4. **Open a pull request** against `main`. Describe what you changed and link any related
   issue (e.g. "Closes #12").

No need for perfect code or huge changes — small, focused pull requests are the easiest to
review and the most fun to merge.

## A note on the project layout

MiniCAD is split into a few projects with a deliberate dependency direction
(`App → Renderer → Core`). The most important rule: **`MiniCAD.Core` stays free of any UI
or graphics dependencies** so it remains easy to test on its own. If you're adding drawing
logic, that usually belongs in `Core`; rendering goes in `Renderer`; windows and buttons go
in `App`. There's more detail in [CLAUDE.md](CLAUDE.md).

Happy hacking! ✏️
