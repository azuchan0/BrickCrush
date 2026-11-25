using Microsoft.ML.Data;

namespace GameML
{
    // Paddle decision; labels must use these exact names (ToString()).
    internal enum PaddleAction
    {
        None,
        MoveLeft,
        MoveRight,
    }

    // One game state + the action taken in that state.
    internal class GameSample
    {
        public float BallX { get; set; }
        public float BallY { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        // Ball X relative to the paddle center.
        public float BallToPaddle { get; set; }
        // PaddleAction name; empty when predicting.
        public string Label { get; set; } = "";
    }

    // Model output, mapped back to a PaddleAction name.
    internal class ActionPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = "";
    }
}
