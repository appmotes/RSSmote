# RSSmote — working notes for Claude

RSSmote is a cross-platform RSS reader (macOS, Windows, Linux) built with .NET 10 and Avalonia 12.
It is distributed by direct download, possibly with full source, so every dependency must be free and
permissively licensed (see `Docs/dependencies.md`), and no code may reference other products by name.

## Communication and Coding Do's and Dont's
- Do not overdo abstractions when a simpler and more readable solution suffice
- Do not complicate when a simpler solution suffice
- Do investigate if there is a chance of reuse, modulisation or componisation
- Do be brief and to the point.
- Do use short sentences.
- Do make an effort to speak plainly and visualize an important point using short examples.
- Do explain using small code samples whenever possible.
- Do avoid long, convoluted explanations.
- Don't try to be nice or unpleasant - just be direct and state your findings, your opinions or the facts as-is.

## Design system — documents, and what each one carries

**Read `Docs/pitfalls.md` before touching anything under `src/RSSmote.App/Controls/` or restyling a view**
— it is the list of things that fail with no error.

Putting the right thing in the wrong document is how they rot. A finding written into a manual reads as a
description of how the control is meant to work; a defect written into a design doc never gets fixed.

| Document | Carries | Never carries |
|---|---|---|
| `Docs/architecture.md` | How the app is put together: projects, shell, overlays, storage, refresh, platform seams | — |
| `Docs/design-language.md` | Which shape to reach for and why: tokens, type roles, components, icons, layout metrics, copy | Defects |
| `Docs/pitfalls.md` | What fails silently, the fix, and the measurement that proves it | Anything unmeasured stated as fact |
| `Docs/todo.md` | What must be fixed, need-to-have / nice-to-have, and how to confirm each | Rules |
| `Docs/dependencies.md` | Every package, its version and licence | Anything commercial |
| `src/RSSmote.App/Controls/Templates/templates.html` | **A manual**: what each control is and does, in the present tense | Defects, measurements, what was tried |

So: a control that renders the wrong colour is a **todo** entry, not a note in the manual — but if the manual claims
the intended colour, correct the claim. The rule behind it is a **pitfall**. Why one control shape was chosen over
another is **design-language**.

**The four `Docs/` rule files carry one id per entry** — `L`/`P`/`U` for a pitfall by likelihood, `N`/`W` for a
todo, `A` for architecture, `D` for design-language. **Cite the id, never the heading**: a code comment, another doc
or a review says `see D12` or `(L3)` rather than repeating the rule. Ids are **append-only** — a new entry takes the
next free number in its group, and a retired one leaves a gap rather than renumbering the rest.

**Keep them current at the end of a session** — and keep them lean: reasoning and rules, never a changelog.

Token values live in `src/RSSmote.App/Assets/` and nowhere else — **colour** in `Assets/Themes/*.axaml`, one file per
palette, and everything else (type, spacing, radii, sizes) in `Assets/AppResources.axaml`. Every palette answers an
identical key set; see D4.

## Keep `Controls/Templates/templates.html` in sync — required

`src/RSSmote.App/Controls/Templates/templates.html` is the visual catalogue of every templated control: purpose,
property table, and an HTML/CSS mock of how it looks. It is hand-maintained, so it goes stale silently — nothing
fails to build when it drifts.

**Update it in the same change** whenever any of these happens under `Controls/Templates/`:

| Change | What to update in `templates.html` |
|---|---|
| A control is **added** | New `<article id="...">` in the right group, an entry in `nav.index`, and bump the “N controls” badge |
| A control is **deleted** | Remove its article **and** its index entry, and bump the badge |
| A control is **renamed** | Heading, `id`, index link and any cross-references in other articles |
| A **property** is added / removed / renamed | Its row in that control's property table |
| A **default value** changes | The `Type` column, which spells defaults out (`bool (True)`, `int (3)`, `double (320)`) |
| The **visual** changes materially | The mock in `.preview` — sizes, colours, structure |
| A **usage constraint** is discovered (something a call site must do or cannot do) | The `.note` block. The underlying rule and its measurement go to `Docs/pitfalls.md`, not here |
| A **token** changes in `AppResources.axaml` or `Themes/*.axaml` | The `:root` custom properties at the top of the file, which are a verbatim copy |

**The previews are HTML/CSS recreations, not screenshots**, drawn with the real token values. Keep that honest:
never describe them as screenshots, and if you cannot reproduce a visual faithfully, say so in the article rather
than drawing something plausible.

## Working rules

- Make changes **directly** in this repo — no worktrees. **The user runs the app and handles all git/commits**;
  do not run `git`/`gh` and do not launch the app.
- Verify with a from-scratch build (incremental hides AXAML errors), aiming for 0 warnings:
  ```
  dotnet clean RSSmote.slnx
  dotnet build RSSmote.slnx --no-incremental -clp:ErrorsOnly -v q
  dotnet test RSSmote.slnx
  ```
- Ask before inventing new tokens in `Assets/AppResources.axaml` or `Assets/Themes/*.axaml`.
- Every new package goes into `Directory.Packages.props` **and** `Docs/dependencies.md` with its licence.
  Never add a package that depends on `AvaloniaUI.Licensing` (commercial).
- A `.cs` file's namespace is its project name plus its folder path (`src/RSSmote.App/Controls/Headless/Icon.cs` →
  `RSSmote.App.Controls.Headless`). XAML reaches both control folders through `xmlns:c="urn:RSSmote.App.Controls"`
  (`Controls/XmlNamespace.cs`).
- The product is written **RSSmote**; lowercase `rssmote` only in technical identifiers (data folder
  `com.ahrentlov.rssmote`, `rssmote.sqlite`, `RSSMOTE_*` variables).

## Code comments: minimal, English, and no archaeology

A comment states **what the code does and what a reader must understand to change it safely** — in the present tense,
on the code's own terms. It never tells the story of how the code got here.

No: what was tried before, what it used to do, what the wrong version rendered, which alternative was rejected, that
something "cost a round-trip", or comparisons to code that no longer exists. A future reader has no idea what any of
that refers to, and the comment is longer and harder to trust for it.

| Don't | Do |
|---|---|
| `An OPEN Path, not a Rectangle — a Rectangle strokes its outline and rendered as a solid bar` | `One open segment, so the dash pattern runs along a single edge` |
| `A sub-minute total used to print "0 m"` | `Whole seconds below a minute` |
| `The earlier version sorted by insertion order` | *(nothing — this belongs in the docs)* |

Keep the **rule** and drop the **history**: "never trust a feed's dates — a missing pubDate becomes the fetch time"
is a comment. Why two other designs were dropped is not.

**Reasoning, alternatives and measurements belong in the docs**, not in the source — in whichever document carries
that kind of thing (see the table above). If a decision needs defending, defend it there and let the code state the
rule in a line. A comment may point at the doc; it may not reproduce it.

## Communication style
Be brief and to the point. Use short sentences. Make an effort to speak plainly and visualize an important point using short examples. Explain using small code samples whenever possible. Avoid long, convoluted explanations. Don't try to be nice or unpleasant - just be direct and state your findings, your opinions or the facts as-is.
