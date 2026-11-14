namespace Wreckabulary.Rules.Wordbound
{
    public sealed partial class AuthoritativeMatch
    {
        CommandEffect SetDefense(string playerId, PlayerCommand command)
        {
            if (command.Target != "" || command.Revision != 0 || command.Slot != -1 ||
                command.Value != "Hold" && command.Value != "Release") return Effect("InvalidPayload");
            if (command.Value == "Release")
            {
                actors[playerId] = Actor(actors[playerId], clearDefense: true);
                return Effect("Ok");
            }
            NormalizeDefense(playerId, Tick + 1);
            var item = DefenseItem(playerId, Tick + 1);
            if (item == null) return Effect("NoShield");
            WordboundDefensePolicy.RaiseTicks(catalogue.Get(item.Word).Shield);
            var defense = WordboundDefensePolicy.Renew(actors[playerId].Defense, "item:" + item.Id, Tick + 1);
            actors[playerId] = Actor(actors[playerId], defense: defense, clearAttack: true);
            return Effect("Ok", defense.ItemEntityId);
        }

        ItemInstance DefenseItem(string playerId, long tick)
        {
            var actor = actors[playerId];
            var player = progress.Player(playerId);
            int index = indices[playerId];
            var inventory = economy.Player(index);
            if (!player.Connected || player.Returned || player.Eliminated || progress.Migrating ||
                progress.Result != null || !inventory.CanAct || inventory.Craft != null ||
                inventory.CarriedFurniture >= 0 || actor.Health <= 0 || actor.StunnedUntilTick > tick ||
                actor.DodgeUntilTick > tick || actor.Attack != null ||
                Mode.IsRace && progress.Team(player.Team).Finished) return null;
            var item = actor.EquippedSlot < 0 ? null : Held(index, actor.EquippedSlot);
            return item == null || catalogue.Get(item.Word).Shield == null ? null : item;
        }

        void NormalizeDefense(string playerId, long tick)
        {
            var actor = actors[playerId];
            var defense = actor.Defense;
            if (defense == null) return;
            var item = DefenseItem(playerId, tick);
            if (defense.UntilTick <= tick || item == null || defense.ItemEntityId != "item:" + item.Id)
                actors[playerId] = Actor(actor, clearDefense: true);
        }

        WordboundDefenseHit ApplyDefense(string playerId, WorldBodyState victim, WorldVector? incomingFrom,
            float amount, long tick)
        {
            NormalizeDefense(playerId, tick);
            var actor = actors[playerId];
            var item = DefenseItem(playerId, tick);
            var stats = item == null ? null : catalogue.Get(item.Word).Shield;
            var hit = WordboundDefensePolicy.Resolve(actor.Defense, stats, victim, incomingFrom, amount, tick);
            if (!hit.Blocked) return hit;
            var tiles = economy.Damage(item.Id, hit.BlockedDamage);
            if (item.IsGone)
            {
                var body = bodies["item:" + item.Id];
                bodies[body.Id] = body.At(victim.Position, active: false, kinematic: true);
                AddTiles(tiles, victim.Position, body.OwnerTeam, body.SourceId, body.Protected, body.RecoveryGeneration);
                ScheduleSource(body);
                NormalizeEquipment(playerId, tick);
                NormalizeDefense(playerId, tick);
            }
            return hit;
        }
    }
}
