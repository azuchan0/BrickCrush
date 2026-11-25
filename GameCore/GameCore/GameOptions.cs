namespace GameCore
{
    // Options passed to the game at creation.
    public class GameOptions
    {
        // Record the player's moves and train a model at the end of the game.
        public bool TrainingEnabled { get; set; }

        // Let the trained model control the paddle.
        public bool UseVirtualPlayer { get; set; }

        // Where the model is saved and loaded.
        public string ModelPath { get; set; } = "models/paddle.zip";
    }
}
