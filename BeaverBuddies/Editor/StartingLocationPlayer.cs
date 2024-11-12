using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.StartingLocationSystem;
using UnityEngine;

namespace BeaverBuddies.Editor
{
    public class StartingLocationPlayer : BaseComponent, IRegisteredComponent, IPersistentEntity
    {
        public static readonly Color[] PLAYER_COLORS =
        {
            new Color(179f / 255f, 153f / 255f, 144f / 255f), // Lightened #770018
            new Color(153f / 255f, 179f / 255f, 171f / 255f), // Lightened #00775f
            new Color(171f / 255f, 153f / 255f, 179f / 255f), // Lightened #5f0077
            new Color(245f / 255f, 189f / 255f, 2f / 255f)    // Original #f5bd02
        };

        public const int MAX_PLAYERS = 4;
        public const int DEFAULT_MAX_STARTING_LOCS = 2;

        public static readonly ComponentKey StartingLocationPlayerKey = new ComponentKey("StartingLocationPlayer");
        public static readonly PropertyKey<int> PlayerIndexKey = new PropertyKey<int>("PlayerIndex");

        public int PlayerIndex;

        public void Start()
        {
            Plugin.Log($"Start index initialized with PlayerIndex: {PlayerIndex}");
            StartingLocationRenderer renderer = GetComponentFast<StartingLocationRenderer>();
            if (renderer != null )
            {
                renderer._renderers.ForEach(r =>
                {
                    if (r.material != null )
                    {
                        r.material.color = PLAYER_COLORS[PlayerIndex];
                    }
                });
            }
        }

        public void Save(IEntitySaver entitySaver)
        {
            entitySaver.GetComponent(StartingLocationPlayerKey).Set(PlayerIndexKey, PlayerIndex);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (entityLoader.HasComponent(StartingLocationPlayerKey))
            {
                int? index = entityLoader.GetComponent(StartingLocationPlayerKey).GetValueOrNullable(PlayerIndexKey);
                if (index == null)
                {
                    Plugin.LogError("PlayerIndex is null; setting to 0");
                }
                PlayerIndex = index ?? 0;
                return;
            }
        }
    }
}
