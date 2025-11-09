using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.StartingLocationSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Editor
{
    public record StartingLocationPlayerSpec : ComponentSpec
    {
        public int PlayerIndex { get; init; }
    }

    public class StartingLocationPlayer : BaseComponent, IAwakableComponent, IRegisteredComponent, IPersistentEntity
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

        private StartingLocationPlayerSpec _startingLocationPlayerSpec;

        public int PlayerIndex { get; set; }

        public void Awake()
        {
            if (TryGetComponent<StartingLocationPlayerSpec>(out _startingLocationPlayerSpec))
            {
                // Copy PlayerIndex from spec to instance, because it might be changed for this instance 
                // later on and Specs are shared between multiple entities.
                PlayerIndex = _startingLocationPlayerSpec.PlayerIndex;
            }
        }

        public void Start()
        {
            Plugin.Log($"Start index initialized with PlayerIndex: {PlayerIndex}");
            StartingLocationRenderer renderer = GetComponent<StartingLocationRenderer>();
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
            if (entityLoader.TryGetComponent(StartingLocationPlayerKey, out IObjectLoader objectLoader))
            {
                int index = 0;
                if (objectLoader.Has(PlayerIndexKey))
                {
                    index = objectLoader.Get(PlayerIndexKey);
                }
                else
                {
                    Plugin.LogError("PlayerIndex is null; setting to 0");
                }
                PlayerIndex = index;
            }
        }
    }
}
