```text
Inventory screen
└── Shell (centered, max 1600)
    ├── Header
    │   ├── Title
    │   ├── Capacity
    │   └── Close
    └── Body ──────────────────────────────────────────────┐
        ├── Items (flex-grow: 1)          ├── Stats (right)
        │   ├── Filters                  │   ├── Item name
        │   └── Vertical scroll          │   ├── Preview
        │       └── Wrapping grid        │   ├── Type
        │           ┌────┬────┬────┬────┐│   ├── Damage
        │           │ 01 │ 02 │ 03 │ 04 ││   ├── Durability
        │           ├────┼────┼────┼────┤│   ├── Letters
        │           │ 05 │ 06 │ 07 │ 08 ││   ├── Flexible space
        │           ├────┼────┼────┼────┤│   ├── Slot 1
        │           │ 09 │ 10 │ 11 │ 12 ││   └── Slot 2
        │           └────┴────┴────┴────┘│
        └───────────────────────────────┴─────────────────┘
```

- Primary Color: `#1E1E24` (Dark Charcoal).
- Accent Color: `#D1A153` (Gold).
- Layout Type: Flexbox grid, wrapping rows, four columns maximum. Each card occupies 23% plus two 1% margins; four cards occupy 100% and a fifth wraps.
- Responsive: scale the PanelSettings with a 1920 × 1080 reference resolution, Match Width Or Height, and Match set to 1 (height). A centered 94% shell caps at 1600 logical pixels. The item area grows, and the stats panel stays right-aligned at 28%, bounded to 250–400 logical pixels. The same hierarchy fits 16:9 and 21:9 without adding a fifth column. Stat rows never shrink into their labels; the item list scrolls when vertical space is constrained.
- Item list scrolls vertically; its parent uses `min-height: 0` so overflow stays inside the shell.
- Gold marks selection and primary actions; teal marks keyboard focus. Dark surfaces, cream labels, 48-pixel actions and visible borders support legibility.
- UXML/USS provide presentation only. Catalogue illustrations reference existing item resources. Stat dashes are placeholders; buttons have stable names for later application binding.

Assets: `Assets/_Project/Resources/UI/Inventory/Inventory.uxml` and `Inventory.uss`.
