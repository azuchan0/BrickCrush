using System.IO;

namespace GameCore
{
    // Options passed to the game at creation.
    public class GameOptions
    {
        // Record the player's moves and train a model at the end of the game.
        public bool TrainingEnabled { get; set; }

        // Let the trained model control the paddle.
        public bool UseVirtualPlayer { get; set; }

        // Where the model is saved and loaded, when no stream is given.
        public string ModelPath { get; set; } = "models/paddle.zip";

        // Optional: read the model from this seekable stream instead of ModelPath
        // (e.g. a Godot or Unity resource). The caller owns and disposes it.
        public Stream? ModelInput { get; set; }

        // Optional: write the trained model to this stream instead of ModelPath.
        // The caller owns and disposes it.
        public Stream? ModelOutput { get; set; }
    }
}
