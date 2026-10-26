# Translation guidance

Translate fluent, concise RimWorld interface text and help for the target locale.
Keep established terminology from the current translation when it remains accurate.
Preserve mod names (WorkRoles, EPrime's Readouts, EPrime's Quality Jobs,
EPrime's Implanner), player-authored names, and technical identifiers.

Owner-reviewed terminology takes precedence:

The Danish terminology and Norwegian/European Portuguese regional notes below
are retained as historical guidance for an owner-approved reactivation. Those
locales are retired; the active tooling does not update their archived content.

- Prefer context-specific player-facing terms over literal translations of
  the English gaming term `pawn`. Use the locale's established term for
  colonist when the text concerns colonists; use worker, patient, character,
  or a natural sentence without the noun when the scope is broader.
- Preserve scope: WorkRoles also manages slaves; Quality Jobs' earlier work
  and shared unfinished items can involve other eligible workers; Readouts'
  carried-item exclusions are not limited to colonists. Implanner's existing
  patient count includes colonists and slaves, as its tooltip explains.
- Do not force chess-piece terms such as Danish `bonde` or German `Bauer`
  for game characters. Keep actual farming/chess terminology and technical
  identifiers unchanged. In short status messages, prefer omitting a repeated
  subject over adding a longer generic character label.
- Finisher skill label: Danish `Færdiggørelseskrav: {0}`;
  German `Fertigstellen ab Stufe {0}`.
- Distinguish implants held back for manual use from implants allocated by automation.
- Japanese: use the game's `思想` for ideoligion, `戒律` for a Precept,
  and `詳細な設定` when quoting the manual-priorities control. Preserve `Ideology`
  when it names the DLC. Keep `入植者`, `グラヴシップ`, `情熱` and `熱心な情熱`
  for their corresponding game concepts.
- Japanese: feeding people is `食事の提供` (prisoners) or `食事の介助`
  (patients), not animal-feeding `給餌`. Keep `給餌` for animals. Use natural
  counters for numeric job, bill and item counts without dropping qualifiers.

Regional locales: Norwegian means Bokmål; Portuguese means European Portuguese;
PortugueseBrazilian means Brazilian Portuguese; Spanish means European Spanish;
SpanishLatin means Latin American Spanish. ChineseSimplified and ChineseTraditional
must use their respective scripts.

Use the target locale's verified in-game names when referring to specific
controls, inspirations, skills, quality tiers or implants. Popular mod
translations provide terminology evidence, but may themselves contain errors
or untranslated text. Do not copy those errors or mix regional variants.
An AI-generated community pack has no presumed correctness advantage; compare
its wording with current English behavior and the game's native terminology.
Keep short buttons, table headers and checkbox labels concise; use tooltips
and help for explanations. Preserve useful abbreviations such as `Config.`.

Context checks established during the translation review:

- `WR_OrderSuffixBar` refers to colonist-bar order, not interface tabs.
- Role and resource search examples must match translated display names.
  Keep parser prefixes such as `r:` unchanged. An English screenshot may be
  identified as an English example; its filename must stay unchanged.
- `WR_TipActivityValue` combines a name with either a native job report or a
  state label. Use a neutral separator when a fixed linking verb would produce
  an ungrammatical sentence.
- `EPR.SeedRaw` names the raw-food group (meat, plant food, eggs and milk),
  not industrial raw materials.
- In `IMP_OptIterationTip` and the overview help, ASAP allocation compares
  colonist priority first. For the same implant at equal priority, legs go to
  the slowest colonist; arms prefer an equipped melee weapon, then the highest
  combined Crafting and Intellectual skill. The displayed Shooting/Melee
  skill values and belt are informational; the equipped weapon is not.
- The ordering help must explicitly say to turn off "Only the first 5 roles
  interrupt for emergencies" to remove the five-role limit.
- WorkRoles recommendation ordering uses coverage to mean which roles include
  another role's jobs; this is different from staffing demand or needed headcount.
- The priority-grid dialog is read-only. It does not have the main WorkRoles
  window's corner resize grip or double-click size reset.
- Readouts rendering help describes detected display failures, direct drawing
  and the logged fallback to the game's own readout. Do not promise automatic
  graphics-resource recovery after sleep; that behavior is not established.
- Distinguish downed pawns from pawns unable to fight or do a particular job;
  distinguish butchering dead creatures from slaughtering live animals.
- Keep native item-quality names distinct from custom suitability verdicts.
  Legitimate game terms for slavery, drugs, nudity or anatomy are not profanity
  to remove from an accurate translation.

Preserve format placeholders exactly, including multiplicity, for example `{0}`,
`{1:N0}` and named placeholders. Preserve literal `\n` paragraph escapes and
rich-text tags, including their attributes. Preserve balanced markup.

For Markdown help, translate frontmatter titles, prose and visible link/image
labels. Keep all link/image destinations, code spans, filenames and `@demo`
directives exactly as supplied, in the same order. Retain the frontmatter structure.
Return complete help pages, including frontmatter, rather than excerpts.

Identical English text is legitimate for brands, identifiers and some cognates.
Do not invent a different translation merely to avoid exact equality.
