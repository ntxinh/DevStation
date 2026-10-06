# DESIGN.md — DevStation visual language

Dark-only developer console. Dense, keyboard-first, information over
decoration. When in doubt: GitHub-dark palette, monospace where the data is
machine-shaped, text where it's human-shaped.

## Tokens (Tailwind v4 `@theme` in `apps/web/app/globals.css`)

| Token | Hex | Use |
|---|---|---|
| `bg` | `#0d1117` | page background |
| `panel` | `#161b22` | cards, tables' hover, sidebar/topbar surfaces |
| `border` | `#30363d` | all borders (`border-border/50` for inner row separators) |
| `fg` | `#e6edf3` | primary text |
| `muted` | `#8b949e` | secondary text, labels, placeholders |
| `accent` | `#58a6ff` | links, focus rings, primary actions (button bg with `text-black`) |
| `ok` | `#3fb950` | healthy/succeeded/running/connected |
| `warn` | `#d29922` | degraded/queued/paused/dirty/dangerous-but-allowed |
| `err` | `#f85149` | failed/unhealthy/errors, destructive emphasis |

Never introduce a new color; extend this table instead.

## Typography

- UI text: default sans (Tailwind `sans`), `text-xs` is the working size;
  `text-sm` for headings inside cards; `text-lg font-semibold` only for page
  `<h1>`s.
- Machine data — SHAs, ports, conn strings, SQL, timestamps, log lines —
  `font-mono` + `text-xs`.
- Muted labels precede values: `text-muted` span next to `text-fg` value;
  spec: `grid-cols-[6rem_1fr]` for label/value pairs in cards.

## Spacing & density

- Page padding `p-4`; card padding `p-3` (dense) or `p-4` (stat cards).
- Inter-section gap `gap-3`/`gap-4`; table row padding `py-1.5`.
- Shell: fixed left nav + `h-12` topbar + scrollable `<main>`.

## Status semantics

`StatusBadge` is the only status renderer: colored dot + lowercase text —
**never color alone** (a11y). Mapping:

- ok-family: `healthy ok running succeeded connected` → `ok`
- warn-family: `queued warn degraded paused created configured` → `warn`
- err-family: `unhealthy err error failing failed dead exited` → `err`
- neutral: `never-run notconfigured disabled cancelled unknown` → `muted`

New statuses must extend the badge's `DOT` map — no ad-hoc colored text.

## Component rules

- Buttons: primary = `bg-accent text-black text-xs rounded px-3 py-1.5`;
  secondary = `border border-border text-muted hover:text-fg`; destructive =
  `text-err`/`bg-err`.
- Inputs: `rounded border border-border bg-bg px-2 py-1.5 text-xs
  focus:border-accent outline-none` (the `INPUT` const in forms).
- Cards: `rounded-lg border border-border bg-panel`.
- Modals: `fixed inset-0 z-50 bg-black/60` backdrop + centered `bg-panel`
  `role="dialog" aria-modal` panel; Escape/backdrop click closes.
- Every list view: loading indicator, `EmptyState`, `ErrorState` — all three.
- Tables: `text-xs`, `border-b border-border` headers, `border-border/50`
  row separators, `hover:bg-panel`.

## Accessibility

- Semantic HTML: `nav`, `header`, `main`, `h1` per page, real `button`s,
  labeled inputs (`<label>` wraps or `aria-label` on icon-only controls).
- Visible focus via `focus:border-accent`; keyboard: `⌘K`/`Ctrl+K` palette,
  `↑↓/Enter` inside it, `Esc` closes overlays, `Ctrl+Enter` runs SQL.
- Icon-only buttons get `aria-label`; decorative icons get `aria-hidden`.
