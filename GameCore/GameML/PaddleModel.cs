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
            if (!File.Exists(path))
                return false;

            using (var stream = File.OpenRead(path))
                Load(stream);

            return true;
        }

        // Loads a model from any seekable stream (engine resources, memory...).
        public void Load(Stream stream)
        {
            var ml = RequireInit();

            ITransformer model = ml.Model.Load(stream, out _);
            _engine = ml.Model.CreatePredictionEngine<GameSample, ActionPrediction>(model);
        }

        // Trains on recorded samples, saves the model to a file and makes it usable.
        public void Train(IEnumerable<GameSample> samples, string path)
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            using (var stream = File.Create(path))
                Train(samples, stream);
        }

        // Trains on recorded samples, writes the model to a stream and makes it usable.
        public void Train(IEnumerable<GameSample> samples, Stream output)
        {
            var ml = RequireInit();

            IDataView data = ml.Data.LoadFromEnumerable(samples);
            ITransformer model = _pipeline!.Fit(data);

            ml.Model.Save(model, data.Schema, output);
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
