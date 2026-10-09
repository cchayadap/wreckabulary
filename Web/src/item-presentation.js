export function recipeDescription(item) {
  if (item.use?.effect === "Heal")
    return `Restore ${item.use.amount} HP · ${item.use.channel ?? 0}s to use`;
  if (item.use?.effect === "Speed")
    return `${item.use.amount}× speed · ${item.use.seconds} seconds`;
  if (item.shield?.frontArc >= 360) return "All-around block · hold LMB / RMB";
  if (item.deploy?.effect === "WindField") return "Forward gust · pushes everyone";
  if (item.deploy?.effect === "SlowField") return "Slowing field · affects everyone";
  if (item.thrown?.fuse) return "Delayed blast · clear the room";
  if (item.consumable && item.thrown) return "One throw · one splat";
  return {
    MeleeSwing: "A satisfying swing",
    MeleeThrust: "A little extra reach",
    Thrown: "Catch. Throw. Repeat.",
    Buff: "A protective bubble",
    Shield: "Frontal block · hold LMB / RMB",
    DeployPad: "A bouncy jump pad",
    DeploySpeed: "A speedy little shortcut",
    DeployZone: "A slippery surprise",
    DeployCover: "Make your own cover",
  }[item.family] ?? "Handy around the house";
}

export function itemActionLabel(item, placesObjective = false) {
  if (item?.origin === "map") return "THROW";
  if (item?.definition?.shield) return "BLOCK";
  if (item?.definition?.use) return "USE";
  if (item?.definition?.thrown) return "THROW";
  if (item?.definition?.deploy || placesObjective) return "PLACE";
  return "SMASH";
}
