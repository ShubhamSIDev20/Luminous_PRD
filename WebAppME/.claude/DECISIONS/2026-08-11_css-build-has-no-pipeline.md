# ADR-2: `wwwroot/css/app.min.css` has no Tailwind rebuild pipeline — treat it as frozen

**Date:** 2026-08-11
**Session:** #5
**Status:** Documented constraint (not a decision to change, a fact to design around)

## Context
While fixing a hover-popover rendering bug reported by the user, direct inspection of
`wwwroot/css/app.min.css` showed that several Tailwind utility classes introduced in the
previous session (`border-t-8`, `border-x-8`, `border-x-transparent`, `w-0`, `h-0`,
`border-t-primary/40`, `border-b-primary/40`) simply do not exist in the compiled CSS. There
is no `package.json` anywhere in the repository, and no build target wires a Tailwind CLI run
into `dotnet build`/`dotnet run`. `tailwind.config.js` exists but nothing invokes it.

## Implication
`app.min.css` (and its unminified companion `app.css`) is a **static, hand-maintained
artifact**. Any Tailwind utility class referenced only from `.razor` markup that isn't
already present in that file will render with no styling at all — silently, with no build
error, since Razor doesn't know or care about Tailwind classes.

## Rule going forward
Before using a new Tailwind utility class (especially arbitrary-value ones like `w-64`,
`bg-status-idle/20`, `border-primary/40`), grep `wwwroot/css/app.min.css` for it first. If
it's not present:
- Prefer an inline `style="..."` attribute (always works, no build step needed), or
- Ask the user whether `app.min.css` should be regenerated/hand-patched, since there's no
  automated way to do it from this environment.

This is why the Task 5 hover-popover arrow (in `DeviceChannel.razor`) silently failed to
render after being merged — see
[[2026-08-11_dashboard-ux-feature-and-followup-fixes]] task file for the fix (inline-style
triangle instead of Tailwind border-width utility classes).
