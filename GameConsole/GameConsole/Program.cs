using GameCore;

// Command line: --train (record and train), --ai (virtual player),
// --selfplay <games> (AI trains alone), --model <path>.
var options = new GameOptions
{
    TrainingEnabled = args.Contains("--train"),
    UseVirtualPlayer = args.Contains("--ai"),
};

int modelIndex = Array.IndexOf(args, "--model");
if (modelIndex >= 0 && modelIndex + 1 < args.Length)
    options.ModelPath = args[modelIndex + 1];

// --selfplay N: the AI trains alone on N games, then the program exits.
int selfPlayIndex = Array.IndexOf(args, "--selfplay");
if (selfPlayIndex >= 0)
{
    int games = 100;
    if (selfPlayIndex + 1 < args.Length && int.TryParse(args[selfPlayIndex + 1], out int n))
        games = n;

    Console.WriteLine($"Self-training on {games} games...");
    BrickBreakerGame.TrainBySelfPlay(games, options.ModelPath);
    Console.WriteLine($"Model saved: {options.ModelPath}");
    return;
}

BrickBreakerGame game;
try
{
    game = new BrickBreakerGame(options);
}
catch (Exception e) when (e is ArgumentException || e is FileNotFoundException)
{
    Console.WriteLine(e.Message);
    return;
}

Console.WriteLine($"Ball: ({game.BallX}, {game.BallY})");
Console.WriteLine($"Bricks left: {game.RemainingBricks}");

// The core handles all timing; the console only measures frame time.
var clock = System.Diagnostics.Stopwatch.StartNew();
double lastFrame = 0;

// The console only reports key presses (no key release), so a key counts
// as held for a short time after each press or auto-repeat.
const double KeyHoldSeconds = 0.15;
double leftHeldUntil = 0;
double rightHeldUntil = 0;

Console.CursorVisible = false;
Console.Clear();

while (!game.IsGameOver && !game.IsWon)
{
    double now = clock.Elapsed.TotalSeconds;
    double deltaTime = now - lastFrame;
    lastFrame = now;

    // Drain all pending key presses for this frame.
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true).Key;

        if (key == ConsoleKey.LeftArrow)
        {
            leftHeldUntil = now + KeyHoldSeconds;
            rightHeldUntil = 0;
        }
        else if (key == ConsoleKey.RightArrow)
        {
            rightHeldUntil = now + KeyHoldSeconds;
            leftHeldUntil = 0;
        }
    }

    game.Update(deltaTime, now < leftHeldUntil, now < rightHeldUntil);

    // Redraw from the top-left corner instead of clearing, to limit flicker.
    Console.SetCursorPosition(0, 0);
    Console.WriteLine($"Score: {game.Score} | Lives: {game.Lives}");

    for (int y = 0; y < game.Height; y++)
    {
        for (int x = 0; x < game.Width; x++)
        {
            char cell = ' ';

            // Draw priority: wall, ball, paddle, brick.
            if (x == 0 || x == game.Width - 1 || y == 0 || y == game.Height - 1)
                cell = '#';
            else if (x == game.BallX && y == game.BallY)
                cell = 'O';
            else if (y == game.PaddleY &&
                     x >= game.PaddleX &&
                     x < game.PaddleX + game.PaddleWidth)
                cell = '=';
            else if (game.IsBrickAt(x, y))
                cell = 'X';

            Console.Write(cell);
        }

        Console.WriteLine();
    }

    Thread.Sleep(15); // ~60 frames per second
}
Console.WriteLine(game.IsWon ? "You win!" : "Game over!");

if (options.TrainingEnabled)
    Console.WriteLine(game.ModelTrained
        ? $"Model saved: {options.ModelPath}"
        : "Not enough data to train (use both left AND right).");

Console.CursorVisible = true;