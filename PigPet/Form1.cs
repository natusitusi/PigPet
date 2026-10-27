
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace PigPet;

public partial class Form1 : Form
{
    // ОСНОВНЫЕ НАСТРОЙКИ
    private const float PigScale = 0.70f;
    private const float Speed = 200f;
    private const int CursorDistance = 50;

    // АНИМАЦИЯ
    private const double SleepDelay = 5.0;
    private const double RunFrameTime = 0.12;  
    private const double SleepFrameTime = 0.4;

    // Порог смены направления
    private const float DirectionThreshold = 1f;

    private enum PigState
    {
        Run,
        Idle,
        Sleep
    }

    private enum PigDirection
    {
        Right,
        Left,
        Up,
        Down
    }

    private readonly Bitmap[] _runFrames;
    private readonly Bitmap[] _upFrames;
    private readonly Bitmap[] _downFrames;
    private readonly Bitmap[] _sleepFrames;
    private readonly Bitmap _idleFrame;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = new();

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;

    private PigState _state = PigState.Run;
    private PigDirection _direction = PigDirection.Right;

    private float _x;
    private float _y;

    private double _lastTime;
    private double _lastMouseMoveTime;
    private double _animationTime;

    private int _frameIndex;
    private Point _lastMousePosition;

    public Form1()
    {
        string folder = Path.Combine(
            AppContext.BaseDirectory,
            "sprites"
        );

        // Бег вправо и влево
        _runFrames = LoadFrames(
            folder,
            "pig_run_*.png"
        );

        // Бег вверх (попой к нам)
        _upFrames = LoadFrames(
            folder,
            "poppi_pig_*.png"
        );

        // Бег вниз (лицом к нам)
        _downFrames = LoadFrames(
            folder,
            "face_pig_*.png"
        );

        // Сон
        _sleepFrames = LoadFrames(
            folder,
            "pig_sleep_*.png"
        );

        string idlePath = Path.Combine(
            folder,
            "pig_idle_01.png"
        );

        if (!File.Exists(idlePath))
        {
            throw new FileNotFoundException(
                "Не найден pig_idle_01.png!"
            );
        }

        _idleFrame = new Bitmap(idlePath);

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 16
        };

        _timer.Tick += UpdatePig;

        // Меню возле часов Windows
        _trayMenu = new ContextMenuStrip();

        _trayMenu.Items.Add(
            "Выход",
            null,
            (_, _) => Close()
        );

        string iconPath = Path.Combine(
            AppContext.BaseDirectory,
            "pig_icon.ico"
        );

