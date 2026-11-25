using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Text;
using GameML;

namespace GameCore
{
    public class BrickBreakerGame
    {
        public int Width { get; set; } = 40;
        public int Height { get; set; } = 20;

        public int PaddleWidth { get; set; } = 7;
        public int PaddleX { get; set; }
        public int PaddleY { get; set; }
        
        
        // Exact ball position; cell (x, y) covers [x, x+1) x [y, y+1).
        // BallX / BallY give the occupied cell, for grid rendering.
        public double BallExactX { get; private set; }
        public double BallExactY { get; private set; }
        public int BallX => (int)Math.Floor(BallExactX);
        public int BallY => (int)Math.Floor(BallExactY);

        // Distance travelled per MoveBall call, in cells.
        public double BallSpeed { get; } = 1.0;

        // Max bounce angle from vertical, reached at the paddle edges.
        private const double MaxBounceAngle = Math.PI / 3;
        // Sub-step size; below one cell so no cell is ever skipped.
        private const double MaxSubStep = 0.25;

        private double _velocityX;
        private double _velocityY;

        public int Score { get; set; }
        public int Lives { get; set; } = 3;
        public bool IsGameOver { get; private set; }
        private readonly bool[,] _bricks;
        public int RemainingBricks { get; private set; }
        public bool IsWon { get; private set; }

        private readonly GameOptions _options;
        private readonly PaddleModel? _model;
        private readonly List<GameSample> _samples = new List<GameSample>();
        private bool _leftPressed;
        private bool _rightPressed;

        // True once a model has been trained and saved at the end of the game.
        public bool ModelTrained { get; private set; }

        public BrickBreakerGame() : this(new GameOptions()) { }

        public BrickBreakerGame(GameOptions options)
        {
            _options = options;

            if (options.TrainingEnabled && options.UseVirtualPlayer)
                throw new ArgumentException("Choose either training or virtual player, not both.");

            if (options.TrainingEnabled || options.UseVirtualPlayer)
            {
                _model = new PaddleModel();
                _model.Init();
            }

            if (options.UseVirtualPlayer && !_model!.Load(options.ModelPath))
                throw new FileNotFoundException($"No model at {options.ModelPath}. Play with --train first.");

            _bricks = new bool[Width, Height];

            // Four rows of bricks, leaving a gap along the side walls.
            for (int y = 2; y < 6; y++)
            {
                for (int x = 2; x < Width - 2; x++)
                {
                    _bricks[x, y] = true;
                    RemainingBricks++;
                }
            }

            PaddleY = Height - 2;
            PaddleX = (Width - PaddleWidth) / 2;

            ResetBall();
        }


        // Human input. Ignored when the AI plays; remembered for training labels.
        public void MovePaddle(bool left, bool right)
        {
            if (_options.UseVirtualPlayer)
                return;

            _leftPressed |= left;
            _rightPressed |= right;
            ShiftPaddle(left, right);
        }

        // Moves the paddle one cell, kept inside the walls.
        private void ShiftPaddle(bool left, bool right)
        {
            if (left && !right)
            {
                PaddleX--;
            }
            else if (right && !left)
            {
                PaddleX++;
            }

            int rightLimit = Width - PaddleWidth - 1;

            if (PaddleX < 1)
                PaddleX = 1;

            if (PaddleX > rightLimit)
                PaddleX = rightLimit;
        }

        public bool IsBrickAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;

            return _bricks[x, y];
        }

        // One ball step. Also where the AI plays and training samples are taken.
        public void MoveBall()
        {
            if (IsGameOver || IsWon)
                return;

            if (_options.UseVirtualPlayer)
                PlayVirtualPlayer();

            if (_options.TrainingEnabled)
                RecordSample();

            StepBall();

            if ((IsGameOver || IsWon) && _options.TrainingEnabled)
                TrainModel();
        }

        // Moves the ball in small steps, X then Y. A blocked axis bounces
        // without moving, so the ball always stays in a free cell.
        private void StepBall()
        {
            int steps = Math.Max(1, (int)Math.Ceiling(BallSpeed / MaxSubStep));
            double fraction = 1.0 / steps;

            for (int i = 0; i < steps; i++)
            {
                MoveBallHorizontally(_velocityX * fraction);

                if (IsWon)
                    return;

                if (!MoveBallVertically(_velocityY * fraction))
                    return;
            }
        }

        private void MoveBallHorizontally(double deltaX)
        {
            double nextX = BallExactX + deltaX;
            int cellX = (int)Math.Floor(nextX);

            if (cellX <= 0 || cellX >= Width - 1 || IsPaddleAt(cellX, BallY))
            {
                _velocityX = -_velocityX;
                return;
            }

            if (_bricks[cellX, BallY])
            {
                DestroyBrick(cellX, BallY);
                _velocityX = -_velocityX;
                return;
            }

            BallExactX = nextX;
        }

