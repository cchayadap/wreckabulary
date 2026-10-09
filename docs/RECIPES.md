# Craftable items

Collect letters, spell an item, then use the miniature in your hand. The recipe screen shows the required letters and current controls. Two carried slots let you keep a tool beside a consumable; deployed tools use the separate two-item limit.

| Item | Use |
| --- | --- |
| APPLE | Eat to restore 30 HP |
| WATER | Quick drink restoring 18 HP |
| CAKE | Eat to restore 50 HP; takes longer than water |
| SODA | Drink for 1.35× movement speed for six seconds |
| SHIELD | Hold block for protection from every direction; attacks wear down its durability |
| FOAM | Temporary bubble protection |
| FAN | Place a directional wind field that pushes players and loose objects |
| CLOCK | Place a circular slowing field |
| MAT | Place a speed floor; follow the moving arrows |
| BED / STOOL | Place a large / compact jump pad |
| SOAP | Leave a slippery patch |
| BAT | Balanced close-range swing |
| BLADE | Fast melee weapon |
| BROOM | Wide, long sweep with strong knockback |
| HAMMER | Slow heavy smash, especially useful against props |
| SPEAR | Long, narrow thrust |
| LAMP | Household melee tool |
| BALL | Recoverable thrown ball |
| PIE | Single-use thrown pie |
| BOMB | Timed explosive |
| PLATE | Raised directional shield |
| TABLE / SOFA | Place cover |

Healing stops at maximum HP. Food and drinks are spent only after the use channel completes; interrupting the channel preserves the item. Used consumables do not return their letters. Wind and slowing fields affect everyone within their area, respect walls and storeys, and stop when picked up or expired. SODA speed and CLOCK slowing are separate effects.

Balance and recipe enablement: `Assets/_Project/Data/Config/items.json`. Generated item illustrations: `Assets/_Project/Art/Generated/Items`. Saved runtime art references: `Assets/_Project/Resources/UI/Generated/PowerItemArt.asset`. The browser shares the same catalogue.
