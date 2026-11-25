using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML;

namespace GameML
{
    // Learns which paddle action to take from the game state.
    internal class PaddleModel
    {
        private MLContext? _ml;
        private IEstimator<ITransformer>? _pipeline;
        private PredictionEngine<GameSample, ActionPrediction>? _engine;

        // Creates the ML context and the training pipeline.
        public void Init(int seed = 0)
        {
            _ml = new MLContext(seed);

            _pipeline = _ml.Transforms.Conversion.MapValueToKey("Label")
                .Append(_ml.Transforms.Concatenate("Features",
                    nameof(GameSample.BallX),
                    nameof(GameSample.BallY),
                    nameof(GameSample.VelocityX),
                    nameof(GameSample.VelocityY),
                    nameof(GameSample.BallToPaddle)))
                .Append(_ml.Transforms.NormalizeMinMax("Features"))
                .Append(_ml.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        }

        // Loads a saved model; returns false if the file does not exist.
        public bool Load(string path)
        {
            var ml = RequireInit();

            if (!File.Exists(path)) return false;

            ITransformer model = ml.Model.Load(path, out _);
            _engine = ml.Model.CreatePredictionEngine<GameSample, ActionPrediction>(model);
            return true;
        }

        // Trains on recorded samples, saves the model and makes it usable.
        public void Train(IEnumerable<GameSample> samples, string path)
        {
            var ml = RequireInit();

            IDataView data = ml.Data.LoadFromEnumerable(samples);
            ITransformer model = _pipeline!.Fit(data);

            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            ml.Model.Save(model, data.Schema, path);
            _engine = ml.Model.CreatePredictionEngine<GameSample, ActionPrediction>(model);
        }

        // Predicts the paddle action for the given state.
        public PaddleAction Predict(GameSample state)
        {
            if (_engine == null)
                throw new InvalidOperationException("Call Load or Train before Predict.");

            string label = _engine.Predict(state).PredictedLabel;
            return Enum.TryParse(label, out PaddleAction action) ? action : PaddleAction.None;
        }

        private MLContext RequireInit()
        {
            return _ml ?? throw new InvalidOperationException("Call Init first.");
        }
    }
}