        // Returns false when movement must stop (ball lost or game won).
        private bool MoveBallVertically(double deltaY)
        {
            double nextY = BallExactY + deltaY;
            int cellY = (int)Math.Floor(nextY);

            if (cellY <= 0)
            {
                _velocityY = -_velocityY;
                return true;
            }

            if (_bricks[BallX, cellY])
            {
                DestroyBrick(BallX, cellY);
                _velocityY = -_velocityY;
                return !IsWon;
            }

            if (_velocityY > 0 && IsPaddleAt(BallX, cellY))
            {
                BounceOnPaddle();
                return true;
            }

            if (cellY >= Height - 1)
            {
                LoseLife();
                return false;
            }

            BallExactY = nextY;
            return true;
        }

        // Straight up at the center, steeper angle towards the edges.
        private void BounceOnPaddle()
        {
            double halfWidth = PaddleWidth / 2.0;
            double paddleCenter = PaddleX + halfWidth;
            double impact = (BallExactX - paddleCenter) / halfWidth;
            impact = Math.Max(-1.0, Math.Min(1.0, impact));

            SetBallDirection(impact * MaxBounceAngle);
        }

        // angle: 0 = straight up, positive = to the right.
        private void SetBallDirection(double angle)
        {
            _velocityX = BallSpeed * Math.Sin(angle);
            _velocityY = -BallSpeed * Math.Cos(angle);
        }

        private bool IsPaddleAt(int x, int y)
        {
            return y == PaddleY && x >= PaddleX && x < PaddleX + PaddleWidth;
        }

        private void DestroyBrick(int x, int y)
        {
            _bricks[x, y] = false;
            RemainingBricks--;
            Score += 10;

            if (RemainingBricks == 0)
                IsWon = true;
        }

        private void LoseLife()
        {
            Lives--;

            if (Lives == 0)
            {
                IsGameOver = true;
                return;
            }

            ResetBall();
        }

        private void ResetBall()
        {
            BallExactX = Width / 2 + 0.5;
            BallExactY = PaddleY - 1 + 0.5;
            SetBallDirection(Math.PI / 4);
        }

        // Plays games without display with a built-in teacher, then trains once.
        public static void TrainBySelfPlay(int games, string modelPath, int seed = 0)
        {
            var rng = new Random(seed);
            var samples = new List<GameSample>();

            for (int g = 0; g < games; g++)
            {
                var game = new BrickBreakerGame();

                for (int t = 0; t < 20000 && !game.IsWon && !game.IsGameOver; t++)
                {
                    PaddleAction action = game.TeacherAction();

                    // Keep 1 step in 5: consecutive steps are almost identical.
                    if (t % 5 == 0)
                        samples.Add(game.CreateSample(action.ToString()));

                    // Sometimes play a random move so games differ and the model
                    // also sees imperfect situations. The label stays the teacher's.
                    if (rng.NextDouble() < 0.1)
                        action = (PaddleAction)rng.Next(3);

                    game.ShiftPaddle(action == PaddleAction.MoveLeft, action == PaddleAction.MoveRight);
                    game.StepBall();
                }
            }

            var model = new PaddleModel();
            model.Init(seed);
            model.Train(samples, modelPath);
        }

        // Simple expert: keep the paddle center under the ball.
        private PaddleAction TeacherAction()
        {
            double offset = BallExactX - (PaddleX + PaddleWidth / 2.0);

            if (offset < -1)
                return PaddleAction.MoveLeft;
            if (offset > 1)
                return PaddleAction.MoveRight;
            return PaddleAction.None;
        }

        // Current state as a model input; label is empty when predicting.
        private GameSample CreateSample(string label)
        {
            return new GameSample
            {
                BallX = (float)BallExactX,
                BallY = (float)BallExactY,
                VelocityX = (float)_velocityX,
                VelocityY = (float)_velocityY,
                BallToPaddle = (float)(BallExactX - (PaddleX + PaddleWidth / 2.0)),
                Label = label,
            };
        }

        private void PlayVirtualPlayer()
        {
            PaddleAction action = _model!.Predict(CreateSample(""));
            ShiftPaddle(action == PaddleAction.MoveLeft, action == PaddleAction.MoveRight);
        }

        // Records the state with the keys pressed since the previous ball step.
        private void RecordSample()
        {
            PaddleAction action = PaddleAction.None;
            if (_leftPressed && !_rightPressed)
                action = PaddleAction.MoveLeft;
            else if (_rightPressed && !_leftPressed)
                action = PaddleAction.MoveRight;

            _samples.Add(CreateSample(action.ToString()));
            _leftPressed = false;
            _rightPressed = false;
        }

        private void TrainModel()
        {
            // Training needs at least two different actions.
            if (_samples.Select(s => s.Label).Distinct().Count() < 2)
                return;

            _model!.Train(_samples, _options.ModelPath);
            ModelTrained = true;
        }

        // One full game tick: paddle then ball.
        public void Update(bool moveLeft, bool moveRight)
        {
            if (IsGameOver || IsWon)
                return;

            MovePaddle(moveLeft, moveRight);
            MoveBall();
        }
    }
}
