namespace Echoes
{
    /// <summary>
    /// Where the minimap's objective marker points. A pure function of the GameSession, written to match what QuestLog and the
    /// exploration objective tell the player in the objective panel, so the map and the text never disagree.
    /// </summary>
    public static class MinimapTargets
    {
        public enum Kind { None, Mira, Spring, Heart, Workshop, Wren, Hen, Alder, Herb, Flower, Ore }

        public static Kind Current(GameSession session)
        {
            if (session == null) return Kind.None;
            if (!session.ExplorationDone)
            {
                switch ((ObjectiveStep)session.ExplorationStep)
                {
                    case ObjectiveStep.MeetVillager: return Kind.Mira;
                    case ObjectiveStep.InspectSpring: return Kind.Spring;
                    default: return Kind.Heart;
                }
            }
            Inventory inventory = session.Inventory;
            switch (session.Resonance)
            {
                case ResonanceStage.SilentValley:
                    if (session.StreamQuest == StreamQuestStage.NotStarted) return Kind.Mira;
                    if (inventory.Has(ItemIds.SpringTonic, 1)) return Kind.Spring;
                    int herbs = inventory.Count(ItemIds.Herb), flowers = inventory.Count(ItemIds.Flower);
                    if (herbs >= 2 && flowers >= 1) return Kind.Workshop;
                    return herbs < 2 ? Kind.Herb : Kind.Flower;

                case ResonanceStage.AwakeningStream:
                    if (session.GardenQuest == QuestStage.NotStarted || inventory.Has(ItemIds.Egg, 1)) return Kind.Wren;
                    if (session.Chicken != ChickenPhase.Hungry || inventory.Has(ItemIds.ChickenFeed, 1)) return Kind.Hen;
                    if (inventory.Has(ItemIds.Flower, 1)) return Kind.Workshop;
                    return Kind.Flower;

                case ResonanceStage.BloomingGarden:
                    if (session.WindmillQuest == QuestStage.NotStarted || inventory.Has(ItemIds.WindmillBraces, 1)) return Kind.Alder;
                    if (inventory.Count(ItemIds.Ore) >= QuestLog.BracesOre) return Kind.Workshop;
                    return Kind.Ore;

                default:
                    return session.EndingSeen ? Kind.None : Kind.Mira;
            }
        }

        /// <summary>The item a gathering target collects (null for people and places).</summary>
        public static string ItemFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Herb: return ItemIds.Herb;
                case Kind.Flower: return ItemIds.Flower;
                case Kind.Ore: return ItemIds.Ore;
                default: return null;
            }
        }
    }
}