        _trayIcon = new NotifyIcon
        {
            Icon = File.Exists(iconPath)
                ? new Icon(iconPath)
                : SystemIcons.Application,

            Text = "PigPet",
            ContextMenuStrip = _trayMenu,
            Visible = false
        };
    }

    private static Bitmap[] LoadFrames(
        string folder,
        string pattern
    )
    {
        string[] files = Directory
            .GetFiles(folder, pattern)
            .OrderBy(path => path)
            .ToArray();

        if (files.Length == 0)
        {
            throw new FileNotFoundException(
                $"Не найдены кадры: {pattern}"
            );
        }

        return files
            .Select(path => new Bitmap(path))
            .ToArray();
    }

    protected override void OnLoad(EventArgs e)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        int pigWidth = Math.Max(
            1,
            (int)MathF.Round(
                _runFrames[0].Width * PigScale
            )
        );

        int pigHeight = Math.Max(
            1,
            (int)MathF.Round(
                _runFrames[0].Height * PigScale
            )
        );

        ClientSize = new Size(
            pigWidth,
            pigHeight
        );

        Point mouse = Cursor.Position;

        _lastMousePosition = mouse;

        _x = mouse.X - Width - 50;
        _y = mouse.Y - Height / 2f;

        Location = new Point(
            (int)_x,
            (int)_y
        );

        base.OnLoad(e);

        _clock.Start();

        _lastTime = _clock.Elapsed.TotalSeconds;
        _lastMouseMoveTime = _lastTime;

        _trayIcon.Visible = true;
        _timer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;

            // Клики проходят сквозь хрюню
            cp.ExStyle |= 0x00000020;

            // Не показываем в Alt+Tab
            cp.ExStyle |= 0x00000080;

            // Не активируем окно
            cp.ExStyle |= 0x08000000;

            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private void ChangeState(PigState newState)
    {
        if (_state == newState)
            return;

        _state = newState;
        _frameIndex = 0;
        _animationTime = 0;
    }

    private void ChangeDirection(PigDirection newDirection)
    {
        if (_direction == newDirection)
            return;

        _direction = newDirection;
        _frameIndex = 0;
        _animationTime = 0;
    }

    private void UpdatePig(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;

        float dt = (float)Math.Min(
            now - _lastTime,
            0.05
        );

        _lastTime = now;

        Point mouse = Cursor.Position;

        if (mouse != _lastMousePosition)
        {
            _lastMouseMoveTime = now;
            _lastMousePosition = mouse;
        }

        // ЦЕЛЬ ХРЮНИ
        // Постоянная точка слева от курсора.
        // Она не зависит от того, куда смотрит хрюня.
        float targetX =
            mouse.X - Width / 2f - CursorDistance;

        float targetY =
            mouse.Y - Height / 2f;

        float dx = targetX - _x;
        float dy = targetY - _y;

        float distance = MathF.Sqrt(
            dx * dx + dy * dy
        );

        double idleTime =
            now - _lastMouseMoveTime;

        if (distance > 2f)
        {
            ChangeState(PigState.Run);

            // ВЫБИРАЕМ НАПРАВЛЕНИЕ ДВИЖЕНИЯ
            if (MathF.Abs(dx) > MathF.Abs(dy))
            {
                if (dx > DirectionThreshold)
                {
                    ChangeDirection(PigDirection.Right);
                }
                else if (dx < -DirectionThreshold)
                {
                    ChangeDirection(PigDirection.Left);
                }
            }
            else
            {
                if (dy > DirectionThreshold)
                {
                    ChangeDirection(PigDirection.Down);
                }
                else if (dy < -DirectionThreshold)
                {
                    ChangeDirection(PigDirection.Up);
                }
            }

            float step = MathF.Min(
                Speed * dt,
                distance
            );

            _x += dx / distance * step;
            _y += dy / distance * step;
        }
        else if (idleTime >= SleepDelay)
        {
            ChangeState(PigState.Sleep);
        }
        else
        {
            ChangeState(PigState.Idle);
        }

        // АНИМАЦИЯ
        if (_state != PigState.Idle)
        {
            _animationTime += dt;

            double frameTime =
                _state == PigState.Run
                    ? RunFrameTime
                    : SleepFrameTime;

            int frameCount = _state switch
            {
                PigState.Sleep => _sleepFrames.Length,

                PigState.Run when
                    _direction == PigDirection.Up
                    => _upFrames.Length,

                PigState.Run when
                    _direction == PigDirection.Down
                    => _downFrames.Length,

                _ => _runFrames.Length
            };

            if (_animationTime >= frameTime)
            {
                _animationTime = 0;

                _frameIndex =
                    (_frameIndex + 1) % frameCount;
            }
        }

        Location = new Point(
            (int)MathF.Round(_x),
            (int)MathF.Round(_y)
        );

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.InterpolationMode =
            InterpolationMode.NearestNeighbor;

        e.Graphics.PixelOffsetMode =
            PixelOffsetMode.Half;

        Bitmap currentFrame;

        if (_state == PigState.Sleep)
        {
            currentFrame = _sleepFrames[_frameIndex];
        }
        else if (_state == PigState.Idle)
        {
            currentFrame = _idleFrame;
        }
        else
        {
            currentFrame = _direction switch
            {
                PigDirection.Up =>
                    _upFrames[_frameIndex],

                PigDirection.Down =>
                    _downFrames[_frameIndex],

                _ => _runFrames[_frameIndex]
            };
        }

        // Отражаем только боковую анимацию
        if (_direction == PigDirection.Left)
        {
            e.Graphics.TranslateTransform(Width, 0);
            e.Graphics.ScaleTransform(-1, 1);
        }

        e.Graphics.DrawImage(
            currentFrame,
            new Rectangle(
                0,
                0,
                ClientSize.Width,
                ClientSize.Height
            ),
            0,
            0,
            currentFrame.Width,
            currentFrame.Height,
            GraphicsUnit.Pixel
        );
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();

        _timer.Stop();
        _timer.Dispose();

        foreach (Bitmap frame in _runFrames)
        {
            frame.Dispose();
        }

        foreach (Bitmap frame in _upFrames)
        {
            frame.Dispose();
        }

        foreach (Bitmap frame in _downFrames)
        {
            frame.Dispose();
        }

        foreach (Bitmap frame in _sleepFrames)
        {
            frame.Dispose();
        }

        _idleFrame.Dispose();

        base.OnFormClosed(e);
    }
}






















